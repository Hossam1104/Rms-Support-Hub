param(
  [Parameter(Mandatory)]
  [string]$PackageRoot,
  [Parameter(Mandatory)]
  [string]$ExpectedSourceCommit,
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Identity([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

if ($ExpectedSourceCommit -notmatch '^[0-9a-f]{40}$') {
  throw "Expected workflow source must be a lowercase full Git SHA, got '$ExpectedSourceCommit'."
}
if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) {
  throw "Release candidate package root does not exist: $PackageRoot"
}

$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$RepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$manifestPath = Join-Path $PackageRoot 'release-manifest.json'
$identityPath = Join-Path $PackageRoot 'wwwroot\build-identity.json'
foreach ($path in @($manifestPath, $identityPath)) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Release candidate identity input is missing: $path"
  }
}

$head = (& git -C $RepositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not read the checked-out Git HEAD.' }
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$identity = Get-Content -Raw -LiteralPath $identityPath | ConvertFrom-Json

Assert-Identity ($head -eq $ExpectedSourceCommit) "Checked-out Git HEAD '$head' does not equal expected workflow source '$ExpectedSourceCommit'."
Assert-Identity ($manifest.sourceCommit -eq $ExpectedSourceCommit) "release-manifest.json sourceCommit '$($manifest.sourceCommit)' does not equal expected workflow source '$ExpectedSourceCommit'."
Assert-Identity ($identity.commit -eq $ExpectedSourceCommit) "wwwroot/build-identity.json commit '$($identity.commit)' does not equal expected workflow source '$ExpectedSourceCommit'."
Assert-Identity ($manifest.sourceCommit -eq $head) 'Release manifest sourceCommit does not equal checked-out Git HEAD.'
Assert-Identity ($identity.commit -eq $head) 'Build identity commit does not equal checked-out Git HEAD.'

Write-Output "Durable RC identity passed: workflow=$ExpectedSourceCommit; git=$head; manifest=$($manifest.sourceCommit); build-identity=$($identity.commit)."
