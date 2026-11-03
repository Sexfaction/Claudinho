@echo off
rem Testa Win32.PorNoTopo contra o estado "preso" (simulado). Nao abre o mascote nem toca nele.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
cd /d "%~dp0.."
if not exist bin mkdir bin
"%CSC%" /nologo /target:exe /codepage:65001 /out:bin\TesteTopo.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  testes\TesteTopo.cs src\Win32.cs
if errorlevel 1 exit /b 1
bin\TesteTopo.exe
