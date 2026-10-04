@echo off
setlocal
cd /d "%~dp0"

where cl >nul 2>nul
if %errorlevel%==0 goto build

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" goto novs
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSPATH=%%i"
if not defined VSPATH goto novs
call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul

:build
if not exist bin mkdir bin
if not exist obj mkdir obj
cl /nologo /std:c++17 /permissive- /EHsc /O2 /MT /LD /W3 /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX /Foobj\ TricBarBlur.cpp /link /DEF:TricBarBlur.def /OUT:bin\TricBarBlur.dll windowsapp.lib ole32.lib
if errorlevel 1 exit /b 1
echo.
echo OK: TricBarBlurDll\bin\TricBarBlur.dll
exit /b 0

:novs
echo Nao achei o compilador C++ (MSVC).
echo Instale o "Build Tools for Visual Studio" com a carga "Desenvolvimento para desktop com C++"
echo (inclui o Windows SDK) e rode este script de novo.
exit /b 1
