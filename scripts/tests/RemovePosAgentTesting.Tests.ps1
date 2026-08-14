$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot '..\remove-pos-agent-testing.ps1'

# Dot-sourcing (rather than invoking) reaches the script's internal functions
# without running Invoke-Int13PCleanup — see the InvocationName guard at the
# bottom of remove-pos-agent-testing.ps1. -Confirm:$false avoids an
# interactive confirmation prompt for the ShouldProcess-gated functions under
# test, matching the existing PosAgentWindowsProvisioning.Tests.ps1 pattern.
. $scriptPath -Confirm:$false

function New-TempDirectory {
    $path = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    return $path
}

Describe 'INT-13P Testing cleanup state-loss recovery (L-3)' {
    Context 'provisioning state discovery' {
        It 'treats an absent state file as nothing to clean up rather than guessing' {
            $script:statePath = Join-Path (New-TempDirectory) 'provisioning.json'
            Read-State | Should Be $null
        }

        It 'reads a present, correctly marked state file' {
            $dir = New-TempDirectory
            $script:statePath = Join-Path $dir 'provisioning.json'
            $payload = [pscustomobject]@{
                OwnershipMarker = $ownershipMarker
                SchemaVersion = 1
                AgentServiceCreated = $false
            } | ConvertTo-Json
            Set-Content -LiteralPath $script:statePath -Value $payload -Encoding utf8

            $state = Read-State
            $state.OwnershipMarker | Should Be $ownershipMarker
            $state.SchemaVersion | Should Be 1
        }

        It 'fails closed on a corrupt state file instead of guessing at ownership' {
            $dir = New-TempDirectory
            $script:statePath = Join-Path $dir 'provisioning.json'
            Set-Content -LiteralPath $script:statePath -Value '{ this is not valid json' -Encoding utf8

            { Read-State } | Should Throw
        }

        It 'fails closed on a state file owned by something other than INT-13P' {
            $dir = New-TempDirectory
            $script:statePath = Join-Path $dir 'provisioning.json'
            $payload = [pscustomobject]@{
                OwnershipMarker = 'some other tool v1'
                SchemaVersion = 1
            } | ConvertTo-Json
            Set-Content -LiteralPath $script:statePath -Value $payload -Encoding utf8

            { Read-State } | Should Throw
        }

        It 'fails closed on a state file with a mismatched schema version' {
            $dir = New-TempDirectory
            $script:statePath = Join-Path $dir 'provisioning.json'
            $payload = [pscustomobject]@{
                OwnershipMarker = $ownershipMarker
                SchemaVersion = 2
            } | ConvertTo-Json
            Set-Content -LiteralPath $script:statePath -Value $payload -Encoding utf8

            { Read-State } | Should Throw
        }
    }

    Context 'manifest file removal (clearly owned vs. ambiguous)' {
        It 'removes only the manifest-listed owned file and leaves unrelated files alone' {
            $root = New-TempDirectory
            try {
                Set-Content -LiteralPath (Join-Path $root 'owned.txt') -Value 'owned' -Encoding utf8
                Set-Content -LiteralPath (Join-Path $root 'unrelated.txt') -Value 'unrelated' -Encoding utf8

                Remove-ManifestFiles $root @('owned.txt')

                Test-Path -LiteralPath (Join-Path $root 'owned.txt') | Should Be $false
                Test-Path -LiteralPath (Join-Path $root 'unrelated.txt') | Should Be $true
            } finally {
                if (Test-Path -LiteralPath $root) {
                    Remove-Item -LiteralPath $root -Recurse -Force
                }
            }
        }

        It 'refuses a manifest entry that would escape the owned install root' {
            $root = New-TempDirectory
            try {
                { Remove-ManifestFiles $root @('..\escaped.txt') } | Should Throw
            } finally {
                if (Test-Path -LiteralPath $root) {
                    Remove-Item -LiteralPath $root -Recurse -Force
                }
            }
        }
    }

    Context 'managed hosts entry removal (unrelated entry preservation)' {
        It 'removes only the INT-13P-marked loopback entry and preserves unrelated entries' {
            $dir = New-TempDirectory
            $script:hostsPath = Join-Path $dir 'hosts'
            $lines = @(
                '127.0.0.1       localhost',
                "127.0.0.1       $canonicalHost       $hostsMarker",
                '10.0.0.5        other-machine.example.com'
            )
            [IO.File]::WriteAllLines($script:hostsPath, $lines, [Text.Encoding]::ASCII)

            Remove-ManagedHostEntry

            $remaining = [IO.File]::ReadAllLines($script:hostsPath)
            ($remaining -contains "127.0.0.1       $canonicalHost       $hostsMarker") | Should Be $false
            ($remaining -contains '127.0.0.1       localhost') | Should Be $true
            ($remaining -contains '10.0.0.5        other-machine.example.com') | Should Be $true
        }

        It 'is a no-op when no INT-13P-marked entry is present' {
            $dir = New-TempDirectory
            $script:hostsPath = Join-Path $dir 'hosts'
            $original = @('127.0.0.1       localhost', '10.0.0.5        other-machine.example.com')
            [IO.File]::WriteAllLines($script:hostsPath, $original, [Text.Encoding]::ASCII)

            Remove-ManagedHostEntry

            [IO.File]::ReadAllLines($script:hostsPath) | Should Be $original
        }
    }

    Context 'ownership-flag no-ops (never touch unowned resources)' {
        It 'does not attempt to touch a service that was not recorded as owned' {
            { Stop-AndRemoveOwnedService 'Some.Unrelated.Service.That.Does.Not.Exist' 'C:\nowhere.exe' $false } | Should Not Throw
        }

        It 'is a no-op for an owned service record that no longer exists on the machine' {
            { Stop-AndRemoveOwnedService 'RmsSupportHub.Pos.Int13.NonExistent.Service' 'C:\nowhere.exe' $true } | Should Not Throw
        }

        It 'does not touch any certificate when the state records none was created' {
            $state = [pscustomobject]@{ CertificateCreated = $false; CertificateThumbprint = $null }
            { Remove-OwnedCertificate $state } | Should Not Throw
        }

        It 'does not touch Agent configuration when the state records no adoption or creation' {
            # Redirect the closure-scoped configuration paths to a location this
            # test controls; the real ProgramData path may carry restrictive ACLs
            # from actual prior Testing provisioning on this machine, and a
            # read-only Test-Path/Get-ChildItem probe against it should not make
            # this no-op path flaky.
            $script:agentConfigurationRoot = New-TempDirectory
            $script:agentConfigurationFile = Join-Path $script:agentConfigurationRoot 'configuration.json'

            $state = [pscustomobject]@{
                AgentConfigurationAdopted = $false
                AgentConfigurationFileCreated = $false
                AgentConfigurationDirectoryCreated = $false
            }
            { Remove-OwnedConfiguration $state } | Should Not Throw
        }
    }
}
