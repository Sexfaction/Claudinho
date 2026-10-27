using System;
using System.Drawing;

namespace Claudinho
{
    // Dormindo: sentado e "derretido", olhos fechados, braços caídos, respirando devagar. No fim
    // de cada bloco de 12 s entra uma variação do baralho, como no Trabalhando. Dormindo, a janela
    // anima a 4 quadros por segundo: nada aqui muda em menos de 250 ms.
    static partial class Sprites
    {
        enum Sono { Baba, Ronco, Bolha, Cabeceada, Cocada }

        static readonly Brush CorBaba = new SolidBrush(Color.FromArgb(0x9F, 0xD3, 0xF0));
        static readonly string[] Serrote = { "O.O.O.", ".O.O.O" }; // o "Z" serrilhado do ronco
        // Os "Z" do sono: nascem maiores perto da cabeça e diminuem ao subir. Acima da cabeça há
        // só 12 linhas; com tamanhos 5, 4 e 3 os três se encostavam e viravam uma corrente.
        static readonly string[][] Zs =
        {
            new[] { "OOOO", "..O.", ".O..", "OOOO" },
            new[] { "OOO", ".O.", "OOO" },
            new[] { "OOO", ".O.", "OOO" },
        };
        const long VidaDoZ = 3600, IntervaloDoZ = 1200; // sempre 3 na tela
        // A bolha de ranho em três tamanhos; a maior é redonda (cantos vazios). (Com 3×3 de
        // cantos vazios ela vira uma cruz.)
        static readonly string[][] Bolhas =
        {
            new[] { "O" },
            new[] { "OO", "OO" },
            new[] { ".OO.", "OOOO", "OOOO", ".OO." },
        };

        static void DesenharDormindo(Tela t, Sessao s, long agora)
        {
            // A Fase entra no tempo para dois bichinhos dormindo não respirarem juntos.
            long tt = Math.Max(0, agora - s.Desde) + s.Fase;
            long k = tt / Bloco;
            var v = (Sono)VariacaoDoBloco(k, s.Fase);
            long m = tt % Bloco - (Bloco - DuracaoDoSono(v)); // ms dentro da variação (< 0: ainda não)

            var p = new Pose();
            p.Dy = 1;                                   // sentado: as pernas dobram
            p.OlhosFechados = true;
            p.Esquerdo = p.Direito = Braco.Pairando;    // braços caídos
            p.Esticar = tt % 3000 >= 1500 ? 1 : 0;      // inspira e expira, 1,5 s cada
            if (m >= 0) PoseDoSono(p, v, m);
            DesenharZzz(t, tt); // antes do corpo: o "Z" novo sai de trás da cabeça
            DesenharPose(t, p, CorpoX, CorpoTopo);

            int cx = CorpoX + p.Dx, rosto = CorpoTopo + p.Dy - p.Esticar;
            if (m >= 0) EfeitoDoSono(t, v, m, cx, rosto);
        }

        // Fila de "Z" subindo em diagonal para a direita, do alto da cabeça até o topo da célula.
        // Cada um vive 3,6 s e muda de tamanho a cada terço da subida; um novo nasce a cada 1,2 s.
        static void DesenharZzz(Tela t, long tt)
        {
            for (int i = 0; i < VidaDoZ / IntervaloDoZ; i++)
            {
                double q = (tt % IntervaloDoZ + i * IntervaloDoZ) / (double)VidaDoZ;
                string[] z = Zs[Math.Min(Zs.Length - 1, (int)(q * Zs.Length))];
                double centroX = CorpoX + 9 + q * 8, centroY = CorpoTopo - 2 - q * 12;
                Grade(t, z, CorZ, (int)Math.Round(centroX - z.Length / 2.0), (int)Math.Round(centroY - z.Length / 2.0));
            }
        }

        static long DuracaoDoSono(Sono v)
        {
            switch (v)
            {
                case Sono.Baba: return 6500;
                case Sono.Ronco: return 3000;
                case Sono.Bolha: return 5000;
                case Sono.Cabeceada: return 3500;
                default: return 2500; // Cocada
            }
        }

        static void PoseDoSono(Pose p, Sono v, long m)
        {
            switch (v)
            {
                case Sono.Baba:
                    p.Boca = m < 3000;                  // boca entreaberta enquanto escorre
                    break;
                case Sono.Ronco:
                    if (m < 1000)
                    {
                        // Puxa o ar: estica e abre os braços.
                        p.Esticar = 1;
                        p.Esquerdo = p.Direito = Braco.Lado;
                    }
                    else if (m < 2250)
                    {
                        // Solta roncando: boca aberta, treme.
                        p.Esticar = 0;
                        p.Boca = true;
                        p.Dx = (m / 250) % 2 == 0 ? 0 : 1;
                    }
                    break;
                case Sono.Bolha:
                    if (m >= 4000 && m < 4500)
                    {
                        // A bolha estourou: acorda num susto e senta direito.
                        p.OlhosFechados = false;
                        p.Dy = 0;
                        p.Esticar = 0;
                    }
                    break;
                case Sono.Cabeceada:
                    // Tomba devagar, quase cai e se endireita num tranco (olhos abrem um instante).
                    // (Inclinação 3 vira uma escada de pixels e o corpo parece quebrado.)
                    if (m < 2250) p.Inclina = m < 1200 ? 1 : 2;
                    else if (m < 2750)
                    {
                        p.Dy = 0;
                        p.Esticar = 0;
                        p.OlhosFechados = m >= 2500;
                    }
                    break;
                case Sono.Cocada:
                    // Coça a cabeça sem acordar.
                    if (m < 400) p.Esquerdo = Braco.Cima;
                    else if (m < 2100) p.Esquerdo = (m / 250) % 2 == 0 ? Braco.NaCabeca : Braco.Cima;
                    break;
            }
        }

        // Os efeitos em volta do rosto; (cx, rosto) é o canto do corpo com o rosto no lugar.
        static void EfeitoDoSono(Tela t, Sono v, long m, int cx, int rosto)
        {
            switch (v)
            {
                case Sono.Baba:
                    // Gota no canto da boca, vira fio, pinga e faz uma poça no chão que seca.
                    if (m < 2400)
                    {
                        int fio = m < 800 ? 1 : m < 1600 ? 2 : 3;
                        for (int i = 0; i < fio; i++) t.Px(CorBaba, cx + 8, rosto + 3 + i);
                    }
                    else if (m < 3000)
                    {
                        t.Px(CorBaba, cx + 8, rosto + 3);
                        int y = rosto + 6 + (int)((m - 2400) / 150);
                        if (y < Linhas - 1) t.Px(CorBaba, cx + 8, y);
                    }
                    if (m >= 2900)
                    {
                        int largura = m < 5500 ? 3 : 1;
                        for (int i = 0; i < largura; i++) t.Px(CorBaba, cx + (largura == 3 ? 6 : 7) + i, Linhas - 1);
                    }
                    break;
                case Sono.Ronco:
                    if (m >= 1000 && m < 2250)
                        // Do lado esquerdo: o direito é da fila de "Z".
                        Grade(t, Serrote, CorZ, cx - 1, rosto - 3 - (int)((m - 1000) / 250));
                    break;
                case Sono.Bolha:
                    // Pendura do nariz, cresce e encolhe com a respiração, cresce de novo e estoura.
                    int b = m < 1000 ? 0 : m < 2000 ? 1 : m < 2500 ? 0 : m < 3000 ? 1 : m < 3750 ? 2 : -1;
                    if (b >= 0) Grade(t, Bolhas[b], CorEspirro, cx + 6, rosto + 3);
                    if (b >= 1) t.Px(CorTeclaAcesa, cx + 6 + b - 1, rosto + 3 + b - 1); // brilho
                    if (m >= 3750 && m < 4000)
                    {
                        t.Px(CorEspirro, cx + 5, rosto + 2);
                        t.Px(CorEspirro, cx + 10, rosto + 2);
                        t.Px(CorEspirro, cx + 5, rosto + 7);
                        t.Px(CorEspirro, cx + 10, rosto + 7);
                    }
                    break;
            }
        }

        static void Grade(Tela t, string[] grade, Brush cor, int col0, int linha0)
        {
            for (int linha = 0; linha < grade.Length; linha++)
                for (int col = 0; col < grade[linha].Length; col++)
                    if (grade[linha][col] != '.') t.Px(cor, col0 + col, linha0 + linha);
        }
    }
}
