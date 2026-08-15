<#
.SYNOPSIS
Fails the build when any tracked PowerShell script or module has a parse error,
an operator parsed as a command, or a broken line continuation.

.DESCRIPTION
Runs the PowerShell-native analysis in PosScriptQuality.psm1 across the whole
repository. PSScriptAnalyzer is used as an additional signal when it is already
installed; it is never installed by this script.
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$SkipScriptAnalyzer
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# $PSScriptRoot is not bound while parameter defaults are evaluated for an
# advanced script started with powershell.exe -File, so resolve it here.
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

Import-Module (Join-Path $PSScriptRoot 'PosScriptQuality.psm1') -Force

$paths = @(Get-PosTrackedScriptPath -RepositoryRoot $RepositoryRoot)
Write-Host "Analysing $($paths.Count) PowerShell files under $RepositoryRoot" -ForegroundColor Cyan

$findings = @(Invoke-PosScriptQualityAnalysis -RepositoryRoot $RepositoryRoot)
$rootPrefix = ([IO.Path]::GetFullPath($RepositoryRoot)).TrimEnd('\') + '\'
foreach ($finding in $findings) {
    $relative = ([IO.Path]::GetFullPath($finding.Path)) -replace ([regex]::Escape($rootPrefix)), ''
    Write-Host "[FAIL] $relative`:$($finding.Line) $($finding.Rule): $($finding.Message)" -ForegroundColor Red
}

if (-not $SkipScriptAnalyzer) {
    $analyzer = Get-Module -ListAvailable PSScriptAnalyzer | Select-Object -First 1
    if ($null -eq $analyzer) {
        Write-Host '[INFO] PSScriptAnalyzer is not installed; the native parse gate is the only signal.' -ForegroundColor Yellow
    } else {
        Import-Module PSScriptAnalyzer -Force
        $analyzerErrors = @(Invoke-ScriptAnalyzer -Path $RepositoryRoot -Recurse -Severity Error -ExcludeRule PSAvoidUsingCmdletAliases |
            Where-Object { $_.ScriptPath -notmatch '\\(node_modules|bin|obj|dist|\.angular|\.git)\\' })
        foreach ($analyzerError in $analyzerErrors) {
            Write-Host "[FAIL] $($analyzerError.ScriptName)`:$($analyzerError.Line) $($analyzerError.RuleName): $($analyzerError.Message)" -ForegroundColor Red
        }
        if ($analyzerErrors.Count -gt 0) {
            throw "PSScriptAnalyzer reported $($analyzerErrors.Count) error-severity findings."
        }
        Write-Host "[PASS] PSScriptAnalyzer $($analyzer.Version) reported no error-severity findings." -ForegroundColor Green
    }
}

if ($findings.Count -gt 0) {
    throw "PowerShell quality analysis reported $($findings.Count) finding(s)."
}

Write-Host "[PASS] All $($paths.Count) PowerShell files parse and contain no dangling operator continuations." -ForegroundColor Green
