using System;
using System.Drawing;

namespace Claudinho
{
    // Compactando (o Claude resumindo a conversa): ele fica em pé numa pilha de folhas e pula;
    // a cada pulo a pilha achata uma linha, até virar um bloquinho. Aí caem folhas novas pelos
    // lados, a pilha cresce de novo (levantando ele junto) e recomeça. Ciclo de 4 s.
    static partial class Sprites
    {
        const int PilhaAlta = 6, Pulos = 5;
        const long TempoDoPulo = 500, TempoDaFolha = 300;
        const long CicloCompactando = Pulos * TempoDoPulo + Pulos * TempoDaFolha;
        const int PilhaX = 1, PilhaFim = 18;

        static void DesenharCompactando(Tela t, Sessao s, long agora)
        {
            long c = (Math.Max(0, agora - s.Desde) + s.Fase) % CicloCompactando;
            var p = new Pose();
            int pilha;
            long folha = -1; // ms da folha que está caindo (-1: nenhuma)

            if (c < Pulos * TempoDoPulo)
            {
                // Pulando: a pilha perde uma linha a cada aterrissagem.
                int pulo = (int)(c / TempoDoPulo);
                long noPulo = c % TempoDoPulo;
                pilha = PilhaAlta - pulo;
                int altura = (int)Math.Round(3 * Math.Sin(Math.PI * noPulo / TempoDoPulo));
                p.Dy = -pilha - altura;
                p.Esquerdo = p.Direito = altura > 0 ? Braco.Cima : Braco.Lado;
                p.OlhosFechados = altura == 0; // aperta os olhos ao aterrissar
            }
            else
            {
                // Caem folhas novas: cada uma que chega sobe a pilha (e ele) uma linha.
                long r = c - Pulos * TempoDoPulo;
                int chegaram = (int)(r / TempoDaFolha);
                folha = r % TempoDaFolha;
                pilha = PilhaAlta - Pulos + chegaram;
                p.Dy = -pilha;
                p.OlhoDx = chegaram % 2 == 0 ? -1 : 1; // olha a folha que vem
                DesenharFolhaCaindo(t, chegaram, folha, pilha);
            }

            DesenharPilha(t, pilha);
            DesenharPose(t, p, CorpoX, CorpoTopo);
        }

        // A pilha: uma linha por folha, alternando branco e cinza-azulado, base e laterais cinza
        // (sem o contorno, o branco some na barra clara e sobram listras soltas).
        static void DesenharPilha(Tela t, int altura)
        {
            for (int i = 0; i < altura; i++)
            {
                int linha = Linhas - 1 - i;
                Brush cor = i == 0 ? CorTampa : i % 2 == 1 ? CorLata : CorLataSombra;
                for (int col = PilhaX; col <= PilhaFim; col++)
                    t.Px(col == PilhaX || col == PilhaFim ? CorTampa : cor, col, linha);
            }
        }

        // Uma folha (4×1, pontas cinza) caindo por um lado da pilha, alternando esquerda e direita.
        static void DesenharFolhaCaindo(Tela t, int k, long noTempo, int pilha)
        {
            int col = k % 2 == 0 ? PilhaX : PilhaFim - 3;
            int ate = Linhas - 1 - pilha; // a linha logo acima da pilha
            int linha = (int)Math.Round(ate * noTempo / (double)TempoDaFolha);
            for (int i = 0; i < 4; i++) t.Px(i == 0 || i == 3 ? CorTampa : CorLata, col + i, linha);
        }
    }
}
