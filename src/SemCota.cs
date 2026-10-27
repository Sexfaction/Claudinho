using System;
using System.Drawing;

namespace Claudinho
{
    // Sem cota: largado no chão, sem energia, com a lata vazia tombada do lado. A cada 20 s
    // (pode durar horas, então o resto é quieto) pega a lata, vira de ponta-cabeça sobre a boca,
    // sacode, cai uma gota só, espia dentro, suspira e larga de novo.
    static partial class Sprites
    {
        const long CicloSemCota = 20000, DuracaoDaLataVazia = 4000;

        static void DesenharSemCota(Tela t, Sessao s, long agora)
        {
            long tt = Math.Max(0, agora - s.Desde) + s.Fase;
            long m = tt % CicloSemCota - (CicloSemCota - DuracaoDaLataVazia); // < 0: só largado

            var p = new Pose();
            p.Dx = -2;                                  // abre espaço para a lata à direita
            p.Dy = 2;                                   // largado no chão: as pernas somem
            p.Inclina = 1;
            p.OlhosFechados = true;                     // olhos pesados
            p.Esquerdo = p.Direito = Braco.Pairando;    // braços caídos
            p.Esticar = tt % 4000 >= 2000 ? 1 : 0;      // respiração bem lenta

            if (m >= 0)
            {
                p.Inclina = 0;
                p.Esticar = 0;
                if (m < 400)
                {
                    // Abre os olhos e olha para a lata.
                    p.OlhosFechados = false;
                    p.OlhoDx = 1;
                }
                else if (m < 2400)
                {
                    // Lata de ponta-cabeça sobre a boca, olhando para ela, boca aberta.
                    p.OlhosFechados = false;
                    p.Boca = true;
                    p.Direito = Braco.Nenhum;
                }
                // 2400–3200, espiando: o olho esquerdo fica apertado (OlhosFechados).
                else
                {
                    // Suspira: enche o peito e murcha mais um pouco.
                    p.Esticar = m < 3600 ? 1 : 0;
                    p.Dy = m < 3600 ? 2 : 3;
                }
            }

            DesenharPose(t, p, CorpoX, CorpoTopo);
            int cx = CorpoX + p.Dx, rosto = CorpoTopo + p.Dy - p.Esticar;

            if (m < 400 || m >= 3200) DesenharLataDeitada(t, cx + 14, Linhas - 3);
            else if (m < 2400)
            {
                // Braço do ombro até a lata, que está sobre a cabeça; sacode de 1200 a 2000.
                int sacode = m >= 1200 && m < 2000 && (m / 100) % 2 == 0 ? 1 : 0;
                for (int i = 0; i < 4; i++) t.Px(CorCorpo, cx + 12 - i + (i == 3 ? sacode : 0), rosto + 1 - i);
                DesenharLataDePontaCabeca(t, cx + 6 + sacode, rosto - 7);
                if (m >= 2000)
                {
                    // Uma gota só, da tampa até a boca.
                    int y = rosto - 2 + (int)((m - 2000) / 80);
                    if (y <= rosto + 3) t.Px(CorBaba, cx + 7, y);
                }
            }
            else
            {
                // Espiando: tampa encostada no olho direito, o braço segura a outra ponta.
                DesenharLataDeitada(t, cx + 10, rosto + 1);
            }
        }

        // Lata de ponta-cabeça (3×5): tampa embaixo, com o furo.
        static void DesenharLataDePontaCabeca(Tela t, int col, int linha)
        {
            for (int i = 0; i < 4; i++)
            {
                Brush cor = i == 2 ? CorFaixa : CorLata;
                t.Px(cor, col, linha + i);
                t.Px(cor, col + 1, linha + i);
                t.Px(CorLataSombra, col + 2, linha + i);
            }
            t.Px(CorTampa, col, linha + 4);
            t.Px(CorOlho, col + 1, linha + 4);
            t.Px(CorTampa, col + 2, linha + 4);
        }

        // Lata deitada (5×3): tampa à esquerda (com o furo no meio), faixa, sombra embaixo.
        static void DesenharLataDeitada(Tela t, int col, int linha)
        {
            for (int i = 0; i < 3; i++)
                t.Px(i == 1 ? CorOlho : CorTampa, col, linha + i);
            for (int c = 1; c < 5; c++)
            {
                Brush cor = c == 2 ? CorFaixa : CorLata;
                t.Px(cor, col + c, linha);
                t.Px(cor, col + c, linha + 1);
                t.Px(CorLataSombra, col + c, linha + 2);
            }
        }
    }
}
