Set-StrictMode -Version Latest

$script:PermanentServiceName = 'RmsSupportAgent'
$script:PermanentDisplayName = 'RMS Support Agent'
$script:PermanentDescription = 'Local diagnostics, maintenance and repair agent for RMS Support Hub'
$script:ProductId = 'RmsSupportAgent'
# The product ID is the permanent resource marker. It is intentionally shared
# with the typed Agent ownership policy so a name-only match can never pass.
$script:OwnershipMarker = $script:ProductId
$script:CanonicalAgentHost = 'rms-pos-agent.localhost'
$script:CanonicalAgentOrigin = 'https://rms-pos-agent.localhost:5001'
$script:MutationLeaseName = 'Global\RmsSupportHub.Pos.Agent.PrivilegedMutationLease'
$script:AllowedExecutableNames = @('RmsSupportHub.Pos.Agent.exe', 'RmsSupportAgent.exe')

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
        TrustRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Trust'
        TrustConfigurationPath = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Trust\package-trust.json'
        CertificateConfigurationPath = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Trust\agent-certificate.json'
        StatePath = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Packages\lifecycle-state.json'
        AvailableRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Packages\available'
        RollbackRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Packages\rollback'
        RecoveryRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Packages\recovery'
        StagingRoot = Join-Path $env:ProgramData 'DBS\RmsSupportAgent\Packages\staging'
        InstalledManifestPath = Join-Path $env:ProgramFiles 'DBS\RmsSupportAgent\manifest.json'
        CanonicalAgentOrigin = $script:CanonicalAgentOrigin
        MutationLeaseName = $script:MutationLeaseName
        AllowedExecutableNames = $script:AllowedExecutableNames
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
        [string]$CurrentArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant(),
        [switch]$CryptographicSignature
    )

    $blockers = [System.Collections.Generic.List[string]]::new()
    foreach ($property in @('SchemaVersion', 'ProductId', 'Version', 'ServiceName', 'ServiceDisplayName', 'ServiceDescription', 'ServiceIdentity', 'Architecture', 'ReleaseChannel', 'Environment', 'PackageSha256', 'Files')) {
        if ($null -eq $Manifest.PSObject.Properties[$property]) { [void]$blockers.Add("missing_$($property.ToLowerInvariant())") }
    }
    $environment = if ($null -ne $Manifest.PSObject.Properties['Environment']) { [string]$Manifest.Environment } else { $null }

    if ($Manifest.SchemaVersion -ne 1) { [void]$blockers.Add('schema_version_invalid') }
    if ($Manifest.ProductId -ne $script:ProductId) { [void]$blockers.Add('product_identity_invalid') }
    if (-not (Test-RmsSafeToken $Manifest.Version) -or [string]$Manifest.Version -notmatch '^(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})$') { [void]$blockers.Add('version_invalid') }
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
    $hasAgentExecutable = $false
    foreach ($file in $files) {
        if ($null -eq $file -or -not (Test-RmsSafeRelativePackagePath $file.RelativePath) -or -not $seen.Add($file.RelativePath.Replace('\', '/'))) { [void]$blockers.Add('file_path_invalid'); continue }
        if ($file.RelativePath.Replace('\', '/') -notmatch '/' -and $script:AllowedExecutableNames -contains $file.RelativePath) { $hasAgentExecutable = $true }
        if ($file.SizeBytes -lt 0 -or $file.SizeBytes -gt 67108864) { [void]$blockers.Add('file_size_invalid') }
        if (-not (Test-RmsSafeSha256 $file.Sha256)) { [void]$blockers.Add('file_hash_invalid') }
    }
    if (-not $hasAgentExecutable) { [void]$blockers.Add('agent_executable_missing') }

    $signatureVerified = $false
    if ($null -ne $Manifest.PSObject.Properties['SignatureVerified']) { $signatureVerified = [bool]$Manifest.SignatureVerified }
    $hasCanonicalSignature = $null -ne $Manifest.PSObject.Properties['Signature']
    if ($Channel -eq 'Production' -and -not $CryptographicSignature -and (-not $signatureVerified -or [string]::IsNullOrWhiteSpace([string]$Manifest.Signer))) { [void]$blockers.Add('production_signature_required') }
    if ($Channel -eq 'Production' -and $CryptographicSignature -and -not $hasCanonicalSignature) { [void]$blockers.Add('production_signature_required') }
    if ($Channel -eq 'Testing' -and -not $CryptographicSignature -and $signatureVerified -and [string]::IsNullOrWhiteSpace([string]$Manifest.Signer)) { [void]$blockers.Add('signer_metadata_invalid') }

    if ($CryptographicSignature) {
        foreach ($property in @('PackageId', 'SupportedOperatingSystem', 'SupportedRuntime', 'ServiceName', 'ScmName', 'SignatureAlgorithm', 'SignerDisplayName', 'Signature', 'PackageSizeBytes', 'AclRequirements', 'CertificateRequirements')) {
            if ($null -eq $Manifest.PSObject.Properties[$property]) { [void]$blockers.Add("missing_$($property.ToLowerInvariant())") }
        }
        if ($null -ne $Manifest.PSObject.Properties['ScmName'] -and $Manifest.ScmName -ne $script:PermanentServiceName) { [void]$blockers.Add('scm_name_invalid') }
        if ($Manifest.SupportedOperatingSystem -ne 'Windows') { [void]$blockers.Add('os_not_supported') }
        if ($Manifest.SupportedRuntime -ne 'net10.0-windows') { [void]$blockers.Add('runtime_not_supported') }
        if ($Manifest.SignatureAlgorithm -ne 'SHA256withRSA') { [void]$blockers.Add('signature_algorithm_invalid') }
        $signerDisplayName = [string]$Manifest.SignerDisplayName
        if ([string]::IsNullOrWhiteSpace($signerDisplayName) -or $signerDisplayName.Length -gt 128) { [void]$blockers.Add('signer_invalid') }
        if ([string]$Manifest.PackageSizeBytes -notmatch '^[1-9][0-9]{0,11}$' -or [int64]$Manifest.PackageSizeBytes -gt 268435456) { [void]$blockers.Add('package_size_invalid') }
        $signatureText = [string]$Manifest.Signature
        if ($signatureText -notmatch '^[A-Za-z0-9+/]+={0,2}$' -or $signatureText.Length % 4 -ne 0) { [void]$blockers.Add('signature_invalid') }
    }

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

function Get-RmsSupportAgentServiceEvidence {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Contract)

    if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) { return $null }
    try {
        $service = Get-CimInstance -ClassName Win32_Service -Filter "Name='$($script:PermanentServiceName)'" -ErrorAction Stop
        if ($null -eq $service) { return $null }
        $binaryPath = [string]$service.PathName
        $executable = $binaryPath.Trim().Trim('"')
        $manifest = if (Test-Path -LiteralPath $Contract.InstalledManifestPath -PathType Leaf) { try { Get-Content -Raw -LiteralPath $Contract.InstalledManifestPath | ConvertFrom-Json } catch { $null } } else { $null }
        [pscustomobject]@{
            ServiceName = [string]$service.Name
            DisplayName = [string]$service.DisplayName
            Description = [string]$service.Description
            BinaryPath = $executable
            ServiceAccount = [string]$service.StartName
            StartMode = [string]$service.StartMode
            State = [string]$service.State
            HasArguments = $(
                $trimmed = $binaryPath.Trim()
                if ($trimmed.StartsWith('"')) {
                    $closingQuote = $trimmed.IndexOf('"', 1)
                    $closingQuote -lt 0 -or $trimmed.Substring($closingQuote + 1).Trim().Length -gt 0
                } else {
                    @($trimmed.Split([char[]]@(' ', "`t"), [StringSplitOptions]::RemoveEmptyEntries)).Count -gt 1
                }
            )
            PackageId = if ($null -ne $manifest) { [string]$manifest.PackageId } else { $null }
            PackageVersion = if ($null -ne $manifest) { [string]$manifest.Version } else { $null }
            OwnershipMarker = if ($null -ne $manifest) { [string]$manifest.ProductId } else { $null }
            BinarySha256 = if (Test-Path -LiteralPath $executable -PathType Leaf) { (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash } else { $null }
        }
    } catch {
        return $null
    }
}

function Test-RmsSupportAgentInstalledIntegrity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Contract,
        [Parameter(Mandatory)][psobject]$Manifest
    )

    $result = Test-RmsSupportAgentPackageManifest -Manifest $Manifest -Channel ([string]$Manifest.ReleaseChannel) -CryptographicSignature
    if (-not $result.Valid) { return $result }
    $root = [IO.Path]::GetFullPath($Contract.InstallRoot).TrimEnd('\')
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_root_unavailable'); Detail = 'The fixed Agent installation root is unavailable.' } }
    $actual = @{}
    try {
        foreach ($item in Get-ChildItem -LiteralPath $root -Recurse -Force) {
            if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_reparse_point'); Detail = 'The installed Agent tree contains a reparse point.' } }
        }
        foreach ($path in Get-ChildItem -LiteralPath $root -Recurse -Force -File) {
            if ($path.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_reparse_point'); Detail = 'The installed Agent tree contains a reparse point.' } }
            $relative = $path.FullName.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'
            if ($relative -eq 'manifest.json') { continue }
            $actual[$relative] = $path
        }
        $expected = @{}
        foreach ($file in @($Manifest.Files)) { $expected[$file.RelativePath.Replace('\', '/')] = $file }
        if ($actual.Keys.Count -ne $expected.Keys.Count) { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_file_set_mismatch'); Detail = 'The installed Agent file set differs from the signed manifest.' } }
        foreach ($key in $expected.Keys) {
            if (-not $actual.ContainsKey($key)) { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_file_set_mismatch'); Detail = 'The installed Agent file set differs from the signed manifest.' } }
            $file = $actual[$key]
            if ($file.Length -ne [int64]$expected[$key].SizeBytes -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne [string]$expected[$key].Sha256) { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_file_hash_mismatch'); Detail = 'An installed Agent file differs from its signed hash or size.' } }
        }
        return [pscustomobject]@{ Valid = $true; Blockers = @(); Detail = 'The fixed installed Agent tree matches its package manifest.' }
    } catch { return [pscustomobject]@{ Valid = $false; Blockers = @('installed_integrity_unknown'); Detail = 'Installed Agent integrity could not be verified safely.' } }
}

function ConvertTo-RmsCanonicalField {
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        [Parameter(Mandatory)][string]$Name,
        [AllowEmptyString()][string]$Value
    )

    if ($null -eq $Value) { $Value = '' }
    $byteCount = [Text.Encoding]::UTF8.GetByteCount($Value)
    [void]$Builder.Append($Name).Append('|').Append($byteCount.ToString([Globalization.CultureInfo]::InvariantCulture)).Append(':').Append($Value).Append("`n")
}

function Sort-RmsOrdinalStrings {
    param([AllowNull()][string[]]$Values)

    $copy = @($Values | ForEach-Object { [string]$_ })
    if ($copy.Count -gt 1) { [Array]::Sort($copy, [StringComparer]::Ordinal) }
    return @($copy)
}

function ConvertTo-RmsInvariantInteger {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) { return '' }
    return ([int64]$Value).ToString([Globalization.CultureInfo]::InvariantCulture)
}

function Get-RmsCanonicalPackagePayloadText {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Manifest)

    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append('RmsSupportAgent.PackageEnvelope.v1').Append("`n")
    ConvertTo-RmsCanonicalField $builder 'schemaVersion' (ConvertTo-RmsInvariantInteger $Manifest.SchemaVersion)
    ConvertTo-RmsCanonicalField $builder 'productId' ([string]$Manifest.ProductId)
    ConvertTo-RmsCanonicalField $builder 'packageId' ([string]$Manifest.PackageId)
    ConvertTo-RmsCanonicalField $builder 'version' ([string]$Manifest.Version)
    ConvertTo-RmsCanonicalField $builder 'supportedOperatingSystem' ([string]$Manifest.SupportedOperatingSystem)
    ConvertTo-RmsCanonicalField $builder 'supportedRuntime' ([string]$Manifest.SupportedRuntime)
    ConvertTo-RmsCanonicalField $builder 'serviceDisplayName' ([string]$Manifest.ServiceDisplayName)
    ConvertTo-RmsCanonicalField $builder 'serviceDescription' ($(if ($null -ne $Manifest.ServiceDescription) { [string]$Manifest.ServiceDescription } else { 'Local diagnostics, maintenance and repair agent for RMS Support Hub' }))
    ConvertTo-RmsCanonicalField $builder 'serviceIdentity' ([string]$Manifest.ServiceIdentity)
    ConvertTo-RmsCanonicalField $builder 'scmName' ([string]$Manifest.ScmName)
    ConvertTo-RmsCanonicalField $builder 'signatureAlgorithm' ([string]$Manifest.SignatureAlgorithm)
    ConvertTo-RmsCanonicalField $builder 'signerDisplayName' ([string]$Manifest.SignerDisplayName)
    ConvertTo-RmsCanonicalField $builder 'architecture' ([string]$Manifest.Architecture)
    ConvertTo-RmsCanonicalField $builder 'releaseChannel' ([string]$Manifest.ReleaseChannel)
    ConvertTo-RmsCanonicalField $builder 'environment' ([string]$Manifest.Environment)
    ConvertTo-RmsCanonicalField $builder 'packageSha256' ([string]$Manifest.PackageSha256).ToLowerInvariant()
    ConvertTo-RmsCanonicalField $builder 'packageSizeBytes' (ConvertTo-RmsInvariantInteger $Manifest.PackageSizeBytes)
    ConvertTo-RmsCanonicalField $builder 'previousVersion' ([string]$Manifest.PreviousVersion)
    ConvertTo-RmsCanonicalField $builder 'rollbackAvailable' ($(if ([bool]$Manifest.RollbackAvailable) { 'true' } else { 'false' }))

    $files = @($Manifest.Files | ForEach-Object { $_ })
    if ($files.Count -gt 1) {
        for ($outer = 1; $outer -lt $files.Count; $outer++) {
            $candidate = $files[$outer]
            $candidatePath = ([string]$candidate.RelativePath).Replace('\', '/')
            $candidateLogical = [string]$candidate.LogicalName
            $inner = $outer - 1
            while ($inner -ge 0) {
                $existingPath = ([string]$files[$inner].RelativePath).Replace('\', '/')
                $comparison = [StringComparer]::Ordinal.Compare($existingPath, $candidatePath)
                if ($comparison -eq 0) { $comparison = [StringComparer]::Ordinal.Compare([string]$files[$inner].LogicalName, $candidateLogical) }
                if ($comparison -le 0) { break }
                $files[$inner + 1] = $files[$inner]
                $inner--
            }
            $files[$inner + 1] = $candidate
        }
    }
    ConvertTo-RmsCanonicalField $builder 'files.count' (ConvertTo-RmsInvariantInteger $files.Count)
    for ($index = 0; $index -lt $files.Count; $index++) {
        $file = $files[$index]
        $prefix = "files[$index]"
        ConvertTo-RmsCanonicalField $builder "$prefix.logicalName" ([string]$file.LogicalName)
        ConvertTo-RmsCanonicalField $builder "$prefix.relativePath" ([string]$file.RelativePath).Replace('\', '/')
        ConvertTo-RmsCanonicalField $builder "$prefix.sizeBytes" (ConvertTo-RmsInvariantInteger $file.SizeBytes)
        ConvertTo-RmsCanonicalField $builder "$prefix.sha256" ([string]$file.Sha256).ToLowerInvariant()
        ConvertTo-RmsCanonicalField $builder "$prefix.required" ($(if ([bool]$file.Required) { 'true' } else { 'false' }))
    }

    foreach ($listName in @('aclRequirements', 'certificateRequirements')) {
        $values = @(Sort-RmsOrdinalStrings @($Manifest.$listName))
        ConvertTo-RmsCanonicalField $builder "$listName.count" (ConvertTo-RmsInvariantInteger $values.Count)
        for ($index = 0; $index -lt $values.Count; $index++) {
            ConvertTo-RmsCanonicalField $builder "$listName[$index]" ([string]$values[$index])
        }
    }

    return $builder.ToString()
}

function Get-RmsCanonicalPackagePayload {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Manifest)

    return ,([Text.Encoding]::UTF8.GetBytes((Get-RmsCanonicalPackagePayloadText -Manifest $Manifest)))
}

function Test-RmsSupportAgentControlFileTrust {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    # Machine-owned trust control files (package-trust.json, agent-certificate.json) are never
    # provisioned by this application. Their ownership/ACL boundary must be verified BEFORE any
    # value inside them is trusted -- a writable configuration must never become authority. This is
    # read-only: it never mutates an ACL to make an untrusted boundary pass.
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
        $directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
        if ([IO.File]::GetAttributes($Path).HasFlag([IO.FileAttributes]::ReparsePoint) -or
            [IO.File]::GetAttributes($directory).HasFlag([IO.FileAttributes]::ReparsePoint)) { return $false }

        # The same narrow %TEMP%-scoped escape hatch the C# ServiceOwnedDirectoryProvisioner grants a
        # disposable test fixture: it never widens what production accepts (real control files live
        # under %ProgramData%\DBS, never %TEMP%), and only applies when the boundary is owned by the
        # current identity with an otherwise-protected, otherwise-untrusted-rule-free ACL.
        $temporaryTestRoot = [IO.Path]::GetFullPath($env:TEMP)
        $allowTemporaryTestIdentity = [IO.Path]::GetFullPath($directory).StartsWith($temporaryTestRoot, [StringComparison]::OrdinalIgnoreCase)
        $currentSid = if ($allowTemporaryTestIdentity) { ([Security.Principal.WindowsIdentity]::GetCurrent()).User } else { $null }

        $acl = Get-Acl -LiteralPath $directory
        $administrators = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
        $system = New-Object Security.Principal.SecurityIdentifier([Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
        $owner = $acl.GetOwner([Security.Principal.SecurityIdentifier])
        $ownerTrusted = ($owner -eq $administrators -or $owner -eq $system) -or ($allowTemporaryTestIdentity -and $owner -eq $currentSid)
        if (-not $ownerTrusted -or -not $acl.AreAccessRulesProtected) { return $false }

        foreach ($rule in $acl.Access) {
            if ($rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow) { continue }
            $identity = try { $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]) } catch { $null }
            if ($null -eq $identity) { return $false }
            if ($identity -eq $administrators -or $identity -eq $system) { continue }
            if ($allowTemporaryTestIdentity -and $identity -eq $currentSid) { continue }
            return $false
        }
        return $true
    } catch { return $false }
}

function Get-RmsSupportAgentSignerCertificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Testing', 'Production')][string]$Channel,
        [Parameter(Mandatory)][psobject]$Contract
    )

    if (-not (Test-Path -LiteralPath $Contract.TrustConfigurationPath -PathType Leaf)) { return $null }
    if ([IO.File]::GetAttributes($Contract.TrustConfigurationPath).HasFlag([IO.FileAttributes]::ReparsePoint)) { return $null }
    if ((Get-Item -LiteralPath $Contract.TrustConfigurationPath).Length -gt 16KB) { return $null }
    if (-not (Test-RmsSupportAgentControlFileTrust -Path $Contract.TrustConfigurationPath)) { return $null }

    try { $trust = Get-Content -Raw -LiteralPath $Contract.TrustConfigurationPath | ConvertFrom-Json } catch { return $null }
    $property = if ($Channel -eq 'Production') { 'productionSignerThumbprint' } else { 'testingSignerThumbprint' }
    $thumbprint = if ($null -ne $trust.PSObject.Properties[$property]) { [string]$trust.$property } else { $null }
    if ([string]::IsNullOrWhiteSpace($thumbprint)) { return $null }
    $thumbprint = ($thumbprint -replace '\s', '').ToUpperInvariant()
    if ($thumbprint -notmatch '^[0-9A-F]{40}$') { return $null }
    if (-not (Test-Path -LiteralPath ("Cert:\LocalMachine\My\$thumbprint") -PathType Leaf)) { return $null }

    try {
        $certificate = Get-Item -LiteralPath ("Cert:\LocalMachine\My\$thumbprint")
        if ($certificate.Thumbprint -replace '\s' -ne $thumbprint) { return $null }
        $now = [DateTime]::UtcNow
        if ($certificate.NotBefore.ToUniversalTime() -gt $now -or $certificate.NotAfter.ToUniversalTime() -lt $now) { return $null }
        $eku = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.5.5.7.3.3' }
        if ($null -eq $eku) { return $null }
        $keyUsage = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.15' })
        if ($keyUsage.Count -eq 1 -and -not ([Security.Cryptography.X509Certificates.X509KeyUsageExtension]$keyUsage[0]).KeyUsages.HasFlag([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature)) { return $null }
        $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
        try {
            $chain.ChainPolicy.RevocationMode = [Security.Cryptography.X509Certificates.X509RevocationMode]::Online
            $chain.ChainPolicy.RevocationFlag = [Security.Cryptography.X509Certificates.X509RevocationFlag]::ExcludeRoot
            $chain.ChainPolicy.VerificationFlags = [Security.Cryptography.X509Certificates.X509VerificationFlags]::NoFlag
            if (-not $chain.Build($certificate)) { return $null }
        } finally { $chain.Dispose() }
        return $certificate
    } catch { return $null }
}

function Test-RmsSupportAgentPackageTrust {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Testing', 'Production')][string]$Channel,
        [Parameter(Mandatory)][psobject]$Contract,
        [switch]$Rollback,
        [string]$SlotRoot
    )

    $blockers = [System.Collections.Generic.List[string]]::new()
    # SlotRoot lets automatic rollback/recovery point this same trust check at an arbitrary retained
    # slot (RollbackRoot or RecoveryRoot) rather than only the two fixed roots -Rollback distinguishes.
    $effectiveRoot = if ($SlotRoot) { $SlotRoot } elseif ($Rollback) { $Contract.RollbackRoot } else { $Contract.AvailableRoot }
    $manifestPath = Join-Path $effectiveRoot 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf) -or [IO.File]::GetAttributes($manifestPath).HasFlag([IO.FileAttributes]::ReparsePoint)) {
        return [pscustomobject]@{ Valid = $false; Blockers = @('package_manifest_unavailable'); Manifest = $null; ArchivePath = $null; SignerThumbprint = $null }
    }

    try { $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json } catch { return [pscustomobject]@{ Valid = $false; Blockers = @('package_manifest_invalid'); Manifest = $null; ArchivePath = $null; SignerThumbprint = $null } }
    try { $policy = Test-RmsSupportAgentPackageManifest -Manifest $manifest -Channel $Channel -CryptographicSignature } catch { return [pscustomobject]@{ Valid = $false; Blockers = @('package_manifest_invalid'); Manifest = $manifest; ArchivePath = $null; SignerThumbprint = $null } }
    $blockers.AddRange([string[]]@($policy.Blockers))
    if (-not $policy.Valid) {
        return [pscustomobject]@{ Valid = $false; Blockers = @($blockers | Select-Object -Unique); Manifest = $manifest; ArchivePath = $null; SignerThumbprint = $null }
    }
    $archiveName = "$($manifest.PackageId)-$($manifest.Version).zip"
    $archivePath = Join-Path $effectiveRoot $archiveName
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf) -or [IO.File]::GetAttributes($archivePath).HasFlag([IO.FileAttributes]::ReparsePoint)) { [void]$blockers.Add('package_artifact_unavailable') }
    else {
        $archive = Get-Item -LiteralPath $archivePath
        if ($archive.Length -ne [int64]$manifest.PackageSizeBytes) { [void]$blockers.Add('package_size_mismatch') }
        if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne ([string]$manifest.PackageSha256).ToLowerInvariant()) { [void]$blockers.Add('package_checksum_mismatch') }
    }

    $certificate = if ($blockers.Count -eq 0) { Get-RmsSupportAgentSignerCertificate -Channel $Channel -Contract $Contract } else { $null }
    if ($null -eq $certificate) { [void]$blockers.Add('signer_trust_unavailable') }
    else {
        try {
            $signatureText = [string]$manifest.Signature
            if ([string]::IsNullOrWhiteSpace($signatureText) -or $signatureText.Length % 4 -ne 0 -or $signatureText -notmatch '^[A-Za-z0-9+/]+={0,2}$') { throw 'signature_invalid' }
            $signature = [Convert]::FromBase64String($signatureText)
            $payload = Get-RmsCanonicalPackagePayload -Manifest $manifest
            $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($certificate)
            try {
                if (-not $rsa.VerifyData($payload, $signature, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)) { [void]$blockers.Add('package_signature_mismatch') }
            } finally { if ($null -ne $rsa) { $rsa.Dispose() } }
        } catch { [void]$blockers.Add('package_signature_invalid') }
    }

    [pscustomobject]@{
        Valid = $blockers.Count -eq 0
        Blockers = @($blockers | Select-Object -Unique)
        Manifest = $manifest
        ArchivePath = $archivePath
        SignerThumbprint = if ($null -ne $certificate) { $certificate.Thumbprint } else { $null }
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

function Enter-RmsSupportAgentMutationLease {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Contract)

    if ([string]::IsNullOrWhiteSpace($Contract.MutationLeaseName)) { return [pscustomobject]@{ Acquired = $false; Code = 'lease_unavailable'; Handle = $null } }
    try {
        $created = $false
        $semaphore = [Threading.Semaphore]::new(1, 1, [string]$Contract.MutationLeaseName, [ref]$created)
        if (-not $semaphore.WaitOne(0)) {
            $semaphore.Dispose()
            return [pscustomobject]@{ Acquired = $false; Code = 'busy'; Handle = $null }
        }
        return [pscustomobject]@{ Acquired = $true; Code = 'acquired'; Handle = $semaphore }
    } catch {
        return [pscustomobject]@{ Acquired = $false; Code = 'lease_unavailable'; Handle = $null }
    }
}

function Exit-RmsSupportAgentMutationLease {
    [CmdletBinding()]
    param([AllowNull()][psobject]$Lease)

    if ($null -ne $Lease -and $null -ne $Lease.Handle) {
        try { $Lease.Handle.Release() | Out-Null } catch { }
        try { $Lease.Handle.Dispose() } catch { }
    }
}

function Get-RmsSupportAgentLifecycleState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Contract)

    if (-not (Test-Path -LiteralPath $Contract.StatePath -PathType Leaf)) { return $null }
    if ([IO.File]::GetAttributes($Contract.StatePath).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The lifecycle checkpoint is a reparse point.' }
    if ((Get-Item -LiteralPath $Contract.StatePath).Length -gt 64KB) { throw 'The lifecycle checkpoint exceeds its bounded size.' }
    try { return Get-Content -Raw -LiteralPath $Contract.StatePath | ConvertFrom-Json } catch { throw 'The lifecycle checkpoint could not be parsed safely.' }
}

function Write-RmsSupportAgentLifecycleState {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Contract,
        [Parameter(Mandatory)][psobject]$State
    )

    $root = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Contract.StatePath))
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { New-Item -ItemType Directory -Path $root -Force | Out-Null }
    $temporary = Join-Path $root ('.lifecycle-state.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        $State | ConvertTo-Json -Depth 8 -Compress | Set-Content -LiteralPath $temporary -Encoding UTF8
        if ([IO.File]::GetAttributes($temporary).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The lifecycle checkpoint became a reparse point.' }
        Move-Item -LiteralPath $temporary -Destination $Contract.StatePath -Force
    } finally { if (Test-Path -LiteralPath $temporary -PathType Leaf) { Remove-Item -LiteralPath $temporary -Force } }
}

function Clear-RmsSupportAgentLifecycleState {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Contract)

    if (Test-Path -LiteralPath $Contract.StatePath -PathType Leaf) {
        if ([IO.File]::GetAttributes($Contract.StatePath).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The lifecycle checkpoint is a reparse point.' }
        Remove-Item -LiteralPath $Contract.StatePath -Force
    }
}

function Test-RmsSupportAgentCertificatePrerequisite {
    [CmdletBinding()]
    param([Parameter(Mandatory)][psobject]$Contract)

    if (-not (Test-Path -LiteralPath $Contract.CertificateConfigurationPath -PathType Leaf)) { return [pscustomobject]@{ Valid = $false; Code = 'certificate_thumbprint_unconfigured' } }
    if ([IO.File]::GetAttributes($Contract.CertificateConfigurationPath).HasFlag([IO.FileAttributes]::ReparsePoint)) { return [pscustomobject]@{ Valid = $false; Code = 'certificate_configuration_reparse_point' } }
    if (-not (Test-RmsSupportAgentControlFileTrust -Path $Contract.CertificateConfigurationPath)) { return [pscustomobject]@{ Valid = $false; Code = 'certificate_configuration_untrusted_boundary' } }
    try { $config = Get-Content -Raw -LiteralPath $Contract.CertificateConfigurationPath | ConvertFrom-Json } catch { return [pscustomobject]@{ Valid = $false; Code = 'certificate_configuration_invalid' } }
    $thumbprint = if ($null -ne $config.PSObject.Properties['certificateThumbprint']) { ([string]$config.certificateThumbprint -replace '\s', '').ToUpperInvariant() } else { $null }
    if ($thumbprint -notmatch '^[0-9A-F]{40}$') { return [pscustomobject]@{ Valid = $false; Code = 'certificate_thumbprint_invalid' } }
    try {
        $certificate = Get-Item -LiteralPath ("Cert:\LocalMachine\My\$thumbprint") -ErrorAction Stop
        if (-not $certificate.HasPrivateKey -or $certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or $certificate.NotAfter.ToUniversalTime() -lt [DateTime]::UtcNow) { return [pscustomobject]@{ Valid = $false; Code = 'certificate_expired_or_private_key_invalid' } }
        if (@($certificate.DnsNameList | Where-Object { $_.Unicode -eq $script:CanonicalAgentHost }).Count -ne 1) { return [pscustomobject]@{ Valid = $false; Code = 'certificate_san_invalid' } }
        $eku = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.5.5.7.3.1' }
        if ($null -eq $eku) { return [pscustomobject]@{ Valid = $false; Code = 'server_authentication_eku_missing' } }
        $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
        if ($null -eq $rsa -or $rsa.GetType().Name -ne 'RSACng') { return [pscustomobject]@{ Valid = $false; Code = 'certificate_provider_invalid' } }
        try {
            $cng = [Security.Cryptography.RSACng]$rsa
            if ($null -eq $cng.Key -or $cng.Key.Provider.Provider -ne 'Microsoft Software Key Storage Provider') { return [pscustomobject]@{ Valid = $false; Code = 'certificate_provider_invalid' } }
            if ($cng.Key.ExportPolicy.HasFlag([Security.Cryptography.CngExportPolicies]::AllowExport)) { return [pscustomobject]@{ Valid = $false; Code = 'private_key_exportable' } }
        } finally { $rsa.Dispose() }
        $marker = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.4.1.55555.1.1' })
        if ($marker.Count -ne 1 -or [Text.Encoding]::UTF8.GetString($marker[0].RawData) -notin @('RmsSupportAgent certificate v1', 'RmsSupportAgent enterprise-managed certificate v1')) { return [pscustomobject]@{ Valid = $false; Code = 'certificate_ownership_unproven' } }
        return [pscustomobject]@{ Valid = $true; Code = 'certificate_ready' }
    } catch { return [pscustomobject]@{ Valid = $false; Code = 'certificate_prerequisite_unknown' } }
}

function Test-RmsSupportAgentHealth {
    [CmdletBinding()]
    param()

    try {
        foreach ($path in @('/health/live', '/health/ready')) {
            $response = Invoke-WebRequest -UseBasicParsing -Uri ($script:CanonicalAgentOrigin + $path) -Method Get -TimeoutSec 5 -MaximumRedirection 0 -ErrorAction Stop
            if ($response.StatusCode -ne 200) { return $false }
        }
        return $true
    } catch { return $false }
}

function Expand-RmsSupportAgentPackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][psobject]$Manifest
    )

    $expected = @{}
    foreach ($file in @($Manifest.Files)) { $expected[$file.RelativePath.Replace('\', '/')] = $file }
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        if ($archive.Entries.Count -ne $expected.Count) { throw 'The package archive file set is not exact.' }
        foreach ($entry in $archive.Entries) {
            $relative = $entry.FullName.Replace('\', '/')
            if ($relative.StartsWith('/') -or @($relative.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0 -or -not $seen.Add($relative) -or -not $expected.ContainsKey($relative)) { throw 'The package archive contains an unsafe path.' }
            $target = [IO.Path]::GetFullPath((Join-Path $Destination $relative))
            $destinationFull = [IO.Path]::GetFullPath($Destination)
            $root = if ($destinationFull.EndsWith('\')) { $destinationFull } else { $destinationFull + '\' }
            if (-not $target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'The package archive escaped the staging root.' }
            $directory = [IO.Path]::GetDirectoryName($target)
            if ((Test-Path -LiteralPath $directory -PathType Container) -and [IO.File]::GetAttributes($directory).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The package staging directory is a reparse point.' }
            if (-not (Test-Path -LiteralPath $directory -PathType Container)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
            $stream = $entry.Open()
            try { $fileStream = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None); try { $stream.CopyTo($fileStream) } finally { $fileStream.Dispose() } } finally { $stream.Dispose() }
            $info = Get-Item -LiteralPath $target
            if ($info.Length -ne [int64]$expected[$relative].SizeBytes -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne [string]$expected[$relative].Sha256) { throw 'The staged file hash or size does not match the manifest.' }
        }
        if ($seen.Count -ne $expected.Count) { throw 'The package archive omitted a manifest file.' }
    } finally { $archive.Dispose() }
}

function Copy-RmsSupportAgentOwnedDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw 'The owned source directory is unavailable.' }
    foreach ($item in Get-ChildItem -LiteralPath $Source -Recurse -Force) {
        if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'A reparse point was found in an owned lifecycle tree.' }
    }
    if (Test-Path -LiteralPath $Destination -PathType Container) {
        if ([IO.File]::GetAttributes($Destination).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The owned destination tree is a reparse point.' }
        foreach ($item in Get-ChildItem -LiteralPath $Destination -Recurse -Force) {
            if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'A reparse point was found in the owned destination tree.' }
        }
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $children = @(Get-ChildItem -LiteralPath $Source -Force)
    foreach ($child in $children) { Copy-Item -LiteralPath $child.FullName -Destination $Destination -Recurse -Force }
    foreach ($item in Get-ChildItem -LiteralPath $Destination -Recurse -Force) {
        if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'A copied lifecycle tree contains a reparse point.' }
    }
}

function Set-RmsSupportAgentOwnedAcl {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'The Agent ACL boundary is Windows-only.' }
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null }
    if ([IO.File]::GetAttributes($Path).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The Agent ACL target is a reparse point.' }

    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    $inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
    $propagation = [Security.AccessControl.PropagationFlags]::None
    $allow = [Security.AccessControl.AccessControlType]::Allow
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
        $rule = New-Object Security.AccessControl.FileSystemAccessRule(
            (New-Object Security.Principal.SecurityIdentifier($sid)),
            [Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance,
            $propagation,
            $allow)
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Ensure-RmsSupportAgentService {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ExecutablePath)

    $fullPath = [IO.Path]::GetFullPath($ExecutablePath)
    $root = [IO.Path]::GetFullPath((Get-RmsSupportAgentDeploymentContract).InstallRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($fullPath) -notin $script:AllowedExecutableNames) { throw 'The Agent executable is outside the fixed product identity.' }
    $binaryPath = '"' + $fullPath + '"'
    $serviceContract = Get-RmsSupportAgentDeploymentContract
    $existing = Get-RmsSupportAgentServiceEvidence -Contract $serviceContract
    if ($null -eq $existing) {
        New-Service -Name $script:PermanentServiceName -DisplayName $script:PermanentDisplayName -StartupType Automatic -BinaryPathName $binaryPath -ErrorAction Stop | Out-Null
    }

    $sc = Join-Path $env:SystemRoot 'System32\sc.exe'
    $config = Start-Process -FilePath $sc -ArgumentList @('config', $script:PermanentServiceName, 'binPath=', $binaryPath, 'start=', 'auto', 'obj=', 'LocalSystem', 'DisplayName=', $script:PermanentDisplayName) -Wait -PassThru -WindowStyle Hidden
    if ($config.ExitCode -ne 0) { throw 'The owned Agent service configuration could not be applied.' }
    $description = Start-Process -FilePath $sc -ArgumentList @('description', $script:PermanentServiceName, $script:PermanentDescription) -Wait -PassThru -WindowStyle Hidden
    if ($description.ExitCode -ne 0) { throw 'The owned Agent service description could not be applied.' }
    $failureActions = Start-Process -FilePath $sc -ArgumentList @('failure', $script:PermanentServiceName, 'reset=', '86400', 'actions=', 'restart/60000/restart/60000/restart/60000') -Wait -PassThru -WindowStyle Hidden
    if ($failureActions.ExitCode -ne 0) { throw 'The owned Agent service recovery policy could not be applied.' }
}

function Stop-RmsSupportAgentService {
    [CmdletBinding()]
    param()

    $service = Get-Service -Name $script:PermanentServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service -or $service.Status -eq 'Stopped') { return }
    Stop-Service -Name $script:PermanentServiceName -Force -ErrorAction Stop
    $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
}

function Start-RmsSupportAgentService {
    [CmdletBinding()]
    param()

    Start-Service -Name $script:PermanentServiceName -ErrorAction Stop
    $service = Get-Service -Name $script:PermanentServiceName -ErrorAction Stop
    $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
}

function Remove-RmsSupportAgentService {
    [CmdletBinding()]
    param()

    $service = Get-Service -Name $script:PermanentServiceName -ErrorAction SilentlyContinue
    if ($null -eq $service) { return }
    if ($service.Status -ne 'Stopped') { Stop-RmsSupportAgentService }
    $sc = Join-Path $env:SystemRoot 'System32\sc.exe'
    $result = Start-Process -FilePath $sc -ArgumentList @('delete', $script:PermanentServiceName) -Wait -PassThru -WindowStyle Hidden
    if ($result.ExitCode -ne 0) { throw 'The owned Agent service could not be removed.' }
}

function Save-RmsSupportAgentRetainedSlot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SlotRoot,
        [Parameter(Mandatory)][psobject]$Contract,
        [Parameter(Mandatory)][psobject]$Manifest
    )

    # Retains only the signed manifest and the already-verified available-package archive -- never a
    # raw copy of the live installation directory. Restoration always re-extracts and re-verifies
    # these exact bytes, so the retained slot itself carries no activation authority.
    try {
        if (Test-Path -LiteralPath $SlotRoot -PathType Container) {
            if ([IO.File]::GetAttributes($SlotRoot).HasFlag([IO.FileAttributes]::ReparsePoint)) { return $false }
            Get-ChildItem -LiteralPath $SlotRoot -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            New-Item -ItemType Directory -Path $SlotRoot -Force | Out-Null
        }

        $availableArchive = Join-Path $Contract.AvailableRoot "$($Manifest.PackageId)-$($Manifest.Version).zip"
        if (-not (Test-Path -LiteralPath $availableArchive -PathType Leaf) -or [IO.File]::GetAttributes($availableArchive).HasFlag([IO.FileAttributes]::ReparsePoint)) { return $false }
        Copy-Item -LiteralPath $availableArchive -Destination (Join-Path $SlotRoot ([IO.Path]::GetFileName($availableArchive))) -Force
        $Manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $SlotRoot 'manifest.json') -Encoding UTF8
        return $true
    } catch { return $false }
}

function Restore-RmsSupportAgentRetainedSlot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Contract,
        [Parameter(Mandatory)][ValidateSet('Install', 'Upgrade', 'Repair', 'Rollback')][string]$FailedOperation,
        [Parameter(Mandatory)][ValidateSet('Testing', 'Production')][string]$Channel
    )

    # Restores a retained package after a failed operation. A failed Install/Upgrade/Repair restores
    # the retained PREVIOUS package from RollbackRoot; a failed explicit Rollback restores the
    # retained CURRENT (pre-rollback) package from the separate bounded RecoveryRoot. Neither slot is
    # ever selected using the failed operation's own incoming manifest; the durable checkpoint's
    # PreviousVersion is the only trusted anchor for "what should be restored", and the exact bytes
    # activated are always re-extracted from the retained signed archive into a brand-new staging
    # directory and re-verified -- never copied from a raw persisted payload directory. Restoration is
    # not terminal until it passes the same live health gate as a forward activation; on any failure
    # the checkpoint is left in place as recovery evidence, never cleared.
    $restoreStage = $null
    try {
        $checkpoint = try { Get-RmsSupportAgentLifecycleState -Contract $Contract } catch { $null }
        if ($null -eq $checkpoint -or [string]::IsNullOrWhiteSpace([string]$checkpoint.PreviousVersion)) {
            return [pscustomobject]@{ Succeeded = $false; Code = 'rollback_target_unconfirmed' }
        }

        $slotRoot = if ($FailedOperation -eq 'Rollback') { $Contract.RecoveryRoot } else { $Contract.RollbackRoot }
        $trust = Test-RmsSupportAgentPackageTrust -Channel $Channel -Contract $Contract -SlotRoot $slotRoot
        if (-not $trust.Valid -or $null -eq $trust.Manifest -or [string]$trust.Manifest.Version -ne [string]$checkpoint.PreviousVersion) {
            return [pscustomobject]@{ Succeeded = $false; Code = 'rollback_trust_failed' }
        }

        $restoreStage = Join-Path $Contract.StagingRoot ('restore-' + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $restoreStage -Force | Out-Null
        Expand-RmsSupportAgentPackage -ArchivePath $trust.ArchivePath -Destination $restoreStage -Manifest $trust.Manifest
        $trust.Manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $restoreStage 'manifest.json') -Encoding UTF8

        $currentManifest = if (Test-Path -LiteralPath $Contract.InstalledManifestPath -PathType Leaf) { try { Get-Content -Raw -LiteralPath $Contract.InstalledManifestPath | ConvertFrom-Json } catch { $null } } else { $null }
        $service = Get-RmsSupportAgentServiceEvidence -Contract $Contract
        if ($null -ne $service -and ($null -eq $currentManifest -or -not (Test-RmsSupportAgentServiceOwnership -Evidence $service -Contract $Contract -ExpectedPackageId ([string]$currentManifest.PackageId) -ExpectedVersion ([string]$currentManifest.Version)).Owned)) {
            return [pscustomobject]@{ Succeeded = $false; Code = 'service_ownership_conflict' }
        }

        Stop-RmsSupportAgentService
        if (Test-Path -LiteralPath $Contract.InstallRoot -PathType Container) { Remove-Item -LiteralPath $Contract.InstallRoot -Recurse -Force }
        Copy-RmsSupportAgentOwnedDirectory -Source $restoreStage -Destination $Contract.InstallRoot

        $executable = @($trust.Manifest.Files | Where-Object { $_.RelativePath -notmatch '[\\/]' -and $script:AllowedExecutableNames -contains $_.RelativePath })
        if ($executable.Count -ne 1) { return [pscustomobject]@{ Succeeded = $false; Code = 'rollback_executable_invalid' } }
        Ensure-RmsSupportAgentService -ExecutablePath (Join-Path $Contract.InstallRoot $executable[0].RelativePath)

        $certificate = Test-RmsSupportAgentCertificatePrerequisite -Contract $Contract
        if (-not $certificate.Valid) { return [pscustomobject]@{ Succeeded = $false; Code = $certificate.Code } }

        Start-RmsSupportAgentService

        # Terminal success requires the same restored-manifest, ownership, running-status, certificate,
        # and live HTTPS health truth as any forward activation -- files copied and the service starting
        # are not, by themselves, a successful rollback.
        if (-not (Test-RmsSupportAgentHealth)) { return [pscustomobject]@{ Succeeded = $false; Code = 'agent_health_failed' } }

        Clear-RmsSupportAgentLifecycleState -Contract $Contract
        if ($FailedOperation -eq 'Rollback' -and (Test-Path -LiteralPath $Contract.RecoveryRoot -PathType Container)) {
            try { Get-ChildItem -LiteralPath $Contract.RecoveryRoot -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue } catch { }
        }
        return [pscustomobject]@{ Succeeded = $true; Code = 'rollback_succeeded'; Manifest = $trust.Manifest }
    } catch {
        return [pscustomobject]@{ Succeeded = $false; Code = 'rollback_unknown' }
    } finally {
        if ($null -ne $restoreStage -and (Test-Path -LiteralPath $restoreStage -PathType Container)) { Remove-Item -LiteralPath $restoreStage -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

function Add-RmsSupportAgentAuditEvent {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Contract,
        [Parameter(Mandatory)][string]$Operation,
        [Parameter(Mandatory)][string]$Outcome,
        [AllowEmptyString()][string]$PackageId,
        [AllowEmptyString()][string]$PackageVersion,
        [AllowEmptyString()][string]$FailureCode,
        [AllowEmptyString()][string]$RecoveryState
    )

    try {
        if (-not (Test-Path -LiteralPath $Contract.AuditRoot -PathType Container)) { New-Item -ItemType Directory -Path $Contract.AuditRoot -Force | Out-Null }
        if ([IO.File]::GetAttributes($Contract.AuditRoot).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The Agent audit root is a reparse point.' }
        $path = Join-Path $Contract.AuditRoot 'events.jsonl'
        if ((Test-Path -LiteralPath $path -PathType Leaf) -and [IO.File]::GetAttributes($path).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The Agent audit stream is a reparse point.' }
        $record = [ordered]@{
            atUtc = [DateTimeOffset]::UtcNow.ToString('O')
            operation = $Operation
            outcome = $Outcome
            packageId = if ($PackageId) { $PackageId } else { $null }
            packageVersion = if ($PackageVersion) { $PackageVersion } else { $null }
            failureCode = if ($FailureCode) { $FailureCode } else { $null }
            recoveryState = if ($RecoveryState) { $RecoveryState } else { $null }
        }
        $line = ($record | ConvertTo-Json -Compress) + [Environment]::NewLine
        if ((Test-Path -LiteralPath $path -PathType Leaf) -and (Get-Item -LiteralPath $path).Length + ([Text.Encoding]::UTF8.GetByteCount($line)) -gt 8MB) {
            $previous = $path + '.previous'
            if (Test-Path -LiteralPath $previous -PathType Leaf) { Remove-Item -LiteralPath $previous -Force }
            Move-Item -LiteralPath $path -Destination $previous -Force
        }
        if ([Text.Encoding]::UTF8.GetByteCount($line) -le 64KB) { Add-Content -LiteralPath $path -Value $line -Encoding UTF8 }
    } catch { }
}

function Invoke-RmsSupportAgentLifecycle {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('Install', 'Upgrade', 'Repair', 'Uninstall', 'Rollback', 'Status')][string]$Mode,
        [Parameter(Mandatory)][ValidateSet('Testing', 'Production')][string]$Channel
    )

    $contract = Get-RmsSupportAgentDeploymentContract -Channel $Channel
    if ($Mode -eq 'Status') {
        $state = try { Get-RmsSupportAgentLifecycleState -Contract $contract } catch { $null }
        $manifest = if (Test-Path -LiteralPath $contract.InstalledManifestPath -PathType Leaf) { try { Get-Content -Raw -LiteralPath $contract.InstalledManifestPath | ConvertFrom-Json } catch { $null } } else { $null }
        $service = Get-RmsSupportAgentServiceEvidence -Contract $contract
        return [pscustomobject]@{
            Mode = $Mode
            State = if ($null -ne $state) { if ($state.RecoveryRequired) { 'RecoveryRequired' } else { [string]$state.Phase } } elseif ($null -ne $service) { [string]$service.State } else { 'NotInstalled' }
            ServiceName = $contract.ServiceName
            ServiceState = if ($null -ne $service) { $service.State } else { 'NotFound' }
            InstalledVersion = if ($null -ne $manifest) { $manifest.Version } else { $null }
            Ownership = if ($null -ne $service -and $null -ne $manifest) { (Test-RmsSupportAgentServiceOwnership -Evidence $service -Contract $contract -ExpectedPackageId $manifest.PackageId -ExpectedVersion $manifest.Version).State } else { 'Missing' }
            Detail = 'Status is read-only; no service, package, certificate, registry, browser policy, or filesystem mutation was performed.'
        }
    }

    $lease = $null
    $stage = $null
    $manifest = $null
    $rollbackAttempted = $false
    try {
        try { Add-RmsSupportAgentAuditEvent -Contract $contract -Operation ('agent-package.' + $Mode.ToLowerInvariant()) -Outcome 'started' -PackageId '' -PackageVersion '' -FailureCode '' -RecoveryState 'not_required' } catch { }
        $existingState = Get-RmsSupportAgentLifecycleState -Contract $contract
        if ($null -ne $existingState) {
            # A checkpoint left behind before any destructive mutation began (staging only, not yet
            # activated, not flagged as needing recovery) is safe to auto-clear and retry. Anything
            # past that point -- or already marked RecoveryRequired -- must never be auto-deleted; it
            # is the only durable evidence of an unresolved prior mutation.
            if (-not $existingState.RecoveryRequired -and [string]$existingState.Phase -in @('Prepared', 'Staged')) {
                Clear-RmsSupportAgentLifecycleState -Contract $contract
            } else {
                return [pscustomobject]@{ State = 'RecoveryRequired'; Code = 'recovery_required'; Detail = 'An incomplete lifecycle checkpoint exists. Recovery must be resolved before another mutation.'; RollbackAttempted = $false; RollbackSucceeded = $false }
            }
        }

        $trust = if ($Mode -eq 'Rollback') { Test-RmsSupportAgentPackageTrust -Channel $Channel -Contract $contract -Rollback } else { Test-RmsSupportAgentPackageTrust -Channel $Channel -Contract $contract }
        if ($Mode -ne 'Uninstall' -and -not $trust.Valid) { return [pscustomobject]@{ State = 'Failed'; Code = 'trust_rejected'; Detail = ($trust.Blockers -join ','); RollbackAttempted = $false; RollbackSucceeded = $false } }
        $manifest = $trust.Manifest
        if ($Mode -eq 'Uninstall') {
            if (-not (Test-Path -LiteralPath $contract.InstalledManifestPath -PathType Leaf)) { return [pscustomobject]@{ State = 'Completed'; Code = 'not_installed'; Detail = 'The permanent Agent is already absent.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
            $manifest = Get-Content -Raw -LiteralPath $contract.InstalledManifestPath | ConvertFrom-Json
            $installedIntegrity = Test-RmsSupportAgentInstalledIntegrity -Contract $contract -Manifest $manifest
            if (-not $installedIntegrity.Valid) { return [pscustomobject]@{ State = 'Failed'; Code = 'installed_integrity_rejected'; Detail = ($installedIntegrity.Blockers -join ','); RollbackAttempted = $false; RollbackSucceeded = $false } }
        }

        $installedManifest = if (Test-Path -LiteralPath $contract.InstalledManifestPath -PathType Leaf) { try { Get-Content -Raw -LiteralPath $contract.InstalledManifestPath | ConvertFrom-Json } catch { $null } } else { $null }
        if (Test-Path -LiteralPath $contract.InstallRoot) {
            if ([IO.File]::GetAttributes($contract.InstallRoot).HasFlag([IO.FileAttributes]::ReparsePoint)) {
                return [pscustomobject]@{ State = 'Failed'; Code = 'installation_reparse_point'; Detail = 'The fixed Agent installation root is a reparse point.'; RollbackAttempted = $false; RollbackSucceeded = $false }
            }
        }
        if ($null -ne $installedManifest -and $Mode -in @('Upgrade', 'Repair', 'Rollback')) {
            $installedIntegrity = Test-RmsSupportAgentInstalledIntegrity -Contract $contract -Manifest $installedManifest
            if (-not $installedIntegrity.Valid) { return [pscustomobject]@{ State = 'Failed'; Code = 'installed_integrity_rejected'; Detail = ($installedIntegrity.Blockers -join ','); RollbackAttempted = $false; RollbackSucceeded = $false } }
        }
        if ($null -eq $installedManifest -and $Mode -ne 'Uninstall' -and (Test-Path -LiteralPath $contract.InstallRoot -PathType Container) -and @(Get-ChildItem -LiteralPath $contract.InstallRoot -Force).Count -gt 0) {
            return [pscustomobject]@{ State = 'Failed'; Code = 'installation_ownership_unproven'; Detail = 'The fixed Agent installation root contains content without a trusted installed manifest.'; RollbackAttempted = $false; RollbackSucceeded = $false }
        }

        $service = Get-RmsSupportAgentServiceEvidence -Contract $contract
        if ($null -ne $service) {
            $ownershipManifest = if ($null -ne $installedManifest) { $installedManifest } else { $manifest }
            $ownership = Test-RmsSupportAgentServiceOwnership -Evidence $service -Contract $contract -ExpectedPackageId ([string]$ownershipManifest.PackageId) -ExpectedVersion ([string]$ownershipManifest.Version)
            if (-not $ownership.Owned -or $service.ServiceAccount -notmatch 'LocalSystem$' -or $service.StartMode -ne 'Auto' -or $service.HasArguments) { return [pscustomobject]@{ State = 'Failed'; Code = 'ownership_conflict'; Detail = 'The same-name Agent service is not independently owned by RMS Support Hub.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
        } elseif ($Mode -in @('Upgrade', 'Repair', 'Rollback', 'Uninstall')) {
            return [pscustomobject]@{ State = 'Failed'; Code = 'service_ownership_unproven'; Detail = 'The installed package has no independently verifiable permanent Agent service.'; RollbackAttempted = $false; RollbackSucceeded = $false }
        }

        if ($Mode -in @('Upgrade', 'Repair', 'Rollback') -and $null -ne $service) {
            $comparison = ([Version]$manifest.Version).CompareTo([Version]$installedManifest.Version)
            if ($Mode -eq 'Repair' -and $comparison -ne 0) { return [pscustomobject]@{ State = 'Failed'; Code = 'repair_version_mismatch'; Detail = 'Repair requires the installed semantic version.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
            if ($Mode -eq 'Upgrade' -and $comparison -lt 0) { return [pscustomobject]@{ State = 'Failed'; Code = 'unsupported_downgrade'; Detail = 'Upgrade cannot select an older semantic version.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
            if ($Mode -eq 'Rollback' -and $comparison -ge 0) { return [pscustomobject]@{ State = 'Failed'; Code = 'rollback_version_invalid'; Detail = 'Rollback must select the explicitly retained prior semantic version.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
        }

        if ($Mode -ne 'Uninstall') {
            $certificate = Test-RmsSupportAgentCertificatePrerequisite -Contract $contract
            if (-not $certificate.Valid) { return [pscustomobject]@{ State = 'Failed'; Code = $certificate.Code; Detail = 'The Agent certificate prerequisite was not satisfied before lifecycle mutation.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
        }

        $lease = Enter-RmsSupportAgentMutationLease -Contract $contract
        if (-not $lease.Acquired) { return [pscustomobject]@{ State = if ($lease.Code -eq 'busy') { 'Busy' } else { 'Failed' }; Code = $lease.Code; Detail = 'Another privileged lifecycle mutation owns the machine-wide lease.'; RollbackAttempted = $false; RollbackSucceeded = $false } }
        foreach ($ownedRoot in @($contract.PackageRoot, $contract.AvailableRoot, $contract.RollbackRoot, $contract.StagingRoot, $contract.InstallRoot, $contract.AuditRoot, $contract.TrustRoot)) {
            Set-RmsSupportAgentOwnedAcl -Path $ownedRoot
        }
        Add-RmsSupportAgentAuditEvent -Contract $contract -Operation ('agent-package.' + $Mode.ToLowerInvariant()) -Outcome 'accepted' -PackageId ([string]$manifest.PackageId) -PackageVersion ([string]$manifest.Version) -FailureCode '' -RecoveryState 'not_required'

        $state = [pscustomobject]@{ OperationId = [Guid]::NewGuid().ToString('N'); Operation = $Mode; Phase = 'Prepared'; PackageId = $manifest.PackageId; PackageVersion = $manifest.Version; PreviousVersion = $null; StartedAtUtc = [DateTimeOffset]::UtcNow; UpdatedAtUtc = [DateTimeOffset]::UtcNow; FailureCode = $null; RecoveryRequired = $false }
        Write-RmsSupportAgentLifecycleState -Contract $contract -State $state
        if ($Mode -eq 'Uninstall') {
            Stop-RmsSupportAgentService
            Remove-RmsSupportAgentService
            if (Test-Path -LiteralPath $contract.InstallRoot -PathType Container) { Remove-Item -LiteralPath $contract.InstallRoot -Recurse -Force }
            Clear-RmsSupportAgentLifecycleState -Contract $contract
            $result = [pscustomobject]@{ State = 'Completed'; Code = 'uninstalled'; Detail = 'The owned Agent service and installation were removed; audit, trust, browser policy, and enterprise certificate state were retained.'; RollbackAttempted = $false; RollbackSucceeded = $false }
            Add-RmsSupportAgentAuditEvent -Contract $contract -Operation 'agent-package.uninstall' -Outcome 'completed' -PackageId ([string]$manifest.PackageId) -PackageVersion ([string]$manifest.Version) -FailureCode '' -RecoveryState 'not_required'
            return $result
        }

        $stage = Join-Path $contract.StagingRoot ($Mode.ToLowerInvariant() + '-' + [Guid]::NewGuid().ToString('N'))
        if (-not (Test-Path -LiteralPath $contract.StagingRoot -PathType Container)) { New-Item -ItemType Directory -Path $contract.StagingRoot -Force | Out-Null }
        New-Item -ItemType Directory -Path $stage -Force | Out-Null
        Expand-RmsSupportAgentPackage -ArchivePath $trust.ArchivePath -Destination $stage -Manifest $manifest
        $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding UTF8
        $state.Phase = 'Staged'; $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow; Write-RmsSupportAgentLifecycleState -Contract $contract -State $state

        $installedManifest = if (Test-Path -LiteralPath $contract.InstalledManifestPath -PathType Leaf) { Get-Content -Raw -LiteralPath $contract.InstalledManifestPath | ConvertFrom-Json } else { $null }
        if ($null -ne $installedManifest) {
            # Upgrade/Repair/Install-over-existing retain the PREVIOUS package in RollbackRoot so a
            # failed activation can restore it. An explicit Rollback instead retains the CURRENT
            # package into the separate bounded RecoveryRoot, so a failed rollback can restore the
            # version it was rolling back from instead of retrying the same failed target. Either way
            # only the signed manifest and its already-verified available archive are retained -- never
            # a raw copy of the live installation directory.
            $rollbackAttempted = $true
            $retainSlotRoot = if ($Mode -eq 'Rollback') { $contract.RecoveryRoot } else { $contract.RollbackRoot }
            if (-not (Save-RmsSupportAgentRetainedSlot -SlotRoot $retainSlotRoot -Contract $contract -Manifest $installedManifest)) { throw 'The prior trusted package could not be retained for rollback or recovery.' }
            $state.PreviousVersion = $installedManifest.Version; $state.Phase = 'PreviousPreserved'; $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow; Write-RmsSupportAgentLifecycleState -Contract $contract -State $state
            Stop-RmsSupportAgentService
        }

        if (Test-Path -LiteralPath $contract.InstallRoot -PathType Container) { Remove-Item -LiteralPath $contract.InstallRoot -Recurse -Force }
        Copy-RmsSupportAgentOwnedDirectory -Source $stage -Destination $contract.InstallRoot
        $state.Phase = 'Activated'; $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow; Write-RmsSupportAgentLifecycleState -Contract $contract -State $state
        $executable = @($manifest.Files | Where-Object { $_.RelativePath -notmatch '[\\/]' -and $script:AllowedExecutableNames -contains $_.RelativePath })
        if ($executable.Count -ne 1) { throw 'The signed package does not contain exactly one approved Agent executable.' }
        Ensure-RmsSupportAgentService -ExecutablePath (Join-Path $contract.InstallRoot $executable[0].RelativePath)
        $certificate = Test-RmsSupportAgentCertificatePrerequisite -Contract $contract
        if (-not $certificate.Valid) { throw $certificate.Code }
        Start-RmsSupportAgentService
        $state.Phase = 'HealthChecking'; $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow; Write-RmsSupportAgentLifecycleState -Contract $contract -State $state
        if (-not (Test-RmsSupportAgentHealth)) { throw 'agent_health_failed' }
        Clear-RmsSupportAgentLifecycleState -Contract $contract
        $result = [pscustomobject]@{ State = 'Completed'; Code = 'completed'; Detail = 'The trusted Agent package was activated, the owned service was configured, and live/ready health was confirmed.'; RollbackAttempted = $rollbackAttempted; RollbackSucceeded = $false }
        Add-RmsSupportAgentAuditEvent -Contract $contract -Operation ('agent-package.' + $Mode.ToLowerInvariant()) -Outcome 'completed' -PackageId ([string]$manifest.PackageId) -PackageVersion ([string]$manifest.Version) -FailureCode '' -RecoveryState 'not_required'
        return $result
    } catch {
        $failure = $_.Exception.Message
        try {
            if ($rollbackAttempted) {
                $restore = Restore-RmsSupportAgentRetainedSlot -Contract $contract -FailedOperation $Mode -Channel $Channel
                if ($restore.Succeeded) {
                    Add-RmsSupportAgentAuditEvent -Contract $contract -Operation ('agent-package.' + $Mode.ToLowerInvariant()) -Outcome 'rollback_succeeded' -PackageId ([string]$manifest.PackageId) -PackageVersion ([string]$manifest.Version) -FailureCode $failure -RecoveryState 'recovered'
                    return [pscustomobject]@{ State = 'RollbackSucceeded'; Code = 'rollback_succeeded'; Detail = 'Activation failed; the prior trusted installation was restored and its own health was independently confirmed.'; RollbackAttempted = $true; RollbackSucceeded = $true }
                }
                $failure = $failure + '; rollback_failed:' + $restore.Code
            }
        } catch { $failure = $failure + '; rollback_failed' }
        try {
            $state = Get-RmsSupportAgentLifecycleState -Contract $contract
            if ($null -ne $state) { $state.Phase = 'RecoveryRequired'; $state.FailureCode = $failure; $state.RecoveryRequired = $true; $state.UpdatedAtUtc = [DateTimeOffset]::UtcNow; Write-RmsSupportAgentLifecycleState -Contract $contract -State $state }
        } catch { }
        Add-RmsSupportAgentAuditEvent -Contract $contract -Operation ('agent-package.' + $Mode.ToLowerInvariant()) -Outcome 'failed' -PackageId ([string]$manifest.PackageId) -PackageVersion ([string]$manifest.Version) -FailureCode $failure -RecoveryState 'recovery_required'
        return [pscustomobject]@{ State = 'RecoveryRequired'; Code = 'recovery_required'; Detail = 'The lifecycle did not reach terminal health truth: ' + $failure; RollbackAttempted = $rollbackAttempted; RollbackSucceeded = $false }
    } finally {
        if ($null -ne $stage -and (Test-Path -LiteralPath $stage -PathType Container)) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
        Exit-RmsSupportAgentMutationLease -Lease $lease
    }
}

Export-ModuleMember -Function @(
    'Get-RmsSupportAgentDeploymentContract',
    'Test-RmsSupportAgentPackageManifest',
    'Test-RmsSupportAgentServiceOwnership',
    'Get-RmsSupportAgentMigrationPlan',
    'Get-RmsSupportAgentBrowserPolicyPlan',
    'Get-RmsSupportAgentCertificatePlan',
    'Get-RmsSupportAgentLifecyclePlan',
    'Get-RmsCanonicalPackagePayload',
    'Get-RmsCanonicalPackagePayloadText',
    'Test-RmsSupportAgentPackageTrust',
    'Test-RmsSupportAgentInstalledIntegrity',
    'Test-RmsSupportAgentControlFileTrust',
    'Get-RmsSupportAgentServiceEvidence',
    'Get-RmsSupportAgentLifecycleState',
    'Save-RmsSupportAgentRetainedSlot',
    'Restore-RmsSupportAgentRetainedSlot',
    'Invoke-RmsSupportAgentLifecycle'
)
