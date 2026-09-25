@echo off
chcp 65001 >nul
setlocal

set "ROOT=%~dp0"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "EXE=%ROOT%ConsoleBaseButtonDetector.exe"
set "SRC=%ROOT%ConsoleBaseButtonDetector.cs"

if not exist "%CSC%" (
    echo [ERROR] C# compiler not found: %CSC%
    pause
    exit /b 1
)

echo Compiling console button detector...
"%CSC%" /nologo /target:winexe /out:"%EXE%" /platform:AnyCPU /reference:System.dll "%SRC%"
if errorlevel 1 (
    echo [ERROR] Compile failed.
    pause
    exit /b 1
)

echo.
echo Starting console detector...
echo Press a base button and watch for BUTTON DOWN.
echo.
"%EXE%"

endlocal
