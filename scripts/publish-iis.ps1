# Builds a self-contained, direct-to-IIS manual publish package: Angular
# production build + .NET Release publish combined into one folder whose
# *contents* (not the folder itself) are what gets extracted into the IIS
# site's physical directory. Deterministic and non-interactive so the same
# logic can later be reused by a CI/CD pipeline.
#
# Usage:
#   .\scripts\publish-iis.ps1
#
# Output:
#   publish/RmsSupportHub-IIS/      staged application files
#   publish/RmsSupportHub-IIS.zip   deployable archive (no outer folder)
#
# This script never touches IIS, never contacts a server, and never writes
# secrets into the package -- connection strings stay server-owned
# (appsettings.Production.json on the server, or CONNECTIONSTRINGS__* /
# other environment variables). See docs/MANUAL_IIS_DEPLOYMENT.md.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path $repoRoot "backend\src\RmsSupportHub.Api\RmsSupportHub.Api.csproj"
$frontendDir = Join-Path $repoRoot "frontend"
$publishRoot = Join-Path $repoRoot "publish"
$stagingDir = Join-Path $publishRoot "RmsSupportHub-IIS"
$zipFile = Join-Path $publishRoot "RmsSupportHub-IIS.zip"

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Fail {
    param([string]$Message)
    Write-Host "FAILED: $Message" -ForegroundColor Red
    exit 1
}

try {
    # 1. Clean previous output ------------------------------------------------
    Write-Step "Cleaning previous publish output"
    if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
    if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
    $frontendDist = Join-Path $frontendDir "dist"
    if (Test-Path $frontendDist) { Remove-Item $frontendDist -Recurse -Force }
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

    # 2. Install frontend dependencies reproducibly ---------------------------
    Write-Step "Installing frontend dependencies (npm ci)"
    Push-Location $frontendDir
    $lockFile = Join-Path $frontendDir "package-lock.json"
    if (Test-Path $lockFile) {
        npm ci
    } else {
        Write-Host "No package-lock.json found; falling back to npm install" -ForegroundColor Yellow
        npm install
    }
    if ($LASTEXITCODE -ne 0) { Pop-Location; Fail "npm dependency install failed with exit code $LASTEXITCODE" }

    # 3. Build Angular production bundle --------------------------------------
    Write-Step "Building Angular production bundle"
    npm run build -- --configuration production
    if ($LASTEXITCODE -ne 0) { Pop-Location; Fail "Angular production build failed with exit code $LASTEXITCODE" }
    Pop-Location

    # 4. Locate the Angular browser output directory --------------------------
    # Discovered from the actual build result rather than assumed, since the
    # Angular application builder nests output under dist/<project>/browser.
    Write-Step "Locating Angular browser output"
    $angularIndexFiles = Get-ChildItem -Path $frontendDist -Recurse -Filter "index.html" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -like "*\browser" }
    if (-not $angularIndexFiles -or $angularIndexFiles.Count -eq 0) {
        $angularIndexFiles = Get-ChildItem -Path $frontendDist -Recurse -Filter "index.html" -File -ErrorAction SilentlyContinue
    }
    if (-not $angularIndexFiles -or $angularIndexFiles.Count -eq 0) {
        Fail "Could not locate index.html under $frontendDist -- Angular build did not produce the expected output"
    }
    $angularBrowserDir = ($angularIndexFiles | Select-Object -First 1).DirectoryName
    Write-Host "Angular browser output: $angularBrowserDir"

    # 5. Publish the .NET backend (Release, framework-dependent) --------------
    Write-Step "Publishing .NET backend (Release)"
    dotnet publish $backendProject -c Release -o $stagingDir
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed with exit code $LASTEXITCODE" }

    $apiDll = Join-Path $stagingDir "RmsSupportHub.Api.dll"
    if (-not (Test-Path $apiDll)) { Fail "Expected backend assembly not found: $apiDll" }

    # 6. Copy Angular production output into wwwroot --------------------------
    Write-Step "Copying Angular production build into wwwroot"
    $wwwrootDir = Join-Path $stagingDir "wwwroot"
    New-Item -ItemType Directory -Path $wwwrootDir -Force | Out-Null
    Copy-Item -Path (Join-Path $angularBrowserDir "*") -Destination $wwwrootDir -Recurse -Force

    $indexHtml = Join-Path $wwwrootDir "index.html"
    if (-not (Test-Path $indexHtml)) { Fail "wwwroot/index.html missing after copy -- Angular output was not staged correctly" }

    $wwwrootAssets = Get-ChildItem -Path $wwwrootDir -Recurse -File |
        Where-Object { $_.Extension -in ".js", ".css" }
    if (-not $wwwrootAssets -or $wwwrootAssets.Count -eq 0) {
        Fail "No production .js/.css assets found under wwwroot -- Angular output looks incomplete"
    }

    # 7. Verify web.config (auto-generated by the ASP.NET Core Web SDK) -------
    Write-Step "Verifying IIS artifacts"
    $webConfig = Join-Path $stagingDir "web.config"
    if (-not (Test-Path $webConfig)) {
        Fail "web.config was not generated by 'dotnet publish' -- check the RmsSupportHub.Api.csproj SDK/target settings"
    }

    # 8. Secret / dev-artifact guard -------------------------------------------
    Write-Step "Scanning staged output for files that must never ship"
    $forbiddenNames = @(".env", "*.pfx", "appsettings.Local.json", "appsettings.*.local.json")
    $forbiddenHits = foreach ($pattern in $forbiddenNames) {
        Get-ChildItem -Path $stagingDir -Recurse -Filter $pattern -File -ErrorAction SilentlyContinue
    }
    if ($forbiddenHits) {
        $names = ($forbiddenHits | ForEach-Object { $_.FullName }) -join ", "
        Fail "Staged output contains files that must not ship: $names"
    }
    if (Test-Path (Join-Path $stagingDir "var")) {
        Fail "Staged output contains a var/ directory (runtime draft data) -- it must be excluded from publish"
    }

    # 9. Package the ZIP (contents, not the parent folder) --------------------
    Write-Step "Creating deployment ZIP"
    Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipFile -Force

    # 10. Summary ---------------------------------------------------------------
    Write-Host ""
    Write-Host "=================================================" -ForegroundColor Green
    Write-Host " RMS SUPPORT HUB - IIS PUBLISH COMPLETE" -ForegroundColor Green
    Write-Host "=================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Publish folder:"
    Write-Host "  $stagingDir"
    Write-Host ""
    Write-Host "Deployment ZIP:"
    Write-Host "  $zipFile"
    Write-Host ""
    Write-Host "Verified:"
    Write-Host "  [OK] API publish       ($apiDll)"
    Write-Host "  [OK] web.config        ($webConfig)"
    Write-Host "  [OK] Angular index.html ($indexHtml)"
    Write-Host "  [OK] Angular assets    ($($wwwrootAssets.Count) js/css files)"
    Write-Host "  [OK] ZIP package       ($zipFile)"
    Write-Host ""
    Write-Host "Deployment:"
    Write-Host "  1. Stop IIS Application Pool: online order tool"
    Write-Host "  2. Backup existing application directory"
    Write-Host "  3. Extract RmsSupportHub-IIS.zip into the IIS physical directory"
    Write-Host "  4. Preserve/restore server-owned production configuration if applicable"
    Write-Host "  5. Start Application Pool"
    Write-Host "  6. Verify application"
    Write-Host ""
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
