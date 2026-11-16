@echo off
title WasteVision Studio - Live Camera Sorting
cd /d "%~dp0"

echo ========================================================
echo   WasteVision Studio - تشغيل كاميرا الفرز الذكي المباشر
echo ========================================================

set "PY_CMD="
if exist "%USERPROFILE%\anaconda3\python.exe" (
    set "PY_CMD=%USERPROFILE%\anaconda3\python.exe"
) else (
    set "PY_CMD=python"
)

set "PYTHONPATH=%~dp0;.runtime-fastapi;.runtime-packages;%PYTHONPATH%"
"%PY_CMD%" -m core.camera_runner
pause
