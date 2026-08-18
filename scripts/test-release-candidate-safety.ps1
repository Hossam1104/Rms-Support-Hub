param(
  [Parameter(Mandatory)]
  [string]$PackageRoot,
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'ReleaseCandidateConfiguration.psm1') -Force

if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) {
  throw "Release candidate package root does not exist: $PackageRoot"
}
$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$verifier = Join-Path $RepositoryRoot 'scripts\verify-release-candidate.ps1'
$configurationPath = Join-Path $PackageRoot 'appsettings.json'
$templatePath = Join-Path $PackageRoot 'deployment\appsettings.Testing.template.json'

Assert-SanitizedTestingConfiguration -Path $configurationPath | Out-Null
Assert-SanitizedTestingConfiguration -Path $templatePath | Out-Null
if ((Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash -ne
    (Get-FileHash -LiteralPath $templatePath -Algorithm SHA256).Hash) {
  throw 'Release candidate appsettings.json is not the exact sanitized Testing template copy.'
}

function Get-RelativePackagePath([string]$Root, [string]$Path) {
  $prefix = ([System.IO.Path]::GetFullPath($Root)).TrimEnd('\') + '\'
  return ([System.IO.Path]::GetFullPath($Path)).Substring($prefix.Length).Replace('\', '/')
}

function Write-IntegrityManifest([string]$Root) {
  $integrityPath = Join-Path $Root 'file-integrity.sha256'
  $paths = @(
    Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
      ForEach-Object { Get-RelativePackagePath $Root $_.FullName } |
      Where-Object { $_ -ne 'file-integrity.sha256' } |
      Sort-Object
  )
  $lines = foreach ($relativePath in $paths) {
    $path = Join-Path $Root ($relativePath -replace '/', '\')
    "$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant())  $relativePath"
  }
  [System.IO.File]::WriteAllText(
    $integrityPath,
    (($lines -join "`n") + "`n"),
    [System.Text.UTF8Encoding]::new($false)
  )
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('rms-release-safety-' + [Guid]::NewGuid().ToString('N'))
$badPackageRoot = Join-Path $temporaryRoot 'bad-package'
$badZipPath = Join-Path $temporaryRoot 'bad-package.zip'
$badExtractionRoot = Join-Path $temporaryRoot 'bad-verification'
New-Item -ItemType Directory -Path $badPackageRoot -Force | Out-Null
try {
  Copy-Item -Path (Join-Path $PackageRoot '*') -Destination $badPackageRoot -Recurse -Force

  $badConfigurationPath = Join-Path $badPackageRoot 'appsettings.json'
  $badConfiguration = Get-Content -Raw -LiteralPath $badConfigurationPath | ConvertFrom-Json
  $badConfiguration.ModuleEndpoints.GhcProduction = 'https://production.example.invalid/api'
  $badConfiguration.SupportHub.Environments.ghc_ecommerce.'GHC Production'.Enabled = $true
  $badConfiguration.SupportHub.Environments.ghc_ecommerce.'GHC Production'.DatabaseOverride = 'ProductionDb'
  [System.IO.File]::WriteAllText(
    $badConfigurationPath,
    (($badConfiguration | ConvertTo-Json -Depth 20) + "`n"),
    [System.Text.UTF8Encoding]::new($false)
  )
  Write-IntegrityManifest $badPackageRoot

  Compress-Archive -Path (Join-Path $badPackageRoot '*') -DestinationPath $badZipPath -CompressionLevel Optimal -Force
  $zipHash = (Get-FileHash -LiteralPath $badZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
  [System.IO.File]::WriteAllText(
    "$badZipPath.sha256",
    "$zipHash  $([System.IO.Path]::GetFileName($badZipPath))`n",
    [System.Text.UTF8Encoding]::new($false)
  )

  $rejected = $false
  try {
    & $verifier -ZipPath $badZipPath -ExtractionRoot $badExtractionRoot | Out-Null
  } catch {
    $rejected = $true
  }
  if (-not $rejected) {
    throw 'Release candidate verifier accepted a package with Production topology/defaults.'
  }
  Write-Output 'Package safety passed: sanitized defaults are present and the real package verifier rejects prohibited Production topology.'
} finally {
  if (Test-Path -LiteralPath $temporaryRoot) {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
  }
}
