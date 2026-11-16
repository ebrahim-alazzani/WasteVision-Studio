@echo off
title WasteVision Studio - Live Camera Sorting
cd /d "%~dp0"

echo ========================================================
echo   WasteVision Studio - تشغيل كاميرا الفرز الذكي المباشر
echo ========================================================

set "PY_CMD=C:\Users\ibral\anaconda3\python.exe"
if not exist "%PY_CMD%" (
    set "PY_CMD=python"
)

set "PYTHONPATH=%~dp0;%~dp0.runtime-fastapi;%~dp0.runtime-packages;%PYTHONPATH%"
"%PY_CMD%" -m core.camera_runner
if %errorlevel% neq 0 (
    echo.
    echo حدث خطأ أثناء تشغيل الكاميرا.
    pause
)
