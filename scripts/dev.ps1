# Runs the .NET API and the Angular dev server together for local development.
# API:      http://localhost:5200
# Frontend: http://localhost:4200 (proxies /api to the API via frontend/proxy.conf.json)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root "backend\src\RmsSupportHub.Api"
$frontend = Join-Path $root "frontend"

Write-Host "Starting API (http://localhost:5200) in a new window..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    "-NoExit", "-Command",
    "Set-Location '$apiProject'; `$env:ASPNETCORE_ENVIRONMENT = 'Development'; dotnet run"
)

Write-Host "Starting Angular dev server (http://localhost:4200) in this window..." -ForegroundColor Cyan
Set-Location $frontend
npx ng serve
