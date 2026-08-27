@echo off
REM ============================================
REM  复制系统配置到 C:\ProgramData\MP\
REM  需要管理员权限
REM ============================================

echo [配置] 复制系统配置文件到 C:\ProgramData\MP\...

if not exist "C:\ProgramData\MP\" (
    mkdir "C:\ProgramData\MP\"
    echo [创建] C:\ProgramData\MP\
)

copy /Y "%~dp0middleware\48.xml" "C:\ProgramData\MP\48.xml"
if errorlevel 1 (
    echo [ERROR] Failed to copy 48.xml. Please run this script as Administrator.
    pause
    exit /b 1
)
echo [OK] 48.xml

if exist "%~dp0middleware\484.xml" (
    copy /Y "%~dp0middleware\484.xml" "C:\ProgramData\MP\484.xml"
) else if exist "C:\MP\484.xml" (
    copy /Y "C:\MP\484.xml" "C:\ProgramData\MP\484.xml"
) else (
    echo [WARN] 484.xml was not found in the package or C:\MP\.
    echo        Unity simulation still works, but the physical platform may not initialize.
)
if exist "C:\ProgramData\MP\484.xml" echo [OK] 484.xml

echo.
echo [完成] 系统配置文件已就绪
echo.

dir "C:\ProgramData\MP\"

pause
