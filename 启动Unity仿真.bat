@echo off
chcp 65001 >nul
setlocal

set "PROJECT=%~dp0Assets (2)"
set "UNITY_EXE="
for %%D in (C D E F G) do (
    if not defined UNITY_EXE if exist "%%D:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" set "UNITY_EXE=%%D:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
    if not defined UNITY_EXE if exist "%%D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" set "UNITY_EXE=%%D:\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe"
    if not defined UNITY_EXE if exist "%%D:\unity\2022.3.62f3c1\Editor\Unity.exe" set "UNITY_EXE=%%D:\unity\2022.3.62f3c1\Editor\Unity.exe"
)

if not exist "%UNITY_EXE%" (
    echo [ERROR] Unity 2022.3.62f3c1 was not found.
    echo Install this exact editor version in Unity Hub, then run this script again.
    pause
    exit /b 1
)

if not exist "%PROJECT%\Assets\Scenes\SampleScene.unity" (
    echo [ERROR] Unity project or SampleScene.unity is missing:
    echo %PROJECT%
    pause
    exit /b 1
)

echo Starting Moon Rover simulation...
echo Unity:   %UNITY_EXE%
echo Project: %PROJECT%
start "Moon Rover Unity" "%UNITY_EXE%" -projectPath "%PROJECT%"

endlocal
