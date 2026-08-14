[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$IUnderstandTestingOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosAgentWindowsProvisioning.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PosTestingConfiguration.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PosSupportHubProvisioning.psm1') -Force

$canonicalHost = 'rms-pos-agent.localhost'
$agentServiceName = 'RmsSupportHub.Pos.Agent'
$testServiceName = 'RmsSupportHub.Pos.Int13.TestService'
$programDataRoot = Join-Path $env:ProgramData 'DBS/RmsSupportHub/Int13Testing'
$agentInstallRoot = Join-Path $env:ProgramFiles 'DBS/RmsSupportHub/PosAgent'
$testInstallRoot = Join-Path $env:ProgramData 'DBS/RmsSupportHub/Int13Testing/TestService'
$agentConfigurationRoot = Join-Path $env:ProgramData 'DBS/PosAdminTool'
$agentConfigurationFile = Join-Path $agentConfigurationRoot 'configuration.json'
$statePath = Join-Path $programDataRoot 'provisioning.json'
$hostsPath = Join-Path $env:SystemRoot 'System32/drivers/etc/hosts'
$ownershipMarker = 'RmsSupportHub INT-13P Testing Provisioning v1'
$hostsMarker = '# RmsSupportHub INT-13P'
$certificateFriendlyName = "RmsSupportHub INT-13P Testing Agent ($canonicalHost)"
$supportHubHost = (Get-PosTestingConfiguration).SupportHubHost
$supportHubCertificateFriendlyName = "RmsSupportHub INT-13D Testing Support Hub ($supportHubHost)"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'INT-13P cleanup requires an elevated Administrator PowerShell session.'
    }
}

function Assert-TestingAuthorization {
    if (-not $IUnderstandTestingOnly) {
        throw 'Pass -IUnderstandTestingOnly to acknowledge that this script removes only resources owned by INT-13P on the current representative Testing machine.'
    }

    Write-Warning 'INT-13P TESTING ONLY cleanup: only resources recorded by the local ownership marker will be touched.'
}

function Get-FullPath([string]$path) {
    return [IO.Path]::GetFullPath($path)
}

function Read-State {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        return $null
    }

    try {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    } catch {
        throw "Provisioning state is unreadable or corrupt; refusing to guess at ownership and delete resources: $statePath"
    }

    if ($null -eq $state -or $state.OwnershipMarker -ne $ownershipMarker -or $state.SchemaVersion -ne 1) {
        throw "Provisioning state is not owned by INT-13P: $statePath"
    }
    return $state
}

function Get-ServiceRecord([string]$name) {
    return Get-CimInstance -ClassName Win32_Service -Filter "Name='$name'" -ErrorAction SilentlyContinue
}

function Stop-AndRemoveOwnedService([string]$name, [string]$expectedExecutable, [bool]$owned) {
    if (-not $owned) {
        return
    }

    $service = Get-ServiceRecord $name
    if ($null -eq $service) {
        return
    }

    $actual = [Environment]::ExpandEnvironmentVariables([string]$service.PathName).Trim()
    $expected = '"' + (Get-FullPath $expectedExecutable) + '"'
    if ($actual -ne $expected -or $service.StartName -ne 'LocalSystem') {
        throw "Owned service $name no longer points to its recorded LocalSystem executable. Refusing removal."
    }

    if ($PSCmdlet.ShouldProcess($name, 'Stop and remove the owned Testing Windows Service')) {
        $serviceController = Get-Service -Name $name -ErrorAction Stop
        if ($serviceController.Status -ne 'Stopped') {
            Stop-Service -Name $name -Force
            $serviceController.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }

        & sc.exe delete $name | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "sc.exe delete failed for owned service $name with exit code $LASTEXITCODE."
        }
    }
}

function Remove-ManifestFiles([string]$root, [object[]]$manifest) {
    if (-not (Test-Path -LiteralPath $root)) {
        return
    }

    $fullRoot = (Get-FullPath $root).TrimEnd('\') + '\'
    foreach ($relative in @($manifest | Sort-Object { $_.Length } -Descending)) {
        $candidate = Get-FullPath (Join-Path $root ([string]$relative))
        if (-not $candidate.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Manifest path escaped its owned install root: $relative"
        }
        if ((Test-Path -LiteralPath $candidate -PathType Leaf) -and $PSCmdlet.ShouldProcess($candidate, 'Remove the INT-13P-owned published file')) {
            Remove-Item -LiteralPath $candidate -Force
        }
    }

    foreach ($directory in @(Get-ChildItem -LiteralPath $root -Directory -Recurse -Force | Sort-Object FullName -Descending)) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0 -and $PSCmdlet.ShouldProcess($directory.FullName, 'Remove the empty INT-13P-owned publish directory')) {
            Remove-Item -LiteralPath $directory.FullName -Force
        }
    }

    $rootEmpty = (Test-Path -LiteralPath $root -PathType Container) -and (@(Get-ChildItem -LiteralPath $root -Force).Count -eq 0)
    if ($rootEmpty -and $PSCmdlet.ShouldProcess($root, 'Remove the empty INT-13P-owned install directory')) {
        Remove-Item -LiteralPath $root -Force
    }
}

function Remove-ManagedHostEntry {
    if (-not (Test-Path -LiteralPath $hostsPath -PathType Leaf)) {
        return
    }

    $lines = [IO.File]::ReadAllLines($hostsPath)
    $managed = @($lines | Where-Object {
            $_ -match "^\s*127\.0\.0\.1\s+$([Regex]::Escape($canonicalHost))\s+$([Regex]::Escape($hostsMarker))\s*$"
        })
    if ($managed.Count -eq 0) {
        return
    }

    $remaining = @($lines | Where-Object {
            $_ -notmatch "^\s*127\.0\.0\.1\s+$([Regex]::Escape($canonicalHost))\s+$([Regex]::Escape($hostsMarker))\s*$"
        })
    if ($PSCmdlet.ShouldProcess($hostsPath, "Remove only the INT-13P-owned $canonicalHost loopback entry")) {
        [IO.File]::WriteAllLines($hostsPath, $remaining, [Text.Encoding]::ASCII)
    }
}

function Remove-OwnedCertificate($state) {
    if (-not $state.CertificateCreated -or [string]::IsNullOrWhiteSpace($state.CertificateThumbprint)) {
        return
    }

    $thumbprint = [string]$state.CertificateThumbprint
    $certificate = Get-ChildItem "Cert:\LocalMachine\My\$thumbprint" -ErrorAction SilentlyContinue
    if ($null -ne $certificate) {
        if ($certificate.FriendlyName -ne $certificateFriendlyName -or $certificate.DnsNameList.Unicode -notcontains $canonicalHost) {
            throw 'The recorded certificate no longer matches INT-13P ownership. Refusing removal.'
        }
        if ($PSCmdlet.ShouldProcess("Cert:\LocalMachine\My\$thumbprint", 'Remove the INT-13P-owned Agent certificate and private key')) {
            Remove-Item -LiteralPath "Cert:\LocalMachine\My\$thumbprint" -Force
        }
    }

    $trusted = Get-ChildItem "Cert:\LocalMachine\Root\$thumbprint" -ErrorAction SilentlyContinue
    if ($null -ne $trusted -and $state.CertificateTrusted -and $PSCmdlet.ShouldProcess("Cert:\LocalMachine\Root\$thumbprint", 'Remove the INT-13P-owned trust entry')) {
        Remove-Item -LiteralPath "Cert:\LocalMachine\Root\$thumbprint" -Force
    }
}

function Remove-OwnedConfiguration($state) {
    if ($state.AgentConfigurationAdopted) {
        $hasConfigurationBackup = -not [string]::IsNullOrWhiteSpace([string]$state.AgentConfigurationBackupPath)
        $hasConfigurationMutation = [bool]$state.AgentConfigurationAclChanged -or -not [string]::IsNullOrWhiteSpace([string]$state.AgentConfigurationProvisionedHash)
        if (-not $hasConfigurationBackup -and -not $hasConfigurationMutation) {
            return
        }
        if (-not (Test-Path -LiteralPath $state.AgentConfigurationBackupPath -PathType Leaf)) {
            throw 'The pre-existing Agent configuration backup is missing. Refusing to overwrite the current configuration.'
        }
        if ((Test-Path -LiteralPath $agentConfigurationFile -PathType Leaf) -and $state.AgentConfigurationProvisionedHash) {
            $currentHash = (Get-FileHash -LiteralPath $agentConfigurationFile -Algorithm SHA256).Hash
            if ($currentHash -ne $state.AgentConfigurationProvisionedHash) {
                throw 'The Agent configuration changed after INT-13P provisioning. Refusing to restore over external changes.'
            }
        }
        if ($PSCmdlet.ShouldProcess($agentConfigurationFile, 'Restore the pre-existing Agent configuration byte-for-byte')) {
            Copy-Item -LiteralPath $state.AgentConfigurationBackupPath -Destination $agentConfigurationFile -Force
        }
        if ($state.AgentConfigurationAclChanged -and $state.AgentConfigurationAclBackupPath -and (Test-Path -LiteralPath $state.AgentConfigurationAclBackupPath -PathType Leaf)) {
            if ($PSCmdlet.ShouldProcess($agentConfigurationRoot, 'Restore the pre-existing Agent configuration directory ACL')) {
                $originalAcl = Import-Clixml -LiteralPath $state.AgentConfigurationAclBackupPath
                Set-Acl -LiteralPath $agentConfigurationRoot -AclObject $originalAcl
            }
        }
        foreach ($backupPath in @($state.AgentConfigurationBackupPath, $state.AgentConfigurationAclBackupPath)) {
            if ($backupPath -and (Test-Path -LiteralPath $backupPath -PathType Leaf) -and $PSCmdlet.ShouldProcess($backupPath, 'Remove the INT-13P-owned rollback backup')) {
                Remove-Item -LiteralPath $backupPath -Force
            }
        }
        return
    }

    if ($state.AgentConfigurationFileCreated -and (Test-Path -LiteralPath $agentConfigurationFile -PathType Leaf)) {
        if ($state.AgentConfigurationProvisionedHash) {
            $currentHash = (Get-FileHash -LiteralPath $agentConfigurationFile -Algorithm SHA256).Hash
            if ($currentHash -ne $state.AgentConfigurationProvisionedHash) {
                throw 'The INT-13P-created Agent configuration changed after provisioning. Refusing removal.'
            }
        }
        if ($PSCmdlet.ShouldProcess($agentConfigurationFile, 'Remove the INT-13P-created Agent allow-list configuration')) {
            Remove-Item -LiteralPath $agentConfigurationFile -Force
        }
    }

    $configurationDirectoryEmpty = (Test-Path -LiteralPath $agentConfigurationRoot -PathType Container) -and (@(Get-ChildItem -LiteralPath $agentConfigurationRoot -Force).Count -eq 0)
    if ($state.AgentConfigurationDirectoryCreated -and $configurationDirectoryEmpty -and $PSCmdlet.ShouldProcess($agentConfigurationRoot, 'Remove the empty INT-13P-created Agent configuration directory')) {
        Remove-Item -LiteralPath $agentConfigurationRoot -Force
    }
}

function Stop-OwnedSupportHubRuntime($state) {
    $hasRuntimeState = $null -ne $state.PSObject.Properties['SupportHubCertificateCreated']
    if (-not $hasRuntimeState) {
        return
    }

    $processId = if ($null -ne $state.PSObject.Properties['SupportHubRuntimeProcessId']) {
        [string]$state.SupportHubRuntimeProcessId
    } else {
        ''
    }
    $runtimeApiDll = if ($null -ne $state.PSObject.Properties['SupportHubRuntimeApiDll']) {
        [string]$state.SupportHubRuntimeApiDll
    } else {
        ''
    }

    if ([string]::IsNullOrWhiteSpace($processId)) {
        $listeners = @(Get-NetTCPConnection -LocalPort (Get-PosTestingConfiguration).SupportHubPort -State Listen -ErrorAction SilentlyContinue)
        if ($listeners.Count -gt 0 -and [bool]$state.SupportHubCertificateCreated) {
            throw 'The Testing Support Hub port has a listener without an owned runtime PID. Refusing certificate or host cleanup.'
        }
        return
    }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$processId" -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        $state.SupportHubRuntimeProcessId = $null
        return
    }

    if ([string]::IsNullOrWhiteSpace($runtimeApiDll)) {
        throw 'The owned Support Hub runtime has a PID but no recorded API assembly. Refusing to stop an unverifiable process.'
    }
    $expectedApiDll = (Get-FullPath $runtimeApiDll)
    $unownedProcess = [string]::IsNullOrWhiteSpace([string]$process.CommandLine) `
        -or [string]$process.CommandLine.IndexOf($expectedApiDll, [StringComparison]::OrdinalIgnoreCase) -lt 0
    if ($unownedProcess) {
        throw 'The recorded Support Hub runtime PID no longer points to the owned API assembly. Refusing to stop it.'
    }

    if ($PSCmdlet.ShouldProcess($processId, 'Stop the owned Testing Support Hub runtime')) {
        Stop-Process -Id ([int]$processId) -Force
        Wait-Process -Id ([int]$processId) -Timeout 30 -ErrorAction SilentlyContinue
        $state.SupportHubRuntimeProcessId = $null
    }
}

function Remove-OwnedSupportHubRuntime($state) {
    $missingRuntimeRoot = $null -eq $state.PSObject.Properties['SupportHubRuntimeRootCreated'] `
        -or -not [bool]$state.SupportHubRuntimeRootCreated `
        -or [string]::IsNullOrWhiteSpace([string]$state.SupportHubRuntimeRoot)
    if ($missingRuntimeRoot) {
        return
    }

    $root = Get-FullPath ([string]$state.SupportHubRuntimeRoot)
    $prefix = $root.TrimEnd('\') + '\'
    if (-not $root.StartsWith((Get-FullPath $programDataRoot).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Recorded Support Hub runtime root escaped the INT-13 state directory: $root"
    }

    foreach ($logPath in @($state.SupportHubRuntimeLogFiles)) {
        if ([string]::IsNullOrWhiteSpace([string]$logPath)) {
            continue
        }
        $fullLogPath = Get-FullPath ([string]$logPath)
        if (-not $fullLogPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Recorded Support Hub log path escaped its owned runtime root: $logPath"
        }
        if ((Test-Path -LiteralPath $fullLogPath -PathType Leaf) -and $PSCmdlet.ShouldProcess($fullLogPath, 'Remove the owned Testing Support Hub log')) {
            Remove-Item -LiteralPath $fullLogPath -Force
        }
    }

    Remove-ManifestFiles $root @($state.SupportHubRuntimePublishedFiles)
    $emptyRuntimeRoot = (Test-Path -LiteralPath $root -PathType Container) `
        -and @(Get-ChildItem -LiteralPath $root -Force).Count -eq 0
    if ($emptyRuntimeRoot -and $PSCmdlet.ShouldProcess($root, 'Remove the empty owned Testing Support Hub runtime directory')) {
        Remove-Item -LiteralPath $root -Force
    }
}

function Invoke-Int13PCleanup {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param()

    Assert-Administrator
    Assert-TestingAuthorization
    $state = Read-State
    if ($null -eq $state) {
        Write-Host 'No INT-13P ownership state exists; no machine resources were changed.' -ForegroundColor Yellow
        return
    }

    $agentExecutable = Join-Path $agentInstallRoot 'RmsSupportHub.Pos.Agent.exe'
    $testExecutable = Join-Path $testInstallRoot 'RmsSupportHub.Pos.Int13.TestService.exe'
    Stop-OwnedSupportHubRuntime $state
    Remove-OwnedSupportHubRuntime $state
    Stop-AndRemoveOwnedService $state.AgentServiceName $agentExecutable ([bool]$state.AgentServiceCreated)
    Stop-AndRemoveOwnedService $state.TestServiceName $testExecutable ([bool]$state.TestServiceCreated)
    Remove-ManifestFiles $agentInstallRoot @($state.AgentPublishedFiles)
    Remove-ManifestFiles $testInstallRoot @($state.TestPublishedFiles)
    Remove-PosAgentBrowserProvisioning $state -WhatIf:$WhatIfPreference
    Remove-OwnedConfiguration $state
    Remove-PosSupportHubCertificate `
        -Hostname $supportHubHost `
        -FriendlyName $supportHubCertificateFriendlyName `
        -State $state `
        -WhatIf:$WhatIfPreference
    Remove-OwnedCertificate $state
    if ($state.HostEntryCreated) {
        Remove-ManagedHostEntry
    }
    Remove-PosSupportHubHostEntry `
        -Hostname $supportHubHost `
        -HostsPath $hostsPath `
        -Marker $hostsMarker `
        -State $state `
        -WhatIf:$WhatIfPreference

    if ((Test-Path -LiteralPath $statePath -PathType Leaf) -and $PSCmdlet.ShouldProcess($statePath, 'Remove the INT-13P ownership state')) {
        Remove-Item -LiteralPath $statePath -Force
    }
    $stateDirectoryEmpty = (Test-Path -LiteralPath $programDataRoot -PathType Container) -and (@(Get-ChildItem -LiteralPath $programDataRoot -Force).Count -eq 0)
    if ($state.StateDirectoryCreated -and $stateDirectoryEmpty -and $PSCmdlet.ShouldProcess($programDataRoot, 'Remove the empty INT-13P state directory')) {
        Remove-Item -LiteralPath $programDataRoot -Force
    }

    Write-Host 'INT-13P cleanup completed for resources owned by this provisioning state.' -ForegroundColor Green
}

# Dot-sourcing this file (as Pester tests do, to reach the functions above in
# isolation) must never trigger the real cleanup sequence; only a direct
# script invocation does.
if ($MyInvocation.InvocationName -ne '.') {
    Invoke-Int13PCleanup
}
