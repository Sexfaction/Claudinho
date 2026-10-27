using System;
using System.Drawing;

namespace Claudinho
{
    // Acordando (1,2 s, Sessoes.TempoAcordando): sai da pose do Dormindo, leva um susto,
    // espreguiça bocejando e puxa o teclado e a lata, terminando na pose do Trabalhando.
    static partial class Sprites
    {
        static void DesenharAcordando(Tela t, Sessao s, long agora)
        {
            long m = Math.Max(0, agora - s.Desde);
            var p = new Pose();
            int teclado = -1; // deslocamento do teclado e da lata (-1: ainda não chegaram)

            if (m < 150)
            {
                // Ainda dormindo (a mesma pose do Dormindo): o prompt chegou agora.
                p.Dy = 1;
                p.OlhosFechados = true;
                p.Esquerdo = p.Direito = Braco.Pairando;
            }
            else if (m < 300)
            {
                // Susto: olhos abertos, pulinho, braços para os lados.
                p.Dy = -1;
                p.Esquerdo = p.Direito = Braco.Esticado;
            }
            else if (m < 450)
            {
                p.Esquerdo = p.Direito = Braco.Lado; // cai sentado
            }
            else if (m < 850)
            {
                // Espreguiça bocejando: braços para cima, corpo esticado, tremidinho no fim.
                p.Esquerdo = p.Direito = Braco.Cima;
                p.Esticar = 1;
                p.OlhosFechados = true;
                p.Boca = true;
                if (m >= 650) p.Dx = (m / 100) % 2 == 0 ? 0 : 1;
            }
            else
            {
                // Puxa o teclado (vem da esquerda) e a lata (vem da direita) e olha para eles.
                double q = Math.Min(1.0, (m - 850) / 250.0); // chega 100 ms antes do fim e assenta
                teclado = (int)Math.Round((1 - q) * Colunas);
                p.Esquerdo = p.Direito = Braco.Pairando;
                p.OlhoDy = q >= 1 ? 1 : 0;
            }

            DesenharPose(t, p, CorpoX, CorpoTopo);
            if (teclado >= 0)
            {
                DesenharTeclado(t, -1, -teclado);
                DesenharLata(t, LataX + teclado, LataY, false);
            }
        }
    }
}
