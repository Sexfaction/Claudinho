using System;
using System.Drawing;

namespace Claudinho
{
    // Onde está cada braço, nas coordenadas do corpo (tronco nas colunas 2–11, linhas 0–5).
    enum Braco { Lado, Pairando, Teclando, Cima, NaCabeca, Esticado, Punho, Nenhum }

    // Uma pose do bichinho: onde o corpo está, para onde olha, o que cada braço faz.
    class Pose
    {
        public int Dx, Dy;              // o corpo inteiro (Dy > 0: sentado, as pernas dobram)
        public int Esticar;             // linhas a mais no alto do tronco (respiração); rosto e braços sobem junto
        public int Inclina;             // tomba para a direita: o alto desloca isto, a base fica
        public int OlhoDx, OlhoDy;      // para onde os olhos olham
        public bool OlhosFechados;      // o olho vira um traço horizontal
        public bool OlhosFelizes;       // o olho vira um "^" (comemorando)
        public bool OlhosX;             // o olho vira um "x" 3×3 (erro, tonto)
        public bool Boca;               // só durante a ação (gole, ronco, bocejo)
        public Braco Esquerdo = Braco.Lado, Direito = Braco.Lado;
    }

    // O corpo montado por partes (tronco, olhos, boca, braços, pernas), para cada estado poder
    // mexer em cada uma.
    static partial class Sprites
    {
        // Pares (coluna, linha) do braço esquerdo; o direito é o espelho (coluna 13 - c).
        static readonly int[][] PixelsDoBraco =
        {
            new[] { 0, 2, 1, 2, 0, 3, 1, 3 },       // Lado
            new[] { 0, 3, 1, 3, 0, 4, 1, 4 },       // Pairando: sobre o teclado, sem encostar
            new[] { 0, 4, 1, 4, 0, 5, 1, 5 },       // Teclando: encosta na linha do teclado
            new[] { 1, 0, 1, -1, 0, -2 },           // Cima
            new[] { 1, 1, 1, 0, 2, -1, 3, -1 },     // NaCabeca
            new[] { -2, 2, -1, 2, 0, 2, 1, 2 },     // Esticado
            new[] { 1, 0, 1, -1, 0, -2, 1, -2, 0, -3, 1, -3 }, // Punho: braço reto para cima, mão fechada 2×2
            new int[0],                             // Nenhum: quem chama desenha o braço
        };

        // A célula de um bichinho na tela: guarda o Graphics, a coluna inicial e a escala.
        class Tela
        {
            readonly Graphics g;
            readonly int x0, escala;

            public Tela(Graphics g, int x0, int escala)
            {
                this.g = g;
                this.x0 = x0;
                this.escala = escala;
            }

            public void Px(Brush cor, int col, int linha)
            {
                Pixel(g, cor, x0, col, linha, escala);
            }
        }

        // Desenha a pose com o canto do corpo em (corpoX, topo). Objetos nas mãos vão em
        // relação a (corpoX + Dx, topo + Dy), para acompanhar o corpo.
        // Rosto e braços são desenhados com o alto do rosto em cy - Esticar.
        static void DesenharPose(Tela t, Pose p, int corpoX, int topo)
        {
            int cx = corpoX + p.Dx, cy = topo + p.Dy, rosto = cy - p.Esticar;
            for (int linha = -p.Esticar; linha < 6; linha++)
                for (int col = 2; col <= 11; col++)
                    PxPose(t, p, CorCorpo, cx + col, cy + linha, cy);
            foreach (int col in new[] { 2, 4, 9, 11 })
            {
                t.Px(CorCorpo, cx + col, cy + 6);
                t.Px(CorCorpo, cx + col, cy + 7);
            }
            foreach (int col in new[] { 4, 9 })
            {
                int x = cx + col + p.OlhoDx;
                if (p.OlhosX)
                {
                    // Centrado uma linha abaixo do olho: na linha 0 ele encostaria no topo da cabeça.
                    for (int d = -1; d <= 1; d++)
                    {
                        PxPose(t, p, CorOlho, x + d, rosto + 2 + d, cy);
                        PxPose(t, p, CorOlho, x + d, rosto + 2 - d, cy);
                    }
                }
                else if (p.OlhosFelizes)
                {
                    PxPose(t, p, CorOlho, x - 1, rosto + 2 + p.OlhoDy, cy);
                    PxPose(t, p, CorOlho, x, rosto + 1 + p.OlhoDy, cy);
                    PxPose(t, p, CorOlho, x + 1, rosto + 2 + p.OlhoDy, cy);
                }
                else if (p.OlhosFechados)
                {
                    // Fechado: um traço de 3 pixels na linha de baixo do olho.
                    for (int d = -1; d <= 1; d++) PxPose(t, p, CorOlho, x + d, rosto + 2 + p.OlhoDy, cy);
                }
                else
                {
                    PxPose(t, p, CorOlho, x, rosto + 1 + p.OlhoDy, cy);
                    PxPose(t, p, CorOlho, x, rosto + 2 + p.OlhoDy, cy);
                }
            }
            if (p.Boca)
            {
                PxPose(t, p, CorOlho, cx + 6, rosto + 3, cy);
                PxPose(t, p, CorOlho, cx + 7, rosto + 3, cy);
            }
            DesenharBraco(t, p, p.Esquerdo, false, cx, rosto, cy);
            DesenharBraco(t, p, p.Direito, true, cx, rosto, cy);
        }

        static void DesenharBraco(Tela t, Pose p, Braco braco, bool direito, int cx, int rosto, int cy)
        {
            int[] pares = PixelsDoBraco[(int)braco];
            for (int i = 0; i < pares.Length; i += 2)
                PxPose(t, p, CorCorpo, cx + (direito ? 13 - pares[i] : pares[i]), rosto + pares[i + 1], cy);
        }

        // Um pixel do tronco para cima, deslocado pela inclinação: a linha do alto anda
        // Inclina, a de baixo (5) não anda.
        static void PxPose(Tela t, Pose p, Brush cor, int col, int linha, int cy)
        {
            int r = Math.Max(0, Math.Min(5, linha - cy));
            t.Px(cor, col + (int)Math.Round(p.Inclina * (5 - r) / 5.0), linha);
        }

        // Número pseudoaleatório fixo para cada par (a, b): a mesma hora sorteia sempre o mesmo.
        static int Sorte(long a, long b)
        {
            unchecked
            {
                ulong x = (ulong)a * 0x9E3779B97F4A7C15UL + (ulong)b * 0xC2B2AE3D27D4EB4FUL + 0x165667B19E3779F9UL;
                x ^= x >> 31;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 29;
                return (int)(x & 0x7FFFFFFF);
            }
        }

        // Uma piscada (130 ms) a cada 3 s, num momento sorteado dentro deles.
        static bool Piscando(long tt)
        {
            long janela = tt / 3000;
            long d = tt % 3000 - (400 + Sorte(janela, 5) % 2200);
            return d >= 0 && d < 130;
        }
    }
}
