$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

$modulePath = Join-Path $PSScriptRoot '..\PosSupportAgentDeployment.psm1'
Import-Module $modulePath -Force

$script:PosTestFixtureRoot = Join-Path ([IO.Path]::GetTempPath()) 'RmsSupportHub.Pos.Tests'

function New-RmsRollbackTestCertificate {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=RMS Rollback Test Signer', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    return $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddDays(30))
}

function New-RmsRollbackTestContract {
    param([Parameter(Mandatory)][string]$Root)

    New-RmsProtectedOwnedAclDirectory -Path $Root | Out-Null
    foreach ($child in @('available', 'rollback', 'recovery', 'staging', 'trust')) {
        New-RmsProtectedOwnedAclDirectory -Path (Join-Path $Root $child) | Out-Null
    }

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
    Protect-RmsTestControlFile -Path $Contract.StatePath
}

function New-RmsProtectedOwnedAclDirectory {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Security.AccessControl.FileSystemAccessRule[]]$AdditionalRules = @()
    )

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $acl = New-Object System.Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $inheritance = ([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit)
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($currentUser, [Security.AccessControl.FileSystemRights]::FullControl, $inheritance, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)))
    foreach ($rule in $AdditionalRules) { $acl.AddAccessRule($rule) }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Protect-RmsTestControlFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Security.AccessControl.FileSystemAccessRule[]]$AdditionalRules = @()
    )

    $acl = New-Object System.Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($currentUser, [Security.AccessControl.FileSystemRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow)))
    foreach ($rule in $AdditionalRules) { $acl.AddAccessRule($rule) }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

if (-not (Test-Path -LiteralPath $script:PosTestFixtureRoot -PathType Container)) {
    New-RmsProtectedOwnedAclDirectory -Path $script:PosTestFixtureRoot | Out-Null
}

Describe 'RMS Support Agent trust-control file ACL boundary (Section 8 parity)' {
    It 'fails closed for an unprotected inherited ACL' {
        $dir = Join-Path $script:PosTestFixtureRoot ('rms-trust-acl-' + [Guid]::NewGuid().ToString('N'))
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
        $dir = Join-Path $script:PosTestFixtureRoot ('rms-trust-acl-' + [Guid]::NewGuid().ToString('N'))
        try {
            $everyone = New-Object Security.Principal.SecurityIdentifier('S-1-1-0')
            $inheritance = ([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit)
            $everyoneRule = New-Object Security.AccessControl.FileSystemAccessRule($everyone, [Security.AccessControl.FileSystemRights]::FullControl, $inheritance, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)
            New-RmsProtectedOwnedAclDirectory -Path $dir -AdditionalRules @($everyoneRule)

            $file = Join-Path $dir 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Protect-RmsTestControlFile -Path $file -AdditionalRules @((New-Object Security.AccessControl.FileSystemAccessRule($everyone, [Security.AccessControl.FileSystemRights]::Read, [Security.AccessControl.AccessControlType]::Allow)))
            Test-RmsSupportAgentControlFileTrust -Path $file | Should Be $false
        } finally {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'succeeds for a protected ACL owned by the current identity with no broad grant' {
        $dir = Join-Path $script:PosTestFixtureRoot ('rms-trust-acl-' + [Guid]::NewGuid().ToString('N'))
        try {
            New-RmsProtectedOwnedAclDirectory -Path $dir | Out-Null
            $file = Join-Path $dir 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Protect-RmsTestControlFile -Path $file
            Test-RmsSupportAgentControlFileTrust -Path $file | Should Be $true
        } finally {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails closed for a control file that does not exist' {
        $missing = Join-Path $script:PosTestFixtureRoot 'missing\package-trust.json'
        Test-RmsSupportAgentControlFileTrust -Path $missing | Should Be $false
    }

    It 'fails closed when the control file is outside the named disposable fixture root' {
        $outside = Join-Path ([IO.Path]::GetTempPath()) ('rms-control-outside-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $outside -Force | Out-Null
        try {
            $file = Join-Path $outside 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Test-RmsSupportAgentControlFileTrust -Path $file | Should Be $false
        } finally {
            Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails closed when a child directory is a reparse point' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-reparse-child-' + [Guid]::NewGuid().ToString('N'))
        $target = Join-Path $script:PosTestFixtureRoot ('rms-reparse-target-' + [Guid]::NewGuid().ToString('N'))
        $link = Join-Path $root 'child'
        try {
            New-RmsProtectedOwnedAclDirectory -Path $root | Out-Null
            New-RmsProtectedOwnedAclDirectory -Path $target | Out-Null
            $file = Join-Path $target 'package-trust.json'
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Protect-RmsTestControlFile -Path $file
            cmd.exe /c "mklink /J `"$link`" `"$target`"" | Out-Null
            Test-RmsSupportAgentControlFileTrust -Path (Join-Path $link 'package-trust.json') | Should Be $false
        } finally {
            try { [System.IO.Directory]::Delete($link) } catch { }
            Remove-Item -LiteralPath $root -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $target -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue
        }
    }

    It 'fails closed when the named service-owned parent is a reparse point' {
        $link = Join-Path $script:PosTestFixtureRoot ('rms-reparse-parent-' + [Guid]::NewGuid().ToString('N'))
        $target = Join-Path ([IO.Path]::GetTempPath()) ('rms-reparse-parent-target-' + [Guid]::NewGuid().ToString('N'))
        $file = Join-Path $target 'package-trust.json'
        try {
            New-RmsProtectedOwnedAclDirectory -Path $target | Out-Null
            Set-Content -LiteralPath $file -Value '{}' -Encoding UTF8
            Protect-RmsTestControlFile -Path $file
            cmd.exe /c "mklink /J `"$link`" `"$target`"" | Out-Null
            Test-RmsSupportAgentControlFileTrust -Path (Join-Path $link 'package-trust.json') | Should Be $false
        } finally {
            try { [System.IO.Directory]::Delete($link) } catch { }
            Remove-Item -LiteralPath $target -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue
        }
    }
}

Describe 'RMS Support Agent machine-owned release mode authority' {
    function New-RmsMachineTrustFixture {
        param(
            [string]$DeploymentMode = 'Testing',
            [string]$ProductionPin = ('1' * 40),
            [string]$TestingPin = ('2' * 40),
            [bool]$ProtectFile = $true,
            [bool]$OutsideNamedRoot = $false
        )

        $root = if ($OutsideNamedRoot) {
            Join-Path ([IO.Path]::GetTempPath()) ('rms-mode-outside-' + [Guid]::NewGuid().ToString('N'))
        } else {
            Join-Path $script:PosTestFixtureRoot ('rms-mode-' + [Guid]::NewGuid().ToString('N'))
        }
        $trustRoot = Join-Path $root 'trust'
        New-RmsProtectedOwnedAclDirectory -Path $root | Out-Null
        New-RmsProtectedOwnedAclDirectory -Path $trustRoot | Out-Null
        $path = Join-Path $trustRoot 'package-trust.json'
        $document = [ordered]@{
            deploymentMode = $DeploymentMode
            productionSignerThumbprint = $ProductionPin
            testingSignerThumbprint = $TestingPin
        }
        $document | ConvertTo-Json -Compress | Set-Content -LiteralPath $path -Encoding UTF8
        if ($ProtectFile) { Protect-RmsTestControlFile -Path $path }
        [pscustomobject]@{
            Root = $root
            Contract = [pscustomobject]@{ TrustConfigurationPath = $path; TestOnlyTrustFixture = $true }
        }
    }

    It 'uses the protected deploymentMode when no caller assertion is supplied' {
        $fixture = New-RmsMachineTrustFixture -DeploymentMode Testing
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Resolve-RmsSupportAgentMachineReleaseMode -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $true
                $result.Channel | Should Be 'Testing'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects a caller channel that does not match the protected deploymentMode' {
        $fixture = New-RmsMachineTrustFixture -DeploymentMode Testing
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Resolve-RmsSupportAgentMachineReleaseMode -Contract $global:RmsMachineTrustFixture.Contract -RequestedChannel Production
                $result.Valid | Should Be $false
                $result.Code | Should Be 'requested_channel_mismatch'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects equal normalized production and testing signer pins' {
        $fixture = New-RmsMachineTrustFixture -ProductionPin ('a' * 40) -TestingPin (('a' * 20) + ('A' * 20))
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_pins_equal'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects equal signer pins when formatting differs only by whitespace' {
        $pin = 'aabbccddeeff00112233445566778899aabbccdd'
        $fixture = New-RmsMachineTrustFixture -ProductionPin (' ' + ($pin -replace '(.{4})', '$1 ') + ' ') -TestingPin $pin
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_pins_equal'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects a missing or invalid machine deployment mode' {
        $fixture = New-RmsMachineTrustFixture -DeploymentMode 'Production'
        $global:RmsMachineTrustFixture = $fixture
        try {
            $json = [ordered]@{ productionSignerThumbprint = ('1' * 40); testingSignerThumbprint = ('2' * 40) } | ConvertTo-Json -Compress
            Set-Content -LiteralPath $fixture.Contract.TrustConfigurationPath -Value $json -Encoding UTF8
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'machine_release_mode_invalid'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects Production mode without a Production signer pin' {
        $fixture = New-RmsMachineTrustFixture -DeploymentMode Production
        $global:RmsMachineTrustFixture = $fixture
        try {
            [ordered]@{
                deploymentMode = 'Production'
                testingSignerThumbprint = ('2' * 40)
            } | ConvertTo-Json -Compress | Set-Content -LiteralPath $fixture.Contract.TrustConfigurationPath -Encoding UTF8
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_thumbprint_missing'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'accepts complete signer snapshots for both Production and Testing modes' {
        foreach ($mode in @('Production', 'Testing')) {
            $fixture = New-RmsMachineTrustFixture -DeploymentMode $mode
            $global:RmsMachineTrustFixture = $fixture
            $global:RmsExpectedMachineTrustMode = $mode
            try {
                InModuleScope PosSupportAgentDeployment {
                    $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                    $result.Valid | Should Be $true
                    $result.DeploymentMode | Should Be $global:RmsExpectedMachineTrustMode
                    $result.ProductionSignerThumbprint | Should Be ('1' * 40)
                    $result.TestingSignerThumbprint | Should Be ('2' * 40)
                }
            } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture, RmsExpectedMachineTrustMode -Scope Global -ErrorAction SilentlyContinue }
        }
    }

    It 'rejects Testing mode without a Testing signer pin' {
        $fixture = New-RmsMachineTrustFixture -DeploymentMode Testing
        $global:RmsMachineTrustFixture = $fixture
        try {
            [ordered]@{
                deploymentMode = 'Testing'
                productionSignerThumbprint = ('1' * 40)
            } | ConvertTo-Json -Compress | Set-Content -LiteralPath $fixture.Contract.TrustConfigurationPath -Encoding UTF8
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_thumbprint_missing'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects whitespace-only signer pins' {
        $fixture = New-RmsMachineTrustFixture -TestingPin '   '
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_thumbprint_invalid'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects non-string signer pins' {
        $fixture = New-RmsMachineTrustFixture
        $global:RmsMachineTrustFixture = $fixture
        try {
            [ordered]@{
                deploymentMode = 'Testing'
                productionSignerThumbprint = 123
                testingSignerThumbprint = ('2' * 40)
            } | ConvertTo-Json -Compress | Set-Content -LiteralPath $fixture.Contract.TrustConfigurationPath -Encoding UTF8
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_thumbprint_invalid'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects an alternate trust path unless the test-only fixture seam is explicit' {
        $fixture = New-RmsMachineTrustFixture
        $global:RmsMachineTrustFixture = $fixture
        try {
            $global:RmsMachineTrustFixture.Contract = [pscustomobject]@{ TrustConfigurationPath = $fixture.Contract.TrustConfigurationPath }
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'machine_trust_configuration_path_invalid'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects malformed signer pins before they can select a certificate' {
        $fixture = New-RmsMachineTrustFixture -TestingPin 'not-a-thumbprint'
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'signer_thumbprint_invalid'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects an untrusted control file even when its JSON is otherwise valid' {
        $fixture = New-RmsMachineTrustFixture -ProtectFile $false
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'machine_trust_configuration_untrusted'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'rejects trust configuration outside the fixed machine or named test roots' {
        $fixture = New-RmsMachineTrustFixture -OutsideNamedRoot $true
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsMachineTrustFixture.Contract
                $result.Valid | Should Be $false
                $result.Code | Should Be 'machine_trust_configuration_untrusted'
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }

    It 'fails a lifecycle mutation before acquiring its lease when mode authority is invalid' {
        $fixture = New-RmsMachineTrustFixture -ProtectFile $false
        $global:RmsMachineTrustFixture = $fixture
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsMachineTrustFixture.Contract }
                Mock Enter-RmsSupportAgentMutationLease { throw 'lease must not be reached' }
                $result = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Production
                $result.State | Should Be 'Failed'
                $result.Code | Should Be 'machine_trust_configuration_untrusted'
                Assert-MockCalled Enter-RmsSupportAgentMutationLease -Times 0 -Scope It
            }
        } finally { Remove-Item -LiteralPath $fixture.Root -Recurse -Force -ErrorAction SilentlyContinue; Remove-Variable RmsMachineTrustFixture -Scope Global -ErrorAction SilentlyContinue }
    }
}

Describe 'RMS Support Agent lifecycle audit operation correlation' {
    It 'uses one operation ID for started, attempted, accepted, rollback, recovery, and failed events' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-audit-correlation-' + [Guid]::NewGuid().ToString('N'))
        foreach ($child in @('available', 'rollback', 'recovery', 'staging', 'install', 'package', 'audit', 'trust')) {
            New-Item -ItemType Directory -Path (Join-Path $root $child) -Force | Out-Null
        }
        $contract = [pscustomobject]@{
            PackageRoot = Join-Path $root 'package'
            AvailableRoot = Join-Path $root 'available'
            RollbackRoot = Join-Path $root 'rollback'
            RecoveryRoot = Join-Path $root 'recovery'
            StagingRoot = Join-Path $root 'staging'
            InstallRoot = Join-Path $root 'install'
            AuditRoot = Join-Path $root 'audit'
            TrustRoot = Join-Path $root 'trust'
            InstalledManifestPath = Join-Path $root 'install\manifest.json'
            StatePath = Join-Path $root 'lifecycle-state.json'
        }
        $manifest = [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '2.0.0'; Files = @() }
        [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '1.0.0' } | ConvertTo-Json -Compress | Set-Content -LiteralPath $contract.InstalledManifestPath -Encoding UTF8
        $global:RmsAuditCorrelationContract = $contract
        $global:RmsAuditCorrelationManifest = $manifest
        $global:RmsAuditCorrelationCalls = @()
        $global:RmsAuditCorrelationStateReads = 0
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsAuditCorrelationContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent {
                    $global:RmsAuditCorrelationCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId }
                }
                Mock Get-RmsSupportAgentLifecycleState {
                    $global:RmsAuditCorrelationStateReads++
                    if ($global:RmsAuditCorrelationStateReads -eq 1) { return $null }
                    return [pscustomobject]@{ Phase = 'Staged'; RecoveryRequired = $false }
                }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = $global:RmsAuditCorrelationManifest; ArchivePath = 'unused.zip' } }
                Mock Test-RmsSupportAgentInstalledIntegrity { [pscustomobject]@{ Valid = $true } }
                Mock Get-RmsSupportAgentServiceEvidence { [pscustomobject]@{ ServiceAccount = 'LocalSystem'; StartMode = 'Auto'; HasArguments = $false } }
                Mock Test-RmsSupportAgentServiceOwnership { [pscustomobject]@{ Owned = $true } }
                Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true } }
                Mock Enter-RmsSupportAgentMutationLease { [pscustomobject]@{ Acquired = $true } }
                Mock Exit-RmsSupportAgentMutationLease {}
                Mock Set-RmsSupportAgentOwnedAcl {}
                Mock Write-RmsSupportAgentLifecycleState {}
                Mock Expand-RmsSupportAgentPackage {}
                Mock Save-RmsSupportAgentRetainedSlot { throw 'activation_failed' }
                Mock Restore-RmsSupportAgentRetainedSlot { [pscustomobject]@{ Succeeded = $false; Code = 'rollback_failed' } }

                $result = Invoke-RmsSupportAgentLifecycle -Mode Upgrade -Channel Testing
                $result.State | Should Be 'RecoveryRequired'
            }

            @($global:RmsAuditCorrelationCalls).Outcome | Should Be @('started', 'attempted', 'accepted', 'rollback_attempted', 'rollback_failed', 'recovery_required', 'failed')
            @($global:RmsAuditCorrelationCalls | Select-Object -ExpandProperty OperationId -Unique).Count | Should Be 1
            [string]::IsNullOrWhiteSpace([string]$global:RmsAuditCorrelationCalls[0].OperationId) | Should Be $false
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsAuditCorrelationContract, RmsAuditCorrelationManifest, RmsAuditCorrelationCalls, RmsAuditCorrelationStateReads -Scope Global -ErrorAction SilentlyContinue
        }
    }
}

# L-3: every early-return path in Invoke-RmsSupportAgentLifecycle that follows the
# started/attempted audit pair must reach exactly one terminal audit outcome carrying the same
# OperationId. Each It below drives one specific blocker and proves the terminal event exists,
# carries the correct FailureCode, and shares the operation's single OperationId.
Describe 'RMS Support Agent lifecycle terminal audit completeness (L-3)' {
    $script:RmsTerminalOutcomes = @('completed', 'failed', 'rollback_succeeded', 'rollback_failed', 'recovery_required')

    function New-RmsTerminalAuditContract {
        param([Parameter(Mandatory)][string]$Root)

        foreach ($child in @('available', 'rollback', 'recovery', 'staging', 'install', 'package', 'audit', 'trust')) {
            New-Item -ItemType Directory -Path (Join-Path $Root $child) -Force | Out-Null
        }
        [pscustomobject]@{
            PackageRoot = Join-Path $Root 'package'
            AvailableRoot = Join-Path $Root 'available'
            RollbackRoot = Join-Path $Root 'rollback'
            RecoveryRoot = Join-Path $Root 'recovery'
            StagingRoot = Join-Path $Root 'staging'
            InstallRoot = Join-Path $Root 'install'
            AuditRoot = Join-Path $Root 'audit'
            TrustRoot = Join-Path $Root 'trust'
            InstalledManifestPath = Join-Path $Root 'install\manifest.json'
            StatePath = Join-Path $Root 'lifecycle-state.json'
        }
    }

    It 'records a single recovery_required terminal event when an unresolved checkpoint blocks the operation' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-recovery-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { [pscustomobject]@{ Phase = 'HealthChecking'; RecoveryRequired = $true } }
                Mock Test-RmsSupportAgentPackageTrust { throw 'trust must not be evaluated while an unresolved checkpoint exists' }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'RecoveryRequired'
            $global:RmsTerminalAuditResult.Code | Should Be 'recovery_required'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'recovery_required')
            @($global:RmsTerminalAuditCalls | Select-Object -ExpandProperty OperationId -Unique).Count | Should Be 1
            ($script:RmsTerminalOutcomes -contains $global:RmsTerminalAuditCalls[-1].Outcome) | Should Be $true
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single failed terminal event with FailureCode trust_rejected when package trust is invalid' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-trust-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $false; Blockers = @('signer_untrusted'); Manifest = $null; ArchivePath = $null } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Failed'
            $global:RmsTerminalAuditResult.Code | Should Be 'trust_rejected'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'failed')
            $global:RmsTerminalAuditCalls[-1].FailureCode | Should Be 'trust_rejected'
            @($global:RmsTerminalAuditCalls | Select-Object -ExpandProperty OperationId -Unique).Count | Should Be 1
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single completed terminal event when Uninstall targets an already-absent Agent' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-absent-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = $null; ArchivePath = 'unused.zip' } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Uninstall -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Completed'
            $global:RmsTerminalAuditResult.Code | Should Be 'not_installed'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'completed')
            @($global:RmsTerminalAuditCalls | Select-Object -ExpandProperty OperationId -Unique).Count | Should Be 1
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single failed terminal event with FailureCode installed_integrity_rejected when Uninstall finds a tampered installation' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-uninstall-integrity-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '1.0.0' } | ConvertTo-Json -Compress | Set-Content -LiteralPath $global:RmsTerminalAuditContract.InstalledManifestPath -Encoding UTF8
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = $null; ArchivePath = 'unused.zip' } }
                Mock Test-RmsSupportAgentInstalledIntegrity { [pscustomobject]@{ Valid = $false; Blockers = @('installed_file_hash_mismatch') } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Uninstall -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Failed'
            $global:RmsTerminalAuditResult.Code | Should Be 'installed_integrity_rejected'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'failed')
            $global:RmsTerminalAuditCalls[-1].FailureCode | Should Be 'installed_integrity_rejected'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single failed terminal event with FailureCode ownership_conflict when the same-name service is not independently owned' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-ownership-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '1.0.0' }; ArchivePath = 'unused.zip' } }
                Mock Get-RmsSupportAgentServiceEvidence { [pscustomobject]@{ ServiceAccount = 'DOMAIN\OtherAccount'; StartMode = 'Auto'; HasArguments = $false } }
                Mock Test-RmsSupportAgentServiceOwnership { [pscustomobject]@{ Owned = $true } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Failed'
            $global:RmsTerminalAuditResult.Code | Should Be 'ownership_conflict'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'failed')
            $global:RmsTerminalAuditCalls[-1].FailureCode | Should Be 'ownership_conflict'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single failed terminal event with FailureCode service_ownership_unproven when Upgrade finds no verifiable service' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-noservice-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '2.0.0' }; ArchivePath = 'unused.zip' } }
                Mock Get-RmsSupportAgentServiceEvidence { $null }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Upgrade -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Failed'
            $global:RmsTerminalAuditResult.Code | Should Be 'service_ownership_unproven'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'failed')
            $global:RmsTerminalAuditCalls[-1].FailureCode | Should Be 'service_ownership_unproven'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single failed terminal event carrying the certificate prerequisite Code when the certificate is not ready' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-cert-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '1.0.0' }; ArchivePath = 'unused.zip' } }
                Mock Get-RmsSupportAgentServiceEvidence { $null }
                Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $false; Code = 'certificate_thumbprint_unconfigured' } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Failed'
            $global:RmsTerminalAuditResult.Code | Should Be 'certificate_thumbprint_unconfigured'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'failed')
            $global:RmsTerminalAuditCalls[-1].FailureCode | Should Be 'certificate_thumbprint_unconfigured'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records a single failed terminal event with FailureCode busy when another mutation owns the machine-wide lease' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-lease-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '1.0.0' }; ArchivePath = 'unused.zip' } }
                Mock Get-RmsSupportAgentServiceEvidence { $null }
                Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' } }
                Mock Enter-RmsSupportAgentMutationLease { [pscustomobject]@{ Acquired = $false; Code = 'busy'; Handle = $null } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Busy'
            $global:RmsTerminalAuditResult.Code | Should Be 'busy'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'failed')
            $global:RmsTerminalAuditCalls[-1].FailureCode | Should Be 'busy'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'records exactly one completed terminal event (no duplicate) for a successful Install' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-success-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditManifest = [pscustomobject]@{
            PackageId = 'rms-support-agent'
            Version = '1.0.0'
            Files = @([pscustomobject]@{ RelativePath = 'RmsSupportAgent.exe' })
        }
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { $null }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = $global:RmsTerminalAuditManifest; ArchivePath = 'unused.zip' } }
                Mock Get-RmsSupportAgentServiceEvidence { $null }
                Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' } }
                Mock Enter-RmsSupportAgentMutationLease { [pscustomobject]@{ Acquired = $true; Code = 'acquired'; Handle = $null } }
                Mock Exit-RmsSupportAgentMutationLease {}
                Mock Set-RmsSupportAgentOwnedAcl {}
                Mock Write-RmsSupportAgentLifecycleState {}
                Mock Expand-RmsSupportAgentPackage {}
                Mock Copy-RmsSupportAgentOwnedDirectory {}
                Mock Ensure-RmsSupportAgentService {}
                Mock Start-RmsSupportAgentService {}
                Mock Test-RmsSupportAgentHealth { $true }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Install -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'Completed'
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'accepted', 'completed')
            @($global:RmsTerminalAuditCalls | Select-Object -ExpandProperty OperationId -Unique).Count | Should Be 1
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditManifest, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }

    It 'represents a successful rollback restoration as a single rollback_succeeded terminal event, not completed' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-terminal-rollback-ok-' + [Guid]::NewGuid().ToString('N'))
        $global:RmsTerminalAuditContract = New-RmsTerminalAuditContract -Root $root
        $global:RmsTerminalAuditManifest = [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '2.0.0'; Files = @() }
        [pscustomobject]@{ PackageId = 'rms-support-agent'; Version = '1.0.0' } | ConvertTo-Json -Compress | Set-Content -LiteralPath $global:RmsTerminalAuditContract.InstalledManifestPath -Encoding UTF8
        $global:RmsTerminalAuditCalls = @()
        try {
            InModuleScope PosSupportAgentDeployment {
                Mock Get-RmsSupportAgentDeploymentContract { $global:RmsTerminalAuditContract }
                Mock Resolve-RmsSupportAgentMachineReleaseMode { [pscustomobject]@{ Valid = $true; Channel = 'Testing' } }
                Mock Add-RmsSupportAgentAuditEvent { $global:RmsTerminalAuditCalls += [pscustomobject]@{ Outcome = $Outcome; OperationId = $OperationId; FailureCode = $FailureCode } }
                Mock Get-RmsSupportAgentLifecycleState { [pscustomobject]@{ Phase = 'Staged'; RecoveryRequired = $false } }
                Mock Test-RmsSupportAgentPackageTrust { [pscustomobject]@{ Valid = $true; Manifest = $global:RmsTerminalAuditManifest; ArchivePath = 'unused.zip' } }
                Mock Test-RmsSupportAgentInstalledIntegrity { [pscustomobject]@{ Valid = $true } }
                Mock Get-RmsSupportAgentServiceEvidence { [pscustomobject]@{ ServiceAccount = 'LocalSystem'; StartMode = 'Auto'; HasArguments = $false } }
                Mock Test-RmsSupportAgentServiceOwnership { [pscustomobject]@{ Owned = $true } }
                Mock Test-RmsSupportAgentCertificatePrerequisite { [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' } }
                Mock Enter-RmsSupportAgentMutationLease { [pscustomobject]@{ Acquired = $true; Code = 'acquired'; Handle = $null } }
                Mock Exit-RmsSupportAgentMutationLease {}
                Mock Set-RmsSupportAgentOwnedAcl {}
                Mock Write-RmsSupportAgentLifecycleState {}
                Mock Expand-RmsSupportAgentPackage {}
                Mock Save-RmsSupportAgentRetainedSlot { throw 'activation_failed' }
                Mock Restore-RmsSupportAgentRetainedSlot { [pscustomobject]@{ Succeeded = $true; Code = 'rollback_succeeded'; Manifest = $global:RmsTerminalAuditManifest } }

                $global:RmsTerminalAuditResult = Invoke-RmsSupportAgentLifecycle -Mode Upgrade -Channel Testing
            }

            $global:RmsTerminalAuditResult.State | Should Be 'RollbackSucceeded'
            $global:RmsTerminalAuditResult.RollbackSucceeded | Should Be $true
            @($global:RmsTerminalAuditCalls).Outcome | Should Be @('started', 'attempted', 'accepted', 'rollback_attempted', 'rollback_succeeded')
            @($global:RmsTerminalAuditCalls | Select-Object -ExpandProperty OperationId -Unique).Count | Should Be 1
            # A restored rollback must never be recorded as a plain 'completed' -- the operation
            # that was actually requested still failed and only the PRIOR version was restored.
            $global:RmsTerminalAuditCalls | Where-Object { $_.Outcome -eq 'completed' } | Should Be $null
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsTerminalAuditContract, RmsTerminalAuditManifest, RmsTerminalAuditCalls, RmsTerminalAuditResult -Scope Global -ErrorAction SilentlyContinue
        }
    }
}

# L-4: TestOnlyTrustFixture is read only inside the non-exported Get-RmsSupportAgentMachineTrustConfiguration
# and is set only by test-only fixtures invoked through Pester's InModuleScope (see the
# 'RMS Support Agent machine-owned release mode authority' Describe above). The normal production
# entry point, Invoke-RmsSupportAgentLifecycle, always builds its contract via the exported
# Get-RmsSupportAgentDeploymentContract, which never emits this property -- so no parameter,
# environment variable, configuration key, package input, or deploymentMode value reaching the
# normal entry point can ever select the alternate trust path.
Describe 'RMS Support Agent normal lifecycle entry point cannot reach the test-only trust fixture (L-4)' {
    It 'never includes TestOnlyTrustFixture on the contract the normal entry point builds' {
        $contract = Get-RmsSupportAgentDeploymentContract -Channel Testing
        $contract.PSObject.Properties['TestOnlyTrustFixture'] | Should Be $null
    }

    It 'ignores an attempted TestOnlyTrustFixture=$true injection because the real entry point never reads it from caller input' {
        # Invoke-RmsSupportAgentLifecycle exposes only -Mode and -Channel; there is no parameter,
        # environment variable, or configuration surface through which a caller could cause the
        # contract it builds to carry TestOnlyTrustFixture. This proves that even if a caller
        # believes it can request the alternate trust path, the real entry point cannot honor it.
        $command = Get-Command Invoke-RmsSupportAgentLifecycle
        $command.Parameters.Keys | Where-Object { $_ -notin @('Mode', 'Channel') -and $command.Parameters[$_].ParameterSets.Values.IsMandatory -contains $true } | Should Be $null
        $command.Parameters.Keys -contains 'TestOnlyTrustFixture' | Should Be $false
        $command.Parameters.Keys -contains 'TrustConfigurationPath' | Should Be $false
    }

    It 'resolves machine trust from the protected canonical path when the normal contract-builder supplies the contract' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-l4-canonical-' + [Guid]::NewGuid().ToString('N'))
        New-RmsProtectedOwnedAclDirectory -Path $root | Out-Null
        $global:RmsL4Contract = Get-RmsSupportAgentDeploymentContract -Channel Testing
        try {
            InModuleScope PosSupportAgentDeployment {
                # The real contract's TrustConfigurationPath is the fixed %ProgramData% path and it
                # carries no TestOnlyTrustFixture property, so resolution must evaluate the canonical
                # path branch -- never the alternate fixture branch -- regardless of what exists on disk.
                $result = Get-RmsSupportAgentMachineTrustConfiguration -Contract $global:RmsL4Contract
                $result.Code | Should Not Be 'machine_trust_configuration_path_invalid'
            }
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Variable RmsL4Contract -Scope Global -ErrorAction SilentlyContinue
        }
    }
}

Describe 'RMS Support Agent LocalSystem private-key ACL evidence' {
    It 'accepts protected administrator and LocalSystem rules with LocalSystem read access' {
        $admin = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
        $system = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
        $rules = @(
            (New-Object Security.AccessControl.FileSystemAccessRule($admin, [Security.AccessControl.FileSystemRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow)),
            (New-Object Security.AccessControl.FileSystemAccessRule($system, [Security.AccessControl.FileSystemRights]::Read, [Security.AccessControl.AccessControlType]::Allow)))
        $global:RmsPrivateKeyAclOwner = $admin
        $global:RmsPrivateKeyAclRules = $rules
        InModuleScope PosSupportAgentDeployment {
            Test-RmsSupportAgentPrivateKeyAclEvidence -Owner $global:RmsPrivateKeyAclOwner -AreAccessRulesProtected $true -Rules $global:RmsPrivateKeyAclRules | Should Be $true
        }
        Remove-Variable RmsPrivateKeyAclOwner, RmsPrivateKeyAclRules -Scope Global -ErrorAction SilentlyContinue
    }

    It 'rejects admin-only private-key evidence because LocalSystem access is not proven' {
        $admin = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
        $rules = @((New-Object Security.AccessControl.FileSystemAccessRule($admin, [Security.AccessControl.FileSystemRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow)))
        $global:RmsPrivateKeyAclOwner = $admin
        $global:RmsPrivateKeyAclRules = $rules
        InModuleScope PosSupportAgentDeployment {
            Test-RmsSupportAgentPrivateKeyAclEvidence -Owner $global:RmsPrivateKeyAclOwner -AreAccessRulesProtected $true -Rules $global:RmsPrivateKeyAclRules | Should Be $false
        }
        Remove-Variable RmsPrivateKeyAclOwner, RmsPrivateKeyAclRules -Scope Global -ErrorAction SilentlyContinue
    }

    It 'rejects a broad private-key allow rule' {
        $admin = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
        $system = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
        $everyone = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::WorldSid, $null)
        $rules = @(
            (New-Object Security.AccessControl.FileSystemAccessRule($admin, [Security.AccessControl.FileSystemRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow)),
            (New-Object Security.AccessControl.FileSystemAccessRule($system, [Security.AccessControl.FileSystemRights]::Read, [Security.AccessControl.AccessControlType]::Allow)),
            (New-Object Security.AccessControl.FileSystemAccessRule($everyone, [Security.AccessControl.FileSystemRights]::Read, [Security.AccessControl.AccessControlType]::Allow)))
        $global:RmsPrivateKeyAclOwner = $admin
        $global:RmsPrivateKeyAclRules = $rules
        InModuleScope PosSupportAgentDeployment {
            Test-RmsSupportAgentPrivateKeyAclEvidence -Owner $global:RmsPrivateKeyAclOwner -AreAccessRulesProtected $true -Rules $global:RmsPrivateKeyAclRules | Should Be $false
        }
        Remove-Variable RmsPrivateKeyAclOwner, RmsPrivateKeyAclRules -Scope Global -ErrorAction SilentlyContinue
    }

    It 'rejects an unprotected private-key ACL' {
        $admin = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
        $system = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
        $rules = @(
            (New-Object Security.AccessControl.FileSystemAccessRule($admin, [Security.AccessControl.FileSystemRights]::FullControl, [Security.AccessControl.AccessControlType]::Allow)),
            (New-Object Security.AccessControl.FileSystemAccessRule($system, [Security.AccessControl.FileSystemRights]::Read, [Security.AccessControl.AccessControlType]::Allow)))
        $global:RmsPrivateKeyAclOwner = $admin
        $global:RmsPrivateKeyAclRules = $rules
        InModuleScope PosSupportAgentDeployment {
            Test-RmsSupportAgentPrivateKeyAclEvidence -Owner $global:RmsPrivateKeyAclOwner -AreAccessRulesProtected $false -Rules $global:RmsPrivateKeyAclRules | Should Be $false
        }
        Remove-Variable RmsPrivateKeyAclOwner, RmsPrivateKeyAclRules -Scope Global -ErrorAction SilentlyContinue
    }

    It 'rejects a key path outside the fixed CNG key directory' {
        $global:RmsPrivateKeyPath = Join-Path ([IO.Path]::GetTempPath()) 'not-a-cng-key'
        InModuleScope PosSupportAgentDeployment {
            Test-RmsSupportAgentLocalSystemPrivateKeyAccess -KeyFilePath $global:RmsPrivateKeyPath | Should Be $false
        }
        Remove-Variable RmsPrivateKeyPath -Scope Global -ErrorAction SilentlyContinue
    }
}

Describe 'Save-RmsSupportAgentRetainedSlot (Blocker B parity: retain only signed artifacts, never a directory copy)' {
    BeforeEach {
        $script:testRoot = Join-Path $script:PosTestFixtureRoot ('rms-retain-' + [Guid]::NewGuid().ToString('N'))
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
        $root = Join-Path $script:PosTestFixtureRoot ('rms-checkpoint-' + [Guid]::NewGuid().ToString('N'))
        New-RmsProtectedOwnedAclDirectory -Path $root | Out-Null
        $path = Join-Path $root 'lifecycle-state.json'
        Set-Content -LiteralPath $path -Value 'not-json' -Encoding UTF8
        Protect-RmsTestControlFile -Path $path
        try {
            { Get-RmsSupportAgentLifecycleState -Contract ([pscustomobject]@{ StatePath = $path }) } | Should Throw
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'throws rather than reading an oversized checkpoint file' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-checkpoint-' + [Guid]::NewGuid().ToString('N'))
        New-RmsProtectedOwnedAclDirectory -Path $root | Out-Null
        $path = Join-Path $root 'lifecycle-state.json'
        Set-Content -LiteralPath $path -Value ('{"Phase":"' + ('a' * 70000) + '"}') -Encoding UTF8
        Protect-RmsTestControlFile -Path $path
        try {
            { Get-RmsSupportAgentLifecycleState -Contract ([pscustomobject]@{ StatePath = $path }) } | Should Throw
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'returns null rather than throwing when no checkpoint exists at all' {
        $root = Join-Path $script:PosTestFixtureRoot ('rms-checkpoint-' + [Guid]::NewGuid().ToString('N'))
        New-RmsProtectedOwnedAclDirectory -Path $root | Out-Null
        $path = Join-Path $root 'lifecycle-state.json'
        Get-RmsSupportAgentLifecycleState -Contract ([pscustomobject]@{ StatePath = $path }) | Should Be $null
    }
}

Describe 'Restore-RmsSupportAgentRetainedSlot (Blockers A/B/C and Sections 6-7 parity)' {
    BeforeEach {
        $global:RmsRollbackTestRoot = Join-Path $script:PosTestFixtureRoot ('rms-restore-' + [Guid]::NewGuid().ToString('N'))
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
