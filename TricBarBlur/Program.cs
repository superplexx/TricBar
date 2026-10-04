using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("windows")]

namespace TricBar;

static class Program
{
    const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10;
    const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    const uint EVENT_OBJECT_CREATE = 0x8000;
    const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
    const uint OBJID_CLIENT = 0xFFFFFFFC;
    const int WCA_ACCENT_POLICY = 19;
    const int ACCENT_DISABLED = 0;
    const int ACCENT_ENABLE_GRADIENT = 1;
    const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
    const int ACCENT_ENABLE_BLURBEHIND = 3;
    const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
    const int AdjustX = 28; // positivo move para a direita, negativo para a esquerda
    const int CHILDID_SELF = 0;
    const int MaxTaskbars = 16;
    const int XamlTaskbarBuild = 22621; // Windows 11 22H2+: o fundo da barra e um retangulo XAML
    const int MaxTapAttempts = 5;       // tentativas por processo do Explorer
    const int MaxTapInjections = 8;     // limite total por execucao (evita loop se o Explorer cair)
    const string TapAliveName = "TricBar_Blur_Alive";
    const string TapActiveName = "TricBar_Blur_Active";
    const string TapAppliedName = "TricBar_Blur_Applied";

    const int WM_DESTROY = 0x0002;
    const int WM_CLOSE = 0x0010;
    const int WM_COMMAND = 0x0111;
    const int WM_TIMER = 0x0113;
    const int WM_RBUTTONUP = 0x0205;
    const int WM_LBUTTONDBLCLK = 0x0203;
    const int WM_USER = 0x0400;
    const int WM_NULL = 0x0000;
    const int WM_TRAY = WM_USER + 1;
    const int ID_EXIT = 1;
    const int ID_OPEN = 2;
    const nuint ID_KEEPALIVE = 1;
    const nuint ID_DEBOUNCE = 2;
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    const uint MF_STRING = 0;
    const uint TPM_RIGHTBUTTON = 0x0002;
    const uint CS_DBLCLKS = 0x0008;

    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
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
    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowExW(uint exStyle, string cls, string title, uint style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern int GetMessageW(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] static extern IntPtr SetTimer(IntPtr hWnd, nuint nIDEvent, uint uElapse, IntPtr lpTimerFunc);
    [DllImport("user32.dll")] static extern bool KillTimer(IntPtr hWnd, nuint nIDEvent);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);
    [DllImport("user32.dll")]
    static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr hMenu);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandleW(string? lpModuleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterWindowMessageW(string lpString);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr ShellExecuteW(IntPtr hwnd, string? op, string file, string? args, string? dir, int show);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATA lpData);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern uint ExtractIconExW(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);
    [DllImport("oleacc.dll")]
    static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint dwObjectID, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
    [DllImport("oleacc.dll")]
    static extern int AccessibleChildren(
        [MarshalAs(UnmanagedType.Interface)] IAccessible paccContainer,
        int iChildStart, int cChildren,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] rgvarChildren,
        out int pcObtained);

    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int InitXamlDiagnosticsEx(
        [MarshalAs(UnmanagedType.LPWStr)] string endPointName, uint pid,
        [MarshalAs(UnmanagedType.LPWStr)] string dllXamlDiagnostics,
        [MarshalAs(UnmanagedType.LPWStr)] string tapDllName,
        Guid tapClsid,
        [MarshalAs(UnmanagedType.LPWStr)] string? initializationData);

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    struct ACCENT { public int State; public int Flags; public uint Color; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    struct WCAD { public int Attribute; public IntPtr Data; public int Size; }
    [StructLayout(LayoutKind.Sequential)]
    struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [ComImport]
    [Guid("618736E0-3C3D-11CF-810C-00AA00389B71")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    interface IAccessible
    {
        [DispId(-5001)] int accChildCount { get; }
        [DispId(-5015)]
        void accLocation(out int pxLeft, out int pyTop, out int pcxWidth, out int pcyHeight,
            [In, Optional] object varChild);
    }

    static readonly int Build = Environment.OSVersion.Version.Build;
    static readonly bool IsWin11 = Build >= 22000;
    static readonly bool UseXamlTap = Build >= XamlTaskbarBuild;
    static readonly Guid TapClsid = new("B7C0A2E1-5D34-4F6A-9E81-3C7A1D5F2B90"); // igual ao da TricBarBlur.cpp
    static readonly Guid IidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");
    static readonly WinEventDelegate EventProc = OnWinEvent;
    static readonly WndProc WindowProcKeepAlive = WndProcImpl;

    static readonly IntPtr[] Taskbars = new IntPtr[MaxTaskbars];
    static int _taskbarCount;

    static IntPtr _hwnd, _tray, _list, _parent, _hook, _accentMem, _iconLarge, _iconSmall;
    static uint _explorerPid;
    static int _accentSize, _safety;
    static bool _exiting;

    // Windows 11 22H2+ (TricBarBlur.dll injetada no Explorer)
    static EventWaitHandle? _tapAlive, _tapActive, _tapApplied;
    static InitXamlDiagnosticsEx? _initXamlDiag;
    static string? _tapPath;
    static string _tipSuffix = "";
    static uint _tapPid;
    static int _tapAttempts, _tapInjections, _tapBusy;
    static long _tapNextTry;

    // ---------- Configuração (gravada pelo TricBarLauncher) ----------
    enum Mode { Normal, Clear, Blur, Acrylic, Color }
    static Mode _mode = Mode.Clear;
    static int _rgb;            // 0xRRGGBB
    static int _opacity = 60;   // 0 a 100
    static bool _center = true; // só vale no Windows 10
    static DateTime _cfgStamp;
    static uint _wmReload;

    static readonly string CfgPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TricBar", "config.ini");

    static void LoadConfig()
    {
        try
        {
            if (!File.Exists(CfgPath)) return;
            _cfgStamp = File.GetLastWriteTimeUtc(CfgPath);
            foreach (var raw in File.ReadAllLines(CfgPath))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string key = raw.Substring(0, eq).Trim().ToLowerInvariant();
                string val = raw.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "mode":
                        _mode = val.ToLowerInvariant() switch
                        {
                            "normal" => Mode.Normal,
                            "blur" => Mode.Blur,
                            "acrylic" => Mode.Acrylic,
                            "color" => Mode.Color,
                            _ => Mode.Clear
                        };
                        break;
                    case "color":
                        if (int.TryParse(val.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                            _rgb = rgb & 0xFFFFFF;
                        break;
                    case "opacity":
                        if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int op))
                            _opacity = Math.Clamp(op, 0, 100);
                        break;
                    case "center":
                        _center = val != "0";
                        break;
                }
            }
        }
        catch { }
    }

    static bool ConfigChanged()
    {
        try { return File.Exists(CfgPath) && File.GetLastWriteTimeUtc(CfgPath) != _cfgStamp; }
        catch { return false; }
    }

    // GradientColor do Windows é ABGR: 0xAABBGGRR
    static uint Abgr(uint alpha) =>
        (alpha << 24) | (uint)(((_rgb & 0xFF) << 16) | (_rgb & 0xFF00) | ((_rgb >> 16) & 0xFF));

    static void ComputeAccent(out int state, out uint color)
    {
        uint a = (uint)(_opacity * 255 / 100);

        if (UseXamlTap)
        {
            // Windows 11 22H2+: cor, blur e acrylic sao desenhados pela TricBarBlur.dll no retangulo XAML
            // do fundo da barra (AcrylicBrush). A janela da barra so precisa ficar "vazada":
            // TRANSPARENTGRADIENT com cor 0. Sem nenhum accent a area transparente aparece PRETA, e o
            // accent de blur/acrylic da janela fica escondido atras do retangulo (nao gera o efeito).
            state = _mode == Mode.Normal ? ACCENT_DISABLED : ACCENT_ENABLE_TRANSPARENTGRADIENT;
            color = 0;
            return;
        }

        switch (_mode)
        {
            case Mode.Normal:
                state = ACCENT_DISABLED; color = 0; break;
            case Mode.Blur:
                state = ACCENT_ENABLE_BLURBEHIND; color = UseXamlTap ? 0u : Abgr(a); break;
            case Mode.Acrylic:
                state = ACCENT_ENABLE_ACRYLICBLURBEHIND; color = UseXamlTap ? 0x01000000u : Abgr(Math.Max(a, 1u)); break;
            case Mode.Color:
                // No 22H2+ a cor e a opacidade sao pintadas pela TricBarBlur.dll no XAML.
                state = UseXamlTap || _opacity < 100 ? ACCENT_ENABLE_TRANSPARENTGRADIENT : ACCENT_ENABLE_GRADIENT;
                color = UseXamlTap ? 0u : Abgr(a); break;
            default: // Clear
                state = ACCENT_ENABLE_TRANSPARENTGRADIENT; color = 0; break;
        }
    }

    static void ApplySettings()
    {
        try
        {
            ApplyAccent();
            EnsureTap();
            if (!IsWin11)
            {
                if (_center) { EnsureHook(); Center(); }
                else RestorePosition();
            }
        }
        catch { }
    }

    static void OpenSettings()
    {
        string? dir = Path.GetDirectoryName(Environment.ProcessPath);
        if (dir == null) return;
        string exe = Path.Combine(dir, "TricBarLauncher.exe");
        if (File.Exists(exe)) ShellExecuteW(IntPtr.Zero, "open", exe, null, dir, 1);
    }

    static void SetAccent(IntPtr hwnd, int state, uint color)
    {
        if (hwnd == IntPtr.Zero) return;
        // Acrylic precisa de AccentFlags = 0; os outros estilos usam 2.
        int flags = state == ACCENT_ENABLE_ACRYLICBLURBEHIND ? 0 : 2;
        var accent = new ACCENT { State = state, Flags = flags, Color = color };
        Marshal.StructureToPtr(accent, _accentMem, false);
        var data = new WCAD { Attribute = WCA_ACCENT_POLICY, Data = _accentMem, Size = _accentSize };
        SetWindowCompositionAttribute(hwnd, ref data);
    }

    static void RefreshTaskbars()
    {
        _taskbarCount = 0;
        var primary = FindWindow("Shell_TrayWnd", null);
        if (primary != IntPtr.Zero) Taskbars[_taskbarCount++] = primary;

        IntPtr h = IntPtr.Zero;
        while (_taskbarCount < MaxTaskbars &&
               (h = FindWindowEx(IntPtr.Zero, h, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            Taskbars[_taskbarCount++] = h;
    }

    static bool TaskbarsStale()
    {
        if (_taskbarCount == 0) return true;
        for (int i = 0; i < _taskbarCount; i++)
            if (!IsWindow(Taskbars[i])) return true;
        return false;
    }

    static void ApplyAccent()
    {
        ComputeAccent(out int state, out uint color);
        ApplyAccent(state, color);
    }

    static void ApplyAccent(int state, uint color)
    {
        if (TaskbarsStale()) RefreshTaskbars();
        for (int i = 0; i < _taskbarCount; i++)
            SetAccent(Taskbars[i], state, color);
    }

    static IntPtr FindTaskList(IntPtr tray)
    {
        var rebar = FindWindowEx(tray, IntPtr.Zero, "ReBarWindow32", null);
        var sw = FindWindowEx(rebar, IntPtr.Zero, "MSTaskSwWClass", null);
        return FindWindowEx(sw, IntPtr.Zero, "MSTaskListWClass", null);
    }

    static bool EnsureHandles()
    {
        if (!IsWindow(_tray) || !IsWindow(_list) || !IsWindow(_parent))
        {
            _tray = FindWindow("Shell_TrayWnd", null);
            _list = _tray == IntPtr.Zero ? IntPtr.Zero : FindTaskList(_tray);
            _parent = _list == IntPtr.Zero ? IntPtr.Zero : GetParent(_list);
        }
        return _list != IntPtr.Zero && _parent != IntPtr.Zero;
    }

    static void EnsureHook()
    {
        if (!EnsureHandles()) return;
        GetWindowThreadProcessId(_tray, out uint pid);
        if (_hook != IntPtr.Zero && pid == _explorerPid) return;

        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        _explorerPid = pid;
        _hook = SetWinEventHook(
            EVENT_OBJECT_CREATE, EVENT_OBJECT_NAMECHANGE,
            IntPtr.Zero, EventProc, pid, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    static void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero || _exiting) return;
        if (eventType == EVENT_OBJECT_LOCATIONCHANGE)
        {
            if (hwnd != _list) return;
        }
        else if (hwnd != _list && hwnd != _tray && hwnd != _parent)
        {
            return;
        }
        RequestCenter();
    }

    static void RequestCenter()
    {
        if (IsWin11 || !_center || _hwnd == IntPtr.Zero) return;
        KillTimer(_hwnd, ID_DEBOUNCE);
        SetTimer(_hwnd, ID_DEBOUNCE, 80, IntPtr.Zero);
    }

    static void AddBounds(IAccessible acc, object child, ref int left, ref int right)
    {
        acc.accLocation(out int x, out int _, out int w, out int h, child);
        if (w <= 0 || h <= 0) return;
        if (x < left) left = x;
        int r = x + w;
        if (r > right) right = r;
    }

    static void CollectButtonBounds(IAccessible acc, ref int left, ref int right)
    {
        int count;
        try { count = acc.accChildCount; }
        catch { return; }
        if (count <= 0) return;

        var kids = new object[count];
        if (AccessibleChildren(acc, 0, count, kids, out int got) == 0 && got > 0)
        {
            for (int i = 0; i < got; i++)
            {
                switch (kids[i])
                {
                    case int id:
                        AddBounds(acc, id, ref left, ref right);
                        break;
                    case IAccessible child:
                        try { AddBounds(child, CHILDID_SELF, ref left, ref right); }
                        finally { Marshal.ReleaseComObject(child); }
                        break;
                }
            }
            return;
        }

        for (int i = 1; i <= count; i++)
            AddBounds(acc, i, ref left, ref right);
    }

    static void Center()
    {
        if (!EnsureHandles()) return;

        GetWindowRect(_tray, out var t);
        GetWindowRect(_list, out var l);

        var iid = IidIAccessible;
        if (AccessibleObjectFromWindow(_list, OBJID_CLIENT, ref iid, out var obj) != 0 || obj is not IAccessible acc)
            return;

        int left = int.MaxValue, right = int.MinValue;
        try { CollectButtonBounds(acc, ref left, ref right); }
        finally { Marshal.ReleaseComObject(acc); }

        if (left > right) return;

        int width = right - left;
        int offset = left - l.Left;
        int targetScreenX = t.Left + ((t.Right - t.Left) - width) / 2 - offset + AdjustX;
        if (Math.Abs(l.Left - targetScreenX) <= 1) return;

        var p = new POINT { X = targetScreenX, Y = l.Top };
        ScreenToClient(_parent, ref p);
        SetWindowPos(_list, IntPtr.Zero, p.X, p.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    static void RestorePosition()
    {
        if (!EnsureHandles()) return;
        GetWindowRect(_list, out var l);
        var p = new POINT { X = l.Left, Y = l.Top };
        ScreenToClient(_parent, ref p);
        SetWindowPos(_list, IntPtr.Zero, 0, p.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // ---- Windows 11 22H2+: fundo transparente via TricBarBlur.dll (XAML Diagnostics) ----

    // Copia a DLL para %LOCALAPPDATA%\TricBar com o hash no nome. O Explorer fica com a DLL
    // carregada ate reiniciar; assim a pasta do app nunca fica travada e atualizar o TricBar
    // gera outro arquivo em vez de falhar.
    static string? PrepareTapDll()
    {
        try
        {
            string src = Path.Combine(AppContext.BaseDirectory, "TricBarBlur.dll");
            if (!File.Exists(src)) return null;

            // Num "dotnet build" (sem single-file) o proprio assembly gerenciado tambem se chama
            // TricBarBlur.dll. Se for esse, nao serve: a DLL nativa precisa estar no lugar dele.
            try { System.Reflection.AssemblyName.GetAssemblyName(src); return null; }
            catch (BadImageFormatException) { } // nativa: e o que a gente quer

            byte[] bytes = File.ReadAllBytes(src);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).Substring(0, 12);
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TricBar");
            Directory.CreateDirectory(dir);

            string name = $"TricBarBlur-{hash}.dll";
            string dst = Path.Combine(dir, name);
            if (!File.Exists(dst)) File.WriteAllBytes(dst, bytes);

            foreach (var old in Directory.GetFiles(dir, "TricBarBlur-*.dll"))
            {
                if (!string.Equals(Path.GetFileName(old), name, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(old); } catch { } // ainda carregada no Explorer: fica pra depois
                }
            }
            return dst;
        }
        catch { return null; }
    }

    static void InjectTap(uint pid, string dll)
    {
        if (_initXamlDiag == null)
        {
            var lib = NativeLibrary.Load(Path.Combine(Environment.SystemDirectory, "Windows.UI.Xaml.dll"));
            var proc = NativeLibrary.GetExport(lib, "InitializeXamlDiagnosticsEx");
            _initXamlDiag = Marshal.GetDelegateForFunctionPointer<InitXamlDiagnosticsEx>(proc);
        }
        _initXamlDiag("VisualDiagConnection1", pid, string.Empty, dll, TapClsid, null);
    }

    // A DLL liga/desliga o fundo transparente conforme este evento: ligado em tudo que nao e "Normal".
    static void SyncTapActive()
    {
        if (_tapActive == null) return;
        try
        {
            if (_mode != Mode.Normal) _tapActive.Set();
            else _tapActive.Reset();
        }
        catch { }
    }

    static void EnsureTap()
    {
        if (!UseXamlTap || _exiting || _tapAlive == null || _tapActive == null || _tapApplied == null) return;
        SyncTapActive();

        if (_tapPath == null)
        {
            _tipSuffix = " - falta TricBarBlur.dll";
            return;
        }

        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero) return;
        GetWindowThreadProcessId(tray, out uint pid);
        if (pid == 0) return;

        if (pid != _tapPid) // Explorer novo (ou primeira vez): tudo de novo
        {
            _tapPid = pid;
            _tapApplied.Reset();
            _tapAttempts = 0;
            _tapNextTry = 0;
        }

        if (_tapApplied.WaitOne(0))
        {
            _tipSuffix = " - ativo";
            return;
        }
        // Modo Normal: nao precisa mexer no Explorer.
        if (_mode == Mode.Normal)
        {
            _tipSuffix = "";
            return;
        }
        if (_tapAttempts >= MaxTapAttempts || _tapInjections >= MaxTapInjections)
        {
            _tipSuffix = " - falha ao aplicar";
            return;
        }

        long now = Environment.TickCount64;
        if (now < _tapNextTry) return;
        if (Interlocked.Exchange(ref _tapBusy, 1) != 0) return;

        _tapAttempts++;
        _tapInjections++;
        _tapNextTry = now + 5000;

        string dll = _tapPath;
        var worker = new Thread(() =>
        {
            try { InjectTap(pid, dll); }
            catch { }
            finally { Volatile.Write(ref _tapBusy, 0); }
        })
        { IsBackground = true };
        worker.Start();
    }

    static void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        AppendMenuW(menu, MF_STRING, (nuint)ID_OPEN, "Configurações");
        AppendMenuW(menu, MF_STRING, (nuint)ID_EXIT, "Sair");
        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd);
        TrackPopupMenu(menu, TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);
    }

    static void ExitApp()
    {
        if (_exiting) return;
        _exiting = true;
        KillTimer(_hwnd, ID_KEEPALIVE);
        KillTimer(_hwnd, ID_DEBOUNCE);
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        try
        {
            ApplyAccent(ACCENT_DISABLED, 0);
            RestorePosition();
        }
        catch { }

        // A TricBarBlur.dll (no Explorer) devolve o fundo original quando estes eventos mudam/somem.
        try { _tapActive?.Reset(); } catch { }
        try { _tapAlive?.Dispose(); } catch { }

        var nid = TrayData();
        Shell_NotifyIconW(NIM_DELETE, ref nid);
        DestroyWindow(_hwnd);
    }

    static NOTIFYICONDATA TrayData() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = WM_TRAY,
        hIcon = _iconSmall != IntPtr.Zero ? _iconSmall : _iconLarge,
        szTip = $"TricBar 3.1.5 - build {Build}{_tipSuffix}",
        szInfo = "",
        szInfoTitle = ""
    };

    // Se o Explorer reiniciar, o ícone da bandeja some. NIM_MODIFY falha quando o ícone não existe,
    // e aí a gente adiciona de novo. (A janela é "message-only", então não recebe o broadcast TaskbarCreated.)
    static void EnsureTrayIcon()
    {
        var nid = TrayData();
        if (!Shell_NotifyIconW(NIM_MODIFY, ref nid))
            Shell_NotifyIconW(NIM_ADD, ref nid);
    }

    static IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_wmReload != 0 && msg == _wmReload)
        {
            LoadConfig();
            ApplySettings();
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case WM_TRAY:
                if (lParam == (IntPtr)WM_RBUTTONUP) ShowMenu();
                else if (lParam == (IntPtr)WM_LBUTTONDBLCLK) OpenSettings();
                return IntPtr.Zero;
            case WM_COMMAND:
                if ((int)wParam == ID_EXIT) ExitApp();
                else if ((int)wParam == ID_OPEN) OpenSettings();
                return IntPtr.Zero;
            case WM_CLOSE:
                ExitApp();
                return IntPtr.Zero;
            case WM_TIMER:
                if (wParam == (IntPtr)(long)ID_DEBOUNCE)
                {
                    KillTimer(hWnd, ID_DEBOUNCE);
                    try { if (_center) Center(); } catch { }
                }
                else if (wParam == (IntPtr)(long)ID_KEEPALIVE)
                {
                    try
                    {
                        EnsureTrayIcon();
                        if (ConfigChanged()) LoadConfig();
                        ApplyAccent();
                        EnsureTap();
                        if (!IsWin11 && _center)
                        {
                            EnsureHook();
                            if (++_safety >= 4)
                            {
                                _safety = 0;
                                Center();
                            }
                        }
                    }
                    catch { }
                }
                return IntPtr.Zero;
            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    [STAThread]
    static void Main()
    {
        SetProcessDPIAware();

        using var mutex = new Mutex(true, "TricBar_SingleInstance", out bool isNew);
        if (!isNew) return;

        if (UseXamlTap)
        {
            _tapAlive = new EventWaitHandle(false, EventResetMode.ManualReset, TapAliveName);
            _tapActive = new EventWaitHandle(false, EventResetMode.ManualReset, TapActiveName);
            _tapApplied = new EventWaitHandle(false, EventResetMode.ManualReset, TapAppliedName);
            _tapPath = PrepareTapDll();
        }

        _accentSize = Marshal.SizeOf<ACCENT>();
        _accentMem = Marshal.AllocHGlobal(_accentSize);

        var hInst = GetModuleHandleW(null);
        var wndProcPtr = Marshal.GetFunctionPointerForDelegate(WindowProcKeepAlive);
        var wc = new WNDCLASS
        {
            style = CS_DBLCLKS,
            lpfnWndProc = wndProcPtr,
            hInstance = hInst,
            lpszClassName = "TricBarHidden"
        };
        if (RegisterClassW(ref wc) == 0) return;

        _hwnd = CreateWindowExW(0, "TricBarHidden", "TricBar", 0, 0, 0, 0, 0,
            new IntPtr(-3), IntPtr.Zero, hInst, IntPtr.Zero); // HWND_MESSAGE
        if (_hwnd == IntPtr.Zero) return;

        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
            ExtractIconExW(path, 0, out _iconLarge, out _iconSmall, 1);
        if (_iconSmall == IntPtr.Zero && _iconLarge == IntPtr.Zero)
            _iconSmall = LoadIconW(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION

        _wmReload = RegisterWindowMessageW("TricBar_Reload");
        LoadConfig();

        var nid = TrayData();
        Shell_NotifyIconW(NIM_ADD, ref nid);

        ApplySettings();

        SetTimer(_hwnd, ID_KEEPALIVE, 1500, IntPtr.Zero);

        MSG msg;
        while (GetMessageW(out msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
        if (_iconLarge != IntPtr.Zero) DestroyIcon(_iconLarge);
        if (_iconSmall != IntPtr.Zero) DestroyIcon(_iconSmall);
        Marshal.FreeHGlobal(_accentMem);
    }
}
