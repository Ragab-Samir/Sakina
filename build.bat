@echo off
chcp 65001 >nul
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /optimize+ /debug- /out:Sakina.exe /win32icon:Sakina.ico /win32manifest:Sakina.manifest /r:System.Windows.Forms.dll /r:System.Drawing.dll Sakina.cs
if errorlevel 1 (echo.& echo فشل البناء & pause & exit /b 1)
echo.
echo تم! الملف: Sakina.exe
pause
