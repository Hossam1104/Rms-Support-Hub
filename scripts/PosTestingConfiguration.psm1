Set-StrictMode -Version Latest

# This module is deliberately Testing-only. Production receives its deployed
# Support Hub origin through deployment configuration; this bounded origin is
# used only by the representative-machine evidence workflow.
$script:DefaultSupportHubOrigin = 'https://support-hub.integration.test:4443'
$script:DefaultSupportHubHost = 'support-hub.integration.test'
$script:DefaultSupportHubPort = 4443
$script:AgentOrigin = 'https://rms-pos-agent.localhost:5001'

function Get-PosTestingConfiguration {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [string]$SupportHubOrigin
    )

    $candidate = if ([string]::IsNullOrWhiteSpace($SupportHubOrigin)) {
        $script:DefaultSupportHubOrigin
    } else {
        $SupportHubOrigin.Trim()
    }

    if ($candidate.IndexOf('*', [StringComparison]::Ordinal) -ge 0) {
        throw 'The Testing Support Hub origin must not contain a wildcard.'
    }

    $uri = $null
    $invalid = (-not [Uri]::TryCreate($candidate, [UriKind]::Absolute, [ref]$uri)) `
        -or $uri.Scheme -ne 'https' `
        -or $uri.Host -ne $script:DefaultSupportHubHost `
        -or $uri.Port -ne $script:DefaultSupportHubPort `
        -or (-not [string]::IsNullOrEmpty($uri.UserInfo)) `
        -or (-not [string]::IsNullOrEmpty($uri.Query)) `
        -or (-not [string]::IsNullOrEmpty($uri.Fragment)) `
        -or ($uri.AbsolutePath -ne '' -and $uri.AbsolutePath -ne '/')
    if ($invalid) {
        throw "The Testing Support Hub origin must be the exact HTTPS origin $($script:DefaultSupportHubOrigin)."
    }

    [pscustomobject]@{
        SupportHubOrigin = $uri.GetLeftPart([UriPartial]::Authority).ToLowerInvariant()
        SupportHubHost = $script:DefaultSupportHubHost
        SupportHubPort = $script:DefaultSupportHubPort
        AgentOrigin = $script:AgentOrigin
    }
}

Export-ModuleMember -Function 'Get-PosTestingConfiguration'
