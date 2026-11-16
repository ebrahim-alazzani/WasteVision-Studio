@echo off
cd /d "%~dp0"
echo ========================================================
echo    WasteVision Web Service - خدمة الويب وبايثون
echo ========================================================
echo.

set "PYTHONPATH=%~dp0.runtime-fastapi;%~dp0.runtime-packages;%PYTHONPATH%"

set "PYTHON_EXE=C:\Users\ibral\anaconda3\python.exe"
if not exist "%PYTHON_EXE%" (
    set "PYTHON_EXE=python"
)

echo Using Python: %PYTHON_EXE%
echo Starting Uvicorn server on http://127.0.0.1:8765 ...
"%PYTHON_EXE%" -m uvicorn app:app --host 127.0.0.1 --port 8765
if %errorlevel% neq 0 (
    echo.
    echo Server stopped with error.
    pause
)
