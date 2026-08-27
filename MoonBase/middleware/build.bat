@echo off
REM Build script for MPSdkMiddleware
REM Requires: .NET Framework 4.6.1+ (csc.exe)

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set OUTDIR=%~dp0
set SRCDIR=%~dp0src

echo === Building MPSdkMiddleware ===
echo Source: %SRCDIR%
echo Output: %OUTDIR%

%CSC% /nologo /target:exe /out:"%OUTDIR%MPSdkMiddleware_lowlatency.exe" /platform:AnyCPU /reference:"System.dll" "%SRCDIR%\Program.cs" "%SRCDIR%\Config.cs" "%SRCDIR%\MotionController.cs"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo === Build Successful ===
    echo Output: %OUTDIR%MPSdkMiddleware_lowlatency.exe
    echo.
    echo === Next Steps ===
    echo 1. Copy DLLs from G:\MoonBase\middleware\ to %OUTDIR%
    echo    Required: MpDll.dll, Algorithm.dll, Newtonsoft.Json.dll
    echo 2. Edit Config.cfg if needed
    echo 3. Run: MPSdkMiddleware_lowlatency.exe
) else (
    echo.
    echo === Build FAILED ===
)
pause
