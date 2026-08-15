$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot '..\PosAgentWindowsProvisioning.psm1'
Import-Module $modulePath -Force

function Get-TestRegistryFingerprint {
    param([Parameter(Mandatory)][string]$SubKey)

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Default)
    $queue = New-Object System.Collections.Generic.Queue[string]
    $queue.Enqueue($SubKey)
    $lines = @()
    try {
        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            $key = $base.OpenSubKey($current, $false)
            if ($null -eq $key) {
                continue
            }

            try {
                foreach ($name in $key.GetValueNames()) {
                    $value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                    $display = if ($value -is [Array]) { @($value) -join '|' } else { [string]$value }
                    $lines += "$current|$name|$($key.GetValueKind($name))|$display"
                }
                foreach ($child in $key.GetSubKeyNames()) {
                    $queue.Enqueue("$current\$child")
                }
            } finally {
                $key.Dispose()
            }
        }
    } finally {
        $base.Dispose()
    }

    return ($lines | Sort-Object) -join "`n"
}

Describe 'POS Windows browser and IWA provisioning contract' {
    It 'normalizes only exact HTTPS origins' {
        (Normalize-PosExactOrigin ' HTTPS://Support-Hub.Integration.Test:4443/ ') | Should Be 'https://support-hub.integration.test:4443'
        { Normalize-PosExactOrigin 'http://support-hub.integration.test:4443' } | Should Not Throw
        (Normalize-PosExactOrigin 'http://support-hub.integration.test:4443') | Should Be $null
        (Normalize-PosExactOrigin 'https://support-hub.integration.test:4443/path') | Should Be $null
        (Normalize-PosExactOrigin 'https://*.integration.test:4443') | Should Be $null
    }

    It 'merges an exact authentication hostname without duplicating or dropping unrelated values' {
        $plan = Merge-PosCommaSeparatedPolicy ' other.example.com, RMS-POS-AGENT.LOCALHOST ' 'rms-pos-agent.localhost'

        $plan.Value | Should Be 'other.example.com,RMS-POS-AGENT.LOCALHOST'
        $plan.RequiredEntryPresent | Should Be $true
        $plan.Added | Should Be $false
    }

    It 'rejects wildcard authentication policy values' {
        { Merge-PosCommaSeparatedPolicy '*' 'rms-pos-agent.localhost' } | Should Throw
        { Merge-PosCommaSeparatedPolicy '*.localhost' 'rms-pos-agent.localhost' } | Should Throw
        { Merge-PosCommaSeparatedPolicy 'other.example.com,,another.example.com' 'rms-pos-agent.localhost' } | Should Throw
    }

    It 'preserves an unrelated corporate authentication wildcard' {
        $plan = Merge-PosCommaSeparatedPolicy '*.example.com' 'rms-pos-agent.localhost'

        $plan.Value | Should Be '*.example.com,rms-pos-agent.localhost'
        $plan.Added | Should Be $true
    }

    It 'does not append duplicate authentication or back-connection entries' {
        $authPlan = Merge-PosCommaSeparatedPolicy 'rms-pos-agent.localhost,RMS-POS-AGENT.LOCALHOST' 'rms-pos-agent.localhost'
        $authPlan.Added | Should Be $false
        $authPlan.Value | Should Be 'rms-pos-agent.localhost,RMS-POS-AGENT.LOCALHOST'

        $backPlan = Merge-PosMultiStringHostnames @('rms-pos-agent.localhost', 'RMS-POS-AGENT.LOCALHOST') 'rms-pos-agent.localhost'
        $backPlan.Added | Should Be $false
        $backPlan.Values.Count | Should Be 2
    }

    It 'merges BackConnectionHostNames as exact case-insensitive REG_MULTI_SZ entries' {
        $plan = Merge-PosMultiStringHostnames @('other.example.com', 'RMS-POS-AGENT.LOCALHOST') 'rms-pos-agent.localhost'

        $plan.Values.Count | Should Be 2
        $plan.Values[0] | Should Be 'other.example.com'
        $plan.Values[1] | Should Be 'RMS-POS-AGENT.LOCALHOST'
        $plan.RequiredHostnamePresent | Should Be $true
        $plan.Added | Should Be $false
    }

    It 'rejects malformed BackConnectionHostNames entries instead of rewriting them' {
        { Merge-PosMultiStringHostnames @('other.example.com', '') 'rms-pos-agent.localhost' } | Should Throw
    }

    It 'selects the browser policy generation from the installed major version' {
        (Get-PosBrowserPolicyContract Chrome 146).LoopbackPolicyName | Should Be 'LoopbackNetworkAllowedForUrls'
        (Get-PosBrowserPolicyContract Edge 151).LoopbackPolicyName | Should Be 'LoopbackNetworkAllowedForUrls'
        (Get-PosBrowserPolicyContract Chrome 145).LoopbackPolicyName | Should Be 'LocalNetworkAccessAllowedForUrls'
        (Get-PosBrowserPolicyContract Edge 140).LoopbackPolicyName | Should Be 'LocalNetworkAccessAllowedForUrls'
        { Get-PosBrowserPolicyContract Chrome 138 } | Should Throw
        { Get-PosBrowserPolicyContract Edge 139 } | Should Throw
    }

    It 'allocates the next numeric browser list value while preserving existing entries' {
        $entries = @(
            [pscustomobject]@{ Name = '1'; Kind = 'String'; Value = 'https://other.example.test:4443' },
            [pscustomobject]@{ Name = '4'; Kind = 'String'; Value = 'https://another.example.test:4443' })

        $plan = New-PosListPolicyMergePlan $entries 'https://support-hub.integration.test:4443'

        $plan.Added | Should Be $true
        $plan.AddedValueName | Should Be '5'
        $plan.Entries.Count | Should Be 2
        $plan.RequiredOriginPresent | Should Be $false
    }

    It 'recognizes an exact origin and still validates the complete browser list' {
        $entries = @(
            [pscustomobject]@{ Name = '1'; Kind = 'String'; Value = 'https://support-hub.integration.test:4443' },
            [pscustomobject]@{ Name = '2'; Kind = 'String'; Value = 'https://[*.]other.example.test/*' })

        $plan = New-PosListPolicyMergePlan $entries 'https://support-hub.integration.test:4443'

        $plan.RequiredOriginPresent | Should Be $true
        $plan.Added | Should Be $false
    }

    It 'rejects a wildcard list value that can match the exact Support Hub origin' {
        $entries = @([pscustomobject]@{ Name = '1'; Kind = 'String'; Value = 'https://[*.]support-hub.integration.test/*' })
        { New-PosListPolicyMergePlan $entries 'https://support-hub.integration.test:4443' } | Should Throw
    }

    It 'rejects incompatible registry types before a policy rewrite' {
        $entries = @([pscustomobject]@{ Name = '1'; Kind = 'DWord'; Value = 1 })
        { New-PosListPolicyMergePlan $entries 'https://support-hub.integration.test:4443' } | Should Throw

        $snapshot = [pscustomobject]@{ Exists = $true; Kind = [Microsoft.Win32.RegistryValueKind]::DWord; Value = 1 }
        { Assert-PosRegistryValueKind $snapshot ([Microsoft.Win32.RegistryValueKind]::String) 'AuthServerAllowlist' } | Should Throw
    }

    It 'does not write machine policy state under WhatIf' {
        $chromePath = 'HKLM:\SOFTWARE\Policies\Google\Chrome'
        $edgePath = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
        $backConnectionPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0'
        $before = @(
            (Get-TestRegistryFingerprint $chromePath.Substring(6)),
            (Get-TestRegistryFingerprint $edgePath.Substring(6)),
            (Get-TestRegistryFingerprint $backConnectionPath.Substring(6))) -join "`n"

        $state = [pscustomobject]@{}
        Ensure-PosAgentBrowserProvisioning -SupportHubOrigin 'https://support-hub.integration.test:4443' -CanonicalHost 'rms-pos-agent.localhost' -State $state -WhatIf | Out-Null

        $after = @(
            (Get-TestRegistryFingerprint $chromePath.Substring(6)),
            (Get-TestRegistryFingerprint $edgePath.Substring(6)),
            (Get-TestRegistryFingerprint $backConnectionPath.Substring(6))) -join "`n"

        $after | Should Be $before
    }

    It 'preserves a previously owned BackConnectionHostNames record on an idempotent rerun' {
        InModuleScope PosAgentWindowsProvisioning {
            # The machine value is mocked so the proof does not depend on
            # whether this host has ever been provisioned.
            Mock Get-PosRegistryValueSnapshot {
                [pscustomobject]@{
                    Exists = $true
                    Kind = [Microsoft.Win32.RegistryValueKind]::MultiString
                    Value = @('rms-pos-agent.localhost')
                }
            }

            $state = [pscustomobject]@{
                BackConnection = [pscustomobject]@{
                    Added = $true
                    ProvisionedValues = @('rms-pos-agent.localhost')
                }
            }

            $result = Ensure-PosBackConnectionHostName 'rms-pos-agent.localhost' $state -WhatIf

            $result.Changed | Should Be $false
            $result.Present | Should Be $true
            $state.BackConnection.Added | Should Be $true
            @($state.BackConnection.ProvisionedValues).Count | Should Be 1
        }
    }

    It 'refuses to overwrite BackConnectionHostNames changed outside RMS+ provisioning' {
        InModuleScope PosAgentWindowsProvisioning {
            Mock Get-PosRegistryValueSnapshot {
                [pscustomobject]@{
                    Exists = $true
                    Kind = [Microsoft.Win32.RegistryValueKind]::MultiString
                    Value = @('rms-pos-agent.localhost', 'someone-else.localhost')
                }
            }

            $state = [pscustomobject]@{
                BackConnection = [pscustomobject]@{
                    Added = $true
                    ProvisionedValues = @('rms-pos-agent.localhost')
                }
            }

            { Ensure-PosBackConnectionHostName 'rms-pos-agent.localhost' $state -WhatIf } | Should Throw
        }
    }

    It 'refuses to remove an owned loopback value that disappeared' {
        InModuleScope PosAgentWindowsProvisioning {
            Mock Get-PosRegistryValueSnapshot {
                [pscustomobject]@{ Exists = $false; Kind = $null; Value = $null }
            }

            $record = [pscustomobject]@{
                Browser = 'Chrome'
                AuthAdded = $false
                LoopbackAdded = $true
                LoopbackPolicyRoot = 'SOFTWARE\\Policies\\Google\\Chrome\\LoopbackNetworkAllowedForUrls'
                LoopbackAddedValueName = '1'
                LoopbackAddedValue = 'https://support-hub.integration.test:4443'
                LoopbackPolicyName = 'LoopbackNetworkAllowedForUrls'
                AuthKeyCreated = $false
            }

            { Remove-PosBrowserLoopbackPolicy $record } | Should Throw
        }
    }

    It 'rolls back an earlier policy write when a later registry write fails' {
        InModuleScope PosAgentWindowsProvisioning {
            $script:setCallCount = 0
            $script:authWritten = $false
            Mock Get-PosDisableLoopbackCheckSnapshot {
                [pscustomobject]@{ Present = $false }
            }
            Mock Get-PosInstalledBrowser {
                param([string]$Browser)
                [pscustomobject]@{
                    Browser = $Browser
                    Installed = $true
                    Version = '151.0.0.0'
                    MajorVersion = 151
                    Path = "C:\\$($Browser.ToLowerInvariant()).exe"
                }
            }
            Mock Get-PosRegistryKeySnapshot {
                [pscustomobject]@{ Exists = $false; Values = @(); SubKeyCount = 0 }
            }
            Mock Get-PosRegistryValueSnapshot {
                param([string]$SubKey, [string]$ValueName)
                if ($script:authWritten -and $ValueName -eq 'AuthServerAllowlist') {
                    [pscustomobject]@{
                        Exists = $true
                        Kind = [Microsoft.Win32.RegistryValueKind]::String
                        Value = 'rms-pos-agent.localhost'
                    }
                } else {
                    [pscustomobject]@{ Exists = $false; Kind = $null; Value = $null }
                }
            }
            Mock Get-PosListPolicyEntries { @() }
            Mock Set-PosRegistryValue {
                $script:setCallCount++
                if ($script:setCallCount -eq 2) {
                    throw 'injected registry write failure'
                }
                $script:authWritten = $true
            }
            Mock Remove-PosRegistryValue {}
            Mock Remove-PosRegistryKeyIfEmpty {}

            $state = [pscustomobject]@{}
            $failure = $null
            try {
                Ensure-PosAgentBrowserProvisioning `
                    -SupportHubOrigin 'https://support-hub.integration.test:4443' `
                    -CanonicalHost 'rms-pos-agent.localhost' `
                    -State $state `
                    -WhatIf:$false `
                    -Confirm:$false
            } catch {
                $failure = $_.Exception.Message
            }
            $failure | Should Not BeNullOrEmpty

            Assert-MockCalled Remove-PosRegistryValue -Times 1 -Scope It
            $script:setCallCount | Should Be 2
        }
    }

    Context 'browser block policy pattern matching (L-4)' {
        It 'matches an exact bare hostname pattern' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                Test-PosPolicyPatternMatchesOrigin 'support-hub.integration.test' $origin | Should Be $true
            }
        }

        It 'matches a wildcard hostname pattern' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                Test-PosPolicyPatternMatchesOrigin '[*.]integration.test' $origin | Should Be $true
                Test-PosPolicyPatternMatchesOrigin '*.integration.test' $origin | Should Be $true
            }
        }

        It 'does not match an unrelated hostname' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                Test-PosPolicyPatternMatchesOrigin 'other.example.com' $origin | Should Be $false
            }
        }

        It 'matches an exact HTTPS origin pattern' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                Test-PosPolicyPatternMatchesOrigin 'https://support-hub.integration.test:4443' $origin | Should Be $true
            }
        }

        It 'honors port on scheme-qualified and bare host patterns' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                Test-PosPolicyPatternMatchesOrigin 'https://support-hub.integration.test:4443' $origin | Should Be $true
                Test-PosPolicyPatternMatchesOrigin 'https://support-hub.integration.test:9999' $origin | Should Be $false
                Test-PosPolicyPatternMatchesOrigin 'support-hub.integration.test:4443' $origin | Should Be $true
                Test-PosPolicyPatternMatchesOrigin 'support-hub.integration.test:9999' $origin | Should Be $false
            }
        }

        It 'fails closed on a malformed or unsupported pattern' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                Test-PosPolicyPatternMatchesOrigin 'not a valid pattern with spaces' $origin | Should Be $true
            }
        }
    }

    Context 'URLBlocklist / URLAllowlist precedence (L-4)' {
        # Mocks Get-PosListPolicyEntries (not Get-PosRegistryKeySnapshot) because an
        # earlier It in this Describe ('rolls back an earlier policy write...') mocks
        # Get-PosListPolicyEntries unconditionally at the Describe scope; Pester 3
        # mock resolution falls back to that parent-scope mock for any function this
        # Context does not itself mock, so a Get-PosRegistryKeySnapshot-level mock
        # here would silently never be reached.
        It 'throws when URLBlocklist matches the exact Support Hub origin' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                $contract = [pscustomobject]@{
                    Browser = 'Chrome'
                    PolicyRoot = 'SOFTWARE\Policies\Google\Chrome'
                    BlockPolicyNames = @()
                }

                Mock Get-PosListPolicyEntries {
                    param([string]$SubKey)
                    if ($SubKey -eq 'SOFTWARE\Policies\Google\Chrome\URLBlocklist') {
                        return @([pscustomobject]@{ Name = '1'; Kind = [Microsoft.Win32.RegistryValueKind]::String; Value = 'support-hub.integration.test' })
                    }
                    @()
                }

                { Assert-PosNoBlockingPolicyMatch $contract $origin } | Should Throw
            }
        }

        It 'does not throw when a URLAllowlist entry exempts the URLBlocklist match' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                $contract = [pscustomobject]@{
                    Browser = 'Chrome'
                    PolicyRoot = 'SOFTWARE\Policies\Google\Chrome'
                    BlockPolicyNames = @()
                }

                Mock Get-PosListPolicyEntries {
                    param([string]$SubKey)
                    if ($SubKey -eq 'SOFTWARE\Policies\Google\Chrome\URLBlocklist') {
                        return @([pscustomobject]@{ Name = '1'; Kind = [Microsoft.Win32.RegistryValueKind]::String; Value = 'support-hub.integration.test' })
                    }
                    if ($SubKey -eq 'SOFTWARE\Policies\Google\Chrome\URLAllowlist') {
                        return @([pscustomobject]@{ Name = '1'; Kind = [Microsoft.Win32.RegistryValueKind]::String; Value = 'https://support-hub.integration.test:4443' })
                    }
                    @()
                }

                { Assert-PosNoBlockingPolicyMatch $contract $origin } | Should Not Throw
            }
        }

        It 'does not throw for an existing non-conflicting enterprise policy' {
            InModuleScope PosAgentWindowsProvisioning {
                $origin = 'https://support-hub.integration.test:4443'
                $contract = [pscustomobject]@{
                    Browser = 'Chrome'
                    PolicyRoot = 'SOFTWARE\Policies\Google\Chrome'
                    BlockPolicyNames = @('LoopbackNetworkBlockedForUrls')
                }

                Mock Get-PosListPolicyEntries {
                    param([string]$SubKey)
                    if ($SubKey -eq 'SOFTWARE\Policies\Google\Chrome\LoopbackNetworkBlockedForUrls') {
                        return @([pscustomobject]@{ Name = '1'; Kind = [Microsoft.Win32.RegistryValueKind]::String; Value = 'other.example.com' })
                    }
                    @()
                }

                { Assert-PosNoBlockingPolicyMatch $contract $origin } | Should Not Throw
            }
        }
    }
}
