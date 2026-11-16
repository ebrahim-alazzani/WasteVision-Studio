@echo off
cd /d "%~dp0"
echo ========================================================
echo    WasteVision Desktop - مختبر فرز النفايات الذكي
echo ========================================================
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [ERROR] .NET SDK is not installed or not in PATH!
    echo Please install .NET 9 SDK.
    pause
    exit /b 1
)

set "PYTHONPATH=%~dp0.runtime-fastapi;%~dp0.runtime-packages;%PYTHONPATH%"
if exist "C:\Users\ibral\anaconda3\python.exe" (
    set "WASTE_PYTHON=C:\Users\ibral\anaconda3\python.exe"
)

echo Starting WasteVision Desktop...
dotnet run --project "%~dp0desktop\WasteVision.Desktop.csproj" --no-restore
if %errorlevel% neq 0 (
    echo.
    echo Application exited with code %errorlevel%.
    pause
)
