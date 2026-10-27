using System;
using System.IO;

namespace Claudinho
{
    // Pasta de dados do mascote (posição salva e log de erros), fora da pasta do programa.
    static class Registro
    {
        public static readonly string Pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claudinho");

        static readonly object trava = new object();

        public static void Erro(string onde, Exception ex)
        {
            try
            {
                lock (trava)
                {
                    Directory.CreateDirectory(Pasta);
                    string arquivo = Path.Combine(Pasta, "erros.log");
                    if (File.Exists(arquivo) && new FileInfo(arquivo).Length > 1024 * 1024) File.Delete(arquivo);
                    File.AppendAllText(arquivo, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + onde + ": " + ex + Environment.NewLine);
                }
            }
            catch (IOException) { } // sem onde registrar o erro; o mascote segue
            catch (UnauthorizedAccessException) { }
        }
    }
}
