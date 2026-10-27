using System;
using System.Drawing;

namespace Claudinho
{
    // A arte em pixel art. Cada bichinho ocupa uma célula de Colunas × Linhas "pixels"; cada pixel
    // vira um quadrado de lado `escala` na tela. O corpo fica encostado no pé da célula; o espaço
    // acima é para pulos, "Z", placa e confete. O corpo é montado por partes (Corpo.cs) e cada
    // estado tem o seu arquivo (Trabalhando.cs, Dormindo.cs...).
    static partial class Sprites
    {
        public const int Colunas = 20;
        public const int Linhas = 20;
        const int CorpoX = 3;   // coluna onde o corpo começa
        const int CorpoTopo = Linhas - 8;

        public const int ColunasMedidor = 6; // as duas barrinhas da cota, à esquerda dos bichinhos

        static readonly Brush CorCorpo = new SolidBrush(Color.FromArgb(0xD9, 0x77, 0x57));
        static readonly Brush CorOlho = new SolidBrush(Color.FromArgb(0x1A, 0x1A, 0x1A));
        // Tons médios: precisam aparecer tanto na barra escura quanto na clara.
        static readonly Brush CorZ = new SolidBrush(Color.FromArgb(0x7F, 0xA7, 0xD9));
        static readonly Brush CorAlerta = new SolidBrush(Color.FromArgb(0xFF, 0xC8, 0x3D));
        static readonly Brush CorCheck = new SolidBrush(Color.FromArgb(0x4C, 0xC3, 0x8A));
        static readonly Brush CorVermelho = new SolidBrush(Color.FromArgb(0xE5, 0x48, 0x4D));
        static readonly Brush CorMarrom = new SolidBrush(Color.FromArgb(0xB0, 0x7A, 0x3A));
        static readonly Brush CorTrilho = new SolidBrush(Color.FromArgb(0x55, 0x55, 0x55));

        public static void Desenhar(Graphics g, Sessao s, long agora, int x0, int escala)
        {
            var t = new Tela(g, x0, escala);
            switch (s.Estado)
            {
                case Estado.Dormindo: DesenharDormindo(t, s, agora); break;
                case Estado.Acordando: DesenharAcordando(t, s, agora); break;
                case Estado.Trabalhando: DesenharTrabalhando(t, s, agora); break;
                case Estado.PrecisaDeVoce: DesenharPrecisaDeVoce(t, s, agora); break;
                case Estado.Terminou: DesenharTerminou(t, s, agora); break;
                case Estado.Erro: DesenharErro(t, s, agora); break;
                case Estado.SemCota: DesenharSemCota(t, s, agora); break;
                case Estado.Compactando: DesenharCompactando(t, s, agora); break;
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

        static void Pixel(Graphics g, Brush cor, int x0, int col, int linha, int escala)
        {
            if (linha < 0 || col < 0 || col >= Colunas || linha >= Linhas) return;
            g.FillRectangle(cor, x0 + col * escala, linha * escala, escala, escala);
        }
    }
}
