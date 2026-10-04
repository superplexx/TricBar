# TricBar

🇧🇷 [Versão em português](#-português-br)

Transparent and customizable taskbar for **Windows 10 and Windows 11**, including **Windows 11 22H2 and later**.

TricBar is a lightweight tray application that makes the Windows taskbar transparent. On Windows 10, it can automatically center the taskbar icons. On Windows 11, it supports transparent, Blur, Acrylic and colored taskbar effects.

---

## Features

* Transparent taskbar
* Blur effect
* Acrylic effect
* Custom taskbar color
* Adjustable opacity
* Automatic icon centering on Windows 10
* Windows 11 native icon centering
* Windows 11 22H2+ support
* Lightweight system tray application
* Automatically restores the default taskbar when closed
* Detects whether the Windows 11 taskbar was successfully modified
* Can run quietly in the background

---

## How it works

### Windows 10

TricBar uses the Windows composition APIs to make the taskbar transparent and automatically centers the taskbar icons.

The icon positioning is updated when applications are opened or closed.

### Windows 11

Windows 11 changed the taskbar architecture.

Starting with **Windows 11 22H2**, the taskbar background is rendered using a XAML element. Because of this, the traditional `SetWindowCompositionAttribute` method alone is no longer enough.

TricBar therefore uses a small native helper loaded into `explorer.exe` through the **XAML Diagnostics API**, using a mechanism similar to applications such as TranslucentTB and Windhawk's Windows 11 Taskbar Styler.

The helper makes the XAML taskbar background transparent, allowing the Blur, Acrylic or color effect applied by TricBar to become visible.

---

## Components

Depending on the build/version, TricBar can contain the following components:

| File                  | Function                                     |
| --------------------- | -------------------------------------------- |
| `TricBar.exe`         | Main tray application                        |
| `TricBarTap.dll`      | Native Windows 11 22H2+ helper               |
| `TricBarBlur.exe`     | Blur/Acrylic/color engine and tray component |
| `TricBarBlur.dll`     | Native DLL loaded inside `explorer.exe`      |
| `TricBarLauncher.exe` | Configuration window                         |

The Windows 11 helper/DLL must remain in the same directory as the executable that loads it.

If the TricBar process is closed or crashes, the Windows 11 helper is designed to restore the original taskbar background.

In **Normal** mode, the XAML helper remains disabled.

---

## Requirements

* Windows 10 or Windows 11
* **.NET 8 Desktop Runtime (x64)**

Download:

[.NET 8 Desktop Runtime x64](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe)

For building from source:

* .NET 8 SDK
* Build Tools for Visual Studio
* **Desktop development with C++** workload

---

## Installation

1. Download the latest `TricBar-vX.X.X.zip` from the [Releases](https://github.com/superplexx/TricBar/releases) page.
2. Extract the ZIP file.
3. Run `TricBar.exe`.
4. Keep the required DLLs in the same folder as the executable.

TricBar runs in the system tray, near the Windows clock.

To close TricBar and restore the normal taskbar:

**Right-click the TricBar icon → Sair / Exit**

---

## Start with Windows

To start TricBar automatically with Windows:

1. Press `Win + R`.
2. Type:

```text
shell:startup
```

3. Press Enter.
4. Create or place a shortcut to `TricBar.exe` in that folder.

---

# Windows 11 22H2 and later

Windows 11 22H2 introduced a XAML-based taskbar background.

Because of this, the Windows 10 transparency technique does not work by itself.

TricBar uses a native helper/DLL and the Windows XAML Diagnostics API to access the taskbar's XAML elements and remove the opaque background.

This allows the composition effect to be visible.

### Important

* Keep `TricBarTap.dll` next to `TricBar.exe` when using the TricBarTap implementation.
* With the Blur implementation, keep `TricBarBlur.dll` next to the appropriate executable.
* The tray tooltip shows **`ativo`** when the helper successfully detects the taskbar.
* **Do not run TricBar as administrator.**
* Do not use TricBar together with **TranslucentTB**.
* Do not use it together with the Windhawk **Windows 11 Taskbar Styler** mod.
* Only one XAML Diagnostics client should control the taskbar at a time.

If the taskbar becomes stuck or something goes wrong, restart Windows Explorer:

**Task Manager → Windows Explorer → Restart**

---

# Transparency, Blur, Acrylic and Color

The Blur version of TricBar provides several taskbar appearance modes:

* **Normal**
* **Transparent**
* **Blur**
* **Acrylic**
* **Color**

The configuration interface can be used to adjust the appearance, including color and opacity.

The composition effect is applied to the taskbar while the native Windows 11 XAML background is made transparent by the helper DLL.

---

# Windows 10

On Windows 10, TricBar makes the taskbar transparent and automatically centers the taskbar icons.

The icon position is updated when applications are opened or closed.

If the icons appear slightly off-center on your screen, you can adjust the horizontal offset in `Program.cs`.

Example:

```csharp
const int AdjustX = 35;
```

Positive values move the icons to the right.

Negative values move the icons to the left.

After changing the value, rebuild the application.

---

# Building from source

Clone the repository:

```powershell
git clone https://github.com/superplexx/TricBar.git
cd TricBar
```

For a normal development build:

```powershell
dotnet run
```

---

## Recommended build

For the complete release build, use the project's build script:

```powershell
./build.ps1
```

The script builds the required components and generates a release ZIP such as:

```text
TricBar-Win11-v3.1.5.zip
```

### Why not use `dotnet build` for the release?

The managed application assembly can also be named `TricBarBlur.dll`.

This can be confused with the **native `TricBarBlur.dll`** required by the Windows 11 helper.

For distribution, use the project's `build.ps1` script and single-file publishing configuration instead of simply copying the output of `dotnet build`.

---

# Building the native DLL

If you only need to build the native helper:

```powershell
TricBarBlurDll\build.bat
```

or, depending on the project version:

```powershell
native\build.bat
```

The resulting native DLL will be generated in the corresponding `bin` directory.

For example:

```text
TricBarBlurDll\bin\TricBarBlur.dll
```

Building the native component requires:

**Build Tools for Visual Studio → Desktop development with C++**

---

# GitHub Actions

You can also build TricBar without having Visual Studio Build Tools installed locally.

The repository includes:

```text
.github/workflows/build.yml
```

On GitHub:

1. Open the repository.
2. Go to **Actions**.
3. Select the **build** workflow.
4. Choose **Run workflow**.
5. Wait for the build to finish.
6. Download the generated ZIP artifact.

This is useful when the machine does not have the required C++ build environment.

---

# Project structure

A typical release may look like:

```text
TricBar/
│
├── TricBar.exe
├── TricBarTap.dll
├── TricBarBlur.exe
├── TricBarBlur.dll
├── TricBarLauncher.exe
│
└── ...
```

Only the components required by the selected TricBar version/build need to be distributed.

---

# Important compatibility notes

### Do not use with TranslucentTB

TricBar and TranslucentTB can both use the Windows XAML Diagnostics mechanism to modify the Windows 11 taskbar.

Running both at the same time can cause conflicts.

### Do not use with Windows 11 Taskbar Styler

The same applies to the Windhawk **Windows 11 Taskbar Styler** mod.

Disable it before using TricBar's Windows 11 XAML helper.

### Do not run as administrator

Run TricBar normally.

Running it elevated can prevent the Explorer process from receiving the expected communication/signals, causing the taskbar modification to immediately revert.

---

# Troubleshooting

### The taskbar is not transparent

Make sure:

* TricBar is running.
* The required DLL is next to the executable.
* You are not running TranslucentTB.
* Windhawk's Windows 11 Taskbar Styler is disabled.
* TricBar is not running as administrator.

On Windows 11 22H2+, check whether the tray tooltip says:

```text
ativo
```

If it does not, restart Windows Explorer.

---

### The taskbar looks normal after restarting Explorer

This can happen because `explorer.exe` was restarted.

Make sure TricBar is still running. The helper should detect the new Explorer/taskbar instance.

If necessary, restart TricBar as well.

---

### Something is broken after changing the taskbar

Open Task Manager:

```text
Ctrl + Shift + Esc
```

Find:

```text
Windows Explorer
```

Right-click it and select:

```text
Restart
```

---

# License

See the repository license for the current licensing terms.

---

# 🇧🇷 Português (BR)

**TricBar** deixa a barra de tarefas do Windows transparente e personalizável no **Windows 10 e Windows 11**, incluindo o **Windows 11 22H2 ou mais novo**.

No Windows 10, ele também pode centralizar automaticamente os ícones.

No Windows 11, o TricBar pode aplicar efeitos como **Transparente, Blur, Acrylic e Cor**, com controle de opacidade.

---

## Recursos

* Barra de tarefas transparente
* Efeito Blur
* Efeito Acrylic
* Cor personalizada
* Controle de opacidade
* Ícones centralizados automaticamente no Windows 10
* Windows 11 já possui centralização nativa dos ícones
* Suporte ao Windows 11 22H2+
* Aplicativo leve na bandeja do sistema
* Restaura a barra padrão ao fechar
* Detecta quando a barra do Windows 11 foi encontrada
* Executa silenciosamente em segundo plano

---

## Como funciona

### Windows 10

No Windows 10, o TricBar utiliza as APIs de composição do Windows para deixar a barra de tarefas transparente.

Ele também pode centralizar automaticamente os ícones e atualizar a posição quando aplicativos são abertos ou fechados.

### Windows 11

O Windows 11 mudou a estrutura da barra de tarefas.

A partir do **Windows 11 22H2**, o fundo da barra é um elemento XAML. Por isso, o método tradicional usando apenas `SetWindowCompositionAttribute` não é suficiente.

O TricBar utiliza uma pequena DLL nativa carregada dentro do `explorer.exe` através da **API de XAML Diagnostics**.

O funcionamento é semelhante ao mecanismo utilizado por ferramentas como TranslucentTB e o mod Windows 11 Taskbar Styler do Windhawk.

A DLL torna transparente o elemento XAML responsável pelo fundo da barra, permitindo que o efeito de composição aplicado pelo TricBar apareça.

---

## Componentes

Dependendo da versão/build do projeto, podem existir os seguintes arquivos:

| Arquivo               | Função                                            |
| --------------------- | ------------------------------------------------- |
| `TricBar.exe`         | Aplicativo principal da bandeja                   |
| `TricBarTap.dll`      | Helper nativo para Windows 11 22H2+               |
| `TricBarBlur.exe`     | Motor de Blur/Acrylic/Cor e componente da bandeja |
| `TricBarBlur.dll`     | DLL nativa carregada dentro do `explorer.exe`     |
| `TricBarLauncher.exe` | Janela de configurações                           |

As DLLs necessárias devem permanecer na mesma pasta do executável que as utiliza.

Se o processo do TricBar for fechado ou travar, o helper do Windows 11 foi projetado para restaurar o fundo original da barra.

No modo **Normal**, o helper XAML permanece desativado.

---

## Requisitos

* Windows 10 ou Windows 11
* **.NET 8 Desktop Runtime (x64)**

Download:

[.NET 8 Desktop Runtime x64](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe)

Para compilar:

* .NET 8 SDK
* Build Tools for Visual Studio
* Carga **Desenvolvimento para desktop com C++**

---

## Instalação

1. Baixe o `TricBar-vX.X.X.zip` na página de [Releases](https://github.com/superplexx/TricBar/releases).
2. Extraia o ZIP.
3. Execute o `TricBar.exe`.
4. Mantenha as DLLs necessárias na mesma pasta do executável.

O TricBar ficará na bandeja do sistema, perto do relógio.

Para fechar o programa e restaurar a barra padrão:

**Clique com o botão direito no ícone do TricBar → Sair**

---

## Iniciar com o Windows

Para iniciar automaticamente com o Windows:

1. Pressione `Win + R`.
2. Digite:

```text
shell:startup
```

3. Pressione Enter.
4. Coloque nessa pasta um atalho para:

```text
TricBar.exe
```

---

# Windows 11 22H2 ou mais novo

No Windows 11 22H2, o fundo da barra de tarefas passou a utilizar um elemento XAML.

Por isso, o método utilizado no Windows 10 não funciona sozinho.

O TricBar utiliza uma DLL nativa e a API de XAML Diagnostics para acessar os elementos XAML da barra e remover o fundo opaco.

Isso permite aplicar os efeitos de transparência, Blur, Acrylic e cor.

### Importante

* Mantenha `TricBarTap.dll` ao lado do `TricBar.exe` quando estiver usando a implementação TricBarTap.
* Na implementação Blur, mantenha `TricBarBlur.dll` ao lado do executável correspondente.
* O tooltip do ícone da bandeja mostra **`ativo`** quando o helper encontrou a barra.
* **Não execute o TricBar como administrador.**
* Não use o TricBar junto com o **TranslucentTB**.
* Não use junto com o mod **Windows 11 Taskbar Styler** do Windhawk.
* Apenas um cliente de XAML Diagnostics deve controlar a barra ao mesmo tempo.

Se algo der errado, reinicie o Explorer:

**Gerenciador de Tarefas → Windows Explorer → Reiniciar**

---

# Transparência, Blur, Acrylic e Cor

A versão Blur do TricBar possui diferentes modos de aparência:

* **Normal**
* **Transparente**
* **Blur**
* **Acrylic**
* **Cor**

A janela de configurações permite alterar a aparência da barra, incluindo cor e opacidade.

O efeito de composição é aplicado à barra enquanto a DLL nativa deixa transparente o fundo XAML do Windows 11.

---

# Windows 10

No Windows 10, o TricBar deixa a barra transparente e pode centralizar automaticamente os ícones.

A posição dos ícones é atualizada quando aplicativos são abertos ou fechados.

Se os ícones ficarem alguns pixels fora do centro, altere `AdjustX` no `Program.cs`.

Exemplo:

```csharp
const int AdjustX = 35;
```

Valores positivos movem os ícones para a direita.

Valores negativos movem os ícones para a esquerda.

Depois de alterar o valor, compile novamente.

---

# Compilar a partir do código-fonte

Clone o repositório:

```powershell
git clone https://github.com/superplexx/TricBar.git
cd TricBar
```

Para executar durante o desenvolvimento:

```powershell
dotnet run
```

---

## Compilação recomendada

Para gerar uma versão completa para distribuição, utilize:

```powershell
./build.ps1
```

O script compila os componentes necessários e gera um ZIP de release, por exemplo:

```text
TricBar-Win11-v3.1.5.zip
```

### Por que não usar apenas `dotnet build`?

O assembly gerenciado do aplicativo também pode se chamar:

```text
TricBarBlur.dll
```

Isso pode ser confundido com a **`TricBarBlur.dll` nativa** usada pelo helper do Windows 11.

Por isso, para distribuir o programa, utilize o `build.ps1` e a configuração de publicação single-file do projeto.

---

# Compilando a DLL nativa

Se quiser compilar apenas a DLL nativa:

```powershell
TricBarBlurDll\build.bat
```

ou, dependendo da versão do projeto:

```powershell
native\build.bat
```

O resultado será gerado no diretório `bin` correspondente.

Por exemplo:

```text
TricBarBlurDll\bin\TricBarBlur.dll
```

É necessário ter instalado:

**Build Tools for Visual Studio → Desenvolvimento para desktop com C++**

---

# GitHub Actions

Também é possível compilar o projeto sem instalar o Visual Studio Build Tools localmente.

O projeto possui:

```text
.github/workflows/build.yml
```

No GitHub:

1. Abra o repositório.
2. Entre em **Actions**.
3. Selecione o workflow **build**.
4. Clique em **Run workflow**.
5. Aguarde a compilação.
6. Baixe o ZIP gerado como artefato.

---

# Estrutura do projeto

Uma versão de release pode conter:

```text
TricBar/
│
├── TricBar.exe
├── TricBarTap.dll
├── TricBarBlur.exe
├── TricBarBlur.dll
├── TricBarLauncher.exe
│
└── ...
```

Os componentes necessários dependem da versão/build utilizada.

---

# Observações importantes

### Não use com TranslucentTB

O TricBar e o TranslucentTB podem utilizar o mecanismo de XAML Diagnostics para modificar a barra do Windows 11.

Usar os dois ao mesmo tempo pode causar conflitos.

### Não use com Windows 11 Taskbar Styler

O mesmo vale para o mod **Windows 11 Taskbar Styler** do Windhawk.

Desative-o antes de utilizar o helper XAML do TricBar.

### Não rode como administrador

Execute o TricBar normalmente.

Executá-lo como administrador pode impedir que o Explorer receba os sinais esperados, fazendo com que a alteração da barra seja revertida.

---

# Solução de problemas

### A barra não ficou transparente

Verifique se:

* O TricBar está executando.
* A DLL necessária está ao lado do executável.
* O TranslucentTB não está executando.
* O Windows 11 Taskbar Styler do Windhawk está desativado.
* O TricBar não está sendo executado como administrador.

No Windows 11 22H2+, verifique se o tooltip da bandeja mostra:

```text
ativo
```

Caso contrário, reinicie o Windows Explorer.

---

### A barra volta ao normal depois de reiniciar o Explorer

Isso pode acontecer porque o `explorer.exe` foi reiniciado.

Verifique se o TricBar continua aberto. O helper deve detectar novamente a nova instância do Explorer/barra.

Se necessário, reinicie o TricBar.

---

### Algo deu errado com a barra

Abra o Gerenciador de Tarefas:

```text
Ctrl + Shift + Esc
```

Procure:

```text
Windows Explorer
```

Clique com o botão direito e selecione:

```text
Reiniciar
```

---

# Licença

Consulte a licença do repositório para conhecer os termos atuais de utilização e distribuição.
