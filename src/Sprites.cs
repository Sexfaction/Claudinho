using System;
using System.Drawing;

namespace Claudinho
{
    // A arte em pixel art, desenhada a partir de grades de caracteres.
    // Cada bichinho ocupa uma célula de Colunas × Linhas "pixels"; cada pixel vira um quadrado de
    // lado `escala` na tela. O corpo fica encostado no pé da célula; o espaço acima é para pulos,
    // "Z", "!" e o check.
    static class Sprites
    {
        public const int Colunas = 20;
        public const int Linhas = 20;
        const int CorpoX = 3;   // coluna onde o corpo começa
        const int CorpoTopo = Linhas - 8;

        // O = corpo, E = olho, . = vazio. Braços são as pontas das linhas 2 e 3.
        static readonly string[] Corpo =
        {
            "..OOOOOOOOOO..",
            "..OOEOOOOEOO..",
            "OOOOEOOOOEOOOO",
            "OOOOOOOOOOOOOO",
            "..OOOOOOOOOO..",
            "..OOOOOOOOOO..",
            "..O.O....O.O..",
            "..O.O....O.O..",
        };
        // Pernas no meio do passo: as de fora levantam.
        const string PernasPasso = "....O......O..";

        public const int ColunasMedidor = 6; // as duas barrinhas da cota, à esquerda dos bichinhos

        // Grades de efeito. Letras: O = cor do efeito, R = vermelho, M = marrom, Y = palha, C = cinza.
        static readonly string[] Z = { "OOOO", "..O.", ".O..", "OOOO" };
        static readonly string[] XVermelho = { "R...R", ".R.R.", "..R..", ".R.R.", "R...R" };
        static readonly string[] Bateria = { "CCCCCCC.", "CRR...CC", "CRR...CC", "CCCCCCC." };
        static readonly string[] Vassoura = { "..M", "..M", ".M.", ".M.", "YYY", "YYY", "YYY" };
        static readonly string[] Exclamacao = { "OO", "OO", "OO", "OO", "..", "OO" };
        static readonly string[] Check =
        {
            "......O",
            ".....OO",
            "O...OO.",
            "OO.OO..",
            ".OOO...",
            "..O....",
        };

        static readonly Brush CorCorpo = new SolidBrush(Color.FromArgb(0xD9, 0x77, 0x57));
        static readonly Brush CorOlho = new SolidBrush(Color.FromArgb(0x1A, 0x1A, 0x1A));
        // Tons médios: precisam aparecer tanto na barra escura quanto na clara.
        static readonly Brush CorZ = new SolidBrush(Color.FromArgb(0x7F, 0xA7, 0xD9));
        static readonly Brush CorAlerta = new SolidBrush(Color.FromArgb(0xFF, 0xC8, 0x3D));
        static readonly Brush CorCheck = new SolidBrush(Color.FromArgb(0x4C, 0xC3, 0x8A));
        static readonly Brush CorPontoAceso = new SolidBrush(Color.FromArgb(0xF2, 0xA0, 0x7B));
        static readonly Brush CorPontoApagado = new SolidBrush(Color.FromArgb(0x9A, 0x8C, 0x80));
        static readonly Brush CorVermelho = new SolidBrush(Color.FromArgb(0xE5, 0x48, 0x4D));
        static readonly Brush CorMarrom = new SolidBrush(Color.FromArgb(0xB0, 0x7A, 0x3A));
        static readonly Brush CorPalha = new SolidBrush(Color.FromArgb(0xE8, 0xC1, 0x5A));
        static readonly Brush CorCinza = new SolidBrush(Color.FromArgb(0x9A, 0x9A, 0x9A));
        static readonly Brush CorTrilho = new SolidBrush(Color.FromArgb(0x55, 0x55, 0x55));
        static readonly Brush[] CorBolas =
        {
            new SolidBrush(Color.FromArgb(0x6C, 0xA8, 0xF0)),
            new SolidBrush(Color.FromArgb(0x4C, 0xC3, 0x8A)),
            new SolidBrush(Color.FromArgb(0xFF, 0xC8, 0x3D)),
        };

        public static void Desenhar(Graphics g, Sessao s, long agora, int x0, int escala)
        {
            long t = agora + s.Fase;
            long noEstado = agora - s.Desde;
            int dx = 0, dy = 0;
            bool olhosFechados = false, passo = false, bracosCima = false;

            switch (s.Estado)
            {
                case Estado.Dormindo:
                    olhosFechados = true;
                    DesenharZ(g, t, x0, escala);
                    DesenharZ(g, t + 1200, x0, escala);
                    break;

                case Estado.Acordando:
                    dy = -(int)Math.Round(3 * Math.Sin(Math.PI * Math.Min(1.0, noEstado / 600.0)));
                    break;

                case Estado.Trabalhando:
                    passo = (t / 180) % 2 == 1;
                    dy = passo ? -1 : 0;
                    olhosFechados = t % 3000 < 120; // piscada
                    if (s.Subagentes > 0)
                    {
                        DesenharMalabares(g, t, Math.Min(3, s.Subagentes), x0, CorpoTopo + dy, escala);
                    }
                    else
                    {
                        long aceso = (t / 300) % 3;
                        for (int i = 0; i < 3; i++)
                            Pixel(g, i == aceso ? CorPontoAceso : CorPontoApagado,
                                x0, CorpoX + 4 + i * 3, CorpoTopo - 2 + dy, escala);
                    }
                    break;

                case Estado.Erro:
                    if (noEstado < 600) dx = (t / 60) % 2 == 0 ? -1 : 1; // treme e para
                    Grade(g, XVermelho, CorVermelho, x0, CorpoX + 5, CorpoTopo - 7, escala);
                    break;

                case Estado.SemCota:
                    olhosFechados = true;
                    if (t % 1000 < 700) Grade(g, Bateria, CorCinza, x0, CorpoX + 3, CorpoTopo - 6, escala);
                    break;

                case Estado.Compactando:
                    passo = (t / 250) % 2 == 1; // a vassoura vem depois do corpo, para ficar na frente
                    break;

                case Estado.PrecisaDeVoce:
                    double p = (t % 800) / 800.0;
                    dy = -(int)Math.Round(4 * Math.Abs(Math.Sin(Math.PI * p)));
                    bracosCima = (t / 400) % 2 == 0; // acenando
                    Grade(g, Exclamacao, CorAlerta, x0, CorpoX + 6, CorpoTopo + dy - 8, escala);
                    break;

                case Estado.Terminou:
                    dy = -(int)Math.Round(2 * Math.Sin(Math.PI * Math.Min(1.0, noEstado / 600.0)));
                    bracosCima = true;
                    Grade(g, Check, CorCheck, x0, CorpoX + 3, CorpoTopo + dy - 9, escala);
                    break;
            }

            DesenharCorpo(g, x0, CorpoX + dx, CorpoTopo + dy, escala, olhosFechados, passo, bracosCima);

            if (s.Estado == Estado.Compactando)
            {
                // Varre para lá e para cá do lado direito, levantando poeira.
                int vai = passo ? 1 : 0;
                Grade(g, Vassoura, CorMarrom, x0, CorpoX + 13 + vai, Linhas - Vassoura.Length, escala);
                Pixel(g, CorCinza, x0, CorpoX + 13 + (passo ? 0 : 3), Linhas - 1, escala);
            }
        }

        // Uma bolinha por subagente (até 3), girando em elipse sobre a cabeça.
        static void DesenharMalabares(Graphics g, long t, int bolas, int x0, int topo, int escala)
        {
            for (int k = 0; k < bolas; k++)
            {
                double angulo = t / 500.0 * Math.PI + k * 2 * Math.PI / bolas;
                int col = CorpoX + 6 + (int)Math.Round(5 * Math.Cos(angulo));
                int linha = topo - 4 + (int)Math.Round(2 * Math.Sin(angulo));
                Brush cor = CorBolas[k];
                Pixel(g, cor, x0, col, linha, escala);
                Pixel(g, cor, x0, col + 1, linha, escala);
                Pixel(g, cor, x0, col, linha + 1, escala);
                Pixel(g, cor, x0, col + 1, linha + 1, escala);
            }
        }

        // Duas barras verticais (5 h e semana), enchendo de baixo para cima; verde, amarela
        // a partir de 70%, vermelha a partir de 90%. Janela já renovada conta como 0%.
        public static void DesenharCota(Graphics g, Cota cota, long agoraUnix, int escala)
        {
            DesenharBarra(g, PorcentagemValida(cota.Uso5h, cota.Renova5h, agoraUnix), 0, escala);
            DesenharBarra(g, PorcentagemValida(cota.UsoSemana, cota.RenovaSemana, agoraUnix), 3, escala);
        }

        public static double? PorcentagemValida(double? uso, long renova, long agoraUnix)
        {
            if (!uso.HasValue) return null;
            if (renova > 0 && renova <= agoraUnix) return 0;
            return Math.Max(0, Math.Min(100, uso.Value));
        }

        static void DesenharBarra(Graphics g, double? pct, int col, int escala)
        {
            if (!pct.HasValue) return;
            const int altura = 10;
            int cheias = (int)Math.Round(altura * pct.Value / 100.0);
            Brush cor = pct.Value >= 90 ? CorVermelho : pct.Value >= 70 ? CorAlerta : CorCheck;
            for (int i = 0; i < altura; i++)
            {
                Brush b = i < cheias ? cor : CorTrilho;
                Pixel(g, b, 0, col, Linhas - 1 - i, escala);
                Pixel(g, b, 0, col + 1, Linhas - 1 - i, escala);
            }
        }

        static void DesenharCorpo(Graphics g, int x0, int corpoX, int topo, int escala, bool olhosFechados, bool passo, bool bracosCima)
        {
            for (int linha = 0; linha < Corpo.Length; linha++)
            {
                string texto = (passo && linha == Corpo.Length - 1) ? PernasPasso : Corpo[linha];
                for (int col = 0; col < texto.Length; col++)
                {
                    char c = texto[col];
                    if (c == '.') continue;
                    bool braco = (linha == 2 || linha == 3) && (col < 2 || col > 11);
                    if (braco && bracosCima) continue;
                    // Olho fechado: a metade de cima some, fica só a linha de baixo.
                    Brush cor = (c == 'E' && !(olhosFechados && linha == 1)) ? CorOlho : CorCorpo;
                    Pixel(g, cor, x0, corpoX + col, topo + linha, escala);
                }
            }
            if (bracosCima)
            {
                Pixel(g, CorCorpo, x0, corpoX + 1, topo - 1, escala);
                Pixel(g, CorCorpo, x0, corpoX + 0, topo - 2, escala);
                Pixel(g, CorCorpo, x0, corpoX + 12, topo - 1, escala);
                Pixel(g, CorCorpo, x0, corpoX + 13, topo - 2, escala);
            }
        }

        // Um "Z" que sobe da cabeça e some, num ciclo de 2,4 s.
        static void DesenharZ(Graphics g, long t, int x0, int escala)
        {
            double p = (t % 2400) / 2400.0;
            int x = CorpoX + 10 + (int)Math.Round(p * 2);
            int y = CorpoTopo - 5 - (int)Math.Round(p * 6);
            Grade(g, Z, CorZ, x0, x, y, escala);
        }

        static void Grade(Graphics g, string[] grade, Brush cor, int x0, int col0, int linha0, int escala)
        {
            for (int linha = 0; linha < grade.Length; linha++)
                for (int col = 0; col < grade[linha].Length; col++)
                {
                    char c = grade[linha][col];
                    if (c == '.') continue;
                    Brush b = c == 'R' ? CorVermelho : c == 'M' ? CorMarrom : c == 'Y' ? CorPalha : c == 'C' ? CorCinza : cor;
                    Pixel(g, b, x0, col0 + col, linha0 + linha, escala);
                }
        }

        static void Pixel(Graphics g, Brush cor, int x0, int col, int linha, int escala)
        {
            if (linha < 0 || col < 0 || col >= Colunas || linha >= Linhas) return;
            g.FillRectangle(cor, x0 + col * escala, linha * escala, escala, escala);
        }
    }
}
