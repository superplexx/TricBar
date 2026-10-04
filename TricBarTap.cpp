// TricBarTap.cpp
//
// DLL carregada DENTRO do explorer.exe pela infraestrutura de XAML Diagnostics
// (InitializeXamlDiagnosticsEx), a mesma tecnica usada pelo TranslucentTB e pelo
// Windhawk. No Windows 11 22H2+ o fundo da barra e um Rectangle XAML
// ("BackgroundFill"), entao SetWindowCompositionAttribute nao tem mais efeito.
// Aqui a gente acha esse retangulo e troca o Fill por transparente.
//
// Restauracao: um DispatcherTimer (na thread de UI do XAML) checa a cada 500 ms
// se o evento nomeado "TricBar_TAP_Alive" ainda existe. Ele e criado pelo
// TricBar.exe; se o app fechar (ou travar), o evento some e o fundo original volta.
//
// Build: native\build.bat (precisa do MSVC + Windows SDK).

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
#include <memory>
#include <string_view>
#include <thread>

namespace wf   = winrt::Windows::Foundation;
namespace wux  = winrt::Windows::UI::Xaml;
namespace wuxm = winrt::Windows::UI::Xaml::Media;
namespace wuxs = winrt::Windows::UI::Xaml::Shapes;

// {B7C0A2E1-5D34-4F6A-9E81-3C7A1D5F2B90}  (tem que ser igual ao TapClsid do Program.cs)
static const GUID CLSID_TricBarTap =
    { 0xb7c0a2e1, 0x5d34, 0x4f6a, { 0x9e, 0x81, 0x3c, 0x7a, 0x1d, 0x5f, 0x2b, 0x90 } };

static constexpr wchar_t kAliveEvent[]   = L"TricBar_TAP_Alive";
static constexpr wchar_t kAppliedEvent[] = L"TricBar_TAP_Applied";
static constexpr wchar_t kTagMarker[]    = L"TricBar.Transparent";

// ---------------------------------------------------------------- sinais com o app

static bool AppAlive()
{
    HANDLE h = OpenEventW(SYNCHRONIZE, FALSE, kAliveEvent);
    if (!h) return false;
    CloseHandle(h);
    return true;
}

static void SignalApplied()
{
    HANDLE h = OpenEventW(EVENT_MODIFY_STATE, FALSE, kAppliedEvent);
    if (!h) return;
    SetEvent(h);
    CloseHandle(h);
}

// ---------------------------------------------------------------- aplicar / restaurar

struct Entry
{
    wuxs::Rectangle rect{ nullptr };
    wuxm::Brush original{ nullptr };
    wuxm::Brush ours{ nullptr };
    wux::DispatcherTimer timer{ nullptr };
    int64_t token{ 0 };
    bool active{ false };
};

static void Restore(std::shared_ptr<Entry> const& e)
{
    if (!e->active) return;
    e->active = false;

    try
    {
        e->rect.UnregisterPropertyChangedCallback(wuxs::Shape::FillProperty(), e->token);
        e->rect.Fill(e->original);
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
    e->ours = wuxm::SolidColorBrush(winrt::Windows::UI::Colors::Transparent());
    e->active = true;

    rect.Tag(winrt::box_value(winrt::hstring{ kTagMarker }));
    rect.Fill(e->ours);

    // O Windows troca o Fill em alguns estados (tema, Start aberto...). Reaplica.
    e->token = rect.RegisterPropertyChangedCallback(
        wuxs::Shape::FillProperty(),
        [weak = std::weak_ptr<Entry>(e)](wux::DependencyObject const& sender, wux::DependencyProperty const&)
        {
            auto entry = weak.lock();
            if (!entry || !entry->active) return;
            auto r = sender.try_as<wuxs::Rectangle>();
            if (!r) return;
            auto current = r.Fill();
            if (current != entry->ours)
            {
                entry->original = current; // guarda o ultimo fill "do sistema"
                r.Fill(entry->ours);
            }
        });

    e->timer = wux::DispatcherTimer();
    e->timer.Interval(std::chrono::milliseconds(500));
    e->timer.Tick([e](wf::IInspectable const&, wf::IInspectable const&)
    {
        if (!AppAlive()) Restore(e);
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
    if (!IsEqualGUID(clsid, CLSID_TricBarTap)) return CLASS_E_CLASSNOTAVAILABLE;
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
