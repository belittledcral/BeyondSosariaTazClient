@echo off
setlocal

echo ========================================
echo  TazUO Build Script
echo ========================================
echo.

echo Building TazUO (Release)...
echo.

dotnet build "%~dp0ClassicUO.sln" -c Release

if %ERRORLEVEL% neq 0 (
    echo.
    echo ========================================
    echo  BUILD FAILED
    echo ========================================
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================
echo  BUILD SUCCEEDED
echo  Output: %~dp0bin\Release\net10.0\TazUO.exe
echo ========================================
pause
