# TricBar

🇧🇷 [Versão em português](#-português-br)

Transparent and Centralized Taskbar for Windows 10.

A tiny, lightweight tray app that makes your taskbar transparent and keeps your icons centered automatically.

## Features

- Transparent taskbar
- Icons centered automatically (updates when you open or close apps)
- Runs quietly in the system tray
- Restores the default taskbar when you exit

## Requirements

- Windows 10
- [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)

## Installation

1. Download `TricBar-vX.X.X.zip` from the [Releases](https://github.com/superplexx/TricBar/releases) page.
2. Extract the zip.
3. Run `TricBar.exe`.

To start with Windows, press `Win + R`, type `shell:startup` and put a shortcut to `TricBar.exe` in that folder.

## Usage

TricBar runs in the system tray (near the clock). Right-click the icon and choose **Sair** to close it and restore the taskbar.

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

**TricBar** é uma barra de tarefas transparente e centralizada para o Windows 10.

Um app leve que fica na bandeja do sistema, deixa a barra de tarefas transparente e mantém os ícones centralizados automaticamente.

## Recursos

- Barra de tarefas transparente
- Ícones centralizados automaticamente (atualiza ao abrir ou fechar apps)
- Roda discretamente na bandeja do sistema
- Restaura a barra padrão ao fechar

## Requisitos

- Windows 10
- [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/pt-br/download/dotnet/8.0)

## Instalação

1. Baixe o `TricBar-vX.X.X.zip` na página de [Releases](https://github.com/superplexx/TricBar/releases).
2. Extraia o zip.
3. Abra o `TricBar.exe`.

Para iniciar com o Windows, aperte `Win + R`, digite `shell:startup` e coloque um atalho do `TricBar.exe` nessa pasta.

## Como usar

O TricBar fica na bandeja do sistema (perto do relógio). Clique com o botão direito no ícone e escolha **Sair** para fechar e restaurar a barra.

## Ajustar a posição

Se os ícones estiverem alguns pixels fora do centro na sua tela, mude o `AdjustX` no
