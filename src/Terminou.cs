using System;
using System.Drawing;

namespace Claudinho
{
    // Terminou (3 s, Sessoes.TempoComemorando): uma comemoração a cada resposta que acaba,
    // tirada de um baralho; no último segundo as pálpebras pesam, ele empurra o teclado e a lata e senta na pose
    // do Dormindo, que vem em seguida.
    static partial class Sprites
    {
        enum Festa { Confete, Danca, Brinde, Soco }
        const int Festas = 4;
        const long FimDaFesta = Sessoes.TempoComemorando - 1000;

        static readonly Brush[] CorConfete =
        {
            new SolidBrush(Color.FromArgb(0x6C, 0xA8, 0xF0)),
            new SolidBrush(Color.FromArgb(0x4C, 0xC3, 0x8A)),
            new SolidBrush(Color.FromArgb(0xFF, 0xC8, 0x3D)),
            new SolidBrush(Color.FromArgb(0xF2, 0x8B, 0xB0)),
        };

        static void DesenharTerminou(Tela t, Sessao s, long agora)
        {
            long m = Math.Max(0, agora - s.Desde);
            // Baralho pela contagem de respostas: a cada 4 respostas, as 4 comemorações saem
            // uma vez cada, sem repetir a anterior.
            int n = Math.Max(0, s.Comemoracoes - 1);
            var festa = (Festa)Baralho(n / Festas, s.Fase, Festas)[n % Festas];
            var p = new Pose();
            int sai = 0;            // quanto o teclado e a lata já foram empurrados
            bool lataNaMao = false;

            if (m < FimDaFesta) lataNaMao = PoseDaFesta(p, festa, m);
            else
            {
                // Indo dormir: uma piscada lenta, os olhos fecham de vez, empurra o teclado e a
                // lata de olhos fechados e senta.
                long d = m - FimDaFesta;
                p.Esquerdo = p.Direito = Braco.Pairando;
                p.OlhosFechados = (d >= 100 && d < 300) || d >= 400;
                if (d >= 600) sai = (int)Math.Round(Math.Min(1.0, (d - 600) / 300.0) * Colunas);
                if (d >= 900) p.Dy = 1;
            }

            DesenharPose(t, p, CorpoX, CorpoTopo);
            int cx = CorpoX + p.Dx, cy = CorpoTopo + p.Dy;
            DesenharTeclado(t, -1, -sai);
            if (!lataNaMao) DesenharLata(t, LataX + sai, LataY, true);
            else if (festa == Festa.Brinde) DesenharBrinde(t, m, cx, cy);
            if (festa == Festa.Confete && m < FimDaFesta) DesenharConfete(t, s, m);
        }

        // Devolve true quando a lata está na mão (o lugar dela fica vazio).
        static bool PoseDaFesta(Pose p, Festa festa, long m)
        {
            p.OlhosFelizes = true;
            switch (festa)
            {
                case Festa.Confete:
                    // Dois pulos de braços para cima; o confete estoura no primeiro.
                    p.Esquerdo = p.Direito = Braco.Cima;
                    if (m < 1400) p.Dy = -(int)Math.Round(4 * Math.Abs(Math.Sin(Math.PI * m / 700.0)));
                    p.Boca = true;
                    return false;
                case Festa.Danca:
                    // Balança de um lado para o outro, um braço para cima de cada vez, no compasso.
                    bool batida = (m / 250) % 2 == 0;
                    p.Dx = batida ? -1 : 1;
                    p.Dy = (m / 125) % 2 == 0 ? -1 : 0;
                    p.Esquerdo = batida ? Braco.Cima : Braco.Lado;
                    p.Direito = batida ? Braco.Lado : Braco.Cima;
                    return false;
                case Festa.Brinde:
                    // Ergue a lata bem alto, balança, dá um gole e devolve.
                    if (m < 1500)
                    {
                        if (m >= 900)
                        {
                            p.OlhosFelizes = false;
                            p.OlhosFechados = true;
                            p.Boca = true;
                            p.Direito = Braco.Nenhum;
                        }
                        else p.Direito = Braco.Cima;
                        return true;
                    }
                    return false;
                default: // Soco
                    // Mão na cintura, dois socos no ar e um pulo com os dois braços.
                    p.Boca = true;
                    if (m < 800) p.Direito = (m / 200) % 2 == 0 ? Braco.Punho : Braco.Lado;
                    else if (m < 1300)
                    {
                        p.Esquerdo = p.Direito = Braco.Punho;
                        p.Dy = -(int)Math.Round(3 * Math.Sin(Math.PI * (m - 800) / 500.0));
                    }
                    else p.Direito = Braco.Punho;
                    return false;
            }
        }

        static void DesenharBrinde(Tela t, long m, int cx, int cy)
        {
            if (m >= 900)
            {
                DesenharGole(t, Gole.Bebe, 0, 1, cx, cy); // a lata inclinada na boca
                return;
            }
            // Lata acima da mão direita erguida; balança no brinde, com brilho em volta.
            int balanco = m >= 400 && (m / 100) % 2 == 0 ? 1 : 0;
            DesenharLata(t, cx + 12 + balanco, cy - 7, true);
            if (m >= 400)
            {
                bool quadro = (m / 150) % 2 == 0;
                t.Px(CorTeclaAcesa, cx + (quadro ? 11 : 16), cy - 8);
                t.Px(CorTeclaAcesa, cx + (quadro ? 16 : 11), cy - 5);
            }
        }

        // 14 papeizinhos que saem de cima da cabeça, sobem e caem com a gravidade.
        static void DesenharConfete(Tela t, Sessao s, long m)
        {
            double seg = (m - 100) / 1000.0;
            if (seg < 0) return;
            for (int i = 0; i < 14; i++)
            {
                long semente = s.Desde * 16 + i;
                double vx = Sorte(semente, 11) % 19 - 9;        // colunas por segundo
                double vy = -(6 + Sorte(semente, 12) % 6);      // linhas por segundo, para cima
                int x = (int)Math.Round(CorpoX + 7 + vx * seg);
                int y = (int)Math.Round(CorpoTopo - 4 + vy * seg + 9 * seg * seg);
                t.Px(CorConfete[Sorte(semente, 13) % CorConfete.Length], x, y);
            }
        }
    }
}
