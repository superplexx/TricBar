# Gera TricBar-Win11-v<versao>.zip com TricBarBlur.exe + TricBarBlur.dll + TricBarLauncher.exe
# Requisitos: .NET 8 SDK e Build Tools for Visual Studio (C++ desktop).
$ErrorActionPreference = "Stop"
$version = "3.1.5"
$out = Join-Path $PSScriptRoot "dist"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

# 1) DLL nativa (vai dentro do Explorer)
& cmd /c "`"$PSScriptRoot\TricBarBlurDll\build.bat`""
if ($LASTEXITCODE -ne 0) { throw "Falhou ao compilar a TricBarBlur.dll" }

# 2) Os dois executaveis (single-file: o assembly gerenciado fica dentro do .exe)
foreach ($proj in @("TricBarBlur", "TricBarLauncher")) {
    dotnet publish "$PSScriptRoot\$proj\$proj.csproj" -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true -o "$out\TricBar"
    if ($LASTEXITCODE -ne 0) { throw "Falhou ao publicar $proj" }
}

# 3) Copia a DLL nativa depois do publish, para nunca ser sobrescrita
Copy-Item "$PSScriptRoot\TricBarBlurDll\bin\TricBarBlur.dll" "$out\TricBar\TricBarBlur.dll" -Force

$zip = Join-Path $PSScriptRoot "TricBar-Win11-v$version.zip"
Compress-Archive -Path "$out\TricBar\*.exe", "$out\TricBar\TricBarBlur.dll" -DestinationPath $zip -Force
Write-Host "Pronto: $zip"
