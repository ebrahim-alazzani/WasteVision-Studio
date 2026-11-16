$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet SDK not found. Please install .NET 9 SDK."
    exit 1
}

$env:PYTHONPATH = "$PSScriptRoot\.runtime-fastapi;$PSScriptRoot\.runtime-packages;" + $env:PYTHONPATH
if (Test-Path "C:\Users\ibral\anaconda3\python.exe") {
    $env:WASTE_PYTHON = "C:\Users\ibral\anaconda3\python.exe"
}

$project = Join-Path $PSScriptRoot "desktop\WasteVision.Desktop.csproj"
& dotnet run --project $project --no-restore
