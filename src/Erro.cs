using System;
using System.Drawing;

namespace Claudinho
{
    // Erro, em dois casos (Sessao.DepoisDoErro diz qual):
    // - ferramenta falhou (2,5 s, volta a trabalhar): faísca no teclado, susto com olhos "x x",
    //   balança a cabeça e volta a digitar; ou, sorteado, põe a mão na testa;
    // - a resposta morreu (5 s, dorme): faísca, susto, o teclado solta fumaça, ele afunda atrás
    //   dele, empurra o teclado de olhos fechados e senta na pose do Dormindo.
    static partial class Sprites
    {
        static readonly Brush CorFumaca = new SolidBrush(Color.FromArgb(0x8A, 0x8A, 0x8A));
        static readonly Brush CorFumacaClara = new SolidBrush(Color.FromArgb(0xB4, 0xB4, 0xB4));

        static void DesenharErro(Tela t, Sessao s, long agora)
        {
            long m = Math.Max(0, agora - s.Desde);
            var p = new Pose();
            int tecla = -1, sai = 0;
            bool fatal = s.DepoisDoErro == Estado.Dormindo;
            bool maoNaTesta = !fatal && Sorte(s.Desde, s.Fase) % 2 == 1;
            bool fumaca = false;

            if (m < 150)
            {
                // A faísca pega ele digitando.
                p.Esquerdo = p.Direito = Braco.Teclando;
                p.OlhoDy = 1;
            }
            else if (m < 600 && !maoNaTesta)
            {
                // Susto: pula, braços abertos, olhos "x x"; cai de volta.
                p.Dy = m < 450 ? -2 : 0;
                p.Esquerdo = p.Direito = m < 450 ? Braco.Esticado : Braco.Lado;
                p.OlhosX = true;
            }
            else if (!fatal && !maoNaTesta)
            {
                if (m < 1500)
                {
                    // Balança a cabeça para se recompor; os olhos voltam no meio.
                    p.Dx = (m / 100) % 2 == 0 ? -1 : 1;
                    p.Esquerdo = p.Direito = Braco.Pairando;
                    p.OlhosX = m < 1100;
                }
                else tecla = Digitar(p, m, false);
            }
            else if (maoNaTesta)
            {
                if (m < 450) p.Dy = -1; // sustinho
                if (m < 1700)
                {
                    // Mão na testa, balançando a cabeça devagar; o olho de fora fechado.
                    p.Esquerdo = Braco.Nenhum;
                    p.Direito = Braco.Pairando;
                    p.OlhosFechados = true;
                    if (m >= 450) p.Dx = (m / 300) % 2 == 0 ? 0 : 1;
                }
                else tecla = Digitar(p, m, false);
            }
            else
            {
                // Resposta morreu: fumaça, afunda devagar, apaga.
                p.Esquerdo = p.Direito = Braco.Pairando;
                if (m < 4300)
                {
                    fumaca = true;
                    p.OlhosX = true;
                    p.Dy = m < 1500 ? 0 : m < 2500 ? 1 : 2;
                }
                else
                {
                    p.OlhosFechados = true;
                    p.Dy = 1;
                    sai = (int)Math.Round(Math.Min(1.0, (m - 4300) / 300.0) * Colunas);
                }
            }

            DesenharPose(t, p, CorpoX, CorpoTopo);
            int cx = CorpoX + p.Dx, cy = CorpoTopo + p.Dy;
            if (maoNaTesta && p.Esquerdo == Braco.Nenhum)
            {
                // O braço sobe do lado e a mão cobre o olho esquerdo (mais escuros, para aparecer
                // na frente do corpo).
                t.Px(CorBracoCruzado, cx, cy + 3);
                t.Px(CorBracoCruzado, cx + 1, cy + 3);
                for (int col = 1; col <= 5; col++) t.Px(CorBracoCruzado, cx + col, cy + 2);
                for (int col = 3; col <= 6; col++) t.Px(CorBracoCruzado, cx + col, cy + 1);
            }
            DesenharTeclado(t, tecla, -sai);
            DesenharLata(t, LataX + sai, LataY, true);
            if (m < 300) Faisca(t, m);
            if (fumaca) Fumaca(t, m);
        }

        // Estalos amarelos e brancos pulando do teclado.
        static void Faisca(Tela t, long m)
        {
            bool quadro = (m / 75) % 2 == 0;
            t.Px(CorAlerta, quadro ? 7 : 10, TecladoY - 1);
            t.Px(CorTeclaAcesa, quadro ? 9 : 6, TecladoY - 2);
            t.Px(CorAlerta, quadro ? 12 : 4, TecladoY - 1);
            t.Px(CorAlerta, quadro ? 5 : 13, TecladoY - 3);
            t.Px(CorTeclaAcesa, quadro ? 11 : 8, TecladoY);
        }

        // Fumaça subindo em espiral da ponta esquerda do teclado (na frente do corpo, parecia
        // sujeira): 5 baforadas, cada uma sobe 12 linhas em 1,6 s; nasce 2×2 e afina.
        static void Fumaca(Tela t, long m)
        {
            for (int i = 0; i < 5; i++)
            {
                long idade = (m + i * 320) % 1600;
                double q = idade / 1600.0;
                int x = TecladoX + 1 + (int)Math.Round(Math.Sin(idade / 220.0 + i) * 1.2);
                int y = TecladoY - 1 - (int)Math.Round(q * 12);
                Brush cor = q < 0.5 ? CorFumaca : CorFumacaClara;
                t.Px(cor, x, y);
                if (q < 0.35)
                {
                    t.Px(cor, x + 1, y);
                    t.Px(cor, x, y - 1);
                    t.Px(cor, x + 1, y - 1);
                }
            }
        }
    }
}
