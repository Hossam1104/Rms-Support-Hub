[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('Install', 'Upgrade', 'Repair', 'Uninstall', 'Rollback', 'Status')]
    [string]$Mode = 'Status',
    [ValidateSet('Testing', 'Production')]
    [string]$Channel = 'Production',
    [switch]$Silent,
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosSupportAgentDeployment.psm1') -Force

$contract = Get-RmsSupportAgentDeploymentContract -Channel $Channel
$plan = Get-RmsSupportAgentLifecyclePlan -Mode $Mode -Contract $contract

if ($PlanOnly -or $Mode -eq 'Status') {
    [pscustomobject]@{
        Contract = $contract
        Plan = $plan
        Status = 'NotExecuted'
        Detail = 'This invocation produced a deterministic deployment plan. No service, certificate, registry, host, or package state was changed.'
    } | ConvertTo-Json -Depth 8
    exit $contract.SilentExitCodes.Success
}

if (-not $Silent) {
    Write-Error 'Only -Silent or -PlanOnly is supported by the machine-scoped deployment boundary. Use the trusted onboarding bootstrap for interactive UAC.'
    exit $contract.SilentExitCodes.InvalidArguments
}

Write-Error 'The permanent Agent package source and trusted signing evidence are not provisioned in this repository session. The installer fails closed before SCM or certificate mutation.'
exit $contract.SilentExitCodes.TrustRejected
