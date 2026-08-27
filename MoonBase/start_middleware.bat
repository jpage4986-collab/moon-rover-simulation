@echo off
chcp 65001 >nul
title MoonRover Mbox100 Middleware

echo ===========================================
echo   MPSdkMiddleware - Mbox100 Motion Platform
echo ===========================================
echo.

REM Always run from the middleware folder next to this script.
REM This keeps the package working after it is moved to another drive/folder.
cd /d "%~dp0middleware"

REM Never force-kill a running middleware: that could bypass its shutdown homing.
tasklist /FI "IMAGENAME eq MPSdkMiddleware.exe" 2>nul | find /I "MPSdkMiddleware.exe" >nul
if not errorlevel 1 (
    echo [INFO] MPSdkMiddleware.exe is already running.
    echo        Close it with Ctrl+C before restarting so the platform can home safely.
    pause
    exit /b 0
)

tasklist /FI "IMAGENAME eq MPSdkMiddleware_lowlatency.exe" 2>nul | find /I "MPSdkMiddleware_lowlatency.exe" >nul
if not errorlevel 1 (
    echo [INFO] MPSdkMiddleware_lowlatency.exe is already running.
    echo        Close it with Ctrl+C before restarting so the platform can home safely.
    pause
    exit /b 0
)

set "MIDDLEWARE_EXE=MPSdkMiddleware_lowlatency.exe"
if not exist "%MIDDLEWARE_EXE%" set "MIDDLEWARE_EXE=MPSdkMiddleware.exe"

if not exist "%MIDDLEWARE_EXE%" (
    echo [ERROR] Middleware executable not found!
    pause
    exit /b 1
)

for %%F in (MpDll.dll Algorithm.dll Newtonsoft.Json.dll Config.cfg 48.xml) do (
    if not exist "%%F" (
        echo [ERROR] Required file is missing: %%F
        pause
        exit /b 1
    )
)

if not exist "C:\ProgramData\MP\48.xml" (
    echo [WARN] C:\ProgramData\MP\48.xml is missing.
    echo        Run copy_config.bat as Administrator first.
)

ping -n 1 -w 500 192.168.15.201 >nul
if errorlevel 1 (
    echo [WARN] Mbox100 controller 192.168.15.201 is not reachable.
    echo        The middleware can start, but the physical platform will not move.
)

start "MPSdkMiddleware" "%MIDDLEWARE_EXE%"

echo.
echo [DONE] Middleware started in a separate window
echo.
pause
