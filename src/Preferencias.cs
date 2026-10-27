using System;
using System.Collections.Generic;
using System.IO;

namespace Claudinho
{
    // Preferências do usuário (posição, sempre no topo, som), uma por linha "chave=valor" em
    // %LOCALAPPDATA%\Claudinho\preferencias.txt. Lê também os arquivos soltos da primeira
    // versão (posicao.txt, sempre-no-topo.txt), sem apagá-los.
    static class Preferencias
    {
        static readonly string Arquivo = Path.Combine(Registro.Pasta, "preferencias.txt");
        static Dictionary<string, string> valores;

        public static string Ler(string chave, string padrao)
        {
            Carregar();
            string valor;
            return valores.TryGetValue(chave, out valor) ? valor : padrao;
        }

        public static void Gravar(string chave, string valor)
        {
            Carregar();
            valores[chave] = valor;
            try
            {
                Directory.CreateDirectory(Registro.Pasta);
                var linhas = new List<string>();
                foreach (var par in valores) linhas.Add(par.Key + "=" + par.Value);
                File.WriteAllLines(Arquivo, linhas);
            }
            catch (IOException ex) { Registro.Erro("gravar preferências", ex); }
            catch (UnauthorizedAccessException ex) { Registro.Erro("gravar preferências", ex); }
        }

        static void Carregar()
        {
            if (valores != null) return;
            valores = new Dictionary<string, string>();
            try
            {
                LerAntigo("posicao.txt", "posicao");
                LerAntigo("sempre-no-topo.txt", "topo");
                if (!File.Exists(Arquivo)) return;
                foreach (string linha in File.ReadAllLines(Arquivo))
                {
                    int igual = linha.IndexOf('=');
                    if (igual > 0) valores[linha.Substring(0, igual).Trim()] = linha.Substring(igual + 1).Trim();
                }
            }
            catch (IOException ex) { Registro.Erro("ler preferências", ex); }
            catch (UnauthorizedAccessException ex) { Registro.Erro("ler preferências", ex); }
        }

        static void LerAntigo(string nome, string chave)
        {
            string caminho = Path.Combine(Registro.Pasta, nome);
            if (File.Exists(caminho)) valores[chave] = File.ReadAllText(caminho).Trim();
        }
    }
}
