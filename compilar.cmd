@echo off
rem Compila o mascote com o compilador de C# que ja vem no Windows (.NET Framework 4.x).
rem Nao baixa nada. Feche o mascote antes: o Windows nao deixa sobrescrever um .exe aberto.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
cd /d "%~dp0"
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:bin\Claudinho.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  src\*.cs
