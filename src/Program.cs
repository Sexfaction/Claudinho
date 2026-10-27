using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Claudinho
{
    // Modos de execução:
    //   Claudinho.exe              abre o mascote (uma instância só)
    //   Claudinho.exe --hook       usado pelos hooks SessionStart e UserPromptSubmit: entrega o
    //                            evento lido da entrada (com o processo dono da janela da
    //                            sessão) e, se o mascote estiver fechado, abre
    //   Claudinho.exe --permissao  usado pelo hook PermissionRequest: mostra o balão e espera o
    //                            clique; escreve a decisão para o Claude (ou nada, se não houver)
    //   Claudinho.exe --statusline usado como barra de status do Claude Code: repassa a cota da
    //                            assinatura ao mascote e escreve a linha que o terminal mostra
    //   Claudinho.exe --demo       mostra 4 estados lado a lado e o medidor, sem precisar do Claude
    //   Claudinho.exe --retrato X  grava em X (PNG) todos os estados, para conferir a arte
    //   Claudinho.exe --filme X estado início passo quadros [subagentes] [fase] [depois do erro]
    //                            grava em X (PNG) quadros seguidos de um estado (tempos em ms
    //                            desde que entrou nele), para conferir uma animação no tempo
    static class Program
    {
        public const int Porta = 47821;

        [STAThread]
        static int Main(string[] args)
        {
            string modo = args.Length > 0 ? args[0] : "";
            if (modo == "--hook") return ModoHook();
            if (modo == "--statusline") return ModoStatusline();
            if (modo == "--permissao") return ModoPermissao();
            if (modo == "--retrato" && args.Length > 1) return Retrato(args[1]);
            if (modo == "--filme" && args.Length > 5) return Filme(args);

            bool primeira;
            using (var unica = new Mutex(true, "Local\\Claudinho", out primeira))
            {
                if (!primeira) return 0;
                Win32.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var janela = new JanelaMascote();
                if (modo == "--demo") janela.Demonstrar();
                var servidor = new Servidor(Porta, janela.ReceberEvento, janela.EstadoJson, janela.PedirPermissao);
                if (!servidor.Iniciar()) return 1;
                Application.Run(janela);
                servidor.Parar();
            }
            return 0;
        }

        // Sai sempre com 0: um hook com erro apareceria na sessão do Claude, e o mascote não
        // pode atrapalhar o trabalho. O que der errado vai para o erros.log.
        static int ModoHook()
        {
            try
            {
                byte[] corpo = LerEntrada();
                // Qual programa (VS Code, terminal, app) tem a janela desta sessão, para onde o
                // clique no bichinho leva; e qual é o processo do Claude Code, para o mascote saber
                // quando ele fechou. Os hooks de curl não sabem disso; só estes dois.
                string extras = "X-Processo-Janela: " + Janelas.ProcessoComJanela() + "\r\n";
                int claude = Janelas.ProcessoDoClaude();
                if (claude > 0)
                {
                    try
                    {
                        using (var p = System.Diagnostics.Process.GetProcessById(claude))
                            extras += "X-Processo-Claude: " + claude + ";" + p.StartTime.ToFileTimeUtc() + "\r\n";
                    }
                    catch (ArgumentException) { }                          // fechou nesse meio-tempo
                    catch (System.ComponentModel.Win32Exception) { }       // sem acesso: segue sem
                }
                if (Enviar(corpo, extras, 500)) return 0;

                AbrirMascote();
                for (int i = 0; i < 30 && !Enviar(corpo, extras, 500); i++) Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Registro.Erro("hook", ex);
            }
            return 0;
        }

        // Entrega o pedido ao mascote e espera o balão. Com o mascote fechado, ou sem decisão
        // (não perturbe, escondido, respondido no pedido normal), não escreve nada: o Claude
        // segue só com o pedido normal. Nunca abre o mascote e sai sempre com 0.
        static int ModoPermissao()
        {
            try
            {
                byte[] corpo = LerEntrada();
                using (var cliente = new TcpClient())
                {
                    IAsyncResult conexao = cliente.BeginConnect("127.0.0.1", Porta, null, null);
                    if (!conexao.AsyncWaitHandle.WaitOne(200)) return 0;
                    cliente.EndConnect(conexao);
                    cliente.ReceiveTimeout = 600 * 1000;
                    NetworkStream fluxo = cliente.GetStream();
                    byte[] cabecalho = Encoding.ASCII.GetBytes("POST /permissao HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: "
                        + corpo.Length + "\r\nConnection: close\r\n\r\n");
                    fluxo.Write(cabecalho, 0, cabecalho.Length);
                    fluxo.Write(corpo, 0, corpo.Length);

                    var resposta = new MemoryStream();
                    fluxo.CopyTo(resposta); // o mascote fecha a conexão depois de responder
                    string texto = Encoding.UTF8.GetString(resposta.ToArray());
                    int fim = texto.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    string decisao = fim < 0 ? "" : texto.Substring(fim + 4);
                    if (decisao.Length == 0) return 0;
                    byte[] saida = new UTF8Encoding(false).GetBytes(decisao);
                    using (Stream stdout = Console.OpenStandardOutput())
                        stdout.Write(saida, 0, saida.Length);
                }
            }
            catch (SocketException) { }  // mascote fechado: vale o pedido normal
            catch (IOException) { }      // conexão caiu: idem
            catch (Exception ex)
            {
                Registro.Erro("permissao", ex);
            }
            return 0;
        }

        // A barra de status recebe o mesmo JSON que o Claude Code mostraria; dele saem a cota da
        // assinatura (para o medidor) e a linha do terminal. Não abre o mascote: quem abre são
        // os hooks. Sai sempre com 0, pelo mesmo motivo do hook.
        static int ModoStatusline()
        {
            try
            {
                string texto = Encoding.UTF8.GetString(LerEntrada()).TrimStart('﻿');
                var leitor = new System.Web.Script.Serialization.JavaScriptSerializer();
                leitor.MaxJsonLength = int.MaxValue;
                var dados = leitor.DeserializeObject(texto) as System.Collections.Generic.Dictionary<string, object>;
                if (dados == null) return 0;

                // Vai mesmo sem rate_limits: o mascote registra que a barra rodou (ver GET /estado).
                object limites;
                var cota = new System.Collections.Generic.Dictionary<string, object>();
                cota["hook_event_name"] = "_Cota";
                cota["rate_limits"] = dados.TryGetValue("rate_limits", out limites) ? limites : null;
                Enviar(Encoding.UTF8.GetBytes(leitor.Serialize(cota)), "", 200);
                // Em UTF-8 sem BOM, como o Claude Code lê; o Console do Windows usaria a página antiga.
                byte[] linha = new UTF8Encoding(false).GetBytes(LinhaDeStatus(dados));
                using (Stream saida = Console.OpenStandardOutput())
                    saida.Write(linha, 0, linha.Length);
            }
            catch (Exception ex)
            {
                Registro.Erro("statusline", ex);
            }
            return 0;
        }

        // "[Opus] contexto 12% · cota 5h 23% · semana 41%", com o que vier.
        static string LinhaDeStatus(System.Collections.Generic.Dictionary<string, object> dados)
        {
            var partes = new System.Collections.Generic.List<string>();
            var modelo = Filho(dados, "model");
            if (modelo != null && modelo.ContainsKey("display_name")) partes.Add("[" + modelo["display_name"] + "]");
            var contexto = Filho(dados, "context_window");
            if (contexto != null && contexto.ContainsKey("used_percentage") && contexto["used_percentage"] != null)
                partes.Add("contexto " + Porcento(contexto["used_percentage"]));
            var limites = Filho(dados, "rate_limits");
            if (limites != null)
            {
                var h5 = Filho(limites, "five_hour");
                var semana = Filho(limites, "seven_day");
                if (h5 != null && h5.ContainsKey("used_percentage")) partes.Add("cota 5h " + Porcento(h5["used_percentage"]));
                if (semana != null && semana.ContainsKey("used_percentage")) partes.Add("semana " + Porcento(semana["used_percentage"]));
            }
            return string.Join(" · ", partes.ToArray());
        }

        static System.Collections.Generic.Dictionary<string, object> Filho(System.Collections.Generic.Dictionary<string, object> pai, string chave)
        {
            object valor;
            return pai.TryGetValue(chave, out valor) ? valor as System.Collections.Generic.Dictionary<string, object> : null;
        }

        static string Porcento(object valor)
        {
            return Math.Round(Convert.ToDouble(valor, System.Globalization.CultureInfo.InvariantCulture)) + "%";
        }

        static byte[] LerEntrada()
        {
            using (Stream entrada = Console.OpenStandardInput())
            using (var memoria = new MemoryStream())
            {
                entrada.CopyTo(memoria);
                return memoria.ToArray();
            }
        }

        static bool Enviar(byte[] corpo, string cabecalhosExtras, int esperaConexaoMs)
        {
            try
            {
                using (var cliente = new TcpClient())
                {
                    IAsyncResult conexao = cliente.BeginConnect("127.0.0.1", Porta, null, null);
                    if (!conexao.AsyncWaitHandle.WaitOne(esperaConexaoMs)) return false;
                    cliente.EndConnect(conexao);
                    cliente.ReceiveTimeout = 1000;
                    NetworkStream fluxo = cliente.GetStream();
                    byte[] cabecalho = Encoding.ASCII.GetBytes("POST /evento HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: "
                        + corpo.Length + "\r\n" + cabecalhosExtras + "Connection: close\r\n\r\n");
                    fluxo.Write(cabecalho, 0, cabecalho.Length);
                    fluxo.Write(corpo, 0, corpo.Length);
                    fluxo.ReadByte(); // espera a resposta do mascote
                    return true;
                }
            }
            catch (SocketException) { return false; } // mascote fechado: quem chamou decide se abre
            catch (IOException) { return false; }
        }

        // Abre o mascote fora do "job" do processo que rodou o hook. Sem isso, quando a sessão do
        // Claude que o abriu fecha, o Windows leva o mascote junto, mesmo com outras sessões abertas.
        // Se o job não permitir sair, abre do jeito normal (o próximo prompt reabre, se preciso).
        static void AbrirMascote()
        {
            string exe = Application.ExecutablePath;
            string pasta = Path.GetDirectoryName(exe);
            var si = new Win32.STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            Win32.PROCESS_INFORMATION pi;
            bool abriu = Win32.CreateProcess(exe, new StringBuilder("\"" + exe + "\""), IntPtr.Zero, IntPtr.Zero,
                    false, Win32.CREATE_BREAKAWAY_FROM_JOB, IntPtr.Zero, pasta, ref si, out pi)
                || Win32.CreateProcess(exe, new StringBuilder("\"" + exe + "\""), IntPtr.Zero, IntPtr.Zero,
                    false, 0, IntPtr.Zero, pasta, ref si, out pi);
            if (!abriu) throw new System.ComponentModel.Win32Exception();
            Win32.CloseHandle(pi.hProcess);
            Win32.CloseHandle(pi.hThread);
        }

        // Folha com cada estado em 4 momentos, sobre fundo escuro e claro (as duas barras do
        // Windows). A 4ª linha é "trabalhando com 3 subagentes"; a última, o medidor de cota em
        // 4 níveis (20/50, 75/40, 95/92, só semana).
        static int Retrato(string arquivo)
        {
            const int escala = 3;
            Estado[] estados = { Estado.Dormindo, Estado.Acordando, Estado.Trabalhando, Estado.Trabalhando,
                Estado.PrecisaDeVoce, Estado.Terminou, Estado.Erro, Estado.SemCota, Estado.Compactando };
            long[] momentos = { 0, 300, 600, 1500 };
            double?[,] cotas = { { 20, 50 }, { 75, 40 }, { 95, 92 }, { null, 60 } };
            int w = Sprites.Colunas * escala, h = Sprites.Linhas * escala;

            using (var folha = new Bitmap(w * momentos.Length * 2, h * (estados.Length + 1)))
            using (Graphics g = Graphics.FromImage(folha))
            {
                for (int lado = 0; lado < 2; lado++)
                {
                    using (var fundo = new SolidBrush(lado == 0 ? Color.FromArgb(32, 32, 32) : Color.FromArgb(238, 238, 238)))
                        g.FillRectangle(fundo, lado * w * momentos.Length, 0, w * momentos.Length, folha.Height);
                    for (int i = 0; i < estados.Length; i++)
                        for (int j = 0; j < momentos.Length; j++)
                        {
                            var s = new Sessao();
                            s.Estado = estados[i];
                            s.Desde = 0;
                            s.Subagentes = i == 3 ? 3 : 0;
                            g.TranslateTransform(0, i * h);
                            Sprites.Desenhar(g, s, momentos[j], (lado * momentos.Length + j) * w, escala);
                            g.ResetTransform();
                        }
                    for (int j = 0; j < momentos.Length; j++)
                    {
                        var c = new Cota();
                        c.Uso5h = cotas[j, 0];
                        c.UsoSemana = cotas[j, 1];
                        g.TranslateTransform((lado * momentos.Length + j) * w + 4 * escala, estados.Length * h);
                        Sprites.DesenharCota(g, c, 0, escala);
                        g.ResetTransform();
                    }
                }
                folha.Save(arquivo, ImageFormat.Png);
            }
            return 0;
        }

        // Quadros seguidos, 5 por linha; cada um no fundo escuro e no claro, com o tempo embaixo.
        static int Filme(string[] args)
        {
            const int escala = 6, porLinha = 5, legenda = 14; // o dobro da tela, para ver pixel a pixel
            var estado = (Estado)Enum.Parse(typeof(Estado), args[2], true);
            long inicio = long.Parse(args[3]), passo = long.Parse(args[4]);
            int quadros = int.Parse(args[5]);
            int subagentes = args.Length > 6 ? int.Parse(args[6]) : 0;
            int fase = args.Length > 7 ? int.Parse(args[7]) : 0; // muda os sorteios (ex.: qual comemoração)
            var depoisDoErro = args.Length > 8 ? (Estado)Enum.Parse(typeof(Estado), args[8], true) : Estado.Dormindo;
            int w = Sprites.Colunas * escala, h = Sprites.Linhas * escala;
            int linhas = (quadros + porLinha - 1) / porLinha;

            using (var folha = new Bitmap(porLinha * 2 * w, linhas * (h + legenda)))
            using (Graphics g = Graphics.FromImage(folha))
            using (var escuro = new SolidBrush(Color.FromArgb(32, 32, 32)))
            using (var claro = new SolidBrush(Color.FromArgb(238, 238, 238)))
            using (var fonte = new Font("Segoe UI", 7f))
            {
                g.Clear(Color.White);
                for (int i = 0; i < quadros; i++)
                {
                    int x = (i % porLinha) * 2 * w, y = (i / porLinha) * (h + legenda);
                    long momento = inicio + i * passo;
                    var s = new Sessao();
                    s.Estado = estado;
                    s.Desde = 0;
                    s.Subagentes = subagentes;
                    s.Fase = fase;
                    s.DepoisDoErro = depoisDoErro;
                    g.FillRectangle(escuro, x, y, w, h);
                    g.FillRectangle(claro, x + w, y, w, h);
                    for (int lado = 0; lado < 2; lado++)
                    {
                        g.TranslateTransform(0, y);
                        Sprites.Desenhar(g, s, momento, x + lado * w, escala);
                        g.ResetTransform();
                    }
                    g.DrawString(momento + " ms", fonte, Brushes.Black, x + 2, y + h);
                }
                folha.Save(args[1], ImageFormat.Png);
            }
            return 0;
        }
    }
}
