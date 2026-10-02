using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace TaskbarLite;

static class Program
{
    // ---------- WinAPI ----------
    const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")]
    static extern int SetWindowCompositionAttribute(IntPtr h, ref WCAD data);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    struct ACCENT { public int State; public int Flags; public uint Color; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    struct WCAD { public int Attribute; public IntPtr Data; public int Size; }

    // ---------- Transparência ----------
    static void SetAccent(IntPtr hwnd, int state)
    {
        if (hwnd == IntPtr.Zero) return;
        var accent = new ACCENT { State = state, Flags = 2, Color = 0x00000000 }; // alpha 0 = transparente
        int size = Marshal.SizeOf<ACCENT>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WCAD { Attribute = 19, Data = ptr, Size = size }; // 19 = WCA_ACCENT_POLICY
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    static void ForEachTaskbar(Action<IntPtr> action)
    {
        action(FindWindow("Shell_TrayWnd", null));
        IntPtr h = IntPtr.Zero;
        while ((h = FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            action(h);
    }

    // ---------- Centralização (barra principal) ----------
    static IntPtr FindTaskList(IntPtr tray)
    {
        var rebar = FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
        var sw = FindWindowEx(rebar, IntPtr.Zero, "MSTaskSwWClass", null);
        return FindWindowEx(sw, IntPtr.Zero, "MSTaskListWClass", null);
    }
    const int AdjustX = 20; // positivo move para a direita, negativo para a esquerda

    static void Center()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return;
        var list = FindTaskList(tray);
        if (list == IntPtr.Zero) return;
        var parent = GetParent(list);

        GetWindowRect(tray, out var t);
        GetWindowRect(list, out var l);

        var buttons = AutomationElement.FromHandle(list)
            .FindAll(TreeScope.Children, Condition.TrueCondition);

        double left = double.MaxValue, right = double.MinValue;
        foreach (AutomationElement b in buttons)
        {
            var r = b.Current.BoundingRectangle;
            if (r.IsEmpty) continue;
            left = Math.Min(left, r.Left);
            right = Math.Max(right, r.Right);
        }
        if (left > right) return;

        int width = (int)(right - left);
        int offset = (int)left - l.Left; // onde o 1º botão começa dentro da lista
        int targetScreenX = t.Left + ((t.Right - t.Left) - width) / 2 - offset + AdjustX;

        if (Math.Abs(l.Left - targetScreenX) <= 1) return; // já está no lugar

        var p = new POINT { X = targetScreenX, Y = l.Top };
        ScreenToClient(parent, ref p);
        SetWindowPos(list, IntPtr.Zero, p.X, p.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    static void RestorePosition()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        var list = FindTaskList(tray);
        if (list == IntPtr.Zero) return;
        GetWindowRect(list, out var l);
        var p = new POINT { X = l.Left, Y = l.Top };
        ScreenToClient(GetParent(list), ref p);
        SetWindowPos(list, IntPtr.Zero, 0, p.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // ---------- App ----------
    [STAThread]
    static void Main()
    {
        SetProcessDPIAware(); // precisa vir antes de qualquer coisa, senão as coordenadas ficam erradas

        using var mutex = new Mutex(true, "TaskbarLite_SingleInstance", out bool isNew);
        if (!isNew) return;

        var timer = new Timer { Interval = 500 };
        timer.Tick += (_, _) =>
        {
            try
            {
                ForEachTaskbar(h => SetAccent(h, 2)); // 2 = TRANSPARENTGRADIENT
                Center();
            }
            catch { /* explorer reiniciando, UIA indisponível etc. — tenta de novo no próximo tick */ }
        };

        var menu = new ContextMenuStrip();
        var icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "TaskbarLite",
            Visible = true,
            ContextMenuStrip = menu
        };
        menu.Items.Add("Sair", null, (_, _) =>
        {
            timer.Stop();
            try { ForEachTaskbar(h => SetAccent(h, 0)); RestorePosition(); } catch { }
            icon.Visible = false;
            Application.Exit();
        });

        timer.Start();
        Application.Run();
    }
}