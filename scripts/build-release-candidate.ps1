param(
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
  [string]$OutputRoot = '',
  [ValidateSet('Testing')]
  [string]$Environment = 'Testing',
  [string]$BuildTimestampUtc = '',
  [switch]$SkipDependencyInstall
)

$ErrorActionPreference = 'Stop'

function Write-Utf8NoBom([string]$Path, [string]$Content) {
  $utf8 = [System.Text.UTF8Encoding]::new($false)
  [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Get-Sha256([string]$Path) {
  return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativePath([string]$Root, [string]$Path) {
  $prefix = ([System.IO.Path]::GetFullPath($Root)).TrimEnd('\') + '\'
  return ([System.IO.Path]::GetFullPath($Path)).Substring($prefix.Length).Replace('\', '/')
}

function Write-Step([string]$Message) {
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory) {
  & $FilePath @ArgumentList
  if ($LASTEXITCODE -ne 0) {
    throw "$FilePath failed with exit code $LASTEXITCODE."
  }
}

function Get-DeterministicBuildTimestamp([string]$ExplicitTimestamp, [string]$Root, [string]$Commit) {
  if (-not [string]::IsNullOrWhiteSpace($ExplicitTimestamp)) {
    $parsedExplicit = [DateTimeOffset]::Parse($ExplicitTimestamp, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    return $parsedExplicit.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture)
  }

  $commitTimestamp = (& git -C $Root show -s --format=%cI $Commit).Trim()
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commitTimestamp)) {
    throw 'Could not derive a deterministic build timestamp from the source commit.'
  }
  return ([DateTimeOffset]::Parse($commitTimestamp, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture)
}

function Write-IntegrityManifest([string]$Root, [string]$Path) {
  $relativePaths = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force | ForEach-Object {
    Get-RelativePath $Root $_.FullName
  } | Where-Object { $_ -ne 'file-integrity.sha256' })
  if ($relativePaths.Count -eq 0) { throw 'Cannot write an empty package integrity manifest.' }

  $orderedPaths = [string[]]$relativePaths
  [Array]::Sort($orderedPaths, [StringComparer]::Ordinal)
  $builder = [System.Text.StringBuilder]::new()
  foreach ($relativePath in $orderedPaths) {
    $hash = Get-Sha256 (Join-Path $Root ($relativePath -replace '/', '\'))
    [void]$builder.Append("$hash  $relativePath`n")
  }
  Write-Utf8NoBom $Path $builder.ToString()
}

function Write-DeterministicZip([string]$Root, [string]$ZipPath, [string]$TimestampUtc) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $timestamp = [DateTimeOffset]::Parse($TimestampUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AssumeUniversal)
  $relativePaths = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force | ForEach-Object {
    Get-RelativePath $Root $_.FullName
  })
  $orderedPaths = [string[]]$relativePaths
  [Array]::Sort($orderedPaths, [StringComparer]::Ordinal)

  $stream = [System.IO.File]::Open($ZipPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
  $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
  try {
    foreach ($relativePath in $orderedPaths) {
      $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
      $entry.LastWriteTime = $timestamp
      $input = [System.IO.File]::OpenRead((Join-Path $Root ($relativePath -replace '/', '\')))
      $output = $entry.Open()
      try {
        $input.CopyTo($output)
      }
      finally {
        $output.Dispose()
        $input.Dispose()
      }
    }
  }
  finally {
    $archive.Dispose()
  }
}

try {
  $RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
  if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $RepositoryRoot 'publish'
  } elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $RepositoryRoot $OutputRoot
  }
  $OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

  $backendProject = Join-Path $RepositoryRoot 'backend\src\RmsSupportHub.Api\RmsSupportHub.Api.csproj'
  $frontendRoot = Join-Path $RepositoryRoot 'frontend'
  $stagingRoot = Join-Path $OutputRoot 'RmsSupportHub-IIS'
  $zipPath = Join-Path $OutputRoot 'RmsSupportHub-IIS.zip'
  $verificationRoot = Join-Path $OutputRoot 'verification'
  foreach ($requiredPath in @($backendProject, (Join-Path $frontendRoot 'package-lock.json'), (Join-Path $frontendRoot 'scripts\build-identity.mjs'))) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) { throw "Required build input is missing: $requiredPath" }
  }

  $sourceCommit = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
  if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'The release candidate source commit is not a lowercase full Git SHA.'
  }
  $sourceStatus = (& git -C $RepositoryRoot status --porcelain).Trim()
  if (-not [string]::IsNullOrWhiteSpace($sourceStatus)) {
    throw 'Release candidate generation requires a clean source tree; commit or remove tracked changes first.'
  }
  $buildTimestamp = Get-DeterministicBuildTimestamp $BuildTimestampUtc $RepositoryRoot $sourceCommit

  Write-Step 'Cleaning previous release candidate output'
  if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
  if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
  if (Test-Path -LiteralPath "$zipPath.sha256") { Remove-Item -LiteralPath "$zipPath.sha256" -Force }
  if (Test-Path -LiteralPath $verificationRoot) { Remove-Item -LiteralPath $verificationRoot -Recurse -Force }
  New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

  $frontendDist = Join-Path $frontendRoot 'dist'
  if (Test-Path -LiteralPath $frontendDist) { Remove-Item -LiteralPath $frontendDist -Recurse -Force }

  Push-Location $frontendRoot
  try {
    if (-not $SkipDependencyInstall) {
      Write-Step 'Installing frontend dependencies with npm ci'
      Invoke-Checked 'npm' @('ci', '--ignore-scripts') $frontendRoot
    }
    Write-Step 'Building Angular production assets'
    Invoke-Checked 'npm' @('run', 'build', '--', '--configuration', 'production') $frontendRoot
  }
  finally {
    Pop-Location
  }

  $indexFiles = @(Get-ChildItem -LiteralPath $frontendDist -Recurse -File -Filter 'index.html' | Where-Object { $_.DirectoryName -like '*\browser' })
  if ($indexFiles.Count -eq 0) {
    $indexFiles = @(Get-ChildItem -LiteralPath $frontendDist -Recurse -File -Filter 'index.html')
  }
  if ($indexFiles.Count -eq 0) { throw 'Angular build did not produce an index.html browser output.' }
  $angularBrowserRoot = $indexFiles[0].DirectoryName

  Write-Step 'Finalizing deterministic Testing build identity'
  Invoke-Checked 'node' @(
    (Join-Path $frontendRoot 'scripts\build-identity.mjs'),
    'finalize',
    '--output', $angularBrowserRoot,
    '--environment', $Environment,
    '--commit', $sourceCommit,
    '--source-state', 'clean',
    '--built-at-utc', $buildTimestamp
  ) $frontendRoot

  Write-Step 'Publishing framework-dependent .NET backend'
  Invoke-Checked 'dotnet' @(
    'publish', $backendProject,
    '-c', 'Release',
    '-o', $stagingRoot,
    '--nologo',
    '--self-contained', 'false',
    '-p:ContinuousIntegrationBuild=true',
    '-p:Deterministic=true'
  ) $RepositoryRoot

  Write-Step 'Staging Angular assets under wwwroot'
  $wwwRoot = Join-Path $stagingRoot 'wwwroot'
  New-Item -ItemType Directory -Path $wwwRoot -Force | Out-Null
  Copy-Item -Path (Join-Path $angularBrowserRoot '*') -Destination $wwwRoot -Recurse -Force

  # Development settings and compiler symbols are never runtime package
  # inputs. Runtime storage is created by the host and is intentionally not
  # copied from the developer checkout.
  foreach ($developmentFile in @('appsettings.Development.json')) {
    $path = Join-Path $stagingRoot $developmentFile
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
  }
  Get-ChildItem -LiteralPath $stagingRoot -Recurse -File -Filter '*.pdb' -ErrorAction SilentlyContinue | Remove-Item -Force

  Write-Step 'Adding deployment and configuration documentation'
  $deploymentRoot = Join-Path $stagingRoot 'deployment'
  New-Item -ItemType Directory -Path $deploymentRoot -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs\release\DEPLOYMENT.md') -Destination $deploymentRoot -Force
  Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs\release\ROLLBACK.md') -Destination $deploymentRoot -Force
  Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs\release\SMOKE.md') -Destination $deploymentRoot -Force
  Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs\release\configuration-schema.json') -Destination $deploymentRoot -Force
  Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'docs\release\appsettings.Testing.template.json') -Destination $deploymentRoot -Force

  $releaseManifest = [ordered]@{
    schemaVersion = 1
    artifactKind = 'TestingStagingIisReleaseCandidate'
    artifactName = 'RmsSupportHub-IIS.zip'
    environment = $Environment
    sourceCommit = $sourceCommit
    sourceState = 'clean'
    buildId = ((Get-Content -Raw -LiteralPath (Join-Path $wwwRoot 'build-identity.json') | ConvertFrom-Json).buildId)
    configurationSchemaId = 'rms-support-hub.configuration'
    configurationSchemaVersion = 1
    configurationTemplate = 'deployment/appsettings.Testing.template.json'
    integrityManifest = 'file-integrity.sha256'
    runtimePrerequisites = [ordered]@{
      hostingBundle = 'ASP.NET Core Hosting Bundle 10.x (framework-dependent publish)'
      iis = 'IIS with ASP.NET Core Module v2; application pool No Managed Code / Integrated'
      deploymentTier = 'Testing unless explicitly server-configured otherwise'
      writablePaths = @('var/drafts')
      runtimeStorage = 'Create var/drafts and grant the IIS app-pool identity Modify access.'
    }
    packageExclusions = @('.env', 'appsettings.Development.json', '*.local.json', '*.pfx', '*.p12', '*.pem', '*.key', '*.cer', '*.crt', '*.map', '*.pdb', 'var/**', 'node_modules/**', '.angular/**')
  }
  $manifestPath = Join-Path $stagingRoot 'release-manifest.json'
  Write-Utf8NoBom $manifestPath (($releaseManifest | ConvertTo-Json -Depth 10) + "`n")

  Write-Step 'Scanning staged runtime for public dependency URLs'
  & (Join-Path $RepositoryRoot 'scripts\verify-offline-runtime.ps1') -PackageRoot $stagingRoot
  if ($LASTEXITCODE -ne 0) { throw 'Offline runtime verification failed for staged output.' }

  Write-Step 'Writing package file-integrity manifest'
  Write-IntegrityManifest $stagingRoot (Join-Path $stagingRoot 'file-integrity.sha256')

  Write-Step 'Creating deterministic ZIP archive'
  Write-DeterministicZip $stagingRoot $zipPath $buildTimestamp
  $zipHash = Get-Sha256 $zipPath
  Write-Utf8NoBom "$zipPath.sha256" "$zipHash  $([System.IO.Path]::GetFileName($zipPath))`n"

  Write-Step 'Unpacking and verifying the release candidate in a fresh directory'
  & (Join-Path $RepositoryRoot 'scripts\verify-release-candidate.ps1') -ZipPath $zipPath -ExtractionRoot $verificationRoot
  if ($LASTEXITCODE -ne 0) { throw 'Release candidate fresh-extraction verification failed.' }

  Write-Host ''
  Write-Host 'Release candidate generated.' -ForegroundColor Green
  Write-Host "ZIP: $zipPath"
  Write-Host "ZIP SHA-256: $zipHash"
  Write-Host "Source commit: $sourceCommit"
  Write-Host "Build ID: $($releaseManifest.buildId)"
  Write-Host "Fresh extraction: $verificationRoot"
}
catch {
  Write-Host $_.Exception.Message -ForegroundColor Red
  Write-Host $_.InvocationInfo.PositionMessage -ForegroundColor DarkRed
  Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
  exit 1
}
