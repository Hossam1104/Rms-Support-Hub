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

if ($PlanOnly) {
    [pscustomobject]@{
        Contract = $contract
        Plan = $plan
        Status = 'NotExecuted'
        Detail = 'This invocation produced a deterministic deployment plan. No service, certificate, registry, host, or package state was changed.'
    } | ConvertTo-Json -Depth 8
    exit $contract.SilentExitCodes.Success
}

if ($Mode -eq 'Status') {
    try {
        $status = Invoke-RmsSupportAgentLifecycle -Mode Status -Channel $Channel
        $status | ConvertTo-Json -Depth 12
        exit $contract.SilentExitCodes.Success
    } catch {
        [pscustomobject]@{
            State = 'Failed'
            Code = 'status_unavailable'
            Detail = 'The read-only Agent status boundary could not be evaluated safely.'
        } | ConvertTo-Json -Depth 8
        exit $contract.SilentExitCodes.UnexpectedFailure
    }
}

if (-not $Silent) {
    Write-Error 'Only -Silent or -PlanOnly is supported by the machine-scoped deployment boundary. Use the trusted onboarding bootstrap for interactive UAC.'
    exit $contract.SilentExitCodes.InvalidArguments
}

try {
    $result = Invoke-RmsSupportAgentLifecycle -Mode $Mode -Channel $Channel
    $result | ConvertTo-Json -Depth 12
    switch ([string]$result.State) {
        'Completed' { exit $contract.SilentExitCodes.Success }
        'RollbackSucceeded' { exit $contract.SilentExitCodes.Success }
        'Busy' { exit $contract.SilentExitCodes.OwnershipConflict }
        'RecoveryRequired' { exit $contract.SilentExitCodes.RecoveryRequired }
        default {
            switch ([string]$result.Code) {
                'trust_rejected' { exit $contract.SilentExitCodes.TrustRejected }
                'ownership_conflict' { exit $contract.SilentExitCodes.OwnershipConflict }
                'service_ownership_unproven' { exit $contract.SilentExitCodes.OwnershipConflict }
                default { exit $contract.SilentExitCodes.UnexpectedFailure }
            }
        }
    }
} catch {
    [pscustomobject]@{
        State = 'RecoveryRequired'
        Code = 'unexpected_failure'
        Detail = 'The Agent lifecycle boundary failed closed before a terminal result was established.'
    } | ConvertTo-Json -Depth 8
    exit $contract.SilentExitCodes.UnexpectedFailure
}
