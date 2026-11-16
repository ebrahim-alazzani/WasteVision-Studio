$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

$env:PYTHONPATH = "$PSScriptRoot\.runtime-fastapi;$PSScriptRoot\.runtime-packages;" + $env:PYTHONPATH

$pythonCandidates = @(
    "C:\Users\ibral\anaconda3\python.exe",
    (Join-Path $PSScriptRoot ".venv\Scripts\python.exe"),
    "python"
)

$pythonExe = $null
foreach ($cand in $pythonCandidates) {
    if (Test-Path $cand) {
        $pythonExe = $cand
        break
    }
    if ($cand -eq "python" -and (Get-Command python -ErrorAction SilentlyContinue)) {
        $pythonExe = "python"
        break
    }
}

if (-not $pythonExe) {
    Write-Error "Python not found. Please install Python or Anaconda."
    exit 1
}

Write-Host "Using Python: $pythonExe" -ForegroundColor Cyan
Write-Host "Starting Uvicorn server on http://127.0.0.1:8765 ..." -ForegroundColor Green
& $pythonExe -m uvicorn app:app --host 127.0.0.1 --port 8765
