using System;
using System.Drawing;

namespace Claudinho
{
    // Precisa de você: ergue uma plaquinha amarela com "!" e balança, olhando para você, com
    // pulinhos. Sem resposta, fica impaciente: aos 20 s finca a placa e bate o pé; aos 60 s cruza
    // os braços e tamborila, erguendo a placa de novo de vez em quando. A placa fica abaixo das 3
    // primeiras linhas da célula: ali abre o balão de permissão.
    static partial class Sprites
    {
        const long BatePe = 20000, CruzaBracos = 60000;

        // K = o "!" escuro no amarelo, com uma linha amarela acima e abaixo (encostado na borda,
        // ele se perdia no fundo escuro). 6 de altura: erguida, a placa começa na linha 3.
        static readonly string[] Placa = { "YYYYY", "YYKYY", "YYKYY", "YYYYY", "YYKYY", "YYYYY" };
        static readonly Brush CorBracoCruzado = new SolidBrush(Color.FromArgb(0xB0, 0x5C, 0x40));

        static void DesenharPrecisaDeVoce(Tela t, Sessao s, long agora)
        {
            long m = Math.Max(0, agora - s.Desde);
            var p = new Pose();
            p.OlhosFechados = Piscando(m + s.Fase);
            bool ergue = m < BatePe || (m >= CruzaBracos && m % 8000 < 1500);
            bool cruzados = m >= CruzaBracos && !ergue;
            int balanco = 0;

            if (ergue)
            {
                // Placa no alto balançando, dois pulinhos a cada 2 s.
                p.Direito = Braco.Cima;
                long c = m % 2000;
                p.Dy = c < 150 || (c >= 300 && c < 450) ? -1 : 0;
                balanco = (int)((m / 300) % 4);
                balanco = balanco == 3 ? 0 : balanco - 1; // -1, 0, 1, 0
            }
            else if (cruzados)
            {
                p.Esquerdo = p.Direito = Braco.Nenhum;
                p.OlhoDx = (m / 4000) % 2 == 0 ? -1 : 1;
            }
            else
            {
                // Bate o pé: dois toques e uma pausa, olhando para um lado e para o outro.
                long c = m % 900;
                p.Dy = c < 120 || (c >= 240 && c < 360) ? -1 : 0;
                p.OlhoDx = (m / 3000) % 2 == 0 ? -1 : 1;
            }

            DesenharPose(t, p, CorpoX, CorpoTopo);
            DesenharTeclado(t, -1);
            DesenharLata(t, LataX, LataY, true);
            int cx = CorpoX + p.Dx, cy = CorpoTopo + p.Dy;

            if (ergue)
            {
                t.Px(CorMarrom, cx + 13, cy - 3); // cabo, entre a mão e a placa
                DesenharPlaca(t, cx + 11 + balanco, cy - 9);
            }
            else
            {
                // Fincada no chão à esquerda, apoiada no teclado.
                for (int linha = 14; linha < TecladoY; linha++) t.Px(CorMarrom, 2, linha);
                DesenharPlaca(t, 0, 8);
            }

            if (cruzados)
            {
                // Um braço por cima do outro na frente da barriga, e um dedo tamborilando.
                for (int col = 1; col <= 11; col++) t.Px(CorBracoCruzado, cx + col, cy + 3);
                for (int col = 2; col <= 12; col++) t.Px(CorBracoCruzado, cx + col, cy + 4);
                t.Px(CorBracoCruzado, cx + 12, cy + ((m / 200) % 2 == 0 ? 3 : 2));
            }
        }

        static void DesenharPlaca(Tela t, int col0, int linha0)
        {
            for (int linha = 0; linha < Placa.Length; linha++)
                for (int col = 0; col < Placa[linha].Length; col++)
                    t.Px(Placa[linha][col] == 'K' ? CorOlho : CorAlerta, col0 + col, linha0 + linha);
        }
    }
}
