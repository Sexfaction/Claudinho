using System;
using System.Drawing;
using System.Windows.Forms;

namespace Claudinho
{
    // O balão de permissão, sobre o bichinho da sessão. Não rouba o foco de quem está digitando;
    // os botões funcionam com um clique mesmo assim. Fica sempre por cima: é um pedido esperando.
    class Balao : Form
    {
        static readonly Color Fundo = Color.FromArgb(0x26, 0x26, 0x26);
        static readonly Color FundoCodigo = Color.FromArgb(0x14, 0x14, 0x14);
        static readonly Color Texto = Color.FromArgb(0xEE, 0xEE, 0xEE);
        static readonly Color Laranja = Color.FromArgb(0xD9, 0x77, 0x57);

        public readonly PedidoPermissao Pedido;
        public bool Armado { get; private set; } // os botões já valem (ver o fim do construtor)
        readonly ToolTip dica = new ToolTip();

        public Balao(PedidoPermissao pedido, string pasta, float fator, Action abrirJanela)
        {
            Pedido = pedido;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Laranja; // vira a borda: o painel de dentro é escuro
            Padding = new Padding(Math.Max(1, (int)(2 * fator)));
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Text = "Permissão — Claudinho";
            dica.ShowAlways = true;

            int largura = (int)(380 * fator);
            var painel = new FlowLayoutPanel();
            painel.FlowDirection = FlowDirection.TopDown;
            painel.WrapContents = false;
            painel.AutoSize = true;
            painel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            painel.BackColor = Fundo;
            painel.Padding = new Padding((int)(10 * fator));
            painel.Dock = DockStyle.Fill;

            var titulo = new Label();
            titulo.AutoSize = true;
            titulo.MaximumSize = new Size(largura, 0);
            titulo.ForeColor = Texto;
            titulo.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            titulo.Text = pasta + " quer usar " + pedido.Ferramenta;
            painel.Controls.Add(titulo);

            var codigo = new Label();
            codigo.AutoSize = true;
            codigo.MaximumSize = new Size(largura, (int)(150 * fator));
            codigo.MinimumSize = new Size(largura, 0);
            codigo.ForeColor = Texto;
            codigo.BackColor = FundoCodigo;
            codigo.Font = new Font("Consolas", 9.5f);
            codigo.Padding = new Padding((int)(6 * fator));
            codigo.Margin = new Padding(0, (int)(6 * fator), 0, (int)(8 * fator));
            codigo.UseMnemonic = false; // "&" no comando aparece como "&"
            codigo.AutoEllipsis = true;
            codigo.Text = pedido.Resumo(400);
            dica.SetToolTip(codigo, pedido.Resumo(3000)); // a dica mostra o comando inteiro
            painel.Controls.Add(codigo);
            // Comando comprido ou de muitas linhas: o quadro corta, então avisa onde ver o resto.
            if (!pedido.ResumoCabe(400) || pedido.Resumo(400).Split('\n').Length > 7)
            {
                var aviso = new Label();
                aviso.AutoSize = true;
                aviso.ForeColor = Laranja;
                aviso.Font = new Font("Segoe UI", 8f);
                aviso.Margin = new Padding(0, 0, 0, (int)(6 * fator));
                aviso.Text = "Texto cortado: passe o mouse no quadro para ver tudo.";
                painel.Controls.Add(aviso);
            }

            var botoes = new FlowLayoutPanel();
            botoes.FlowDirection = FlowDirection.LeftToRight;
            botoes.AutoSize = true;
            botoes.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            botoes.WrapContents = true;
            botoes.MaximumSize = new Size(largura, 0);
            botoes.Margin = new Padding(0);
            botoes.Controls.Add(Botao("Permitir", Laranja, fator, delegate { Decidir(pedido.Permitir()); }));
            string sempre = pedido.DescreverSugestao();
            if (sempre != null)
            {
                Button b = Botao("Sempre permitir", Fundo, fator, delegate { Decidir(pedido.PermitirSempre()); });
                dica.SetToolTip(b, "Grava: " + sempre);
                botoes.Controls.Add(b);
            }
            botoes.Controls.Add(Botao("Negar", Fundo, fator, delegate { Decidir(pedido.Negar()); }));
            Button abrir = Botao("Abrir", Fundo, fator, delegate { abrirJanela(); });
            dica.SetToolTip(abrir, "Traz a janela da sessão para frente (o pedido continua aqui)");
            botoes.Controls.Add(abrir);
            painel.Controls.Add(botoes);

            if (sempre != null)
            {
                var regra = new Label();
                regra.AutoSize = true;
                regra.MaximumSize = new Size(largura, 0);
                regra.ForeColor = Color.FromArgb(0xAA, 0xAA, 0xAA);
                regra.Font = new Font("Segoe UI", 8f);
                regra.Margin = new Padding(0, (int)(6 * fator), 0, 0);
                regra.Text = "Sempre permitir grava: " + sempre;
                painel.Controls.Add(regra);
            }

            Controls.Add(painel);

            // O balão aparece de surpresa sobre a barra de tarefas: um clique que já ia para lá
            // não pode cair em "Permitir". Os botões só valem depois de meio segundo.
            foreach (Control b in botoes.Controls) b.Enabled = false;
            var armar = new Timer();
            armar.Interval = 500;
            armar.Tick += delegate
            {
                armar.Dispose();
                foreach (Control b in botoes.Controls) b.Enabled = true;
                Armado = true;
            };
            armar.Start();
        }

        Button Botao(string texto, Color fundo, float fator, EventHandler aoClicar)
        {
            var b = new Button();
            b.Text = texto;
            b.AutoSize = true;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Laranja;
            b.BackColor = fundo;
            b.ForeColor = Texto;
            b.Font = new Font("Segoe UI", 9.5f);
            b.Padding = new Padding((int)(4 * fator), (int)(1 * fator), (int)(4 * fator), (int)(1 * fator));
            b.Margin = new Padding(0, 0, (int)(6 * fator), 0);
            b.UseMnemonic = false;
            b.Click += aoClicar;
            return b;
        }

        void Decidir(string json)
        {
            Pedido.Decidir(json);
            Close();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
                return p;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) dica.Dispose();
            base.Dispose(disposing);
        }
    }
}
