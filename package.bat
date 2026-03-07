@echo off
setlocal

echo [1/2] Building Release...
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo Build failed. Fix errors above then re-run.
    pause
    exit /b 1
)

echo.
echo [2/2] Packaging...
PowerShell -ExecutionPolicy Bypass -File "%~dp0package.ps1"

echo.
pause
