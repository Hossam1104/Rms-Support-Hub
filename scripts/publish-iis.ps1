param(
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
  [string]$OutputRoot = '',
  [ValidateSet('Testing')]
  [string]$Environment = 'Testing',
  [string]$BuildTimestampUtc = '',
  [string]$ExpectedSourceCommit = '',
  [switch]$SkipDependencyInstall
)

$ErrorActionPreference = 'Stop'

# Backwards-compatible entry point. The release-candidate pipeline is now the
# only supported IIS package builder; it never deploys or contacts IIS.
$arguments = @{
  RepositoryRoot = $RepositoryRoot
  Environment = $Environment
}
if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) { $arguments.OutputRoot = $OutputRoot }
if (-not [string]::IsNullOrWhiteSpace($BuildTimestampUtc)) { $arguments.BuildTimestampUtc = $BuildTimestampUtc }
if (-not [string]::IsNullOrWhiteSpace($ExpectedSourceCommit)) { $arguments.ExpectedSourceCommit = $ExpectedSourceCommit }
if ($SkipDependencyInstall) { $arguments.SkipDependencyInstall = $true }

& (Join-Path $PSScriptRoot 'build-release-candidate.ps1') @arguments
exit $LASTEXITCODE
