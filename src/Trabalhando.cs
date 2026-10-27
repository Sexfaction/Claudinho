using System;
using System.Drawing;

namespace Claudinho
{
    // Trabalhando: digita num teclado, com uma lata de energético do lado. A base é a digitação
    // em rajadas e pausas; no fim de cada bloco de 12 s entra uma variação, tirada de um baralho
    // (cada uma sai uma vez por rodada, em ordem sorteada, nunca a mesma duas vezes seguidas).
    // Tudo sai do tempo no estado: a mesma hora desenha sempre a mesma pose.
    static partial class Sprites
    {
        enum Variacao { Gole, Estalar, Pensando, Apagar, OlhaVoce }
        const int Variacoes = 5;
        const long Bloco = 12000;
        const long Cafeina = 3000;      // depois do gole, digita no dobro da velocidade
        const int GolesPorLata = 4;

        // Teclado nas 2 linhas de baixo (esconde as pernas); lata em pé à direita dele.
        const int TecladoX = 1, TecladoFim = 16, TecladoY = Linhas - 2;
        const int LataX = TecladoFim + 1, LataY = Linhas - 5;

        // Tons médios: aparecem na barra escura e na clara.
        static readonly Brush CorTeclado = new SolidBrush(Color.FromArgb(0x5A, 0x5A, 0x5A));
        static readonly Brush CorTecla = new SolidBrush(Color.FromArgb(0x8E, 0x8E, 0x8E));
        static readonly Brush CorTeclaAcesa = new SolidBrush(Color.FromArgb(0xF4, 0xF4, 0xF4));
        static readonly Brush CorTecladoFrente = new SolidBrush(Color.FromArgb(0x3C, 0x3C, 0x3C));
        static readonly Brush CorLata = new SolidBrush(Color.FromArgb(0xF4, 0xF4, 0xF4));
        static readonly Brush CorLataSombra = new SolidBrush(Color.FromArgb(0xB8, 0xC0, 0xC8));
        static readonly Brush CorTampa = new SolidBrush(Color.FromArgb(0x80, 0x80, 0x80));
        static readonly Brush CorFaixa = new SolidBrush(Color.FromArgb(0x9F, 0xD3, 0xF0));
        static readonly Brush CorEspirro = new SolidBrush(Color.FromArgb(0xDD, 0xEE, 0xFF));

        // As etapas do gole, com o fim de cada uma (ms desde o começo do gole).
        enum Gole { Alcanca, Ergue, Abre, Bebe, Ahh, Devolve, Amassa, Joga, NovaLata, Fim }
        static readonly long[] FimDoGole = { 350, 750, 1100, 2300, 2800, 3400 };
        static readonly long[] FimDoGoleAmassando = { 350, 750, 1100, 2300, 2800, -1, 3300, 4000, 4400 };

        static void DesenharTrabalhando(Tela t, Sessao s, long agora)
        {
            long tt = Math.Max(0, agora - s.Desde);
            long k = tt / Bloco, noBloco = tt % Bloco;
            long rodada = k / Variacoes;
            int posicao = (int)(k % Variacoes);
            int[] baralho = Baralho(rodada, s.Fase);
            Variacao v = (Variacao)baralho[posicao];
            int golesAntes = (int)(rodada % GolesPorLata);      // goles que a lata já levou nesta rodada
            bool amassando = golesAntes == GolesPorLata - 1;
            long inicio = Bloco - DuracaoDe(v, amassando);
            long m = noBloco - inicio;                            // ms dentro da variação (< 0: ainda não)

            // Quantos goles a lata no lugar já levou: o da rodada conta depois que o bloco dele passou.
            int goles = golesAntes + (posicao > Array.IndexOf(baralho, (int)Variacao.Gole) ? 1 : 0);
            bool lataAberta = goles % GolesPorLata > 0;

            var p = new Pose();
            int tecla = -1;
            Gole etapa = Gole.Fim;
            if (m < 0)
            {
                bool cafeina = k > 0 && noBloco < Cafeina && (Variacao)VariacaoDoBloco(k - 1, s.Fase) == Variacao.Gole;
                tecla = Digitar(p, tt, cafeina);
            }
            else
            {
                switch (v)
                {
                    case Variacao.Gole:
                        etapa = EtapaDoGole(m, amassando);
                        PoseDoGole(p, etapa, m);
                        break;
                    case Variacao.Estalar:
                        // Braços esticados para os lados, tremendo de leve.
                        bool esticado = m >= 200 && m < 1100;
                        p.Esquerdo = p.Direito = esticado ? Braco.Esticado : Braco.Pairando;
                        if (esticado) p.Dy = (m / 80) % 2 == 0 ? 0 : -1;
                        p.OlhosFechados = m >= 400 && m < 900;
                        break;
                    case Variacao.Pensando:
                        // Coça a cabeça e olha para um lado e para o outro; a outra mão tamborila.
                        p.Esquerdo = (m / 250) % 2 == 0 ? Braco.NaCabeca : Braco.Cima;
                        p.Direito = (m / 300) % 2 == 0 ? Braco.Teclando : Braco.Pairando;
                        p.OlhoDx = m < 1250 ? -1 : 1;
                        break;
                    case Variacao.Apagar:
                        // Olhos apertados, martela a tecla do canto direito.
                        p.Esquerdo = Braco.Pairando;
                        bool bate = m >= 300 && m < 1500 && (m / 150) % 2 == 0;
                        p.Direito = bate ? Braco.Teclando : Braco.Pairando;
                        if (bate) tecla = TecladoFim;
                        p.OlhoDy = 1;
                        p.OlhosFechados = true;
                        break;
                    case Variacao.OlhaVoce:
                        // Endireita, olha para a frente e pisca uma vez.
                        p.Dy = -1;
                        p.Esquerdo = p.Direito = Braco.Pairando;
                        p.OlhosFechados = m >= 700 && m < 850;
                        break;
                }
            }

            DesenharPose(t, p, CorpoX, CorpoTopo);
            DesenharTeclado(t, tecla);
            int cx = CorpoX + p.Dx, cy = CorpoTopo + p.Dy;
            if (etapa == Gole.Fim) DesenharLata(t, LataX, LataY, lataAberta);
            else DesenharGole(t, etapa, m, golesAntes, cx, cy);
            if (s.Subagentes > 0 || agora - s.SubagenteSaiu < 500) DesenharMinis(t, s, agora, cx, cy);
        }

        // Rajadas de digitação e pausas curtas, em ciclos de 4 s de tamanho sorteado. Devolve a
        // tecla que acendeu (-1: nenhuma).
        static int Digitar(Pose p, long tt, bool cafeina)
        {
            long ciclo = tt / 4000, noCiclo = tt % 4000;
            p.OlhosFechados = Piscando(tt);
            if (cafeina || noCiclo < 2600 + Sorte(ciclo, 1) % 900)
            {
                long n = tt / (cafeina ? 65 : 130);
                int mao = Sorte(n, 2) % 3; // 0 esquerda, 1 direita, 2 as duas no ar
                p.Esquerdo = mao == 0 ? Braco.Teclando : Braco.Pairando;
                p.Direito = mao == 1 ? Braco.Teclando : Braco.Pairando;
                p.OlhoDy = 1;              // olhando para o teclado
                if (mao == 0) return TecladoX + 1 + Sorte(n, 3) % 7;
                if (mao == 1) return TecladoX + 8 + Sorte(n, 3) % 7;
                return -1;
            }
            // Pausa: tira os olhos do teclado, olha para um lado, tamborila com a mão direita.
            // (Olho uma linha acima encosta no topo da cabeça e parece um recorte.)
            p.OlhoDx = Sorte(ciclo, 4) % 2 == 0 ? -1 : 1;
            p.Esquerdo = Braco.Pairando;
            p.Direito = (noCiclo / 220) % 2 == 0 ? Braco.Teclando : Braco.Pairando;
            return -1;
        }

        // Ordem das variações numa rodada. Se a primeira repetir a última da rodada anterior,
        // troca com a segunda. `quantas`: o Terminou usa o mesmo baralho com 4 comemorações.
        static int[] Baralho(long rodada, int semente, int quantas = Variacoes)
        {
            int[] cartas = Embaralhar(rodada, semente, quantas);
            if (rodada > 0 && cartas[0] == Embaralhar(rodada - 1, semente, quantas)[quantas - 1])
            {
                int x = cartas[0];
                cartas[0] = cartas[1];
                cartas[1] = x;
            }
            return cartas;
        }

        static int[] Embaralhar(long rodada, int semente, int quantas)
        {
            var cartas = new int[quantas];
            for (int i = 0; i < quantas; i++) cartas[i] = i;
            for (int i = quantas - 1; i > 0; i--)
            {
                int j = Sorte(rodada, semente * 31 + i) % (i + 1);
                int x = cartas[i];
                cartas[i] = cartas[j];
                cartas[j] = x;
            }
            return cartas;
        }

        // Serve também ao Dormindo, que tem o mesmo número de variações.
        static int VariacaoDoBloco(long k, int semente)
        {
            return Baralho(k / Variacoes, semente)[(int)(k % Variacoes)];
        }

        static long DuracaoDe(Variacao v, bool amassando)
        {
            switch (v)
            {
                case Variacao.Gole: return amassando ? FimDoGoleAmassando[(int)Gole.NovaLata] : FimDoGole[(int)Gole.Devolve];
                case Variacao.Estalar: return 1500;
                case Variacao.Pensando: return 2500;
                case Variacao.Apagar: return 1800;
                default: return 1600; // OlhaVoce
            }
        }

        static Gole EtapaDoGole(long m, bool amassando)
        {
            long[] fins = amassando ? FimDoGoleAmassando : FimDoGole;
            for (int i = 0; i < fins.Length; i++)
                if (m < fins[i]) return (Gole)i;
            return Gole.Fim;
        }

        static void PoseDoGole(Pose p, Gole etapa, long m)
        {
            p.Esquerdo = Braco.Pairando;
            p.OlhoDx = 1; // olha para a lata
            switch (etapa)
            {
                case Gole.Alcanca:
                case Gole.Devolve:
                    p.Direito = Braco.Nenhum;
                    break;
                case Gole.Bebe:
                    p.Direito = Braco.Nenhum;
                    p.OlhoDx = 0;
                    p.OlhosFechados = true;
                    p.Boca = true;
                    p.Dy = (m / 400) % 2 == 0 ? 0 : -1; // engolindo
                    break;
                case Gole.Ahh:
                    p.OlhoDx = 0;
                    p.OlhosFechados = true;
                    p.Boca = true;
                    p.Dx = (m / 70) % 2 == 0 ? 0 : 1;   // arrepio
                    break;
                case Gole.Amassa:
                    p.OlhosFechados = (m / 100) % 2 == 0; // força
                    break;
                case Gole.Joga:
                    p.Direito = Braco.Cima;
                    p.OlhoDx = 1;
                    break;
            }
        }

        // A lata e o braço direito em cada etapa do gole; (cx, cy) é o canto do corpo.
        static void DesenharGole(Tela t, Gole etapa, long m, int golesAntes, int cx, int cy)
        {
            bool primeiro = golesAntes == 0;
            switch (etapa)
            {
                case Gole.Alcanca:
                case Gole.Devolve:
                    // Braço estendido até a lata, que está no lugar.
                    t.Px(CorCorpo, cx + 12, cy + 4);
                    t.Px(CorCorpo, cx + 13, cy + 4);
                    t.Px(CorCorpo, cx + 13, cy + 5);
                    DesenharLata(t, LataX, LataY, !primeiro || etapa == Gole.Devolve);
                    break;
                case Gole.Ergue:
                    DesenharLata(t, cx + 14, cy - 1, !primeiro);
                    break;
                case Gole.Abre:
                    DesenharLata(t, cx + 14, cy - 1, true);
                    if (primeiro) // "psst": espirra da tampa
                    {
                        bool quadro = (m / 80) % 2 == 0;
                        t.Px(CorEspirro, cx + (quadro ? 15 : 14), cy - 2);
                        t.Px(CorEspirro, cx + (quadro ? 13 : 16), cy - 3);
                        t.Px(CorEspirro, cx + (quadro ? 16 : 15), cy - 4);
                    }
                    break;
                case Gole.Bebe:
                    // Lata inclinada, tampa na boca, fundo para cima; a mão segura o fundo.
                    t.Px(CorTampa, cx + 8, cy + 3);
                    t.Px(CorLata, cx + 9, cy + 3);
                    t.Px(CorLata, cx + 9, cy + 2);
                    t.Px(CorLataSombra, cx + 10, cy + 2);
                    t.Px(CorLata, cx + 10, cy + 1);
                    t.Px(CorLataSombra, cx + 11, cy + 1);
                    t.Px(CorLata, cx + 11, cy);
                    t.Px(CorLataSombra, cx + 12, cy);
                    t.Px(CorCorpo, cx + 12, cy + 2);
                    t.Px(CorCorpo, cx + 13, cy + 2);
                    t.Px(CorCorpo, cx + 13, cy + 1);
                    t.Px(CorCorpo, cx + 13, cy);
                    break;
                case Gole.Ahh:
                    DesenharLata(t, cx + 14, cy - 1, true);
                    break;
                case Gole.Amassa:
                    // Lata achatada na mão.
                    t.Px(CorTampa, cx + 14, cy + 2);
                    t.Px(CorLataSombra, cx + 15, cy + 2);
                    t.Px(CorLata, cx + 14, cy + 3);
                    t.Px(CorLataSombra, cx + 15, cy + 3);
                    break;
                case Gole.Joga:
                    // Voa em arco por cima da cabeça, para trás, e some ao longe (fica com 1 pixel).
                    double q = (m - FimDoGoleAmassando[(int)Gole.Amassa]) / 700.0;
                    int x = cx + 14 - (int)Math.Round(q * 11);
                    int y = cy + 2 - (int)Math.Round(Math.Sin(Math.PI * q * 0.75) * 13);
                    t.Px(q < 0.6 ? CorLata : CorLataSombra, x, y);
                    if (q < 0.6) t.Px(CorTampa, x, y + 1);
                    break;
                case Gole.NovaLata:
                    DesenharLata(t, LataX, LataY, false);
                    break;
            }
        }

        // Lata em pé, 3×5: tampa (com o furo no meio, se aberta), corpo branco com sombra à direita
        // e uma faixa azul-gelo.
        static void DesenharLata(Tela t, int col, int linha, bool aberta)
        {
            t.Px(CorTampa, col, linha);
            t.Px(aberta ? CorOlho : CorTampa, col + 1, linha);
            t.Px(CorTampa, col + 2, linha);
            for (int i = 1; i < 5; i++)
            {
                Brush cor = i == 2 ? CorFaixa : CorLata;
                t.Px(cor, col, linha + i);
                t.Px(cor, col + 1, linha + i);
                t.Px(CorLataSombra, col + 2, linha + i);
            }
        }

        // deslocamento: o teclado chegando de lado (Acordando).
        static void DesenharTeclado(Tela t, int acesa, int deslocamento = 0)
        {
            for (int col = TecladoX; col <= TecladoFim; col++)
            {
                Brush tecla = col == acesa ? CorTeclaAcesa : (col - TecladoX) % 2 == 1 ? CorTecla : CorTeclado;
                t.Px(tecla, col + deslocamento, TecladoY);
                t.Px(CorTecladoFrente, col + deslocamento, TecladoY + 1);
            }
        }

        // Mini-Claudinhos em cima da cabeça, um por subagente (até 3), quicando como se digitassem;
        // o terceiro sobe nos outros dois, em pirâmide. O que acabou de chegar cai do alto; o que
        // acabou de sair pula e some num "pof".
        static readonly string[] Mini = { ".OOOO.", ".EOOE.", "OOOOOO", ".O..O." };
        // Pares (coluna, linha) de cada mini, em relação ao canto do corpo, para 1, 2 e 3 minis.
        static readonly int[][] LugarDosMinis =
        {
            new[] { 4, -4 },
            new[] { 0, -4, 7, -4 },
            new[] { 0, -4, 7, -4, 4, -8 },
        };

        static void DesenharMinis(Tela t, Sessao s, long agora, int cx, int cy)
        {
            int n = Math.Min(3, s.Subagentes);
            long saiu = agora - s.SubagenteSaiu;
            bool saindo = saiu < 500 && n < 3;
            int total = saindo ? n + 1 : n;
            if (total == 0) return;
            long chegou = agora - s.SubagenteChegou;
            for (int k = 0; k < total; k++)
            {
                int col = cx + LugarDosMinis[total - 1][2 * k];
                int linha = cy + LugarDosMinis[total - 1][2 * k + 1];
                if (saindo && k == total - 1)
                {
                    if (saiu < 250) DesenharMini(t, col, linha - (int)(saiu / 60));
                    else Pof(t, col + 3, linha - 2, saiu >= 375);
                    continue;
                }
                if (k == n - 1 && chegou < 400) linha -= (int)((400 - chegou) / 40);
                else if ((agora + k * 170) % 450 < 160) linha -= 1;
                DesenharMini(t, col, linha);
            }
        }

        static void DesenharMini(Tela t, int col0, int linha0)
        {
            for (int linha = 0; linha < Mini.Length; linha++)
                for (int col = 0; col < Mini[linha].Length; col++)
                {
                    char c = Mini[linha][col];
                    if (c != '.') t.Px(c == 'E' ? CorOlho : CorCorpo, col0 + col, linha0 + linha);
                }
        }

        // Fumacinha de sumiço: uma cruz que se abre em quatro pontas.
        static void Pof(Tela t, int col, int linha, bool aberto)
        {
            int d = aberto ? 2 : 1;
            if (!aberto) t.Px(CorEspirro, col, linha);
            t.Px(CorEspirro, col - d, linha);
            t.Px(CorEspirro, col + d, linha);
            t.Px(CorEspirro, col, linha - d);
            t.Px(CorEspirro, col, linha + d);
        }
    }
}
