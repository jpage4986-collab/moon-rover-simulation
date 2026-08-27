@echo off
chcp 65001 >nul
setlocal

set "ROOT=%~dp0"
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "EXE=%ROOT%ButtonDetector.exe"
set "SRC=%ROOT%RawInputButtonDetector.cs"

if not exist "%CSC%" (
    echo [ERROR] 未找到 C# 编译器: %CSC%
    pause
    exit /b 1
)

echo 正在编译实体按钮检测器...
"%CSC%" /nologo /target:exe /out:"%EXE%" /platform:AnyCPU /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.ComponentModel.DataAnnotations.dll "%SRC%"
if errorlevel 1 (
    echo [ERROR] 编译失败。
    pause
    exit /b 1
)

echo.
echo 启动检测器。按下底座实体按钮，观察是否出现 HID BUTTON INPUT。
echo 只监听输入，不发送平台运动指令。
echo.
"%EXE%"

endlocal
