using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Claudinho
{
    // A janela transparente sobre a barra de tarefas. Uma janela só: o medidor de cota à esquerda
    // (quando o Claude já mandou a cota) e os bichinhos lado a lado.
    // A transparência é por cor-chave: o que for magenta vira buraco (e o clique passa por ali).
    class JanelaMascote : Form
    {
        static readonly Color CorChave = Color.FromArgb(255, 0, 255);
        const int DistanciaArrasto = 4; // px: menos que isso entre apertar e soltar é clique
        const int DicaMedidor = -2;

        readonly Stopwatch relogio = Stopwatch.StartNew();
        readonly Sessoes sessoes;
        readonly Timer animacao = new Timer();
        readonly Timer vigia = new Timer();
        readonly Timer repique = new Timer();
        readonly ToolTip dica = new ToolTip();
        readonly int escala;
        readonly int larguraCelula, alturaCelula, larguraMedidor;
        readonly Win32.WinEventDelegate aoMudarFoco; // guardado em campo: o Windows chama de volta
        readonly IntPtr ganchoFoco;

        int ancoraX, ancoraBase;    // canto inferior esquerdo do grupo (medidor + bichinhos)
        DateTime escondidoAte = DateTime.MinValue;
        bool sempreNoTopo, somLigado;
        bool naoPerturbe;           // não gravado: volta desligado quando o mascote reabre
        readonly List<PedidoPermissao> pedidos = new List<PedidoPermissao>(); // na fila, o 1º está no balão
        Balao balao;
        Point? arrastando;          // posição do mouse dentro da janela ao apertar o botão
        Point inicioClique;         // posição na tela ao apertar, para separar clique de arrasto
        bool moveu;
        int dicaIndice = -1;

        long Agora { get { return relogio.ElapsedMilliseconds; } }

        static long AgoraUnix
        {
            get { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
        }

        // Largura ocupada pelo medidor (0 enquanto o Claude não mandou a cota).
        int Deslocamento
        {
            get { return sessoes.Cota.Uso5h.HasValue || sessoes.Cota.UsoSemana.HasValue ? larguraMedidor : 0; }
        }

        public JanelaMascote()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            sempreNoTopo = Preferencias.Ler("topo", "sim") != "nao";
            somLigado = Preferencias.Ler("som", "sim") != "nao";
            TopMost = sempreNoTopo;
            StartPosition = FormStartPosition.Manual;
            BackColor = CorChave;
            TransparencyKey = CorChave;
            DoubleBuffered = true;
            Text = "Claudinho";

            sessoes = new Sessoes(Agora);
            sessoes.AoMudar = delegate(Sessao s, Estado novo)
            {
                if (somLigado && !naoPerturbe && !s.Fixa) Som.Tocar(novo, Agora);
            };
            using (Graphics g = CreateGraphics())
                escala = Math.Max(2, (int)Math.Round(3 * g.DpiX / 96.0));
            larguraCelula = Sprites.Colunas * escala;
            alturaCelula = Sprites.Linhas * escala;
            larguraMedidor = Sprites.ColunasMedidor * escala;

            CarregarPosicao();
            AjustarTamanho();

            ContextMenuStrip = CriarMenu();
            dica.ShowAlways = true; // a janela nunca fica ativa; sem isso a dica não aparece
            animacao.Interval = 100;
            animacao.Tick += delegate { Animar(); };
            animacao.Start();
            vigia.Interval = 2000;
            vigia.Tick += delegate { Vigiar(); };
            vigia.Start();
            repique.Interval = 200;
            repique.Tick += delegate
            {
                repique.Stop();
                Vigiar();
            };

            // A barra de tarefas também é "sempre no topo", e entre duas janelas assim fica por cima
            // a ativada por último: ao clicar nela ou trocar de janela, ela cobre o mascote. O
            // Windows avisa na hora em que o foco muda, e o mascote volta para cima nesse instante.
            aoMudarFoco = delegate { AoMudarFoco(); };
            ganchoFoco = Win32.SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                aoMudarFoco, 0, 0, Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);

            // A janela já existe (o CreateGraphics acima a criou): os eventos que chegarem antes do
            // Application.Run têm onde cair.
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                // Fora do Alt+Tab e sem roubar o foco de quem está digitando. O "sempre no topo"
                // vem da propriedade TopMost, que o menu liga e desliga.
                CreateParams p = base.CreateParams;
                p.ExStyle |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
                return p;
            }
        }

        // Chamado pela thread do servidor.
        public void ReceberEvento(Dictionary<string, object> evento)
        {
            try
            {
                BeginInvoke(new Action(delegate
                {
                    sessoes.Aplicar(evento, Agora);
                    ResolverPedidosPor(evento);
                    AjustarTamanho();
                    if (!Visible) Vigiar(); // primeira sessão: aparece já, sem esperar a vigia
                    Invalidate();
                }));
            }
            // Janela já fechando (inclui ObjectDisposedException): o evento não tem mais onde aparecer.
            catch (InvalidOperationException) { }
        }

        // ---- Balão de permissão ----

        // Chamado pela thread do servidor, que fica segurando o hook até o pedido acabar.
        public void PedirPermissao(PedidoPermissao pedido)
        {
            try
            {
                BeginInvoke(new Action(delegate
                {
                    // Sem balão (vale só o pedido normal): não perturbe, escondido, tela cheia, fila
                    // cheia, ou ferramentas que pedem uma resposta e não um sim/não (aprovação de
                    // plano, pergunta com opções): "Permitir" as aprovaria sem a resposta.
                    if (naoPerturbe || DateTime.Now < escondidoAte || Win32.EmTelaCheia() || pedidos.Count >= 20
                        || pedido.Ferramenta == "ExitPlanMode" || pedido.Ferramenta == "AskUserQuestion")
                    {
                        pedido.Cancelar();
                        return;
                    }
                    pedidos.Add(pedido);
                    MostrarProximoBalao();
                }));
            }
            catch (InvalidOperationException) { pedido.Cancelar(); } // janela fechando
        }

        void MostrarProximoBalao()
        {
            pedidos.RemoveAll(p => p.Encerrado);
            if (balao != null && !balao.IsDisposed && !balao.Pedido.Encerrado) return; // um por vez
            if (balao != null && !balao.IsDisposed) balao.Close();
            balao = null;
            if (pedidos.Count == 0) return;

            PedidoPermissao pedido = pedidos[0];
            Sessao s = sessoes.Lista.Find(x => x.Id == pedido.Sessao);
            string pasta = s != null ? s.Pasta : "Uma sessão";
            balao = new Balao(pedido, pasta, escala / 3f, delegate
            {
                // Procura na hora do clique: a sessão pode ter chegado depois do balão.
                Sessao agora = sessoes.Lista.Find(x => x.Id == pedido.Sessao);
                if (agora != null) AbrirJanelaDa(agora);
                else dica.Show("Ainda não sei qual é a janela desta sessão", this, 0, -2 * escala, 2000);
            });
            balao.FormClosed += delegate
            {
                if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(MostrarProximoBalao));
            };
            balao.Show();
            PosicionarBalao(s);
        }

        // Em cima do bichinho da sessão (ou do grupo), sem sair do monitor.
        void PosicionarBalao(Sessao s)
        {
            if (balao == null || balao.IsDisposed) return;
            int indice = s == null ? -1 : sessoes.Lista.IndexOf(s);
            int centro = indice >= 0 && indice < sessoes.Visiveis
                ? Left + Deslocamento + indice * larguraCelula + larguraCelula / 2
                : Left + Width / 2;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            int x = Math.Max(area.Left, Math.Min(centro - balao.Width / 2, area.Right - balao.Width));
            int y = Math.Max(area.Top, Top + 3 * escala - balao.Height);
            balao.Location = new Point(x, y);
            Win32.SetWindowPos(balao.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }

        // O pedido se resolveu por outro caminho: a ferramenta já rodou (mesmo nome e entrada),
        // a resposta acabou, veio prompt novo ou a sessão terminou.
        void ResolverPedidosPor(Dictionary<string, object> evento)
        {
            object valor;
            string nome = evento.TryGetValue("hook_event_name", out valor) ? valor as string : null;
            string sessao = evento.TryGetValue("session_id", out valor) ? valor as string : null;
            if (nome == null || sessao == null || pedidos.Count == 0) return;
            bool fimDoTurno = nome == "Stop" || nome == "StopFailure" || nome == "UserPromptSubmit" || nome == "SessionEnd";
            bool ferramentaRodou = nome == "PostToolUse" || nome == "PostToolUseFailure";
            if (!fimDoTurno && !ferramentaRodou) return;

            string ferramenta = evento.TryGetValue("tool_name", out valor) ? valor as string : null;
            string entrada = ferramentaRodou && evento.TryGetValue("tool_input", out valor)
                ? new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(valor) : null;
            foreach (PedidoPermissao p in pedidos)
            {
                if (p.Sessao != sessao) continue;
                if (fimDoTurno || (p.Ferramenta == ferramenta && p.EntradaSerializada == entrada)) p.Cancelar();
            }
            FecharBalaoSeAcabou();
        }

        void FecharBalaoSeAcabou()
        {
            if (balao != null && !balao.IsDisposed && balao.Pedido.Encerrado) balao.Close(); // o FormClosed mostra o próximo
            else if (balao == null && pedidos.Count > 0) MostrarProximoBalao();
        }

        void CancelarPedidos()
        {
            foreach (PedidoPermissao p in pedidos) p.Cancelar();
            FecharBalaoSeAcabou();
        }

        // Chamado pela thread do servidor (GET /estado): lê as sessões na thread da janela.
        public string EstadoJson()
        {
            try
            {
                return (string)Invoke(new Func<string>(delegate
                {
                    var lista = new List<Dictionary<string, object>>();
                    foreach (Sessao s in sessoes.Lista)
                    {
                        var item = new Dictionary<string, object>();
                        item["pasta"] = s.Pasta;
                        item["estado"] = NomeDoEstado(s.Estado);
                        item["subagentes"] = s.Subagentes;
                        item["detalhe"] = s.Detalhe;
                        item["processo"] = s.ProcessoJanela;
                        item["claude"] = s.ProcessoClaude;
                        string titulo;
                        Janelas.Escolher(s.ProcessoJanela, s.Pasta, out titulo);
                        item["janela"] = titulo; // para onde o clique levaria
                        lista.Add(item);
                    }
                    var cota = new Dictionary<string, object>();
                    cota["5h"] = sessoes.Cota.Uso5h;
                    cota["semana"] = sessoes.Cota.UsoSemana;
                    cota["barra_rodou"] = sessoes.Cota.BarraRodou.HasValue
                        ? sessoes.Cota.BarraRodou.Value.ToString("HH:mm:ss") : null;
                    var tudo = new Dictionary<string, object>();
                    tudo["sessoes"] = lista;
                    tudo["cota"] = cota;
                    tudo["balao"] = balao != null && !balao.IsDisposed ? balao.Pedido.Ferramenta : null;
                    tudo["balao_armado"] = balao != null && !balao.IsDisposed && balao.Armado;
                    tudo["fila"] = pedidos.FindAll(p => !p.Encerrado).Count;
                    return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(tudo);
                }));
            }
            catch (InvalidOperationException) { return "{\"sessoes\":[]}"; } // janela fechando
        }

        public void Demonstrar()
        {
            long agora = Agora;
            // Três fixos; o 4º lugar fica para uma sessão real (os estados que dependem de
            // transição, como o acordando, aparecem nela a cada prompt).
            sessoes.NovaFixa("dormindo", Estado.Dormindo, 0, agora);
            sessoes.NovaFixa("trabalhando", Estado.Trabalhando, 0, agora);
            sessoes.NovaFixa("compactando a conversa", Estado.Compactando, 0, agora);
            sessoes.Cota.Uso5h = 72;
            sessoes.Cota.UsoSemana = 35;
            AjustarTamanho();
        }

        void Animar()
        {
            if (!sessoes.Avancar(Agora))
            {
                Close();
                return;
            }
            AjustarTamanho();
            FecharBalaoSeAcabou(); // o servidor cancela quando o Claude derruba a conexão
            // Dormindo, 4 quadros por segundo bastam para os "Z"; acordado, 10.
            animacao.Interval = sessoes.AlgumaAcordada() && !naoPerturbe ? 100 : 250;
            if (Visible) Invalidate();
        }

        // Foco mudou em outro programa: volta para cima na hora e confere de novo em 200 ms, porque
        // a barra de tarefas às vezes se reordena logo depois do aviso.
        void AoMudarFoco()
        {
            if (sempreNoTopo && Visible) VoltarAoTopo();
            if (balao != null && !balao.IsDisposed)
                Win32.SetWindowPos(balao.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            repique.Stop();
            repique.Start();
        }

        // A cada 2 s e a cada troca de foco: tira as sessões cujo Claude fechou; some em tela cheia
        // (vídeo, jogo, apresentação), escondido pelo menu ou sem sessão; só mexe na ordem das
        // janelas se algo cobriu o mascote.
        void Vigiar()
        {
            var antes = sessoes.Lista.ConvertAll(x => x.Id);
            sessoes.RemoverEncerradas(ClaudeFechou);
            if (sessoes.Lista.Count != antes.Count)
            {
                // Pedido de uma sessão cujo Claude fechou não tem mais quem responder.
                foreach (PedidoPermissao p in pedidos)
                    if (antes.Contains(p.Sessao) && !sessoes.Lista.Exists(x => x.Id == p.Sessao)) p.Cancelar();
                FecharBalaoSeAcabou();
                AjustarTamanho();
                Invalidate();
            }

            bool mostrar = sessoes.Visiveis > 0 && DateTime.Now >= escondidoAte && !Win32.EmTelaCheia();
            if (!mostrar)
            {
                if (Visible) Hide();
                return;
            }
            if (!Visible)
            {
                Win32.ShowWindow(Handle, Win32.SW_SHOWNOACTIVATE);
                if (sempreNoTopo) VoltarAoTopo();
            }
            else if (sempreNoTopo && Coberto())
            {
                VoltarAoTopo();
            }
        }

        // O processo do Claude Code da sessão não existe mais (ou o número foi reaproveitado por
        // outro programa, o que se vê pela hora de início). Sem número conhecido, não dá para saber.
        static bool ClaudeFechou(Sessao s)
        {
            if (s.ProcessoClaude == 0) return false;
            try
            {
                using (Process p = Process.GetProcessById(s.ProcessoClaude))
                    return s.InicioClaude != 0 && p.StartTime.ToFileTimeUtc() != s.InicioClaude;
            }
            catch (ArgumentException) { return true; }          // não está rodando
            catch (Win32Exception) { return false; }            // sem permissão para ler: segue viva
            catch (InvalidOperationException) { return true; }  // saiu enquanto líamos
        }

        void VoltarAoTopo()
        {
            Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
        }

        // Olha o que está na tela num ponto do corpo do primeiro bichinho (coluna 10, linha 14
        // da célula: é corpo em todos os quadros da animação). Janela de outro programa ali = coberto.
        bool Coberto()
        {
            var ponto = new Point(Left + Deslocamento + 10 * escala + escala / 2, Top + 14 * escala + escala / 2);
            IntPtr raiz = Win32.GetAncestor(Win32.WindowFromPoint(ponto), Win32.GA_ROOT);
            if (raiz == Handle || raiz == IntPtr.Zero) return false;
            uint processo;
            Win32.GetWindowThreadProcessId(raiz, out processo);
            return (int)processo != Process.GetCurrentProcess().Id; // a própria dica ou menu não contam
        }

        void AjustarTamanho()
        {
            int largura = Deslocamento + Math.Max(1, sessoes.Visiveis) * larguraCelula;
            var limites = new Rectangle(ancoraX, ancoraBase - alturaCelula, largura, alturaCelula);
            if (Bounds != limites) Bounds = limites;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            long agora = Agora;
            if (Deslocamento > 0) Sprites.DesenharCota(e.Graphics, sessoes.Cota, AgoraUnix, escala);
            int n = sessoes.Visiveis;
            for (int i = 0; i < n; i++)
            {
                Sessao s = sessoes.Lista[i];
                if (naoPerturbe)
                {
                    // Não perturbe: todos aparecem dormindo; o estado real segue guardado (e na dica).
                    var dormindo = new Sessao();
                    dormindo.Estado = Estado.Dormindo;
                    dormindo.Fase = s.Fase;
                    s = dormindo;
                }
                Sprites.Desenhar(e.Graphics, s, agora, Deslocamento + i * larguraCelula, escala);
            }
        }

        // Qual bichinho está sob o x da janela: índice, DicaMedidor ou -1.
        int IndiceEm(int x)
        {
            if (x < Deslocamento) return DicaMedidor;
            int indice = (x - Deslocamento) / larguraCelula;
            return indice < sessoes.Visiveis ? indice : -1;
        }

        // ---- Clicar, arrastar e dica ----
        // Clique (soltar sem mover) traz a janela da sessão para frente; apertar e mover arrasta.

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                arrastando = e.Location;
                inicioClique = PointToScreen(e.Location);
                moveu = false;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (arrastando.HasValue)
            {
                Point tela = PointToScreen(e.Location);
                if (Math.Abs(tela.X - inicioClique.X) > DistanciaArrasto || Math.Abs(tela.Y - inicioClique.Y) > DistanciaArrasto)
                    moveu = true;
                if (moveu)
                {
                    ancoraX = tela.X - arrastando.Value.X;
                    ancoraBase = tela.Y - arrastando.Value.Y + alturaCelula;
                    AjustarTamanho();
                }
            }
            else
            {
                AtualizarDica(IndiceEm(e.X));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (arrastando.HasValue && e.Button == MouseButtons.Left)
            {
                int indice = IndiceEm(arrastando.Value.X);
                arrastando = null;
                if (moveu)
                {
                    ManterNaTela();
                    Preferencias.Gravar("posicao", ancoraX + ";" + ancoraBase);
                }
                else if (indice >= 0)
                {
                    AbrirJanelaDa(sessoes.Lista[indice]);
                }
            }
            base.OnMouseUp(e);
        }

        void AbrirJanelaDa(Sessao s)
        {
            string titulo;
            IntPtr janela = Janelas.Escolher(s.ProcessoJanela, s.Pasta, out titulo);
            if (!Janelas.Focar(janela))
                dica.Show("Não achei a janela de \"" + s.Pasta + "\"", this, 0, -2 * escala, 2000);
        }

        void AtualizarDica(int indice)
        {
            if (indice == dicaIndice || indice == -1) return;
            dicaIndice = indice;
            if (indice == DicaMedidor)
            {
                dica.SetToolTip(this, TextoDaCota());
                return;
            }
            Sessao s = sessoes.Lista[indice];
            string texto = s.Pasta + " — " + NomeDoEstado(s.Estado);
            if (s.Subagentes > 0) texto += " com " + s.Subagentes + (s.Subagentes == 1 ? " subagente" : " subagentes");
            if (s.Detalhe != null && (s.Estado == Estado.Erro || s.Estado == Estado.SemCota)) texto += ": " + s.Detalhe;
            if (naoPerturbe) texto += " (não perturbe)";
            dica.SetToolTip(this, texto + "\nclique para abrir");
        }

        string TextoDaCota()
        {
            Cota c = sessoes.Cota;
            long agora = AgoraUnix;
            return "Cota da assinatura\n"
                + "5 horas: " + TextoDaJanela(Sprites.PorcentagemValida(c.Uso5h, c.Renova5h, agora), c.Renova5h, false) + "\n"
                + "semana: " + TextoDaJanela(Sprites.PorcentagemValida(c.UsoSemana, c.RenovaSemana, agora), c.RenovaSemana, true);
        }

        static string TextoDaJanela(double? pct, long renova, bool comDia)
        {
            if (!pct.HasValue) return "sem dado";
            string texto = Math.Round(pct.Value) + "% usado";
            if (renova > AgoraUnix)
            {
                DateTime quando = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(renova).ToLocalTime();
                texto += ", renova " + quando.ToString(comDia ? "ddd dd/MM 'às' HH:mm" : "'às' HH:mm",
                    new System.Globalization.CultureInfo("pt-BR"));
            }
            return texto;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            dicaIndice = -1;
            base.OnMouseLeave(e);
        }

        static string NomeDoEstado(Estado estado)
        {
            switch (estado)
            {
                case Estado.Acordando: return "acordando";
                case Estado.Trabalhando: return "trabalhando";
                case Estado.PrecisaDeVoce: return "precisa de você";
                case Estado.Terminou: return "terminou";
                case Estado.Erro: return "erro";
                case Estado.SemCota: return "sem cota";
                case Estado.Compactando: return "compactando a conversa";
                default: return "dormindo";
            }
        }

        // ---- Menu do botão direito ----

        ContextMenuStrip CriarMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Dormir todos", null, delegate
            {
                sessoes.DormirTodas(Agora);
                Invalidate();
            });
            var perturbe = new ToolStripMenuItem("Não perturbe");
            perturbe.CheckOnClick = true;
            perturbe.CheckedChanged += delegate
            {
                naoPerturbe = perturbe.Checked;
                if (naoPerturbe) CancelarPedidos(); // os pedidos abertos voltam a valer só no pedido normal
                Invalidate();
            };
            menu.Items.Add(perturbe);
            var som = new ToolStripMenuItem("Som");
            som.Checked = somLigado;
            som.CheckOnClick = true;
            som.CheckedChanged += delegate
            {
                somLigado = som.Checked;
                Preferencias.Gravar("som", somLigado ? "sim" : "nao");
            };
            menu.Items.Add(som);
            var topo = new ToolStripMenuItem("Sempre no topo");
            topo.Checked = sempreNoTopo;
            topo.CheckOnClick = true;
            topo.CheckedChanged += delegate
            {
                sempreNoTopo = topo.Checked;
                TopMost = sempreNoTopo;
                if (sempreNoTopo) VoltarAoTopo();
                Preferencias.Gravar("topo", sempreNoTopo ? "sim" : "nao");
            };
            menu.Items.Add(topo);
            menu.Items.Add("Esconder por 1 hora", null, delegate
            {
                escondidoAte = DateTime.Now.AddHours(1);
                CancelarPedidos();
                Hide();
            });
            menu.Items.Add("Fechar (volta no próximo prompt)", null, delegate { Close(); });
            return menu;
        }

        // ---- Posição ----

        void CarregarPosicao()
        {
            Rectangle tela = Screen.PrimaryScreen.Bounds;
            // Padrão: canto esquerdo, com os pés no pé da tela (dentro da barra de tarefas).
            ancoraX = tela.Left + 8 * escala;
            ancoraBase = tela.Bottom;
            string[] partes = Preferencias.Ler("posicao", "").Split(';');
            int x, y;
            if (partes.Length == 2 && int.TryParse(partes[0], out x) && int.TryParse(partes[1], out y))
            {
                ancoraX = x;
                ancoraBase = y;
            }
            ManterNaTela();
        }

        // Pelo menos um bichinho inteiro dentro do monitor principal.
        void ManterNaTela()
        {
            Rectangle tela = Screen.PrimaryScreen.Bounds;
            ancoraX = Math.Max(tela.Left, Math.Min(ancoraX, tela.Right - larguraCelula));
            ancoraBase = Math.Max(tela.Top + alturaCelula, Math.Min(ancoraBase, tela.Bottom));
            AjustarTamanho();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (PedidoPermissao p in pedidos) p.Cancelar(); // o servidor devolve "sem decisão"
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (ganchoFoco != IntPtr.Zero) Win32.UnhookWinEvent(ganchoFoco);
                animacao.Dispose();
                vigia.Dispose();
                repique.Dispose();
                dica.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
