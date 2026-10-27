using System;
using System.IO;
using System.Media;
using System.Text;

namespace Claudinho
{
    // Bipes curtos em 8 bits, sintetizados aqui mesmo (nenhum arquivo de áudio).
    // No máximo um a cada 10 s, para três sessões terminando juntas não virarem uma orquestra.
    static class Som
    {
        const int Taxa = 22050;
        const long Intervalo = 10000;

        static readonly SoundPlayer terminou = Criar(new[] { 1319.0, 1760.0 }, new[] { 70, 120 });
        static readonly SoundPlayer precisa = Criar(new[] { 880.0, 0.0, 880.0 }, new[] { 70, 50, 70 });
        static readonly SoundPlayer semCota = Criar(new[] { 659.0, 440.0 }, new[] { 110, 180 });
        static long ultimo = -Intervalo;

        public static void Tocar(Estado estado, long agora)
        {
            SoundPlayer som = estado == Estado.Terminou ? terminou
                : estado == Estado.PrecisaDeVoce ? precisa
                : estado == Estado.SemCota ? semCota
                : null;
            if (som == null || agora - ultimo < Intervalo) return;
            ultimo = agora;
            try { som.Play(); }
            catch (InvalidOperationException ex) { Registro.Erro("som", ex); } // sem placa de som
        }

        // Notas em sequência (0 Hz = pausa), onda quadrada com volume baixo e caída no fim de cada nota.
        static SoundPlayer Criar(double[] notas, int[] duracoesMs)
        {
            var amostras = new MemoryStream();
            for (int n = 0; n < notas.Length; n++)
            {
                int total = Taxa * duracoesMs[n] / 1000;
                for (int i = 0; i < total; i++)
                {
                    double envelope = 1.0 - (double)i / total;
                    double onda = notas[n] <= 0 ? 0 : (Math.Sin(2 * Math.PI * notas[n] * i / Taxa) >= 0 ? 1 : -1);
                    short valor = (short)(onda * envelope * 0.18 * short.MaxValue);
                    amostras.WriteByte((byte)(valor & 0xFF));
                    amostras.WriteByte((byte)((valor >> 8) & 0xFF));
                }
            }
            byte[] dados = amostras.ToArray();

            var wav = new MemoryStream();
            var w = new BinaryWriter(wav);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dados.Length);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);          // PCM
            w.Write((short)1);          // mono
            w.Write(Taxa);
            w.Write(Taxa * 2);          // bytes por segundo
            w.Write((short)2);          // bytes por amostra
            w.Write((short)16);         // bits
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dados.Length);
            w.Write(dados);
            wav.Position = 0;

            var player = new SoundPlayer(wav);
            player.Load();
            return player;
        }
    }
}
