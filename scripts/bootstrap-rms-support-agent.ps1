[CmdletBinding()]
param(
    [ValidateSet('Install', 'Upgrade', 'Repair', 'Uninstall', 'Rollback', 'Status')]
    [string]$Mode = 'Install',
    [ValidateSet('Testing', 'Production')]
    [string]$Channel = 'Production',
    [switch]$Silent,
    [switch]$PlanOnly,
    [switch]$NoSelfElevate,
    [switch]$ElevatedHandoff
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosSupportAgentDeployment.psm1') -Force

$contract = Get-RmsSupportAgentDeploymentContract -Channel $Channel
$installer = Join-Path $PSScriptRoot 'install-rms-support-agent.ps1'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Test-RmsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-RmsPowerShellHost {
    foreach ($name in @('pwsh.exe', 'powershell.exe')) {
        $candidate = Join-Path $PSHOME $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }

    throw 'No supported PowerShell host was found for the single elevation handoff.'
}

function Invoke-RmsSingleElevation {
    if ($ElevatedHandoff) {
        Write-Error 'The elevated onboarding handoff returned without Administrator rights.'
        exit $contract.SilentExitCodes.ElevationRequired
    }

    if ($PSCommandPath.IndexOf('"', [StringComparison]::Ordinal) -ge 0) {
        Write-Error 'The onboarding script path contains an unsupported quote character.'
        exit $contract.SilentExitCodes.InvalidArguments
    }

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f [IO.Path]::GetFullPath($PSCommandPath)),
        '-Mode', $Mode,
        '-Channel', $Channel,
        '-ElevatedHandoff'
    )
    if ($Silent) { $arguments += '-Silent' }
    if ($PlanOnly) { $arguments += '-PlanOnly' }

    Write-Host 'RMS Support Agent onboarding requires one Administrator approval. Requesting UAC elevation.' -ForegroundColor Yellow
    try {
        $process = Start-Process `
            -FilePath (Get-RmsPowerShellHost) `
            -ArgumentList ($arguments -join ' ') `
            -WorkingDirectory $repositoryRoot `
            -Verb RunAs `
            -Wait `
            -PassThru
    } catch {
        Write-Error 'Elevation was declined or could not be started. No Agent, service, certificate, or registry state was changed.'
        exit $contract.SilentExitCodes.ElevationRequired
    }

    exit $process.ExitCode
}

if ($PlanOnly -or $Mode -eq 'Status') {
    & $installer -Mode $Mode -Channel $Channel -PlanOnly
    exit $LASTEXITCODE
}

if (-not (Test-RmsAdministrator)) {
    if ($NoSelfElevate) {
        Write-Error 'Administrator rights are required. Re-run without -NoSelfElevate to request one UAC elevation, or use an elevated machine-management session.'
        exit $contract.SilentExitCodes.ElevationRequired
    }

    Invoke-RmsSingleElevation
}

if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    Write-Error 'The trusted Agent installer boundary is unavailable.'
    exit $contract.SilentExitCodes.UnexpectedFailure
}

# The installer itself remains silent and fail-closed after the one UAC handoff.
# No second elevation, recursive launch, browser policy write, or SCM mutation is
# hidden here; trust evidence must be provisioned before lifecycle mutation is enabled.
& $installer -Mode $Mode -Channel $Channel -Silent
exit $LASTEXITCODE
