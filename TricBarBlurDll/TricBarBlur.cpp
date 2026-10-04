// TricBarBlur.cpp  ->  TricBarBlur.dll
//
// DLL carregada DENTRO do explorer.exe pela infraestrutura de XAML Diagnostics
// (InitializeXamlDiagnosticsEx), a mesma tecnica usada pelo TranslucentTB e pelo
// Windhawk. No Windows 11 22H2+ o fundo da barra e um Rectangle XAML
// ("BackgroundFill") que fica POR CIMA da janela da barra, entao o
// SetWindowCompositionAttribute (blur / acrylic / cor) do TricBarBlur.exe nao
// aparece. Aqui a gente acha esse retangulo e troca o Fill por transparente; o
// efeito aplicado pelo .exe na janela passa a ficar visivel.
//
// Sinais com o TricBarBlur.exe (eventos nomeados):
//   TricBar_Blur_Alive   existe enquanto o app esta rodando. Se sumir (app fechou
//                        ou travou), o fundo original volta e a DLL se desarma.
//   TricBar_Blur_Active  sinalizado = efeito ligado (modo != Normal).
//                        Nao sinalizado = devolve o fundo original, mas continua pronta.
//   TricBar_Blur_Applied o app espera por ele para saber que a DLL achou a barra.
//
// Build: TricBarBlurDll\build.bat (precisa do MSVC + Windows SDK).

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <unknwn.h>
#include <ocidl.h>
#include <xamlom.h>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.UI.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/Windows.UI.Xaml.Shapes.h>

#include <chrono>
#include <cwchar>
#include <cctype>
#include <cstdarg>
#include <cstdio>
#include <cstdlib>
#include <memory>
#include <mutex>
#include <string>
#include <string_view>
#include <thread>

namespace wf   = winrt::Windows::Foundation;
namespace wux  = winrt::Windows::UI::Xaml;
namespace wuxm = winrt::Windows::UI::Xaml::Media;
namespace wuxs = winrt::Windows::UI::Xaml::Shapes;

// {B7C0A2E1-5D34-4F6A-9E81-3C7A1D5F2B90}  (tem que ser igual ao TapClsid do TricBarBlur/Program.cs)
static const GUID CLSID_TricBarBlur =
    { 0xb7c0a2e1, 0x5d34, 0x4f6a, { 0x9e, 0x81, 0x3c, 0x7a, 0x1d, 0x5f, 0x2b, 0x90 } };

static constexpr wchar_t kAliveEvent[]   = L"TricBar_Blur_Alive";
static constexpr wchar_t kActiveEvent[]  = L"TricBar_Blur_Active";
static constexpr wchar_t kAppliedEvent[] = L"TricBar_Blur_Applied";
static constexpr wchar_t kTagMarker[]    = L"TricBar.Transparent";
static constexpr wchar_t kVersion[]      = L"3.1.5";

// Log simples em %LOCALAPPDATA%\\TricBar\\blur.log (so grava quando algo muda).
static void Log(const wchar_t* fmt, ...)
{
    wchar_t base[MAX_PATH]{};
    DWORD n = GetEnvironmentVariableW(L"LOCALAPPDATA", base, MAX_PATH);
    if (n == 0 || n >= MAX_PATH) return;
    std::wstring path = std::wstring(base) + L"\\TricBar\\blur.log";

    wchar_t line[400]{};
    va_list ap;
    va_start(ap, fmt);
    _vsnwprintf(line, 380, fmt, ap);
    va_end(ap);
    wcscat(line, L"\r\n");

    char utf8[1200]{};
    int len = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, sizeof(utf8), nullptr, nullptr);
    if (len <= 1) return;

    HANDLE h = CreateFileW(path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;
    DWORD w = 0;
    WriteFile(h, utf8, (DWORD)(len - 1), &w, nullptr);
    CloseHandle(h);
}

// ---------------------------------------------------------------- sinais com o app

static bool AppAlive()
{
    HANDLE h = OpenEventW(SYNCHRONIZE, FALSE, kAliveEvent);
    if (!h) return false;
    CloseHandle(h);
    return true;
}

static bool EffectActive()
{
    HANDLE h = OpenEventW(SYNCHRONIZE, FALSE, kActiveEvent);
    if (!h) return false;
    bool on = WaitForSingleObject(h, 0) == WAIT_OBJECT_0;
    CloseHandle(h);
    return on;
}

static void SignalApplied()
{
    HANDLE h = OpenEventW(EVENT_MODIFY_STATE, FALSE, kAppliedEvent);
    if (!h) return;
    SetEvent(h);
    CloseHandle(h);
}

// ---------------------------------------------------------------- aplicar / restaurar

// ---------------------------------------------------------------- config (mesmo config.ini do Launcher)
//
// No Windows 11 22H2+ a opacidade da cor passada para o blur/acrylic da janela nao e
// respeitada (a cor aparece mesmo com opacidade 0). Entao a cor e a opacidade sao
// aplicadas aqui, no proprio retangulo XAML: alfa 0 = so o blur, sem tinta.

struct Cfg
{
    int  mode = 1;      // 0 normal, 1 clear, 2 blur, 3 acrylic, 4 color
    int  rgb = 0;       // 0xRRGGBB
    int  opacity = 60;  // 0..100

    bool operator==(Cfg const& o) const { return mode == o.mode && rgb == o.rgb && opacity == o.opacity; }
};

static std::mutex g_cfgLock;
static Cfg g_cfg;
static FILETIME g_cfgStamp{};
static bool g_cfgHasStamp = false;

static void RefreshCfg()
{
    wchar_t appdata[MAX_PATH]{};
    DWORD n = GetEnvironmentVariableW(L"APPDATA", appdata, MAX_PATH);
    if (n == 0 || n >= MAX_PATH) return;
    std::wstring path = std::wstring(appdata) + L"\\TricBar\\config.ini";

    WIN32_FILE_ATTRIBUTE_DATA fad{};
    if (!GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &fad)) return;

    std::lock_guard<std::mutex> lock(g_cfgLock);
    if (g_cfgHasStamp && CompareFileTime(&fad.ftLastWriteTime, &g_cfgStamp) == 0) return;

    HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                           nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;
    char buf[1024]{};
    DWORD got = 0;
    ReadFile(h, buf, sizeof(buf) - 1, &got, nullptr);
    CloseHandle(h);

    g_cfgStamp = fad.ftLastWriteTime;
    g_cfgHasStamp = true;

    Cfg c;
    std::string text(buf, got);
    size_t pos = 0;
    while (pos < text.size())
    {
        size_t end = text.find('\n', pos);
        if (end == std::string::npos) end = text.size();
        std::string line = text.substr(pos, end - pos);
        pos = end + 1;
        while (!line.empty() && (line.back() == '\r' || line.back() == ' ')) line.pop_back();
        size_t eq = line.find('=');
        if (eq == std::string::npos || eq == 0) continue;
        std::string key = line.substr(0, eq), val = line.substr(eq + 1);
        for (auto& ch : key) ch = (char)tolower((unsigned char)ch);
        for (auto& ch : val) ch = (char)tolower((unsigned char)ch);
        if (key == "mode")
            c.mode = val == "normal" ? 0 : val == "blur" ? 2 : val == "acrylic" ? 3 : val == "color" ? 4 : 1;
        else if (key == "color")
        {
            if (!val.empty() && val[0] == '#') val.erase(0, 1);
            c.rgb = (int)(strtoul(val.c_str(), nullptr, 16) & 0xFFFFFF);
        }
        else if (key == "opacity")
        {
            int o = atoi(val.c_str());
            c.opacity = o < 0 ? 0 : o > 100 ? 100 : o;
        }
    }
    g_cfg = c;
    Log(L"[v%s] config lida: mode=%d cor=%06X opacidade=%d", kVersion, c.mode, c.rgb, c.opacity);
}

static Cfg GetCfg()
{
    std::lock_guard<std::mutex> lock(g_cfgLock);
    return g_cfg;
}

// Pincel do fundo da barra para a config atual.
//  - Clear:           cor totalmente transparente (a janela, com TRANSPARENTGRADIENT, deixa ver o papel de parede).
//  - Cor:             cor + opacidade (cor lisa, sem blur).
//  - Blur / Acrylic:  AcrylicBrush do proprio XAML, o mesmo que o Windhawk Taskbar Styler usa no fundo da barra.
//                     E o unico jeito de ter desfoque no 22H2+: um SolidColorBrush so pinta cor por cima
//                     e o accent de blur da janela nao aparece atras do retangulo.
//                     Se o Windows estiver com "efeitos de transparencia" desligados ou em economia de energia,
//                     o AcrylicBrush cai sozinho para a FallbackColor (a cor com a opacidade escolhida).
static wuxm::Brush MakeBrush(Cfg const& c)
{
    uint8_t r = (uint8_t)((c.rgb >> 16) & 0xFF), g = (uint8_t)((c.rgb >> 8) & 0xFF), b = (uint8_t)(c.rgb & 0xFF);
    uint8_t a = c.mode >= 2 ? (uint8_t)(c.opacity * 255 / 100) : (uint8_t)0;

    if (c.mode == 2 || c.mode == 3)
    {
        try
        {
            wuxm::AcrylicBrush br;
            br.BackgroundSource(wuxm::AcrylicBackgroundSource::Backdrop);
            br.TintColor(winrt::Windows::UI::Color{ 255, r, g, b });
            br.TintOpacity(c.opacity / 100.0);
            br.FallbackColor(winrt::Windows::UI::Color{ a, r, g, b });
            // Blur: so a tinta sobre o fundo desfocado (sem o clareamento de luminosidade do Acrylic).
            if (c.mode == 2)
                br.TintLuminosityOpacity(winrt::box_value(0.0).as<wf::IReference<double>>());
            Log(L"[v%s] AcrylicBrush criado: modo=%d cor=%06X tint=%d%%", kVersion, c.mode, c.rgb, c.opacity);
            return br;
        }
        catch (...)
        {
            Log(L"[v%s] AcrylicBrush falhou (0x%08X), usando cor lisa", kVersion, (unsigned)winrt::to_hresult());
        }
    }
    return wuxm::SolidColorBrush(winrt::Windows::UI::Color{ a, r, g, b });
}

struct Entry
{
    wuxs::Rectangle rect{ nullptr };
    wuxm::Brush original{ nullptr };
    wuxm::Brush ours{ nullptr };
    Cfg applied{ -1, -1, -1 };  // config com que 'ours' foi montado
    wux::DispatcherTimer timer{ nullptr };
    int64_t token{ 0 };
    bool alive{ false };    // callback + timer registrados
    bool enabled{ false };  // fundo atualmente transparente
};

// Liga: guarda o fill atual do sistema e poe o transparente.
static void Enable(std::shared_ptr<Entry> const& e)
{
    if (!e->alive || e->enabled) return;
    try
    {
        auto cur = e->rect.Fill();
        if (cur != e->ours) e->original = cur;
        e->applied = GetCfg();
        e->ours = MakeBrush(e->applied);
        e->enabled = true;
        Log(L"[v%s] fundo da barra LIGADO: modo=%d cor=%06X opacidade=%d", kVersion, e->applied.mode, e->applied.rgb, e->applied.opacity);
        e->rect.Fill(e->ours);
    }
    catch (...) { e->enabled = false; }
}

// Desliga: devolve o fill original (mas continua observando).
static void Disable(std::shared_ptr<Entry> const& e)
{
    if (!e->alive || !e->enabled) return;
    e->enabled = false;
    Log(L"[v%s] fundo original restaurado", kVersion);
    try { e->rect.Fill(e->original); } catch (...) {}
}

// Desarma de vez (app fechou).
static void Teardown(std::shared_ptr<Entry> const& e)
{
    if (!e->alive) return;
    Disable(e);
    e->alive = false;

    try
    {
        e->rect.UnregisterPropertyChangedCallback(wuxs::Shape::FillProperty(), e->token);
        e->rect.Tag(nullptr);
    }
    catch (...) {}

    if (e->timer)
    {
        auto t = e->timer;
        e->timer = nullptr;
        try { t.Stop(); } catch (...) {}
    }
}

static void ApplyOnUiThread(wuxs::Rectangle const& rect)
{
    auto e = std::make_shared<Entry>();
    e->rect = rect;
    e->original = rect.Fill();
    RefreshCfg();
    e->applied = GetCfg();
    e->ours = MakeBrush(e->applied);
    e->alive = true;

    rect.Tag(winrt::box_value(winrt::hstring{ kTagMarker }));

    // O Windows troca o Fill em alguns estados (tema, Start aberto...). Reaplica.
    e->token = rect.RegisterPropertyChangedCallback(
        wuxs::Shape::FillProperty(),
        [weak = std::weak_ptr<Entry>(e)](wux::DependencyObject const& sender, wux::DependencyProperty const&)
        {
            auto entry = weak.lock();
            if (!entry || !entry->alive || !entry->enabled) return;
            auto r = sender.try_as<wuxs::Rectangle>();
            if (!r) return;
            auto current = r.Fill();
            if (current != entry->ours)
            {
                entry->original = current; // guarda o ultimo fill "do sistema"
                r.Fill(entry->ours);
            }
        });

    if (EffectActive()) Enable(e);

    e->timer = wux::DispatcherTimer();
    e->timer.Interval(std::chrono::milliseconds(400));
    e->timer.Tick([e](wf::IInspectable const&, wf::IInspectable const&)
    {
        if (!AppAlive()) { Teardown(e); return; }
        RefreshCfg();
        if (EffectActive())
        {
            Enable(e);
            if (e->enabled) // modo/cor/opacidade mudaram no Launcher: monta o pincel de novo
            {
                Cfg now = GetCfg();
                if (!(now == e->applied))
                {
                    e->applied = now;
                    e->ours = MakeBrush(now);
                    e->rect.Fill(e->ours);
                    Log(L"[v%s] fundo atualizado: modo=%d cor=%06X opacidade=%d", kVersion, now.mode, now.rgb, now.opacity);
                }
            }
        }
        else Disable(e);
    });
    e->timer.Start();
}

static bool IsTaskbarElement(wux::DependencyObject const& start)
{
    wux::DependencyObject cur = start;
    for (int i = 0; i < 10 && cur; ++i)
    {
        cur = wuxm::VisualTreeHelper::GetParent(cur);
        if (!cur) break;
        winrt::hstring name = winrt::get_class_name(cur);
        std::wstring_view sv{ name };
        if (sv.substr(0, 8) == L"Taskbar.") return true;
    }
    return false;
}

static void HandleRectangle(wuxs::Rectangle const& rect)
{
    if (!IsTaskbarElement(rect)) return;

    // Outra sessao (ou um evento repetido) ja tratou este retangulo.
    auto tag = winrt::unbox_value_or<winrt::hstring>(rect.Tag(), winrt::hstring{});
    if (tag == kTagMarker)
    {
        SignalApplied();
        return;
    }

    auto disp = rect.Dispatcher();
    if (disp && !disp.HasThreadAccess())
    {
        disp.RunAsync(winrt::Windows::UI::Core::CoreDispatcherPriority::Normal, [rect]
        {
            try { ApplyOnUiThread(rect); SignalApplied(); } catch (...) {}
        });
        return;
    }

    ApplyOnUiThread(rect);
    SignalApplied();
}

// ---------------------------------------------------------------- observador da arvore visual

struct VisualTreeWatcher : winrt::implements<VisualTreeWatcher, IVisualTreeServiceCallback2, winrt::non_agile>
{
    explicit VisualTreeWatcher(winrt::com_ptr<IXamlDiagnostics> diag) : m_diag(std::move(diag)) {}

    HRESULT STDMETHODCALLTYPE OnVisualTreeChange(
        ParentChildRelation, VisualElement element, VisualMutationType mutationType) noexcept override
    {
        try
        {
            if (mutationType == Add && element.Type && element.Name &&
                std::wcscmp(element.Type, L"Windows.UI.Xaml.Shapes.Rectangle") == 0 &&
                (std::wcscmp(element.Name, L"BackgroundFill") == 0 ||
                 std::wcscmp(element.Name, L"BackgroundStroke") == 0))
            {
                wf::IInspectable obj{ nullptr };
                winrt::check_hresult(m_diag->GetIInspectableFromHandle(
                    element.Handle, reinterpret_cast<::IInspectable**>(winrt::put_abi(obj))));

                if (auto rect = obj.try_as<wuxs::Rectangle>())
                    HandleRectangle(rect);
            }
        }
        catch (...) {}
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE OnElementStateChanged(InstanceHandle, VisualElementState, LPCWSTR) noexcept override
    {
        return S_OK;
    }

private:
    winrt::com_ptr<IXamlDiagnostics> m_diag;
};

// ---------------------------------------------------------------- site (ponto de entrada do TAP)

struct TapSite : winrt::implements<TapSite, IObjectWithSite>
{
    HRESULT STDMETHODCALLTYPE SetSite(IUnknown* site) noexcept override
    {
        try
        {
            m_site.copy_from(site);
            if (!site) return S_OK;
            Log(L"[v%s] DLL carregada no Explorer", kVersion);

            auto diag = m_site.as<IXamlDiagnostics>();
            auto svc = diag.as<IVisualTreeService3>();
            auto watcher = winrt::make_self<VisualTreeWatcher>(diag);

            // Chamar AdviseVisualTreeChange na mesma thread pode travar o XAML do Explorer
            // (visto no Windhawk Taskbar Styler), entao vai numa thread separada.
            std::thread([svc, watcher]
            {
                CoInitializeEx(nullptr, COINIT_MULTITHREADED);
                svc->AdviseVisualTreeChange(static_cast<IVisualTreeServiceCallback*>(watcher.get()));
            }).detach();
        }
        catch (...)
        {
            return winrt::to_hresult();
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetSite(REFIID riid, void** ppv) noexcept override
    {
        if (!ppv) return E_POINTER;
        *ppv = nullptr;
        if (!m_site) return E_FAIL;
        return m_site->QueryInterface(riid, ppv);
    }

private:
    winrt::com_ptr<IUnknown> m_site;
};

struct TapFactory : winrt::implements<TapFactory, IClassFactory>
{
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID riid, void** ppv) noexcept override
    {
        if (!ppv) return E_POINTER;
        *ppv = nullptr;
        if (outer) return CLASS_E_NOAGGREGATION;
        return winrt::make_self<TapSite>()->QueryInterface(riid, ppv);
    }

    HRESULT STDMETHODCALLTYPE LockServer(BOOL) noexcept override { return S_OK; }
};

// ---------------------------------------------------------------- exports da DLL

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;
    if (!IsEqualGUID(clsid, CLSID_TricBarBlur)) return CLASS_E_CLASSNOTAVAILABLE;
    return winrt::make_self<TapFactory>()->QueryInterface(riid, ppv);
}

extern "C" HRESULT __stdcall DllCanUnloadNow() { return S_FALSE; }

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);
        // Fixa a DLL na memoria do Explorer: threads/timers nossos continuam rodando
        // depois do restore, entao descarregar a DLL seria crash garantido.
        HMODULE pinned = nullptr;
        GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_PIN | GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
            reinterpret_cast<LPCWSTR>(&DllMain), &pinned);
    }
    return TRUE;
}
