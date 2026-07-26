# Full verification build: backend tests, backend Release build, frontend
# production build. Exits non-zero on the first failure.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

try {
    Write-Host "==> dotnet test" -ForegroundColor Cyan
    Push-Location (Join-Path $root "backend")
    dotnet test OnlineOrderTool.slnx --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }

    Write-Host "==> dotnet build -c Release" -ForegroundColor Cyan
    dotnet build OnlineOrderTool.slnx -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
    Pop-Location

    Write-Host "==> npm run build (production)" -ForegroundColor Cyan
    Push-Location (Join-Path $root "frontend")
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE" }
    Pop-Location

    Write-Host "All checks passed." -ForegroundColor Green
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
