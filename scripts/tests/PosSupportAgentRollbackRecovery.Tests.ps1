$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

$modulePath = Join-Path $PSScriptRoot '..\PosSupportAgentDeployment.psm1'
Import-Module $modulePath -Force

function New-RmsRollbackTestCertificate {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=RMS Rollback Test Signer', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    return $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddDays(30))
}

function New-RmsRollbackTestContract {
    param([Parameter(Mandatory)][string]$Root)

    [pscustomobject]@{
        AvailableRoot = Join-Path $Root 'available'
        RollbackRoot = Join-Path $Root 'rollback'
        RecoveryRoot = Join-Path $Root 'recovery'
        StagingRoot = Join-Path $Root 'staging'
        InstallRoot = Join-Path $Root 'install'
        InstalledManifestPath = Join-Path $Root 'install\manifest.json'
        StatePath = Join-Path $Root 'lifecycle-state.json'
        TrustConfigurationPath = Join-Path $Root 'trust\package-trust.json'
        CertificateConfigurationPath = Join-Path $Root 'trust\agent-certificate.json'
    }
}

function New-RmsRollbackTestManifest {
    param(
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][byte[]]$FileBytes,
        [Parameter(Mandatory)][Security.Cryptography.X509Certificates.X509Certificate2]$SigningCertificate,
        [string]$PreviousVersion = ''
    )

    $archiveBytes = [IO.File]::ReadAllBytes($ArchivePath)
    $manifest = [pscustomobject]@{
        SchemaVersion = 1
        ProductId = 'RmsSupportAgent'
        PackageId = 'rms-support-agent'
        Version = $Version
        SupportedOperatingSystem = 'Windows'
        SupportedRuntime = 'net10.0-windows'
        ServiceName = 'RmsSupportAgent'
        ServiceDisplayName = 'RMS Support Agent'
        ServiceDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
        ServiceIdentity = 'LocalSystem'
        ScmName = 'RmsSupportAgent'
        SignatureAlgorithm = 'SHA256withRSA'
        SignerDisplayName = 'DBS rollback test signer'
        Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
        ReleaseChannel = 'Testing'
        Environment = 'Testing'
        PackageSha256 = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash
        PackageSizeBytes = $archiveBytes.LongLength
        PreviousVersion = $PreviousVersion
        RollbackAvailable = $true
        Files = @([pscustomobject]@{
            LogicalName = 'agent'
            RelativePath = 'RmsSupportAgent.exe'
            SizeBytes = $FileBytes.Length
            Sha256 = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($FileBytes)).Replace('-', '')
            Required = $true
        })
        AclRequirements = @('AgentServiceReadWrite')
        CertificateRequirements = @('AgentHttpsCertificate')
        Signature = $null
    }

    $payload = Get-RmsCanonicalPackagePayload -Manifest $manifest
    $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($SigningCertificate)
    try {
        $signature = $rsa.SignData($payload, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $manifest.Signature = [Convert]::ToBase64String($signature)
    } finally { $rsa.Dispose() }
    return $manifest
}

function New-RmsRollbackTestPackage {
    param(
        [Parameter(Mandatory)][string]$AvailableRoot,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][Security.Cryptography.X509Certificates.X509Certificate2]$SigningCertificate,
        [string]$PreviousVersion = '',
        [byte[]]$FileBytes = [byte[]]@(1, 2, 3, 4)
    )

    if (-not (Test-Path -LiteralPath $AvailableRoot -PathType Container)) { New-Item -ItemType Directory -Path $AvailableRoot -Force | Out-Null }
    $archivePath = Join-Path $AvailableRoot "rms-support-agent-$Version.zip"
    if (Test-Path -LiteralPath $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    $archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entry = $archive.CreateEntry('RmsSupportAgent.exe')
        $stream = $entry.Open()
        try { $stream.Write($FileBytes, 0, $FileBytes.Length) } finally { $stream.Dispose() }
    } finally { $archive.Dispose() }

    return New-RmsRollbackTestManifest -Version $Version -ArchivePath $archivePath -FileBytes $FileBytes -SigningCertificate $SigningCertificate -PreviousVersion $PreviousVersion
}

function Write-RmsRollbackTestCheckpoint {
    param(
        [Parameter(Mandatory)][psobject]$Contract,
        [Parameter(Mandatory)][string]$Phase,
        [Parameter(Mandatory)][string]$PreviousVersion,
        [bool]$RecoveryRequired = $false
    )

    ([pscustomobject]@{ Phase = $Phase; PreviousVersion = $PreviousVersion; UpdatedAtUtc = [DateTimeOffset]::UtcNow; RecoveryRequired = $RecoveryRequired }) |
        ConvertTo-Json -Depth 8 -Compress | Set-Content -LiteralPath $Contract.StatePath -Encoding UTF8
}

function New-RmsProtectedOwnedAclDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Security.AccessControl.FileSystemAccessRule[]]$AdditionalRules = @()
    )

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($rule in @($acl.Access)) { [void]$acl.RemoveAccessRule($rule) }
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($currentUser, [Security.AccessControl.FileSystemRights]::FullControl, $inheritance, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)))
    foreach ($rule in $AdditionalRules) { $acl.AddAccessRule($rule) }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

Describe 'RMS Support Agent trust-control file ACL boundary (Section 8 parity)' {
    It 'fails closed for an unprotected inherited ACL' {
        $dir = Join-Path ([IO.Path]::GetTempPath()) ('rms-trust-acl-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        try {
            $file = Join-Path $dir 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Test-RmsSupportAgentControlFileTrust -Path $file | Should Be $false
        } finally {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails closed for a broad Everyone grant on an otherwise-protected owned ACL' {
        $dir = Join-Path ([IO.Path]::GetTempPath()) ('rms-trust-acl-' + [Guid]::NewGuid().ToString('N'))
        try {
            $everyone = New-Object Security.Principal.SecurityIdentifier('S-1-1-0')
            $inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
            $everyoneRule = New-Object Security.AccessControl.FileSystemAccessRule($everyone, [Security.AccessControl.FileSystemRights]::FullControl, $inheritance, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)
            New-RmsProtectedOwnedAclDirectory -Path $dir -AdditionalRules @($everyoneRule)

            $file = Join-Path $dir 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Test-RmsSupportAgentControlFileTrust -Path $file | Should Be $false
        } finally {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'succeeds for a protected ACL owned by the current identity with no broad grant' {
        $dir = Join-Path ([IO.Path]::GetTempPath()) ('rms-trust-acl-' + [Guid]::NewGuid().ToString('N'))
        try {
            New-RmsProtectedOwnedAclDirectory -Path $dir | Out-Null
            $file = Join-Path $dir 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Test-RmsSupportAgentControlFileTrust -Path $file | Should Be $true
        } finally {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails closed for a control file that does not exist' {
        $missing = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N') + '.json')
        Test-RmsSupportAgentControlFileTrust -Path $missing | Should Be $false
    }
}

Describe 'Save-RmsSupportAgentRetainedSlot (Blocker B parity: retain only signed artifacts, never a directory copy)' {
    BeforeEach {
        $script:testRoot = Join-Path ([IO.Path]::GetTempPath()) ('rms-retain-' + [Guid]::NewGuid().ToString('N'))
        $script:contract = New-RmsRollbackTestContract -Root $script:testRoot
        $script:certificate = New-RmsRollbackTestCertificate
    }

    AfterEach {
        if (Test-Path -LiteralPath $script:testRoot) { Remove-Item -LiteralPath $script:testRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'retains only the manifest and the archive, never a copy of the live installation tree' {
        $manifest = New-RmsRollbackTestPackage -AvailableRoot $script:contract.AvailableRoot -Version '1.0.0' -SigningCertificate $script:certificate

        $result = Save-RmsSupportAgentRetainedSlot -SlotRoot $script:contract.RollbackRoot -Contract $script:contract -Manifest $manifest

        $result | Should Be $true
        $items = @(Get-ChildItem -LiteralPath $script:contract.RollbackRoot -Force)
        $items.Count | Should Be 2
        @($items | Where-Object { $_.PSIsContainer }).Count | Should Be 0
        (Test-Path -LiteralPath (Join-Path $script:contract.RollbackRoot 'manifest.json')) | Should Be $true
        (Test-Path -LiteralPath (Join-Path $script:contract.RollbackRoot 'rms-support-agent-1.0.0.zip')) | Should Be $true
    }

    It 'fails without fabricating a manifest when the available archive is missing' {
        $manifest = New-RmsRollbackTestPackage -AvailableRoot $script:contract.AvailableRoot -Version '1.0.0' -SigningCertificate $script:certificate
        Remove-Item -LiteralPath (Join-Path $script:contract.AvailableRoot 'rms-support-agent-1.0.0.zip') -Force

        $result = Save-RmsSupportAgentRetainedSlot -SlotRoot $script:contract.RollbackRoot -Contract $script:contract -Manifest $manifest

        $result | Should Be $false
        (Test-Path -LiteralPath (Join-Path $script:contract.RollbackRoot 'manifest.json')) | Should Be $false
    }
}

Describe 'Get-RmsSupportAgentLifecycleState fails closed on a malformed checkpoint (Section 13 parity)' {
    It 'throws rather than silently treating unparsable checkpoint JSON as absent' {
        $path = Join-Path ([IO.Path]::GetTempPath()) ('rms-checkpoint-' + [Guid]::NewGuid().ToString('N') + '.json')
        Set-Content -LiteralPath $path -Value 'not-json' -Encoding UTF8
        try {
            { Get-RmsSupportAgentLifecycleState -Contract ([pscustomobject]@{ StatePath = $path }) } | Should Throw
        } finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }

    It 'throws rather than reading an oversized checkpoint file' {
        $path = Join-Path ([IO.Path]::GetTempPath()) ('rms-checkpoint-' + [Guid]::NewGuid().ToString('N') + '.json')
        Set-Content -LiteralPath $path -Value ('{"Phase":"' + ('a' * 70000) + '"}') -Encoding UTF8
        try {
            { Get-RmsSupportAgentLifecycleState -Contract ([pscustomobject]@{ StatePath = $path }) } | Should Throw
        } finally {
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }

    It 'returns null rather than throwing when no checkpoint exists at all' {
        $path = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N') + '.json')
        Get-RmsSupportAgentLifecycleState -Contract ([pscustomobject]@{ StatePath = $path }) | Should Be $null
    }
}

Describe 'Restore-RmsSupportAgentRetainedSlot (Blockers A/B/C and Sections 6-7 parity)' {
    BeforeEach {
        $global:RmsRollbackTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('rms-restore-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsRollbackTestContract = New-RmsRollbackTestContract -Root $global:RmsRollbackTestRoot
        $global:RmsRollbackTestCertificate = New-RmsRollbackTestCertificate
        $global:RmsRollbackTestResult = $null
    }

    AfterEach {
        if (Test-Path -LiteralPath $global:RmsRollbackTestRoot) { Remove-Item -LiteralPath $global:RmsRollbackTestRoot -Recurse -Force -ErrorAction SilentlyContinue }
        Remove-Variable -Name RmsRollbackTestRoot, RmsRollbackTestContract, RmsRollbackTestCertificate, RmsRollbackTestResult -Scope Global -ErrorAction SilentlyContinue
    }

    It 'never claims RollbackSucceeded when no checkpoint exists (Section 7 parity: nothing existed before)' {
        InModuleScope PosSupportAgentDeployment {
            $global:RmsRollbackTestResult = Restore-RmsSupportAgentRetainedSlot -Contract $global:RmsRollbackTestContract -FailedOperation Install -Channel Testing
        }

        $global:RmsRollbackTestResult.Succeeded | Should Be $false
        $global:RmsRollbackTestResult.Code | Should Be 'rollback_target_unconfirmed'
    }

    It 'refuses to restore a retained package whose version does not match the checkpoint PreviousVersion (Blocker A parity)' {
        $manifest = New-RmsRollbackTestPackage -AvailableRoot $global:RmsRollbackTestContract.AvailableRoot -Version '1.0.0' -SigningCertificate $global:RmsRollbackTestCertificate
        Save-RmsSupportAgentRetainedSlot -SlotRoot $global:RmsRollbackTestContract.RollbackRoot -Contract $global:RmsRollbackTestContract -Manifest $manifest | Should Be $true
        # The retained slot itself is a legitimately signed 1.0.0 package. The checkpoint --
        # the only trusted anchor for "what should be restored" -- disagrees. This must never
        # be resolved by trusting whatever bytes happen to sit in the rollback directory.
        Write-RmsRollbackTestCheckpoint -Contract $global:RmsRollbackTestContract -Phase 'HealthChecking' -PreviousVersion '9.9.9'

        InModuleScope PosSupportAgentDeployment {
            Mock Get-RmsSupportAgentSignerCertificate { $global:RmsRollbackTestCertificate }
            Mock Ensure-RmsSupportAgentService {}

            $global:RmsRollbackTestResult = Restore-RmsSupportAgentRetainedSlot -Contract $global:RmsRollbackTestContract -FailedOperation Upgrade -Channel Testing

            Assert-MockCalled Ensure-RmsSupportAgentService -Times 0 -Scope It
        }

        $global:RmsRollbackTestResult.Succeeded | Should Be $false
        $global:RmsRollbackTestResult.Code | Should Be 'rollback_trust_failed'
        (Test-Path -LiteralPath $global:RmsRollbackTestContract.InstallRoot) | Should Be $false
        Get-RmsSupportAgentLifecycleState -Contract $global:RmsRollbackTestContract | Should Not Be $null
    }

    It 'rejects a tampered retained archive and activates nothing (Blocker B parity: always re-verify, never trust the persisted payload)' {
        $manifest = New-RmsRollbackTestPackage -AvailableRoot $global:RmsRollbackTestContract.AvailableRoot -Version '1.0.0' -SigningCertificate $global:RmsRollbackTestCertificate
        Save-RmsSupportAgentRetainedSlot -SlotRoot $global:RmsRollbackTestContract.RollbackRoot -Contract $global:RmsRollbackTestContract -Manifest $manifest | Should Be $true
        $retainedArchive = Join-Path $global:RmsRollbackTestContract.RollbackRoot 'rms-support-agent-1.0.0.zip'
        [IO.File]::WriteAllBytes($retainedArchive, [byte[]]@(9, 9, 9, 9, 9, 9, 9, 9))
        Write-RmsRollbackTestCheckpoint -Contract $global:RmsRollbackTestContract -Phase 'HealthChecking' -PreviousVersion '1.0.0'

        InModuleScope PosSupportAgentDeployment {
            Mock Get-RmsSupportAgentSignerCertificate { $global:RmsRollbackTestCertificate }
            Mock Ensure-RmsSupportAgentService {}

            $global:RmsRollbackTestResult = Restore-RmsSupportAgentRetainedSlot -Contract $global:RmsRollbackTestContract -FailedOperation Upgrade -Channel Testing

            Assert-MockCalled Ensure-RmsSupportAgentService -Times 0 -Scope It
        }

        $global:RmsRollbackTestResult.Succeeded | Should Be $false
        $global:RmsRollbackTestResult.Code | Should Be 'rollback_trust_failed'
        (Test-Path -LiteralPath $global:RmsRollbackTestContract.InstallRoot) | Should Be $false
    }

    It 'identifies the retained PREVIOUS package via the checkpoint, re-extracts fresh, and restores it once health confirms (Blocker A/B positive parity)' {
        $manifest = New-RmsRollbackTestPackage -AvailableRoot $global:RmsRollbackTestContract.AvailableRoot -Version '1.0.0' -SigningCertificate $global:RmsRollbackTestCertificate
        Save-RmsSupportAgentRetainedSlot -SlotRoot $global:RmsRollbackTestContract.RollbackRoot -Contract $global:RmsRollbackTestContract -Manifest $manifest | Should Be $true
        Write-RmsRollbackTestCheckpoint -Contract $global:RmsRollbackTestContract -Phase 'HealthChecking' -PreviousVersion '1.0.0'

        InModuleScope PosSupportAgentDeployment {
            Mock Get-RmsSupportAgentSignerCertificate { $global:RmsRollbackTestCertificate }
            Mock Get-RmsSupportAgentServiceEvidence { $null }
            Mock Stop-RmsSupportAgentService {}
            Mock Ensure-RmsSupportAgentService {}
            Mock Start-RmsSupportAgentService {}
            Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' } }
            Mock Test-RmsSupportAgentHealth { $true }

            $global:RmsRollbackTestResult = Restore-RmsSupportAgentRetainedSlot -Contract $global:RmsRollbackTestContract -FailedOperation Upgrade -Channel Testing

            Assert-MockCalled Ensure-RmsSupportAgentService -Times 1 -Scope It
        }

        $global:RmsRollbackTestResult.Succeeded | Should Be $true
        $global:RmsRollbackTestResult.Manifest.Version | Should Be '1.0.0'
        (Test-Path -LiteralPath (Join-Path $global:RmsRollbackTestContract.InstallRoot 'RmsSupportAgent.exe')) | Should Be $true
        Get-RmsSupportAgentLifecycleState -Contract $global:RmsRollbackTestContract | Should Be $null
    }

    It 'fails closed with agent_health_failed and preserves the checkpoint when restored health does not confirm (Blocker C parity)' {
        $manifest = New-RmsRollbackTestPackage -AvailableRoot $global:RmsRollbackTestContract.AvailableRoot -Version '1.0.0' -SigningCertificate $global:RmsRollbackTestCertificate
        Save-RmsSupportAgentRetainedSlot -SlotRoot $global:RmsRollbackTestContract.RollbackRoot -Contract $global:RmsRollbackTestContract -Manifest $manifest | Should Be $true
        Write-RmsRollbackTestCheckpoint -Contract $global:RmsRollbackTestContract -Phase 'HealthChecking' -PreviousVersion '1.0.0'

        InModuleScope PosSupportAgentDeployment {
            Mock Get-RmsSupportAgentSignerCertificate { $global:RmsRollbackTestCertificate }
            Mock Get-RmsSupportAgentServiceEvidence { $null }
            Mock Stop-RmsSupportAgentService {}
            Mock Ensure-RmsSupportAgentService {}
            Mock Start-RmsSupportAgentService {}
            Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' } }
            Mock Test-RmsSupportAgentHealth { $false }

            $global:RmsRollbackTestResult = Restore-RmsSupportAgentRetainedSlot -Contract $global:RmsRollbackTestContract -FailedOperation Upgrade -Channel Testing
        }

        $global:RmsRollbackTestResult.Succeeded | Should Be $false
        $global:RmsRollbackTestResult.Code | Should Be 'agent_health_failed'
        # Files were copied and the service was told to start, but that alone is not a
        # successful rollback -- the checkpoint must remain as recovery evidence, not be cleared.
        Get-RmsSupportAgentLifecycleState -Contract $global:RmsRollbackTestContract | Should Not Be $null
    }

    It 'restores the CURRENT pre-rollback package from RecoveryRoot after a failed explicit rollback, then clears RecoveryRoot on success (Section 6 parity)' {
        $current = New-RmsRollbackTestPackage -AvailableRoot $global:RmsRollbackTestContract.AvailableRoot -Version '2.0.0' -SigningCertificate $global:RmsRollbackTestCertificate
        Save-RmsSupportAgentRetainedSlot -SlotRoot $global:RmsRollbackTestContract.RecoveryRoot -Contract $global:RmsRollbackTestContract -Manifest $current | Should Be $true
        Write-RmsRollbackTestCheckpoint -Contract $global:RmsRollbackTestContract -Phase 'RollingBack' -PreviousVersion '2.0.0' -RecoveryRequired $true

        InModuleScope PosSupportAgentDeployment {
            Mock Get-RmsSupportAgentSignerCertificate { $global:RmsRollbackTestCertificate }
            Mock Get-RmsSupportAgentServiceEvidence { $null }
            Mock Stop-RmsSupportAgentService {}
            Mock Ensure-RmsSupportAgentService {}
            Mock Start-RmsSupportAgentService {}
            Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' } }
            Mock Test-RmsSupportAgentHealth { $true }

            $global:RmsRollbackTestResult = Restore-RmsSupportAgentRetainedSlot -Contract $global:RmsRollbackTestContract -FailedOperation Rollback -Channel Testing
        }

        $global:RmsRollbackTestResult.Succeeded | Should Be $true
        $global:RmsRollbackTestResult.Manifest.Version | Should Be '2.0.0'
        @(Get-ChildItem -LiteralPath $global:RmsRollbackTestContract.RecoveryRoot -Force -ErrorAction SilentlyContinue).Count | Should Be 0
    }
}
