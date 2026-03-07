@echo off
setlocal EnableDelayedExpansion
echo Step 1 - start
set "COMFY_TYPE_FILE=tools\mob-ai-comfytype.txt"
set "MODEL_DIR_FILE=tools\mob-ai-modeldir.txt"
set "COMFY_TYPE="
echo Step 2 - vars set

if exist "%COMFY_TYPE_FILE%" (
    set /p COMFY_TYPE=<"%COMFY_TYPE_FILE%"
    for /f "tokens=* delims= " %%a in ("!COMFY_TYPE!") do set "COMFY_TYPE=%%a"
)
echo Step 3 - after first if

if "!COMFY_TYPE!"=="desktop" (
    echo Step 3a desktop branch
)
echo Step 4 - after desktop check

if "!COMFY_TYPE!"=="portable" (
    echo Step 4a portable branch
)
echo Step 5 - after portable check

set "DESKTOP_EXE=%LOCALAPPDATA%\Programs\ComfyUI\ComfyUI.exe"
if exist "%DESKTOP_EXE%" (
    echo Step 5a desktop detected
) else (
    echo Step 5b no desktop exe
)
echo Step 6 - done
pause
