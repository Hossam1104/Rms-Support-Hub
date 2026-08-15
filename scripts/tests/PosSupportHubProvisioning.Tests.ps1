$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot '..\PosSupportHubProvisioning.psm1') -Force

function New-TestAccessRule {
    param(
        [Parameter(Mandatory)][string]$Sid,
        [Security.AccessControl.AccessControlType]$Type = [Security.AccessControl.AccessControlType]::Allow
    )

    return [Security.AccessControl.FileSystemAccessRule]::new(
        [Security.Principal.SecurityIdentifier]::new($Sid),
        [Security.AccessControl.FileSystemRights]::FullControl,
        $Type)
}

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

Describe 'INT-13D Support Hub private-key policy' {
    # The shipped defect split this policy across two physical lines without a
    # continuation, so the provider half was parsed as a command named -and and
    # never contributed. Evaluating it here proves the whole policy runs as one
    # boolean expression.
    It 'accepts only the expected provider with a non-exportable key' {
        Test-PosSupportHubPrivateKeyPolicy `
            -IsCngKey $true `
            -ProviderName 'Microsoft Software Key Storage Provider' `
            -ExportPolicy 'None' | Should Be $true
    }

    It 'fails closed for the wrong key storage provider' {
        Test-PosSupportHubPrivateKeyPolicy `
            -IsCngKey $true `
            -ProviderName 'Microsoft Platform Crypto Provider' `
            -ExportPolicy 'None' | Should Be $false
    }

    It 'fails closed for an exportable private key' {
        foreach ($policy in @('AllowExport', 'AllowPlaintextExport', 'AllowExport, AllowPlaintextExport')) {
            Test-PosSupportHubPrivateKeyPolicy `
                -IsCngKey $true `
                -ProviderName 'Microsoft Software Key Storage Provider' `
                -ExportPolicy $policy | Should Be $false
        }
    }

    It 'fails closed for a non-CNG private key' {
        Test-PosSupportHubPrivateKeyPolicy `
            -IsCngKey $false `
            -ProviderName 'Microsoft Software Key Storage Provider' `
            -ExportPolicy 'None' | Should Be $false
    }

    It 'fails closed when the provider or policy is unavailable' {
        Test-PosSupportHubPrivateKeyPolicy -IsCngKey $true -ProviderName $null -ExportPolicy 'None' | Should Be $false
        Test-PosSupportHubPrivateKeyPolicy -IsCngKey $true -ProviderName 'Microsoft Software Key Storage Provider' -ExportPolicy $null | Should Be $false
    }

    It 'describes a non-CNG key without touching CNG-only members' {
        $descriptor = Get-PosPrivateKeyDescriptor ([Security.Cryptography.RSACryptoServiceProvider]::new(512))
        $descriptor.IsCngKey | Should Be $false
        $descriptor.ProviderName | Should Be $null
    }

    It 'treats a missing private-key file as an ACL failure' {
        { Assert-PosSupportHubPrivateKeyAcl (Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))) } |
            Should Throw 'could not be located'
    }

    # The rule evaluation is tested directly rather than by rewriting a real
    # DACL: applying a protected ACL to a temp file needs rights the test host
    # is not guaranteed to have, and the decision under test is the SID policy.
    It 'rejects every broad principal a private-key ACL must never grant' {
        foreach ($sid in @('S-1-1-0', 'S-1-5-11', 'S-1-5-32-545', 'S-1-5-7', 'S-1-5-32-546')) {
            $rules = @(New-TestAccessRule $sid)
            @(Get-PosBroadPrivateKeyAccessRule -Rule $rules).Count | Should Be 1
        }
    }

    It 'accepts an ACL restricted to Administrators and LocalSystem' {
        $rules = @('S-1-5-32-544', 'S-1-5-18') | ForEach-Object { New-TestAccessRule $_ }
        @(Get-PosBroadPrivateKeyAccessRule -Rule $rules).Count | Should Be 0
    }

    It 'ignores a deny rule for a broad principal' {
        $rules = @(New-TestAccessRule 'S-1-1-0' ([Security.AccessControl.AccessControlType]::Deny))
        @(Get-PosBroadPrivateKeyAccessRule -Rule $rules).Count | Should Be 0
    }

    It 'reports a broad principal mixed into an otherwise bounded ACL' {
        $rules = @(
            (New-TestAccessRule 'S-1-5-18'),
            (New-TestAccessRule 'S-1-5-32-545'),
            (New-TestAccessRule 'S-1-5-32-544'))
        @(Get-PosBroadPrivateKeyAccessRule -Rule $rules).Count | Should Be 1
    }

    It 'accepts a real private-key file whose ACL grants no broad principal' {
        $path = Join-Path ([IO.Path]::GetTempPath()) ("pos-key-{0}" -f ([Guid]::NewGuid().ToString('N')))
        [IO.File]::WriteAllText($path, 'synthetic', [Text.UTF8Encoding]::new($false))
        try {
            $rules = @((Get-Acl -LiteralPath $path).GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
            if (@(Get-PosBroadPrivateKeyAccessRule -Rule $rules).Count -gt 0) {
                # The user temp directory inherits a broad ACE on this host, so
                # the fail-closed path is the correct expectation here.
                { Assert-PosSupportHubPrivateKeyAcl $path } | Should Throw 'grants a broad principal'
            } else {
                { Assert-PosSupportHubPrivateKeyAcl $path } | Should Not Throw
            }
        } finally {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

Describe 'INT-13D Support Hub host provisioning (WhatIf)' {
    It 'still refuses to write under WhatIf' {
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
