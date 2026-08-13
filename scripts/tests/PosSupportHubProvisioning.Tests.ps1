$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\PosSupportHubProvisioning.psm1') -Force

function New-TestHostsFile {
    $file = New-TemporaryFile
    [IO.File]::WriteAllText($file.FullName, "127.0.0.1`tother.test`r`n", [Text.Encoding]::ASCII)
    return $file.FullName
}

Describe 'INT-13D Support Hub host provisioning' {
    It 'is idempotent and removes only its owned entry' {
        $hostsPath = New-TestHostsFile
        try {
            $state = [pscustomobject]@{ SupportHubHostEntryCreated = $false }
            Ensure-PosSupportHubHostEntry `
                -Hostname 'support-hub.integration.test' `
                -HostsPath $hostsPath `
                -Marker '# RmsSupportHub INT-13P' `
                -State $state `
                -Confirm:$false
            $first = [IO.File]::ReadAllText($hostsPath)

            Ensure-PosSupportHubHostEntry `
                -Hostname 'support-hub.integration.test' `
                -HostsPath $hostsPath `
                -Marker '# RmsSupportHub INT-13P' `
                -State $state `
                -Confirm:$false
            [IO.File]::ReadAllText($hostsPath) | Should Be $first

            Remove-PosSupportHubHostEntry `
                -Hostname 'support-hub.integration.test' `
                -HostsPath $hostsPath `
                -Marker '# RmsSupportHub INT-13P' `
                -State $state `
                -Confirm:$false
            [IO.File]::ReadAllText($hostsPath) | Should Be "127.0.0.1`tother.test`r`n"
        } finally {
            if (Test-Path -LiteralPath $hostsPath) {
                Remove-Item -LiteralPath $hostsPath -Force
            }
        }
    }

    It 'rejects an unowned host entry without rewriting it' {
        $hostsPath = New-TemporaryFile
        try {
            $original = "10.0.0.8`tsupport-hub.integration.test`r`n"
            [IO.File]::WriteAllText($hostsPath.FullName, $original, [Text.Encoding]::ASCII)
            $state = [pscustomobject]@{ SupportHubHostEntryCreated = $false }

            {
                Ensure-PosSupportHubHostEntry `
                    -Hostname 'support-hub.integration.test' `
                    -HostsPath $hostsPath.FullName `
                    -Marker '# RmsSupportHub INT-13P' `
                    -State $state `
                    -Confirm:$false
            } | Should Throw
            [IO.File]::ReadAllText($hostsPath.FullName) | Should Be $original
        } finally {
            if (Test-Path -LiteralPath $hostsPath.FullName) {
                Remove-Item -LiteralPath $hostsPath.FullName -Force
            }
        }
    }

    It 'does not write a host entry under WhatIf' {
        $hostsPath = New-TestHostsFile
        try {
            $original = [IO.File]::ReadAllText($hostsPath)
            $state = [pscustomobject]@{ SupportHubHostEntryCreated = $false }
            Ensure-PosSupportHubHostEntry `
                -Hostname 'support-hub.integration.test' `
                -HostsPath $hostsPath `
                -Marker '# RmsSupportHub INT-13P' `
                -State $state `
                -WhatIf `
                -Confirm:$false
            [IO.File]::ReadAllText($hostsPath) | Should Be $original
            $state.SupportHubHostEntryCreated | Should Be $false
        } finally {
            if (Test-Path -LiteralPath $hostsPath) {
                Remove-Item -LiteralPath $hostsPath -Force
            }
        }
    }
}
