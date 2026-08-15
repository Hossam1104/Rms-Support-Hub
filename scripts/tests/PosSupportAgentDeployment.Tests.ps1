$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot '..\PosSupportAgentDeployment.psm1'
Import-Module $modulePath -Force

Describe 'Permanent RMS Support Agent deployment contract' {
    It 'uses the permanent service identity and deterministic silent exit codes' {
        $contract = Get-RmsSupportAgentDeploymentContract -Channel Production

        $contract.ProductId | Should Be 'RmsSupportAgent'
        $contract.ServiceName | Should Be 'RmsSupportAgent'
        $contract.DisplayName | Should Be 'RMS Support Agent'
        $contract.Description | Should Be 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
        $contract.SilentExitCodes.TrustRejected | Should Be 20
        $contract.SilentExitCodes.OwnershipConflict | Should Be 30
        $contract.OwnershipMarker | Should Be 'RmsSupportAgent'
    }

    It 'rejects a Production package without trusted signing evidence' {
        $manifest = [pscustomobject]@{
            SchemaVersion = 1
            ProductId = 'RmsSupportAgent'
            Version = '1.0.0'
            ServiceName = 'RmsSupportAgent'
            ServiceDisplayName = 'RMS Support Agent'
            ServiceDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
            ServiceIdentity = 'LocalSystem'
            Architecture = 'x64'
            ReleaseChannel = 'Production'
            PackageSha256 = ('a' * 64)
            Files = @([pscustomobject]@{ RelativePath = 'RmsSupportAgent.exe'; SizeBytes = 10; Sha256 = ('b' * 64) })
            SignatureVerified = $false
            Signer = $null
        }

        $result = Test-RmsSupportAgentPackageManifest -Manifest $manifest -Channel Production -CurrentArchitecture x64

        $result.Valid | Should Be $false
        (@($result.Blockers) -contains 'production_signature_required') | Should Be $true
    }

    It 'permits explicitly non-Production Testing packages only with the Testing channel' {
        $manifest = [pscustomobject]@{
            SchemaVersion = 1
            ProductId = 'RmsSupportAgent'
            Version = '1.0.0'
            ServiceName = 'RmsSupportAgent'
            ServiceDisplayName = 'RMS Support Agent'
            ServiceDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
            ServiceIdentity = 'LocalSystem'
            Architecture = 'x64'
            ReleaseChannel = 'Testing'
            Environment = 'Testing'
            PackageSha256 = ('a' * 64)
            Files = @([pscustomobject]@{ RelativePath = 'RmsSupportAgent.exe'; SizeBytes = 10; Sha256 = ('b' * 64) })
            SignatureVerified = $false
            Signer = $null
        }

        $result = Test-RmsSupportAgentPackageManifest -Manifest $manifest -Channel Testing -CurrentArchitecture x64

        $result.Valid | Should Be $true
        $result.NonProduction | Should Be $true
    }

    It 'rejects browser policy generation below the supported major version' {
        { Get-RmsSupportAgentBrowserPolicyPlan -Browser Chrome -MajorVersion 138 -SupportHubOrigin 'https://support-hub.integration.test:4443' } | Should Throw
        { Get-RmsSupportAgentBrowserPolicyPlan -Browser Edge -MajorVersion 139 -SupportHubOrigin 'https://support-hub.integration.test:4443' } | Should Throw
    }

    It 'rejects rooted, ADS, URI, query, fragment, and traversal package paths' {
        foreach ($path in @('C:\agent.exe', '\\server\agent.exe', 'agent.exe:stream', 'https://example.test/a', 'agent?.exe', 'agent#.exe', '..\agent.exe', 'folder/../agent.exe')) {
            $manifest = [pscustomobject]@{
                SchemaVersion = 1; ProductId = 'RmsSupportAgent'; Version = '1.0.0'; ServiceName = 'RmsSupportAgent';
                ServiceDisplayName = 'RMS Support Agent'; ServiceDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub';
                ServiceIdentity = 'LocalSystem'; Architecture = 'x64'; ReleaseChannel = 'Testing'; PackageSha256 = ('a' * 64);
                Files = @([pscustomobject]@{ RelativePath = $path; SizeBytes = 10; Sha256 = ('b' * 64) })
            }
            (@((Test-RmsSupportAgentPackageManifest -Manifest $manifest -Channel Testing -CurrentArchitecture x64).Blockers) -contains 'file_path_invalid') | Should Be $true
        }
    }

    It 'plans no-op, owned migration, and unowned conflict without touching SCM' {
        $contract = Get-RmsSupportAgentDeploymentContract -Channel Testing
        $permanent = [pscustomobject]@{
            ServiceName = 'RmsSupportAgent'; DisplayName = 'RMS Support Agent'; Description = 'Local diagnostics, maintenance and repair agent for RMS Support Hub';
            BinaryPath = (Join-Path $contract.InstallRoot 'RmsSupportAgent.exe'); PackageId = 'RmsSupportAgent'; PackageVersion = '1.0.0'; OwnershipMarker = 'RmsSupportAgent'; BinarySha256 = ('a' * 64)
        }
        (Get-RmsSupportAgentMigrationPlan -Contract $contract -PermanentServiceEvidence $permanent).Action | Should Be 'NoOp'

        $legacy = [pscustomobject]@{
            ServiceName = 'RmsSupportHub.Pos.Agent'; DisplayName = 'RMS+ POS Agent (Testing)';
            BinaryPath = (Join-Path $contract.InstallRoot 'legacy.exe'); OwnershipMarker = 'RmsSupportHub INT-13 Testing Provisioning v1'
        }
        (Get-RmsSupportAgentMigrationPlan -Contract $contract -LegacyServiceEvidence @($legacy)).Action | Should Be 'MigrateOwnedLegacy'

        $unowned = $legacy | Select-Object *
        $unowned.OwnershipMarker = 'unknown'
        (Get-RmsSupportAgentMigrationPlan -Contract $contract -LegacyServiceEvidence @($unowned)).Action | Should Be 'Conflict'
    }

    It 'creates exact browser and certificate plans without registry or certificate-store writes' {
        $browser = Get-RmsSupportAgentBrowserPolicyPlan -Browser Chrome -MajorVersion 146 -SupportHubOrigin 'https://support-hub.integration.test:4443'
        $browser.LoopbackAllowlistPolicy | Should Be 'LoopbackNetworkAllowedForUrls'
        $browser.LoopbackAllowlist[0] | Should Be 'https://support-hub.integration.test:4443'
        $browser.WritesRegistry | Should Be $false
        (@($browser.DisallowedSettings) -contains 'DisableLoopbackCheck') | Should Be $true

        $certificate = Get-RmsSupportAgentCertificatePlan -Channel Production
        $certificate.DnsName | Should Be 'rms-pos-agent.localhost'
        $certificate.Provider | Should Be 'Microsoft Software Key Storage Provider'
        $certificate.KeyExportable | Should Be $false
        $certificate.WritesCertificateStore | Should Be $false
    }

    It 'makes every lifecycle mode machine-scoped, silent, idempotent, and non-destructive to unknown resources' {
        $contract = Get-RmsSupportAgentDeploymentContract
        foreach ($mode in @('Install', 'Upgrade', 'Repair', 'Uninstall', 'Rollback', 'Status')) {
            $plan = Get-RmsSupportAgentLifecyclePlan -Mode $mode -Contract $contract
            $plan.Silent | Should Be $true
            $plan.Idempotent | Should Be $true
            $plan.AllowsUnknownResourceDeletion | Should Be $false
            $plan.AllowsRmsProductServiceControl | Should Be $false
        }
    }

    It 'recognizes the INT-13 disposable service as Testing-only owned legacy evidence' {
        $contract = Get-RmsSupportAgentDeploymentContract -Channel Testing
        $legacy = [pscustomobject]@{
            ServiceName = 'RmsSupportHub.Pos.Int13.TestService'; DisplayName = 'RMS+ INT-13 Disposable Testing Service';
            BinaryPath = (Join-Path $contract.InstallRoot 'legacy.exe'); OwnershipMarker = 'RmsSupportHub INT-13P Testing Provisioning v1'
        }

        (Get-RmsSupportAgentMigrationPlan -Contract $contract -LegacyServiceEvidence @($legacy)).Action | Should Be 'MigrateOwnedLegacy'
    }
}
