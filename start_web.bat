@echo off
title WasteVision Studio - Web Service
cd /d "%~dp0"
echo ========================================================
echo    WasteVision Web Service - خدمة الويب وبايثون
echo ========================================================
echo.

set "PYTHONPATH=%~dp0;.runtime-fastapi;.runtime-packages;%PYTHONPATH%"
set "PYTHON_EXE=C:\Users\ibral\anaconda3\python.exe"
if not exist "%PYTHON_EXE%" (
    set "PYTHON_EXE=python"
)

:: Check if port 8765 is already running
netstat -ano | findstr /R /C:":8765 .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo [OK] خدمة بايثون تعمل بالفعل على http://127.0.0.1:8765
    echo جار فتح الواجهة في المتصفح...
    start http://127.0.0.1:8765/
    exit /b 0
)

echo Using Python: %PYTHON_EXE%
echo Starting Uvicorn server on http://127.0.0.1:8765 ...
start "" http://127.0.0.1:8765/
"%PYTHON_EXE%" -m uvicorn app:app --host 127.0.0.1 --port 8765
if %errorlevel% neq 0 (
    echo.
    echo Server stopped with error.
    pause
)
