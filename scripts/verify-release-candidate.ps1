param(
  [Parameter(Mandatory)]
  [string]$ZipPath,
  [string]$ExtractionRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'ReleaseCandidateConfiguration.psm1') -Force

$ExpectedDotnetSdkVersion = '10.0.302'
$ExpectedNodeVersion = '24.18.0'
$ExpectedNpmVersion = '12.0.1'

function Get-Sha256([string]$Path) {
  return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativePackagePath([string]$Root, [string]$Path) {
  $prefix = ([System.IO.Path]::GetFullPath($Root)).TrimEnd('\') + '\'
  return ([System.IO.Path]::GetFullPath($Path)).Substring($prefix.Length).Replace('\', '/')
}

function Assert-RequiredFile([string]$Root, [string]$RelativePath) {
  $path = Join-Path $Root ($RelativePath -replace '/', '\')
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Release candidate is missing required file '$RelativePath'."
  }
}

function Assert-SafeZipEntries([string]$Path) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
  try {
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $archive.Entries) {
      $name = $entry.FullName.Replace('\', '/')
      if ([string]::IsNullOrWhiteSpace($name)) { throw 'ZIP contains an empty entry name.' }
      if ($name.StartsWith('/') -or $name -match '^[A-Za-z]:/' -or $name.Split('/') -contains '..') {
        throw "ZIP contains an unsafe path entry '$name'."
      }
      if (-not $seen.Add($name)) {
        throw "ZIP contains duplicate entry '$name'."
      }
    }
  }
  finally {
    $archive.Dispose()
  }
}

function Assert-PackageExclusions([string]$Root) {
  $forbidden = @(Get-ChildItem -LiteralPath $Root -Recurse -Force -ErrorAction SilentlyContinue | Where-Object {
    if ($_.PSIsContainer) {
      $_.Name -in @('var', 'node_modules', '.angular', 'bin', 'obj', '.git', 'publish')
    } else {
      $_.Name -eq '.env' -or
      $_.Name -match '(?i)(^|\.)\.env$' -or
      $_.Name -match '(?i)\.(pfx|p12|pem|key|cer|crt|map|pdb)$' -or
      $_.Name -in @('appsettings.Development.json', 'appsettings.Local.json') -or
      $_.Name -match '(?i)\.local\.json$'
    }
  })
  if ($forbidden.Count -gt 0) {
    throw "Release candidate contains forbidden files or directories: $($forbidden.FullName -join ', ')"
  }
}

if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
  throw "Release candidate ZIP does not exist: $ZipPath"
}
$ZipPath = (Resolve-Path -LiteralPath $ZipPath).Path
$sidecarPath = "$ZipPath.sha256"
if (-not (Test-Path -LiteralPath $sidecarPath -PathType Leaf)) {
  throw "Release candidate ZIP SHA-256 sidecar is missing: $sidecarPath"
}

$sidecarLines = @(Get-Content -LiteralPath $sidecarPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($sidecarLines.Count -ne 1 -or $sidecarLines[0] -notmatch '^(?<hash>[0-9a-f]{64})  (?<name>.+)$') {
  throw 'ZIP SHA-256 sidecar must contain exactly one lowercase hash and filename line.'
}
$expectedZipHash = $Matches.hash
$expectedZipName = $Matches.name
if ($expectedZipName -ne [System.IO.Path]::GetFileName($ZipPath)) {
  throw "ZIP sidecar names '$expectedZipName' but verifies '$([System.IO.Path]::GetFileName($ZipPath))'."
}
$actualZipHash = Get-Sha256 $ZipPath
if ($actualZipHash -ne $expectedZipHash) {
  throw "ZIP SHA-256 mismatch: expected $expectedZipHash, got $actualZipHash."
}

Assert-SafeZipEntries $ZipPath

if ([string]::IsNullOrWhiteSpace($ExtractionRoot)) {
  $ExtractionRoot = Join-Path ([System.IO.Path]::GetDirectoryName($ZipPath)) ('verification-' + [Guid]::NewGuid().ToString('N'))
}
if (Test-Path -LiteralPath $ExtractionRoot) {
  throw "Extraction target must be fresh and absent: $ExtractionRoot"
}
$parent = Split-Path -Parent $ExtractionRoot
New-Item -ItemType Directory -Path $parent -Force | Out-Null
New-Item -ItemType Directory -Path $ExtractionRoot -Force | Out-Null
Expand-Archive -LiteralPath $ZipPath -DestinationPath $ExtractionRoot -Force

$requiredFiles = @(
  'RmsSupportHub.Api.dll',
  'appsettings.json',
  'web.config',
  'wwwroot/index.html',
  'wwwroot/build-identity.json',
  'release-manifest.json',
  'file-integrity.sha256',
  'deployment/configuration-schema.json',
  'deployment/appsettings.Testing.template.json',
  'deployment/DEPLOYMENT.md',
  'deployment/ROLLBACK.md',
  'deployment/SMOKE.md'
)
foreach ($requiredFile in $requiredFiles) {
  Assert-RequiredFile $ExtractionRoot $requiredFile
}
Assert-PackageExclusions $ExtractionRoot

$manifestPath = Join-Path $ExtractionRoot 'release-manifest.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.artifactKind -ne 'TestingStagingIisReleaseCandidate') {
  throw 'Release manifest schema or artifact kind is unsupported.'
}
if ($manifest.environment -ne 'Testing') {
  throw "Release manifest environment must be Testing, got '$($manifest.environment)'."
}
if ($manifest.sourceCommit -notmatch '^[0-9a-f]{40}$') {
  throw 'Release manifest sourceCommit must be a lowercase full Git SHA.'
}
if ($manifest.buildId -notmatch '^[0-9a-f]{64}$') {
  throw 'Release manifest buildId must be a lowercase SHA-256 value.'
}
if ($null -eq $manifest.toolchain -or
    $manifest.toolchain.dotnetSdkVersion -ne $ExpectedDotnetSdkVersion -or
    $manifest.toolchain.nodeVersion -ne $ExpectedNodeVersion -or
    $manifest.toolchain.npmVersion -ne $ExpectedNpmVersion) {
  throw "Release manifest toolchain must record .NET SDK $ExpectedDotnetSdkVersion, Node.js $ExpectedNodeVersion, and npm $ExpectedNpmVersion."
}
if ($manifest.reproducibility -ne 'Byte identity is guaranteed for the same source commit, recorded toolchain, and pipeline logic.') {
  throw 'Release manifest reproducibility scope is missing or inaccurate.'
}
if ($manifest.configurationSchemaId -ne 'rms-support-hub.configuration' -or $manifest.configurationSchemaVersion -ne 1) {
  throw 'Release manifest configuration schema identity is unsupported.'
}
if ($manifest.integrityManifest -ne 'file-integrity.sha256') {
  throw 'Release manifest does not point at file-integrity.sha256.'
}
if (@($manifest.runtimePrerequisites.writablePaths) -notcontains 'var/drafts') {
  throw 'Release manifest does not declare writable runtime path var/drafts.'
}
if ([string]$manifest.runtimePrerequisites.hostingBundle -notmatch '(?i)ASP\.NET Core Hosting Bundle.*10') {
  throw 'Release manifest does not declare the .NET 10 ASP.NET Core Hosting Bundle prerequisite.'
}

$identityPath = Join-Path $ExtractionRoot 'wwwroot\build-identity.json'
$identity = Get-Content -Raw -LiteralPath $identityPath | ConvertFrom-Json
if ($identity.schemaVersion -ne 1 -or $identity.environment -ne 'Testing' -or $identity.sourceState -ne 'clean') {
  throw 'Packaged frontend build identity is not a clean Testing identity.'
}
if ($identity.commit -ne $manifest.sourceCommit -or $identity.buildId -ne $manifest.buildId) {
  throw 'Packaged build identity does not match release-manifest source/build identity.'
}

$packagedConfigurationPath = Join-Path $ExtractionRoot 'appsettings.json'
$templateConfigurationPath = Join-Path $ExtractionRoot 'deployment\appsettings.Testing.template.json'
Assert-SanitizedTestingConfiguration -Path $packagedConfigurationPath | Out-Null
Assert-SanitizedTestingConfiguration -Path $templateConfigurationPath | Out-Null
if ((Get-Sha256 $packagedConfigurationPath) -ne (Get-Sha256 $templateConfigurationPath)) {
  throw 'Packaged appsettings.json must be the exact sanitized Testing template copy.'
}

$integrityPath = Join-Path $ExtractionRoot 'file-integrity.sha256'
$integrity = @{}
foreach ($line in @(Get-Content -LiteralPath $integrityPath)) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  if ($line -notmatch '^(?<hash>[0-9a-f]{64})  (?<path>.+)$') {
    throw "Malformed integrity line: $line"
  }
  $relativePath = $Matches.path
  if ($relativePath.Contains('\') -or $relativePath.StartsWith('/') -or $relativePath.Split('/') -contains '..' -or $relativePath -eq 'file-integrity.sha256') {
    throw "Unsafe or excluded integrity path '$relativePath'."
  }
  if ($integrity.ContainsKey($relativePath)) { throw "Duplicate integrity path '$relativePath'." }
  $integrity[$relativePath] = $Matches.hash
}

$actualFiles = @{}
foreach ($file in @(Get-ChildItem -LiteralPath $ExtractionRoot -Recurse -File -Force)) {
  $relativePath = Get-RelativePackagePath $ExtractionRoot $file.FullName
  if ($relativePath -eq 'file-integrity.sha256') { continue }
  $actualFiles[$relativePath] = Get-Sha256 $file.FullName
}
if ($actualFiles.Count -ne $integrity.Count) {
  throw "Integrity file count mismatch: manifest $($integrity.Count), package $($actualFiles.Count)."
}
foreach ($relativePath in $integrity.Keys) {
  if (-not $actualFiles.ContainsKey($relativePath)) {
    throw "Integrity manifest references missing package file '$relativePath'."
  }
  if ($actualFiles[$relativePath] -ne $integrity[$relativePath]) {
    throw "Integrity hash mismatch for '$relativePath'."
  }
}

$offlineScript = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\verify-offline-runtime.ps1'
& $offlineScript -PackageRoot $ExtractionRoot

Write-Output "Release candidate verified from fresh extraction: $ExtractionRoot"
Write-Output "ZIP SHA-256: $actualZipHash"
Write-Output "Source commit: $($manifest.sourceCommit)"
Write-Output "Build ID: $($manifest.buildId)"
