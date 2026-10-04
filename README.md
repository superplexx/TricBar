# TricBar

🇧🇷 [Versão em português](#-português-br)

Transparent taskbar for Windows 10 and 11 (11 22H2 and later included), with automatic icon centering on Windows 10.

A tiny, lightweight tray app that makes your taskbar transparent and centers your icons automatically on Windows 10.

## Features

- Transparent taskbar
- Icons centered automatically on Windows 10 (updates when you open or close apps)
- On Windows 11, applies transparency only; Windows 11 already centers the icons
- Windows 11 22H2+ uses a small native helper (`TricBarTap.dll`, must stay next to `TricBar.exe`)
- Runs quietly in the system tray
- Restores the default taskbar when you exit
- On Windows 10, use the Stable Version v1.0.3!

## Requirements

- Windows 10 or 11
- [.NET 8 Desktop Runtime (x64)](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe)

## Installation

1. Download `TricBar-vX.X.X.zip` from the [Releases](https://github.com/superplexx/TricBar/releases) page.
2. Extract the zip.
3. Run `TricBar.exe`.

To start with Windows, press `Win + R`, type `shell:startup` and put a shortcut to `TricBar.exe` in that folder.

## Usage

TricBar runs in the system tray (near the clock). Right-click the icon and choose **Sair** to close it and restore the taskbar.

## Windows 11 (22H2 and later)

The Windows 11 taskbar background is a XAML rectangle, so the Windows 10 method no longer works. TricBar loads `TricBarTap.dll` into `explorer.exe` through the XAML Diagnostics API (same mechanism as TranslucentTB and Windhawk) and clears that rectangle. Notes:

- Keep `TricBarTap.dll` next to `TricBar.exe`. The tray tooltip shows `ativo` when it worked.
- Don't run it together with TranslucentTB or the Windhawk *Windows 11 Taskbar Styler* (only one XAML diagnostics client at a time).
- Don't run TricBar as administrator (Explorer couldn't see its signal and would restore the taskbar right away).
- If something goes wrong, restart Explorer (Task Manager > Windows Explorer > Restart).
- Build the DLL once with `native\build.bat` (needs *Build Tools for Visual Studio* with the C++ desktop workload), then build/publish the app as usual.

## Adjusting the position

If the icons look a few pixels off-center on your screen, change `AdjustX` in `Program.cs` (positive moves right, negative moves left) and rebuild:

```csharp
const int AdjustX = 35;
```

## Build from source

```powershell
git clone https://github.com/superplexx/TricBar.git
cd TricBar
dotnet run
```

To publish a single `.exe`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

---

# 🇧🇷 Português (BR)

**TricBar** deixa a barra de tarefas transparente no Windows 10 e 11 (inclusive 11 22H2 ou mais novo) e centraliza os ícones automaticamente no Windows 10.

Um app leve que fica na bandeja do sistema, deixa a barra de tarefas transparente e centraliza os ícones automaticamente no Windows 10.

## Recursos

- Barra de tarefas transparente
- Ícones centralizados automaticamente no Windows 10 (atualiza ao abrir ou fechar apps)
- No Windows 11, aplica apenas a transparência; o próprio Windows 11 já centraliza os ícones
- No Windows 11 22H2+ usa um pequeno componente nativo (`TricBarTap.dll`, que precisa ficar ao lado do `TricBar.exe`)
- Roda discretamente na bandeja do sistema
- Restaura a barra padrão ao fechar
- No Windows 10 use a Versão Estável v1.0.3

## Requisitos

- Windows 10 ou 11
- [.NET 8 Desktop Runtime (x64)](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe)

## Instalação

1. Baixe o `TricBar-vX.X.X.zip` na página de [Releases](https://github.com/superplexx/TricBar/releases).
2. Extraia o zip.
3. Abra o `TricBar.exe`.

Para iniciar com o Windows, aperte `Win + R`, digite `shell:startup` e coloque um atalho do `TricBar.exe` nessa pasta.

## Como usar

O TricBar fica na bandeja do sistema (perto do relógio). Clique com o botão direito no ícone e escolha **Sair** para fechar e restaurar a barra.

## Windows 11 (22H2 ou mais novo)

No Windows 11 o fundo da barra é um retângulo XAML, então o método do Windows 10 não funciona mais. O TricBar carrega a `TricBarTap.dll` dentro do `explorer.exe` pela API de XAML Diagnostics (o mesmo mecanismo do TranslucentTB e do Windhawk) e limpa esse retângulo. Observações:

- Mantenha a `TricBarTap.dll` ao lado do `TricBar.exe`. O tooltip do ícone na bandeja mostra `ativo` quando deu certo.
- Não use junto com o TranslucentTB nem com o mod *Windows 11 Taskbar Styler* do Windhawk (só pode haver um cliente de XAML Diagnostics por vez).
- Não rode o TricBar como administrador (o Explorer não enxergaria o sinal dele e restauraria a barra na hora).
- Se algo der errado, reinicie o Explorer (Gerenciador de Tarefas > Windows Explorer > Reiniciar).
- Compile a DLL uma vez com `native\build.bat` (precisa do *Build Tools for Visual Studio* com a carga de C++ para desktop) e depois compile/publique o app normalmente.

## Ajustando a posição

Se os ícones parecerem estar alguns pixels fora do centro na sua tela, altere `AdjustX` no arquivo `Program.cs` (valores positivos movem para a direita, negativos para a esquerda) e recompile:

```csharp
const int AdjustX = 35;
```

## Compilar a partir do código-fonte

```powershell
git clone https://github.com/superplexx/TricBar.git
cd TricBar
dotnet run
```

Para publicar um único arquivo `.exe`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```
