using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Claudinho;

// Testa Win32.PorNoTopo contra o estado "preso": o Windows responde ok ao HWND_TOPMOST e não põe a
// marca. O estado real apareceu no uso e não foi reproduzido com nenhuma sequência de chamadas,
// então aqui ele é SIMULADO: a janela de teste descarta o primeiro pedido de mudança de ordem ao
// receber WM_WINDOWPOSCHANGING. Isto prova que a função percebe o pedido ignorado e faz a sequência
// completa. Que o HWND_NOTOPMOST é o que destrava foi medido na janela real, não aqui.
class JanelaPresa : Form
{
    const int WM_WINDOWPOSCHANGING = 0x0046;
    const uint SWP_NOZORDER = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    public int IgnorarProximos;
    public int Pedidos;

    public JanelaPresa()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(0, 0, 8, 8);
        BackColor = Color.FromArgb(255, 0, 255);
        TransparencyKey = BackColor;
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_WINDOWPOSCHANGING)
        {
            var wp = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
            if ((wp.flags & SWP_NOZORDER) == 0)
            {
                Pedidos++;
                if (IgnorarProximos > 0)
                {
                    IgnorarProximos--;
                    wp.flags |= SWP_NOZORDER;
                    Marshal.StructureToPtr(wp, m.LParam, false);
                }
            }
        }
        base.WndProc(ref m);
    }
}

static class Programa
{
    static int falhas;

    static void Confere(string nome, bool ok)
    {
        Console.WriteLine((ok ? "ok     " : "FALHA  ") + nome);
        if (!ok) falhas++;
    }

    static int Main()
    {
        const uint f = Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE;
        using (var j = new JanelaPresa())
        {
            j.Show();
            Application.DoEvents();
            IntPtr h = j.Handle;

            Win32.SetWindowPos(h, Win32.HWND_NOTOPMOST, 0, 0, 0, 0, f);
            j.Pedidos = 0;
            Confere("estado normal: não precisa destravar", !Win32.PorNoTopo(h));
            Confere("estado normal: fica com a marca", Win32.TemTopo(h));
            Confere("estado normal: um pedido só", j.Pedidos == 1);

            Win32.SetWindowPos(h, Win32.HWND_NOTOPMOST, 0, 0, 0, 0, f);
            j.IgnorarProximos = 1;
            bool retorno = Win32.SetWindowPos(h, Win32.HWND_TOPMOST, 0, 0, 0, 0, f);
            Confere("simulação fiel: HWND_TOPMOST responde ok", retorno);
            Confere("simulação fiel: e a marca não entra", !Win32.TemTopo(h));

            j.IgnorarProximos = 1;
            j.Pedidos = 0;
            Confere("preso: percebe e destrava", Win32.PorNoTopo(h));
            Confere("preso: fica com a marca", Win32.TemTopo(h));
            Confere("preso: TOPMOST ignorado, NOTOPMOST, TOPMOST (" + j.Pedidos + " pedidos)", j.Pedidos == 3);

            j.Close();
        }
        Console.WriteLine(falhas == 0 ? "TUDO VERDE" : falhas + " FALHA(S)");
        return falhas;
    }
}
