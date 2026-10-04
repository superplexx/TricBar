using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace TricBarLauncher;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

sealed class MainForm : Form
{
    // ---------- WinAPI (para falar com o TricBarBlur.exe) ----------
    const uint WM_CLOSE = 0x0010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindowExW(IntPtr parent, IntPtr after, string? cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterWindowMessageW(string s);
    [DllImport("user32.dll")]
    static extern bool PostMessageW(IntPtr h, uint msg, IntPtr wParam, IntPtr lParam);

    static readonly uint WmReload = RegisterWindowMessageW("TricBar_Reload");
    static IntPtr FindEngine() => FindWindowExW(new IntPtr(-3), IntPtr.Zero, "TricBarHidden", null); // HWND_MESSAGE

    // ---------- Configuração ----------
    static readonly string CfgDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TricBar");
    static readonly string CfgPath = Path.Combine(CfgDir, "config.ini");
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    static readonly string[] ModeKeys = { "normal", "clear", "blur", "acrylic", "color" };
    static readonly string[] ModeNames =
    {
        "Normal (padrão do Windows)", "Transparente", "Blur (desfoque)", "Acrylic", "Cor sólida"
    };
    static readonly (string Name, int Rgb)[] Presets =
    {
        ("Preto", 0x000000), ("Branco", 0xFFFFFF), ("Amarelo", 0xFFD60A), ("Vermelho", 0xE81123),
        ("Verde", 0x10C040), ("Azul", 0x0078D4), ("Roxo", 0x8E44AD)
    };

    static readonly int WinBuild = Environment.OSVersion.Version.Build;

    readonly ComboBox _cbMode = new();
    readonly Panel _pnlColor = new();
    readonly TextBox _tbHex = new();
    readonly Button _btnPick = new();
    readonly Button[] _presetButtons = new Button[Presets.Length];
    readonly TrackBar _tbOpacity = new();
    readonly Label _lblOpacity = new();
    readonly CheckBox _chkCenter = new();
    readonly CheckBox _chkStartup = new();
    readonly Label _lblStatus = new();
    readonly Label _lblWarn = new();
    readonly Button _btnToggle = new();
    readonly ToolTip _tip = new();
    readonly Timer _tmrSave = new() { Interval = 150 };
    readonly Timer _tmrStatus = new() { Interval = 1000 };

    int _rgb;
    bool _loading;

    public MainForm()
    {
        Text = "TricBar 3.2.0";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(440, 470);
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // Estilo
        Controls.Add(MakeLabel("Estilo da barra de tarefas", 16, 14));
        _cbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbMode.SetBounds(16, 38, 408, 28);
        _cbMode.Items.AddRange(ModeNames);
        _cbMode.SelectedIndexChanged += (_, _) => { UpdateEnabled(); ScheduleSave(); };
        Controls.Add(_cbMode);

        // Cor
        Controls.Add(MakeLabel("Cor", 16, 80));
        _pnlColor.SetBounds(16, 104, 40, 28);
        _pnlColor.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(_pnlColor);

        _tbHex.SetBounds(66, 105, 100, 26);
        _tbHex.MaxLength = 7;
        _tbHex.TextChanged += (_, _) =>
        {
            if (_loading) return;
            if (TryParseHex(_tbHex.Text, out int rgb)) SetColor(rgb, updateText: false);
        };
        Controls.Add(_tbHex);

        _btnPick.Text = "Escolher...";
        _btnPick.SetBounds(176, 103, 110, 30);
        _btnPick.Click += (_, _) => PickColor();
        Controls.Add(_btnPick);

        for (int i = 0; i < Presets.Length; i++)
        {
            var (name, rgb) = Presets[i];
            var b = new Button
            {
                Text = "",
                BackColor = RgbToColor(rgb),
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderColor = Color.Gray;
            b.SetBounds(16 + i * 40, 144, 34, 28);
            b.Click += (_, _) => SetColor(rgb, updateText: true);
            _tip.SetToolTip(b, name);
            _presetButtons[i] = b;
            Controls.Add(b);
        }

        // Opacidade
        Controls.Add(_lblOpacity);
        _lblOpacity.SetBounds(16, 190, 408, 20);
        _tbOpacity.SetBounds(12, 212, 414, 45);
        _tbOpacity.Minimum = 0;
        _tbOpacity.Maximum = 100;
        _tbOpacity.TickFrequency = 10;
        _tbOpacity.ValueChanged += (_, _) =>
        {
            _lblOpacity.Text = $"Opacidade da cor: {_tbOpacity.Value}%";
            if (!_loading) ScheduleSave();
        };
        Controls.Add(_tbOpacity);

        // Opções
        _chkCenter.Text = WinBuild >= 22000
            ? "Centralizar ícones (o Windows 11 já centraliza sozinho)"
            : "Centralizar ícones da barra";
        _chkCenter.SetBounds(16, 268, 408, 24);
        _chkCenter.Enabled = WinBuild < 22000;
        _chkCenter.CheckedChanged += (_, _) => { if (!_loading) ScheduleSave(); };
        Controls.Add(_chkCenter);

        _chkStartup.Text = "Iniciar o TricBar com o Windows";
        _chkStartup.SetBounds(16, 298, 408, 24);
        _chkStartup.CheckedChanged += (_, _) => { if (!_loading) SetStartup(_chkStartup.Checked); };
        Controls.Add(_chkStartup);

        // Status
        _lblStatus.SetBounds(16, 336, 408, 20);
        Controls.Add(_lblStatus);

        _lblWarn.SetBounds(16, 360, 408, 56);
        _lblWarn.ForeColor = Color.DarkOrange;
        if (WinBuild >= 22621)
        {
            string dll = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "TricBarBlur.dll");
            if (!File.Exists(dll))
                _lblWarn.Text = "TricBarBlur.dll não encontrada ao lado do launcher. No Windows 11 22H2 ou mais novo " +
                                "ela é necessária para Blur, Acrylic, Cor e Transparente. Não use junto com o TranslucentTB.";
            else
                _lblWarn.Text = "Windows 11: este modo usa a TricBarBlur.dll. Não use junto com o TranslucentTB " +
                                "nem com o mod Taskbar Styler do Windhawk.";
        }
        Controls.Add(_lblWarn);

        _btnToggle.SetBounds(16, 424, 160, 32);
        _btnToggle.Click += (_, _) => ToggleEngine();
        Controls.Add(_btnToggle);

        _tmrSave.Tick += (_, _) => { _tmrSave.Stop(); SaveAndNotify(); };
        _tmrStatus.Tick += (_, _) => UpdateStatus();

        LoadConfig();
        UpdateEnabled();

        if (FindEngine() == IntPtr.Zero) StartEngine();
        UpdateStatus();
        _tmrStatus.Start();
    }

    // ---------- Interface ----------
    static Label MakeLabel(string text, int x, int y) =>
        new() { Text = text, Left = x, Top = y, Width = 408, Height = 20, AutoSize = false };

    static Color RgbToColor(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    static bool TryParseHex(string s, out int rgb)
    {
        rgb = 0;
        s = s.Trim().TrimStart('#');
        return s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb);
    }

    void SetColor(int rgb, bool updateText)
    {
        _rgb = rgb & 0xFFFFFF;
        _pnlColor.BackColor = RgbToColor(_rgb);
        if (updateText)
        {
            _loading = true;
            _tbHex.Text = "#" + _rgb.ToString("X6", CultureInfo.InvariantCulture);
            _loading = false;
        }
        if (!_loading) ScheduleSave();
    }

    void PickColor()
    {
        using var dlg = new ColorDialog { FullOpen = true, AnyColor = true, Color = RgbToColor(_rgb) };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            SetColor((dlg.Color.R << 16) | (dlg.Color.G << 8) | dlg.Color.B, updateText: true);
    }

    // Blur, Acrylic e Cor usam cor + opacidade; Normal e Transparente não.
    void UpdateEnabled()
    {
        bool useColor = _cbMode.SelectedIndex >= 2;
        _pnlColor.Enabled = _tbHex.Enabled = _btnPick.Enabled = _tbOpacity.Enabled = useColor;
        foreach (var b in _presetButtons) b.Enabled = useColor;
    }

    // ---------- Config ----------
    void LoadConfig()
    {
        _loading = true;
        try
        {
            string mode = "clear";
            int rgb = 0, opacity = 60;
            bool center = true;

            if (File.Exists(CfgPath))
            {
                foreach (var raw in File.ReadAllLines(CfgPath))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = raw.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = raw.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "mode": mode = val.ToLowerInvariant(); break;
                        case "color": if (TryParseHex(val, out int c)) rgb = c; break;
                        case "opacity":
                            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int o))
                                opacity = Math.Clamp(o, 0, 100);
                            break;
                        case "center": center = val != "0"; break;
                    }
                }
            }

            int idx = Array.IndexOf(ModeKeys, mode);
            _cbMode.SelectedIndex = idx < 0 ? 1 : idx;
            _tbOpacity.Value = opacity;
            _lblOpacity.Text = $"Opacidade da cor: {opacity}%";
            _chkCenter.Checked = center;
            _rgb = rgb;
            _pnlColor.BackColor = RgbToColor(rgb);
            _tbHex.Text = "#" + rgb.ToString("X6", CultureInfo.InvariantCulture);

            using var key2 = Registry.CurrentUser.OpenSubKey(RunKey);
            _chkStartup.Checked = key2?.GetValue("TricBar") != null;
        }
        catch { }
        finally { _loading = false; }
    }

    void ScheduleSave()
    {
        if (_loading) return;
        _tmrSave.Stop();
        _tmrSave.Start();
    }

    void SaveAndNotify()
    {
        try
        {
            Directory.CreateDirectory(CfgDir);
            string text =
                $"mode={ModeKeys[Math.Max(0, _cbMode.SelectedIndex)]}\n" +
                $"color={_rgb.ToString("X6", CultureInfo.InvariantCulture)}\n" +
                $"opacity={_tbOpacity.Value}\n" +
                $"center={(_chkCenter.Checked ? 1 : 0)}\n";
            string tmp = CfgPath + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, CfgPath, true); // troca atômica: o motor nunca lê arquivo pela metade
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não consegui salvar a configuração:\n" + ex.Message, "TricBar");
            return;
        }

        var h = FindEngine();
        if (h != IntPtr.Zero) PostMessageW(h, WmReload, IntPtr.Zero, IntPtr.Zero);
    }

    // ---------- Motor ----------
    static string EnginePath =>
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "TricBarBlur.exe");

    void StartEngine()
    {
        try
        {
            if (!File.Exists(EnginePath))
            {
                MessageBox.Show(this, "Não achei o TricBarBlur.exe na mesma pasta do launcher.", "TricBar");
                return;
            }
            Process.Start(new ProcessStartInfo(EnginePath) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não consegui iniciar o TricBar:\n" + ex.Message, "TricBar");
        }
    }

    void ToggleEngine()
    {
        var h = FindEngine();
        if (h != IntPtr.Zero) PostMessageW(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        else StartEngine();
        UpdateStatus();
    }

    void UpdateStatus()
    {
        bool running = FindEngine() != IntPtr.Zero;
        _lblStatus.Text = running ? "TricBar em execução" : "TricBar parado";
        _btnToggle.Text = running ? "Parar TricBar" : "Iniciar TricBar";
    }

    void SetStartup(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (on) key.SetValue("TricBar", "\"" + EnginePath + "\"");
            else key.DeleteValue("TricBar", throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Não consegui alterar a inicialização com o Windows:\n" + ex.Message, "TricBar");
        }
    }
}
