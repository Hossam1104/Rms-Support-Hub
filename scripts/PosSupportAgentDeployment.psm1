Set-StrictMode -Version Latest

$script:PermanentServiceName = 'RmsSupportAgent'
$script:PermanentDisplayName = 'RMS Support Agent'
$script:PermanentDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
$script:ProductId = 'RmsSupportAgent'
# The product ID is the permanent resource marker. It is intentionally shared
# with the typed Agent ownership policy so a name-only match can never pass.
$script:OwnershipMarker = $script:ProductId
$script:CanonicalAgentHost = 'rms-pos-agent.localhost'

function Get-RmsSupportAgentDeploymentContract {
    [CmdletBinding()]
    param(
        [ValidateSet('Testing', 'Production')]
        [string]$Channel = 'Production'
    )

    [pscustomobject]@{
        ProductId = $script:ProductId
        ServiceName = $script:PermanentServiceName
        DisplayName = $script:PermanentDisplayName
        Description = $script:PermanentDescription
        OwnershipMarker = $script:OwnershipMarker
        CanonicalAgentHost = $script:CanonicalAgentHost
        InstallRoot = Join-Path $env:ProgramFiles 'DBS\RmsSupportAgent'
        PackageRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Packages'
        AuditRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Audit'
        ReleaseChannel = $Channel
        SilentExitCodes = [ordered]@{
            Success = 0
            InvalidArguments = 2
            PackageUnavailable = 10
            TrustRejected = 20
            OwnershipConflict = 30
            ElevationRequired = 40
            RecoveryRequired = 50
            UnexpectedFailure = 100
        }
    }
}

function Test-RmsSafeToken {
    param(
        [AllowNull()]
        [string]$Value,
        [int]$MaximumLength = 128
    )

    return -not [string]::IsNullOrWhiteSpace($Value) -and
        $Value.Length -le $MaximumLength -and
        $Value -match '^[A-Za-z0-9][A-Za-z0-9._-]*$'
}

function Test-RmsSafeSha256 {
    param([AllowNull()][string]$Value)
    return $Value -match '^[0-9a-fA-F]{64}$'
}

function Test-RmsSafeRelativePackagePath {
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.Length -gt 260) { return $false }
    if ([IO.Path]::IsPathRooted($Value) -or $Value.IndexOf(':') -ge 0 -or
        $Value.IndexOf('?') -ge 0 -or $Value.IndexOf('#') -ge 0 -or
        $Value -match '[\x00-\x1F\x7F]' -or $Value -match '^[a-zA-Z][a-zA-Z0-9+.-]*://') {
        return $false
    }

    $normalized = $Value.Replace('\', '/')
    if ($normalized.StartsWith('/') -or @($normalized.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        return $false
    }

    return $true
}

function Test-RmsSupportAgentPackageManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [psobject]$Manifest,
        [ValidateSet('Testing', 'Production')]
        [string]$Channel = 'Production',
        [string]$CurrentArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    )

    $blockers = [System.Collections.Generic.List[string]]::new()
    foreach ($property in @('SchemaVersion', 'ProductId', 'Version', 'ServiceName', 'ServiceDisplayName', 'ServiceDescription', 'ServiceIdentity', 'Architecture', 'ReleaseChannel', 'Environment', 'PackageSha256', 'Files')) {
        if ($null -eq $Manifest.PSObject.Properties[$property]) { [void]$blockers.Add("missing_$($property.ToLowerInvariant())") }
    }
    $environment = if ($null -ne $Manifest.PSObject.Properties['Environment']) { [string]$Manifest.Environment } else { $null }

    if ($Manifest.SchemaVersion -ne 1) { [void]$blockers.Add('schema_version_invalid') }
    if ($Manifest.ProductId -ne $script:ProductId) { [void]$blockers.Add('product_identity_invalid') }
    if (-not (Test-RmsSafeToken $Manifest.Version)) { [void]$blockers.Add('version_invalid') }
    if ($Manifest.ServiceName -ne $script:PermanentServiceName) { [void]$blockers.Add('service_name_invalid') }
    if ($Manifest.ServiceDisplayName -ne $script:PermanentDisplayName -or $Manifest.ServiceDescription -ne $script:PermanentDescription) { [void]$blockers.Add('service_identity_invalid') }
    if ($Manifest.ServiceIdentity -ne 'LocalSystem') { [void]$blockers.Add('service_account_invalid') }
    if ($Manifest.Architecture -notin @('x64', 'arm64') -or $Manifest.Architecture -ne $CurrentArchitecture) { [void]$blockers.Add('architecture_invalid') }
    if ($Manifest.ReleaseChannel -notin @('Testing', 'Production') -or $Manifest.ReleaseChannel -ne $Channel) { [void]$blockers.Add('release_channel_invalid') }
    if ($environment -notin @('Testing', 'Production') -or $environment -ne $Manifest.ReleaseChannel) { [void]$blockers.Add('environment_invalid') }
    if (-not (Test-RmsSafeSha256 $Manifest.PackageSha256)) { [void]$blockers.Add('package_hash_invalid') }

    $files = @($Manifest.Files)
    if ($files.Count -lt 1 -or $files.Count -gt 256) { [void]$blockers.Add('file_count_invalid') }
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $files) {
        if ($null -eq $file -or -not (Test-RmsSafeRelativePackagePath $file.RelativePath) -or -not $seen.Add($file.RelativePath.Replace('\', '/'))) { [void]$blockers.Add('file_path_invalid'); continue }
        if ($file.SizeBytes -lt 0 -or $file.SizeBytes -gt 67108864) { [void]$blockers.Add('file_size_invalid') }
        if (-not (Test-RmsSafeSha256 $file.Sha256)) { [void]$blockers.Add('file_hash_invalid') }
    }

    $signatureVerified = $false
    if ($null -ne $Manifest.PSObject.Properties['SignatureVerified']) { $signatureVerified = [bool]$Manifest.SignatureVerified }
    if ($Channel -eq 'Production' -and (-not $signatureVerified -or [string]::IsNullOrWhiteSpace([string]$Manifest.Signer))) { [void]$blockers.Add('production_signature_required') }
    if ($Channel -eq 'Testing' -and $signatureVerified -and [string]::IsNullOrWhiteSpace([string]$Manifest.Signer)) { [void]$blockers.Add('signer_metadata_invalid') }

    [pscustomobject]@{
        Valid = $blockers.Count -eq 0
        Blockers = @($blockers | Select-Object -Unique)
        Detail = if ($blockers.Count -eq 0) { 'The permanent Agent package manifest passed the bounded trust and identity contract.' } else { 'The permanent Agent package manifest was rejected before installation.' }
        NonProduction = $Channel -eq 'Testing'
    }
}

function Test-RmsSupportAgentServiceOwnership {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [psobject]$Evidence,
        [Parameter(Mandatory)]
        [psobject]$Contract,
        [string]$ExpectedPackageId = $script:ProductId,
        [string]$ExpectedVersion,
        [string]$ExpectedBinarySha256
    )

    $binaryPath = [string]$Evidence.BinaryPath
    $root = [IO.Path]::GetFullPath([string]$Contract.InstallRoot).TrimEnd('\') + '\'
    $normalizedBinary = if ([string]::IsNullOrWhiteSpace($binaryPath)) { '' } else { [IO.Path]::GetFullPath($binaryPath.Trim().Trim('"')) }
    $owned = $normalizedBinary.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and
        $Evidence.ServiceName -eq $script:PermanentServiceName -and
        $Evidence.DisplayName -eq $script:PermanentDisplayName -and
        $Evidence.Description -eq $script:PermanentDescription -and
        $Evidence.PackageId -eq $ExpectedPackageId -and
        $Evidence.OwnershipMarker -eq $script:ProductId

    if ($owned -and $ExpectedVersion) { $owned = $Evidence.PackageVersion -eq $ExpectedVersion }
    if ($owned -and $ExpectedBinarySha256) { $owned = [string]::Equals([string]$Evidence.BinarySha256, $ExpectedBinarySha256, [StringComparison]::OrdinalIgnoreCase) }

    [pscustomobject]@{
        Owned = [bool]$owned
        State = if ($owned) { 'Owned' } else { 'Conflict' }
        Code = if ($owned) { 'owned' } else { 'service_ownership_unproven' }
        Detail = if ($owned) { 'The permanent Agent service matches its independently verifiable Support Hub ownership evidence.' } else { 'The service identity, binary root, marker, package, or hash could not prove Support Hub ownership. No SCM mutation is permitted.' }
    }
}

function Get-RmsSupportAgentMigrationPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [psobject]$Contract,
        [AllowNull()]
        [psobject]$PermanentServiceEvidence,
        [AllowNull()]
        [psobject[]]$LegacyServiceEvidence = @()
    )

    if ($null -ne $PermanentServiceEvidence) {
        $permanent = Test-RmsSupportAgentServiceOwnership -Evidence $PermanentServiceEvidence -Contract $Contract
        if ($permanent.Owned) {
            return [pscustomobject]@{ Action = 'NoOp'; Code = 'permanent_agent_owned'; Detail = 'The permanent Agent is already owned and correct. Historical services remain untouched.'; Conflicts = @() }
        }
    }

    $conflicts = [System.Collections.Generic.List[string]]::new()
    $ownedLegacy = [System.Collections.Generic.List[string]]::new()
    foreach ($legacy in @($LegacyServiceEvidence)) {
        if ($null -eq $legacy) { continue }
        if ($legacy.ServiceName -notin @('RmsSupportHub.Pos.Agent', 'RmsSupportHub.Pos.Int13.TestService')) {
            [void]$conflicts.Add('legacy_service_not_allow_listed')
            continue
        }
        $expectedLegacyDisplay = if ($legacy.ServiceName -eq 'RmsSupportHub.Pos.Agent') { 'RMS+ POS Agent (Testing)' } else { 'RMS+ INT-13 Disposable Testing Service' }
        $legacyRoot = [IO.Path]::GetFullPath([string]$Contract.InstallRoot).TrimEnd('\') + '\'
        $legacyPath = if ([string]::IsNullOrWhiteSpace([string]$legacy.BinaryPath)) { '' } else { [IO.Path]::GetFullPath(([string]$legacy.BinaryPath).Trim().Trim('"')) }
        $owned = $legacyPath.StartsWith($legacyRoot, [StringComparison]::OrdinalIgnoreCase) -and
            $legacy.OwnershipMarker -in @('RmsSupportHub INT-13 Testing Provisioning v1', 'RmsSupportHub INT-13P Testing Provisioning v1') -and
            $legacy.ServiceName -in @('RmsSupportHub.Pos.Agent', 'RmsSupportHub.Pos.Int13.TestService') -and
            $legacy.DisplayName -eq $expectedLegacyDisplay
        if ($owned) { [void]$ownedLegacy.Add($legacy.ServiceName) } else { [void]$conflicts.Add($legacy.ServiceName) }
    }

    if ($conflicts.Count -gt 0) {
        return [pscustomobject]@{ Action = 'Conflict'; Code = 'legacy_service_conflict'; Detail = 'A same-name or historical service was not independently proven owned. Do not remove, overwrite, or adopt it.'; Conflicts = @($conflicts | Select-Object -Unique) }
    }
    if ($ownedLegacy.Count -gt 0) {
        return [pscustomobject]@{ Action = 'MigrateOwnedLegacy'; Code = 'owned_legacy_migration_available'; Detail = 'Only the independently owned historical Support Hub service may be migrated to the permanent service identity.'; Conflicts = @() }
    }
    [pscustomobject]@{ Action = 'Install'; Code = 'new_installation'; Detail = 'No owned legacy service was found; install the permanent Agent as a new service after package verification.'; Conflicts = @() }
}

function Get-RmsSupportAgentBrowserPolicyPlan {
    [CmdletBinding()]
    param(
        [ValidateSet('Chrome', 'Edge')]
        [string]$Browser,
        [ValidateRange(1, 9999)]
        [int]$MajorVersion,
        [Parameter(Mandatory)]
        [string]$SupportHubOrigin
    )

    $origin = [Uri]$SupportHubOrigin
    if ($origin.Scheme -ne 'https' -or [string]::IsNullOrWhiteSpace($origin.Host) -or $origin.AbsolutePath -notin @('', '/') -or $origin.Query -or $origin.Fragment -or $SupportHubOrigin.Contains('*')) { throw 'The Support Hub origin must be one exact HTTPS origin.' }
    $minimumVersion = if ($Browser -eq 'Chrome') { 139 } else { 140 }
    if ($MajorVersion -lt $minimumVersion) { throw "$Browser $MajorVersion is below the supported exact-origin Local Network Access policy generation ($minimumVersion+)." }
    $loopbackPolicy = if ($MajorVersion -ge 146) { 'LoopbackNetworkAllowedForUrls' } else { 'LocalNetworkAccessAllowedForUrls' }
    [pscustomobject]@{
        Browser = $Browser
        MajorVersion = $MajorVersion
        MinimumSupportedMajorVersion = $minimumVersion
        PolicyRoot = if ($Browser -eq 'Chrome') { 'SOFTWARE\Policies\Google\Chrome' } else { 'SOFTWARE\Policies\Microsoft\Edge' }
        AuthenticationAllowlist = @($origin.Authority)
        LoopbackAllowlistPolicy = $loopbackPolicy
        LoopbackAllowlist = @($SupportHubOrigin.TrimEnd('/'))
        DisallowedSettings = @('DisableLoopbackCheck', 'wildcard-authentication-allowlist', 'localhost:4200-Agent-CORS')
        OwnershipMarker = $script:OwnershipMarker
        WritesRegistry = $false
    }
}

function Get-RmsSupportAgentCertificatePlan {
    [CmdletBinding()]
    param(
        [ValidateSet('Testing', 'Production')]
        [string]$Channel = 'Production'
    )

    [pscustomobject]@{
        Channel = $Channel
        DnsName = $script:CanonicalAgentHost
        StoreLocation = 'LocalMachine'
        Provider = 'Microsoft Software Key Storage Provider'
        KeyExportable = $false
        EnhancedKeyUsages = @('1.3.6.1.5.5.7.3.1')
        OwnedCertificateRemoval = 'thumbprint-and-ownership-marker-required'
        ExternalEnterpriseCertificateAllowed = $true
        TrustStoreChanges = 'none'
        PfxExposure = 'none'
        WritesCertificateStore = $false
    }
}

function Get-RmsSupportAgentLifecyclePlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Install', 'Upgrade', 'Repair', 'Uninstall', 'Rollback', 'Status')]
        [string]$Mode,
        [Parameter(Mandatory)]
        [psobject]$Contract
    )

    [pscustomobject]@{
        Mode = $Mode
        ServiceName = $Contract.ServiceName
        ProductId = $Contract.ProductId
        RequiresAdministrator = $Mode -ne 'Status'
        RequiresTrustedPackage = $Mode -ne 'Status'
        AllowsUnknownResourceDeletion = $false
        AllowsRmsProductServiceControl = $false
        AllowsBrowserPathOrUrl = $false
        RollbackOnInterruptedUpdate = $Mode -in @('Upgrade', 'Repair')
        Idempotent = $true
        Silent = $true
        NonInteractive = $true
    }
}

Export-ModuleMember -Function @(
    'Get-RmsSupportAgentDeploymentContract',
    'Test-RmsSupportAgentPackageManifest',
    'Test-RmsSupportAgentServiceOwnership',
    'Get-RmsSupportAgentMigrationPlan',
    'Get-RmsSupportAgentBrowserPolicyPlan',
    'Get-RmsSupportAgentCertificatePlan',
    'Get-RmsSupportAgentLifecyclePlan'
)
