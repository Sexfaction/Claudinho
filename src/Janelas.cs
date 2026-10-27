using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Claudinho
{
    // Acha e traz para frente a janela do programa onde a sessão do Claude roda (VS Code,
    // terminal, app do Claude).
    static class Janelas
    {
        class Processo
        {
            public int Pai;
            public string Nome;
        }

        class Janela
        {
            public IntPtr Hwnd;
            public int Processo;
            public string Titulo;
        }

        // Chamado no modo --hook. Sobe pela árvore de processos (hook → Claude Code → ... →
        // VS Code) até o primeiro que tem janela visível. O Explorer fica de fora: num terminal
        // antigo a subida chega nele, e a janela dele não é a da sessão.
        public static int ProcessoComJanela()
        {
            Dictionary<int, Processo> processos = FotoDosProcessos();
            List<Janela> janelas = JanelasVisiveis();
            int atual = Process.GetCurrentProcess().Id;
            for (int nivel = 0; nivel < 20; nivel++)
            {
                Processo p;
                if (!processos.TryGetValue(atual, out p) || p.Pai == 0 || p.Pai == atual) return 0;
                atual = p.Pai;
                Processo pai;
                if (processos.TryGetValue(atual, out pai) && pai.Nome.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
                    return 0;
                if (janelas.Exists(j => j.Processo == atual)) return atual;
            }
            return 0;
        }

        // Chamado no modo --hook: o primeiro processo acima chamado claude.exe é o Claude Code da
        // sessão (no app desktop, o app em si também se chama Claude.exe, mas fica mais acima).
        public static int ProcessoDoClaude()
        {
            Dictionary<int, Processo> processos = FotoDosProcessos();
            int atual = Process.GetCurrentProcess().Id;
            for (int nivel = 0; nivel < 20; nivel++)
            {
                Processo p;
                if (!processos.TryGetValue(atual, out p) || p.Pai == 0 || p.Pai == atual) return 0;
                atual = p.Pai;
                Processo pai;
                if (processos.TryGetValue(atual, out pai) && pai.Nome.Equals("claude.exe", StringComparison.OrdinalIgnoreCase))
                    return atual;
            }
            return 0;
        }

        // A janela que o clique levaria: do processo guardado, preferindo a que tem o nome da
        // pasta no título (o VS Code põe a pasta aberta no título). Sem processo conhecido, a
        // primeira janela de qualquer programa (menos o Explorer) com a pasta no título.
        public static IntPtr Escolher(int processo, string pasta, out string titulo)
        {
            titulo = null;
            List<Janela> janelas = JanelasVisiveis();
            List<Janela> doProcesso = janelas.FindAll(j => j.Processo == processo && processo != 0);
            Janela escolhida = doProcesso.Find(j => Contem(j.Titulo, pasta));
            if (escolhida == null && doProcesso.Count > 0) escolhida = doProcesso[0]; // a usada por último
            if (escolhida == null)
            {
                Dictionary<int, Processo> processos = FotoDosProcessos();
                escolhida = janelas.Find(j =>
                {
                    Processo p;
                    bool explorer = processos.TryGetValue(j.Processo, out p)
                        && p.Nome.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);
                    return !explorer && Contem(j.Titulo, pasta);
                });
            }
            if (escolhida == null) return IntPtr.Zero;
            titulo = escolhida.Titulo;
            return escolhida.Hwnd;
        }

        public static bool Focar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (Win32.IsIconic(hwnd)) Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
            if (Win32.SetForegroundWindow(hwnd)) return true;
            // O Windows só deixa trocar o foco a quem recebeu a última tecla ou clique; um toque
            // no Alt conta como esse "último toque" e libera a troca.
            Win32.keybd_event(Win32.VK_MENU, 0, 0, UIntPtr.Zero);
            Win32.keybd_event(Win32.VK_MENU, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
            return Win32.SetForegroundWindow(hwnd);
        }

        static bool Contem(string titulo, string pasta)
        {
            return !string.IsNullOrEmpty(pasta) && titulo.IndexOf(pasta, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Janelas principais visíveis com título, na ordem do Windows (a usada por último primeiro).
        static List<Janela> JanelasVisiveis()
        {
            var lista = new List<Janela>();
            int proprio = Process.GetCurrentProcess().Id;
            Win32.EnumWindows(delegate(IntPtr hwnd, IntPtr param)
            {
                if (!Win32.IsWindowVisible(hwnd) || Win32.GetWindow(hwnd, Win32.GW_OWNER) != IntPtr.Zero) return true;
                int tamanho = Win32.GetWindowTextLength(hwnd);
                if (tamanho == 0) return true;
                var texto = new StringBuilder(tamanho + 1);
                Win32.GetWindowText(hwnd, texto, texto.Capacity);
                uint processo;
                Win32.GetWindowThreadProcessId(hwnd, out processo);
                if ((int)processo == proprio) return true;
                var j = new Janela();
                j.Hwnd = hwnd;
                j.Processo = (int)processo;
                j.Titulo = texto.ToString();
                lista.Add(j);
                return true;
            }, IntPtr.Zero);
            return lista;
        }

        static Dictionary<int, Processo> FotoDosProcessos()
        {
            var mapa = new Dictionary<int, Processo>();
            IntPtr foto = Win32.CreateToolhelp32Snapshot(Win32.TH32CS_SNAPPROCESS, 0);
            if (foto == IntPtr.Zero || foto == new IntPtr(-1)) return mapa;
            try
            {
                var e = new Win32.PROCESSENTRY32();
                e.dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.PROCESSENTRY32));
                for (bool ok = Win32.Process32First(foto, ref e); ok; ok = Win32.Process32Next(foto, ref e))
                {
                    var p = new Processo();
                    p.Pai = (int)e.th32ParentProcessID;
                    p.Nome = e.szExeFile ?? "";
                    mapa[(int)e.th32ProcessID] = p;
                }
            }
            finally
            {
                Win32.CloseHandle(foto);
            }
            return mapa;
        }
    }
}
