$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot '..\PosSupportAgentDeployment.psm1'
Import-Module $modulePath -Force

Describe 'Production Agent package trust and lifecycle boundary' {
    BeforeEach {
        $script:manifest = [pscustomobject]@{
            SchemaVersion = 1
            ProductId = 'RmsSupportAgent'
            PackageId = 'rms-support-agent'
            Version = '1.0.0'
            SupportedOperatingSystem = 'Windows'
            SupportedRuntime = 'net10.0-windows'
            ServiceName = 'RmsSupportAgent'
            ServiceDisplayName = 'RMS Support Agent'
            ServiceDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
            ServiceIdentity = 'LocalSystem'
            ScmName = 'RmsSupportAgent'
            SignatureAlgorithm = 'SHA256withRSA'
            SignerDisplayName = 'DBS test signer'
            Architecture = 'x64'
            ReleaseChannel = 'Testing'
            Environment = 'Testing'
            PackageSha256 = ('a' * 64)
            PackageSizeBytes = 1
            Signature = 'AQ=='
            Files = @([pscustomobject]@{ LogicalName = 'agent'; RelativePath = 'RmsSupportAgent.exe'; SizeBytes = 1; Sha256 = ('b' * 64); Required = $true })
            AclRequirements = @('AgentServiceReadWrite')
            CertificateRequirements = @('AgentHttpsCertificate')
            PreviousVersion = $null
            RollbackAvailable = $false
        }
    }

    It 'binds the package metadata and exact file set to a deterministic envelope' {
        $first = Get-RmsCanonicalPackagePayload -Manifest $manifest
        $reordered = $manifest.PSObject.Copy()
        $reordered.Files = @($manifest.Files)
        $second = Get-RmsCanonicalPackagePayload -Manifest $reordered
        $tampered = $manifest.PSObject.Copy()
        $tampered.Version = '1.0.1'

        [Convert]::ToBase64String($first) | Should Be ([Convert]::ToBase64String($second))
        [Convert]::ToBase64String((Get-RmsCanonicalPackagePayload -Manifest $tampered)) | Should Not Be ([Convert]::ToBase64String($first))
    }

    It 'requires cryptographic signer metadata in the real trust path' {
        $result = Test-RmsSupportAgentPackageManifest -Manifest $manifest -Channel Testing -CurrentArchitecture x64 -CryptographicSignature

        $result.Valid | Should Be $true
        $result.Blockers.Count | Should Be 0

        $tampered = $manifest.PSObject.Copy()
        $tampered.SignatureAlgorithm = 'none'
        $rejected = Test-RmsSupportAgentPackageManifest -Manifest $tampered -Channel Testing -CurrentArchitecture x64 -CryptographicSignature
        $rejected.Valid | Should Be $false
        (@($rejected.Blockers) -contains 'signature_algorithm_invalid') | Should Be $true

        $wrongScm = $manifest.PSObject.Copy()
        $wrongScm.ScmName = 'OtherService'
        $scmRejected = Test-RmsSupportAgentPackageManifest -Manifest $wrongScm -Channel Testing -CurrentArchitecture x64 -CryptographicSignature
        $scmRejected.Valid | Should Be $false
        (@($scmRejected.Blockers) -contains 'scm_name_invalid') | Should Be $true
    }

    It 'keeps the lifecycle read-only status path separate from mutation modes' {
        $contract = Get-RmsSupportAgentDeploymentContract -Channel Production
        $plan = Get-RmsSupportAgentLifecyclePlan -Mode Install -Contract $contract

        $plan.RequiresTrustedPackage | Should Be $true
        $plan.AllowsUnknownResourceDeletion | Should Be $false
        $plan.AllowsRmsProductServiceControl | Should Be $false
        $plan.NonInteractive | Should Be $true
    }
}
