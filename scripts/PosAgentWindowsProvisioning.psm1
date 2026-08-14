Set-StrictMode -Version Latest

$script:RegistryValueKindString = [Microsoft.Win32.RegistryValueKind]::String
$script:RegistryValueKindMultiString = [Microsoft.Win32.RegistryValueKind]::MultiString

function Test-PosHasProperty {
    param(
        [AllowNull()]
        [object]$Object,
        [Parameter(Mandatory)]
        [string]$Name
    )

    if ($null -eq $Object) {
        return $false
    }
    return $null -ne $Object.PSObject.Properties[$Name]
}

function Normalize-PosExactOrigin {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value.IndexOf('*', [StringComparison]::Ordinal) -ge 0) {
        return $null
    }

    $uri = $null
    $invalidOrigin = -not [Uri]::TryCreate($Value.Trim(), [UriKind]::Absolute, [ref]$uri) `
        -or $uri.Scheme -ne 'https' `
        -or [string]::IsNullOrWhiteSpace($uri.Host) `
        -or -not [string]::IsNullOrEmpty($uri.UserInfo) `
        -or -not [string]::IsNullOrEmpty($uri.Query) `
        -or -not [string]::IsNullOrEmpty($uri.Fragment) `
        -or ($uri.AbsolutePath -ne '' -and $uri.AbsolutePath -ne '/')
    if ($invalidOrigin) {
        return $null
    }

    return $uri.GetLeftPart([UriPartial]::Authority).ToLowerInvariant()
}

function Normalize-PosExactHostname {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$Value
    )

    $hostname = $Value.Trim()
    if ($hostname.Length -gt 253 -or
        $hostname.IndexOf('*', [StringComparison]::Ordinal) -ge 0 -or
        $hostname.IndexOfAny([char[]]@('/', '\', ':', ',', ';', '[', ']')) -ge 0 -or
        $hostname -match '\s' -or
        $hostname -notmatch '^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$') {
        throw "The Agent hostname must be one exact DNS hostname without a wildcard or path: $Value"
    }

    return $hostname.ToLowerInvariant()
}

function ConvertTo-PosTrimmedEntries {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [object[]]$Values
    )

    $entries = New-Object System.Collections.Generic.List[string]
    foreach ($value in @($Values)) {
        if ($null -eq $value) {
            continue
        }

        $entry = ([string]$value).Trim()
        if ($entry.Length -gt 0) {
            $entries.Add($entry)
        }
    }

    return @($entries)
}

function Test-PosEntryEquals {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$Left,
        [AllowNull()]
        [string]$Right
    )

    return [string]::Equals(
        ([string]$Left).Trim(),
        ([string]$Right).Trim(),
        [StringComparison]::OrdinalIgnoreCase)
}

function Test-PosAuthPatternMatchesHostname {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Pattern,
        [Parameter(Mandatory)]
        [string]$Hostname
    )

    $candidate = $Pattern.Trim()
    if ($candidate -eq '*') {
        return $true
    }

    if ($candidate.IndexOf('*', [StringComparison]::Ordinal) -lt 0) {
        return [string]::Equals($candidate, $Hostname.Trim(), [StringComparison]::OrdinalIgnoreCase)
    }

    # Chrome and Edge permit wildcard authentication-server entries. Preserve
    # unrelated corporate entries, but fail closed when one would also permit
    # the exact Agent hostname being provisioned.
    $regex = '^' + [Regex]::Escape($candidate).Replace('\*', '.*') + '$'
    return [Regex]::IsMatch($Hostname.Trim(), $regex, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
}

function Merge-PosCommaSeparatedPolicy {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [string]$ExistingValue,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$RequiredEntry
    )

    $rawEntries = if ([string]::IsNullOrEmpty($ExistingValue)) { @() } else { @($ExistingValue -split ',') }
    if (@($rawEntries | Where-Object { [string]::IsNullOrWhiteSpace([string]$_) }).Count -gt 0) {
        throw 'The existing authentication allowlist contains an empty entry. RMS+ refuses to rewrite a malformed policy.'
    }
    $existingEntries = ConvertTo-PosTrimmedEntries $rawEntries

    if (@($existingEntries | Where-Object {
                $_.IndexOf('*', [StringComparison]::Ordinal) -ge 0 -and
                (Test-PosAuthPatternMatchesHostname $_ $RequiredEntry)
            }).Count -gt 0) {
        throw 'The existing authentication allowlist contains a wildcard or entry that already matches the exact RMS+ Agent hostname. RMS+ refuses to merge a conflicting broad authentication policy.'
    }

    $targetEntries = @($existingEntries | Where-Object { Test-PosEntryEquals $_ $RequiredEntry })
    if ($targetEntries.Count -gt 0) {
        return [pscustomobject]@{
            Value = ($existingEntries -join ',')
            RequiredEntryPresent = $true
            Added = $false
        }
    }

    $nextEntries = @($existingEntries) + @($RequiredEntry)
    return [pscustomobject]@{
        Value = ($nextEntries -join ',')
        RequiredEntryPresent = $false
        Added = $true
    }
}

function Merge-PosMultiStringHostnames {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [object[]]$ExistingValues,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$RequiredHostname
    )

    if ($null -ne $ExistingValues -and @($ExistingValues | Where-Object { $null -eq $_ -or [string]::IsNullOrWhiteSpace([string]$_) }).Count -gt 0) {
        throw 'BackConnectionHostNames contains an empty entry. RMS+ refuses to rewrite a malformed loopback policy.'
    }
    $existingEntries = @(ConvertTo-PosTrimmedEntries $ExistingValues)
    $present = @($existingEntries | Where-Object { Test-PosEntryEquals $_ $RequiredHostname }).Count -gt 0
    if ($present) {
        return [pscustomobject]@{
            Values = $existingEntries
            RequiredHostnamePresent = $true
            Added = $false
        }
    }

    $nextEntries = @($existingEntries) + @($RequiredHostname)
    return [pscustomobject]@{
        Values = $nextEntries
        RequiredHostnamePresent = $false
        Added = $true
    }
}

function Get-PosBrowserPolicyContract {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Chrome', 'Edge')]
        [string]$Browser,
        [Parameter(Mandatory)]
        [ValidateRange(1, 9999)]
        [int]$MajorVersion
    )

    $minimumVersion = if ($Browser -eq 'Chrome') { 139 } else { 140 }
    if ($MajorVersion -lt $minimumVersion) {
        throw "$Browser $MajorVersion is below the supported Local Network Access policy generation ($minimumVersion+). Install a supported browser or provide the equivalent centrally managed policy."
    }

    $loopbackPolicy = if ($MajorVersion -ge 146) {
        'LoopbackNetworkAllowedForUrls'
    } else {
        # The older documented policy is the exact-origin Local Network Access
        # allowlist. Do not write the similarly named precedence entry unless
        # a vendor policy contract explicitly exposes it for the installation.
        'LocalNetworkAccessAllowedForUrls'
    }

    $blockPolicies = if ($MajorVersion -ge 146) {
        @('LoopbackNetworkBlockedForUrls', 'LocalNetworkAccessBlockedForUrls', 'LocalNetworkBlockedForUrls')
    } else {
        @('LocalNetworkBlockedForUrls', 'LoopbackNetworkAccessBlockedForUrls', 'LocalNetworkAccessBlockedForUrls')
    }

    $policyRoot = if ($Browser -eq 'Chrome') {
        'SOFTWARE\Policies\Google\Chrome'
    } else {
        'SOFTWARE\Policies\Microsoft\Edge'
    }

    return [pscustomobject]@{
        Browser = $Browser
        MajorVersion = $MajorVersion
        PolicyRoot = $policyRoot
        AuthPolicyName = 'AuthServerAllowlist'
        LoopbackPolicyName = $loopbackPolicy
        BlockPolicyNames = $blockPolicies
        MinimumVersion = $minimumVersion
    }
}

function New-PosListPolicyMergePlan {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [object[]]$ExistingEntries,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$RequiredOrigin
    )

    $normalizedRequiredOrigin = Normalize-PosExactOrigin $RequiredOrigin
    if ($null -eq $normalizedRequiredOrigin) {
        throw "The Support Hub origin is not one exact HTTPS origin: $RequiredOrigin"
    }

    $entries = @()
    $numericNames = New-Object System.Collections.Generic.List[int]
    $requiredOriginPresent = $false
    foreach ($entry in @($ExistingEntries)) {
        if ($null -eq $entry) {
            continue
        }

        $name = ([string]$entry.Name).Trim()
        if ($name -notmatch '^[1-9][0-9]*$') {
            throw "The browser list policy contains an unexpected value name '$name'."
        }

        $kind = [string]$entry.Kind
        if ($kind -ne [string]$script:RegistryValueKindString -and $kind -ne 'String') {
            throw "The browser list policy value '$name' has incompatible registry type '$kind'."
        }

        $value = ([string]$entry.Value).Trim()
        if ($value.Length -eq 0) {
            throw "The browser list policy contains an empty value at '$name'."
        }

        $entries += [pscustomobject]@{ Name = $name; Value = $value }
        $numericNames.Add([int]$name)

        $entryOrigin = Normalize-PosExactOrigin $value
        if ($null -ne $entryOrigin -and [string]::Equals($entryOrigin, $normalizedRequiredOrigin, [StringComparison]::OrdinalIgnoreCase)) {
            $requiredOriginPresent = $true
        }

        if ($value.IndexOf('*', [StringComparison]::Ordinal) -ge 0 -or $value -eq '<all_urls>') {
            if (Test-PosPolicyPatternMatchesOrigin $value $normalizedRequiredOrigin) {
                throw "The browser list policy contains a broad pattern that may match the Support Hub origin: $value"
            }
        }
    }

    if ($requiredOriginPresent) {
        return [pscustomobject]@{
            RequiredOrigin = $normalizedRequiredOrigin
            Entries = $entries
            RequiredOriginPresent = $true
            Added = $false
            AddedValueName = $null
            AddedValue = $null
        }
    }

    $next = 1
    if ($numericNames.Count -gt 0) {
        $next = ([int]($numericNames | Measure-Object -Maximum).Maximum) + 1
    }

    return [pscustomobject]@{
        RequiredOrigin = $normalizedRequiredOrigin
        Entries = $entries
        RequiredOriginPresent = $false
        Added = $true
        AddedValueName = [string]$next
        AddedValue = $normalizedRequiredOrigin
    }
}

function Assert-PosRegistryValueKind {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [object]$Snapshot,
        [Parameter(Mandatory)]
        [Microsoft.Win32.RegistryValueKind]$ExpectedKind,
        [Parameter(Mandatory)]
        [string]$Description
    )

    if ($Snapshot.Exists -and [string]$Snapshot.Kind -ne [string]$ExpectedKind -and [string]$Snapshot.Kind -ne [string]$ExpectedKind.ToString()) {
        throw "$Description has incompatible registry type '$($Snapshot.Kind)'; refusing to rewrite it."
    }
}

function Get-PosRegistryValueSnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SubKey,
        [Parameter(Mandatory)]
        [string]$ValueName
    )

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Default)
    $key = $null
    try {
        $key = $base.OpenSubKey($SubKey, $false)
        if ($null -eq $key -or -not ($key.GetValueNames() -contains $ValueName)) {
            return [pscustomobject]@{ Exists = $false; Kind = $null; Value = $null }
        }

        $value = $key.GetValue($ValueName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $kind = $key.GetValueKind($ValueName)
        return [pscustomobject]@{
            Exists = $true
            Kind = $kind
            Value = if ($value -is [Array]) { @($value) } else { $value }
        }
    } finally {
        if ($null -ne $key) { $key.Dispose() }
        $base.Dispose()
    }
}

function Get-PosRegistryKeySnapshot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SubKey
    )

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Default)
    $key = $null
    try {
        $key = $base.OpenSubKey($SubKey, $false)
        if ($null -eq $key) {
            return [pscustomobject]@{ Exists = $false; Values = @(); SubKeyCount = 0 }
        }

        $values = foreach ($name in $key.GetValueNames()) {
            $value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            [pscustomobject]@{
                Name = $name
                Kind = $key.GetValueKind($name)
                Value = if ($value -is [Array]) { @($value) } else { $value }
            }
        }

        return [pscustomobject]@{
            Exists = $true
            Values = @($values)
            SubKeyCount = @($key.GetSubKeyNames()).Count
        }
    } finally {
        if ($null -ne $key) { $key.Dispose() }
        $base.Dispose()
    }
}

function Set-PosRegistryValue {
    param(
        [Parameter(Mandatory)]
        [string]$SubKey,
        [Parameter(Mandatory)]
        [string]$ValueName,
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory)]
        [Microsoft.Win32.RegistryValueKind]$Kind
    )

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Default)
    $key = $null
    try {
        $key = $base.CreateSubKey($SubKey)
        if ($Kind -eq $script:RegistryValueKindMultiString) {
            $items = @($Value)
            $typedValue = [System.Array]::CreateInstance([string], $items.Count)
            for ($index = 0; $index -lt $items.Count; $index++) {
                $typedValue.SetValue([string]$items[$index], $index)
            }
        } else {
            $typedValue = [string]$Value
        }
        $key.SetValue($ValueName, $typedValue, $Kind)
    } finally {
        if ($null -ne $key) { $key.Dispose() }
        $base.Dispose()
    }
}

function Remove-PosRegistryValue {
    param(
        [Parameter(Mandatory)]
        [string]$SubKey,
        [Parameter(Mandatory)]
        [string]$ValueName
    )

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Default)
    $key = $null
    try {
        $key = $base.OpenSubKey($SubKey, $true)
        if ($null -ne $key -and $key.GetValueNames() -contains $ValueName) {
            $key.DeleteValue($ValueName, $false)
        }
    } finally {
        if ($null -ne $key) { $key.Dispose() }
        $base.Dispose()
    }
}

function Remove-PosRegistryKeyIfEmpty {
    param(
        [Parameter(Mandatory)]
        [string]$SubKey
    )

    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Default)
    $key = $null
    try {
        $key = $base.OpenSubKey($SubKey, $true)
        if ($null -eq $key -or $key.GetValueNames().Count -gt 0 -or $key.GetSubKeyNames().Count -gt 0) {
            return
        }

        $lastSeparator = $SubKey.LastIndexOf('\')
        if ($lastSeparator -lt 1) {
            return
        }

        $parentPath = $SubKey.Substring(0, $lastSeparator)
        $childName = $SubKey.Substring($lastSeparator + 1)
        $parent = $null
        try {
            $parent = $base.OpenSubKey($parentPath, $true)
            if ($null -ne $parent -and $parent.GetSubKeyNames() -contains $childName) {
                $parent.DeleteSubKey($childName, $false)
            }
        } finally {
            if ($null -ne $parent) { $parent.Dispose() }
        }
    } finally {
        if ($null -ne $key) { $key.Dispose() }
        $base.Dispose()
    }
}

function Test-PosRegistryValueEqual {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [object]$Actual,
        [AllowNull()]
        [object]$Expected,
        [Parameter(Mandatory)]
        [Microsoft.Win32.RegistryValueKind]$Kind
    )

    if ($Kind -eq $script:RegistryValueKindMultiString) {
        $actualEntries = if ($null -eq $Actual) { @() } else { @($Actual) }
        $expectedEntries = if ($null -eq $Expected) { @() } else { @($Expected) }
        if ($actualEntries.Count -ne $expectedEntries.Count) {
            return $false
        }

        for ($index = 0; $index -lt $actualEntries.Count; $index++) {
            if (-not [string]::Equals(
                    ([string]$actualEntries[$index]).Trim(),
                    ([string]$expectedEntries[$index]).Trim(),
                    [StringComparison]::Ordinal)) {
                return $false
            }
        }

        return $true
    }

    return [string]::Equals(
        [string]$Actual,
        [string]$Expected,
        [StringComparison]::Ordinal)
}

function Restore-PosRegistryValueAfterProvisioning {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SubKey,
        [Parameter(Mandatory)]
        [string]$ValueName,
        [Parameter(Mandatory)]
        [Microsoft.Win32.RegistryValueKind]$ExpectedKind,
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$ExpectedValue,
        [Parameter(Mandatory)]
        [bool]$OriginalExists,
        [AllowNull()]
        [object]$OriginalValue,
        [switch]$RemoveKeyIfEmpty
    )

    $current = Get-PosRegistryValueSnapshot $SubKey $ValueName
    if (-not $current.Exists) {
        throw "Registry value $SubKey\\$ValueName disappeared before provisioning rollback; refusing to guess ownership."
    }

    Assert-PosRegistryValueKind $current $ExpectedKind "$SubKey\\$ValueName"
    if (-not (Test-PosRegistryValueEqual $current.Value $ExpectedValue $ExpectedKind)) {
        throw "Registry value $SubKey\\$ValueName changed before provisioning rollback; refusing to overwrite an external change."
    }

    if ($OriginalExists) {
        Set-PosRegistryValue $SubKey $ValueName $OriginalValue $ExpectedKind
    } else {
        Remove-PosRegistryValue $SubKey $ValueName
        if ($RemoveKeyIfEmpty) {
            Remove-PosRegistryKeyIfEmpty $SubKey
        }
    }
}

function Add-PosProvisioningRollbackAction {
    param(
        [AllowNull()]
        [System.Collections.Generic.List[object]]$RollbackActions,
        [Parameter(Mandatory)]
        [string]$SubKey,
        [Parameter(Mandatory)]
        [string]$ValueName,
        [Parameter(Mandatory)]
        [Microsoft.Win32.RegistryValueKind]$ExpectedKind,
        [Parameter(Mandatory)]
        [AllowNull()]
        [object]$ExpectedValue,
        [Parameter(Mandatory)]
        [bool]$OriginalExists,
        [AllowNull()]
        [object]$OriginalValue,
        [switch]$RemoveKeyIfEmpty
    )

    if ($null -ne $RollbackActions) {
        [void]$RollbackActions.Add([pscustomobject]@{
                SubKey = $SubKey
                ValueName = $ValueName
                ExpectedKind = $ExpectedKind
                ExpectedValue = $ExpectedValue
                OriginalExists = $OriginalExists
                OriginalValue = $OriginalValue
                RemoveKeyIfEmpty = [bool]$RemoveKeyIfEmpty
            })
    }
}

function Get-PosBrowserExecutableCandidates {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Chrome', 'Edge')]
        [string]$Browser
    )

    if ($Browser -eq 'Chrome') {
        return @(
            (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe'),
            (Join-Path $env:LocalAppData 'Google\Chrome\Application\chrome.exe'))
    }

    return @(
        (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'))
}

function Get-PosInstalledBrowser {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Chrome', 'Edge')]
        [string]$Browser
    )

    $candidates = @(Get-PosBrowserExecutableCandidates $Browser | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($candidates.Count -eq 0) {
        return [pscustomobject]@{ Browser = $Browser; Installed = $false; Version = $null; MajorVersion = $null; Path = $null }
    }

    $items = foreach ($candidate in $candidates) {
        $item = Get-Item -LiteralPath $candidate
        $version = $null
        if ([Version]::TryParse($item.VersionInfo.ProductVersion, [ref]$version)) {
            [pscustomobject]@{ Item = $item; Version = $version }
        }
    }

    $selected = @($items | Sort-Object Version -Descending | Select-Object -First 1)
    if ($selected.Count -eq 0) {
        throw "Installed $Browser executable did not expose a parseable product version."
    }

    return [pscustomobject]@{
        Browser = $Browser
        Installed = $true
        Version = $selected[0].Version.ToString()
        MajorVersion = $selected[0].Version.Major
        Path = $selected[0].Item.FullName
    }
}

function Test-PosPolicyPatternMatchesOrigin {
    <#
    .SYNOPSIS
    Matches a single Chrome/Edge URL-pattern policy value against the exact
    Support Hub origin, following the vendor URL Blocklist Filter Format
    (scheme://host:port/path, all components except host optional) plus the
    documented Local/Loopback Network Access and AuthServerAllowlist host
    forms. Any pattern this parser cannot confidently classify is treated as
    a match (fail closed) so an ambiguous enterprise rule is never silently
    treated as non-blocking.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Pattern,
        [Parameter(Mandatory)]
        [string]$Origin
    )

    $normalizedOrigin = Normalize-PosExactOrigin $Origin
    if ($null -eq $normalizedOrigin) {
        throw "The Support Hub origin is not one exact HTTPS origin: $Origin"
    }

    $candidate = $Pattern.Trim()
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        throw 'A browser block policy contains an empty pattern; RMS+ refuses to proceed.'
    }

    if ($candidate -eq '*' -or $candidate -eq '<all_urls>') {
        return $true
    }

    $originUri = [Uri]$normalizedOrigin
    $targetHost = $originUri.Host.ToLowerInvariant().TrimEnd('.')
    $targetPort = $originUri.Port

    $scheme = $null
    $hostPattern = $null
    $portPattern = $null
    $pathPattern = ''

    if ($candidate -match '^(?<scheme>https?)://(?<host>[^/:]+)(:(?<port>[0-9]+))?(?<path>/.*)?$') {
        # scheme://host[:port][/path] — the full vendor pattern grammar.
        $scheme = $Matches.scheme.ToLowerInvariant()
        $hostPattern = $Matches.host.ToLowerInvariant()
        $portPattern = if ($Matches.ContainsKey('port') -and $Matches.port) { [int]$Matches.port } else { $null }
        $pathPattern = if ($Matches.ContainsKey('path')) { [string]$Matches.path } else { '' }
    } elseif ($candidate -match '^(?<host>[^/:\s]+)(:(?<port>[0-9]+))?$') {
        # Scheme-less bare host[:port]. Chrome/Edge treat this as matching the
        # host on any scheme, e.g. an AuthServerAllowlist/LNA-style entry.
        $hostPattern = $Matches.host.ToLowerInvariant()
        $portPattern = if ($Matches.ContainsKey('port') -and $Matches.port) { [int]$Matches.port } else { $null }
    } else {
        # Unrecognized/malformed pattern: fail closed rather than assume no conflict.
        return $true
    }

    if ($null -ne $scheme -and $scheme -ne $originUri.Scheme.ToLowerInvariant()) {
        return $false
    }

    $hostPattern = $hostPattern.TrimEnd('.')
    $hostMatches = if ($hostPattern -eq '*') {
        $true
    } elseif ($hostPattern.StartsWith('[*.]', [StringComparison]::Ordinal)) {
        $baseHost = $hostPattern.Substring(4)
        [string]::Equals($targetHost, $baseHost, [StringComparison]::OrdinalIgnoreCase) -or
            $targetHost.EndsWith('.' + $baseHost, [StringComparison]::OrdinalIgnoreCase)
    } elseif ($hostPattern.StartsWith('*.', [StringComparison]::Ordinal)) {
        $baseHost = $hostPattern.Substring(2)
        $targetHost.EndsWith('.' + $baseHost, [StringComparison]::OrdinalIgnoreCase)
    } elseif ($hostPattern.IndexOf('*', [StringComparison]::Ordinal) -ge 0) {
        $true
    } else {
        [string]::Equals($targetHost, $hostPattern, [StringComparison]::OrdinalIgnoreCase)
    }

    if (-not $hostMatches) {
        return $false
    }

    if ($null -ne $portPattern -and $portPattern -ne $targetPort) {
        return $false
    }

    if ([string]::IsNullOrEmpty($pathPattern) -or $pathPattern -eq '/') {
        return $true
    }

    if ($pathPattern.IndexOf('*', [StringComparison]::Ordinal) -ge 0) {
        return $true
    }

    return $false
}

function Get-PosListPolicyEntries {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SubKey
    )

    $snapshot = Get-PosRegistryKeySnapshot $SubKey
    if (-not $snapshot.Exists) {
        return @()
    }

    return @($snapshot.Values | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Kind = $_.Kind
                Value = $_.Value
            }
        }
    )
}

function Test-PosUrlListPolicyMatchesOrigin {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SubKey,
        [Parameter(Mandatory)]
        [string]$SupportHubOrigin,
        [Parameter(Mandatory)]
        [string]$PolicyDisplayName
    )

    foreach ($entry in @(Get-PosListPolicyEntries $SubKey)) {
        if ([string]$entry.Kind -ne [string]$script:RegistryValueKindString -and [string]$entry.Kind -ne 'String') {
            throw "$PolicyDisplayName contains an incompatible registry type at value '$($entry.Name)'."
        }

        if (Test-PosPolicyPatternMatchesOrigin ([string]$entry.Value) $SupportHubOrigin) {
            return $true
        }
    }

    return $false
}

function Assert-PosNoBlockingPolicyMatch {
    <#
    .SYNOPSIS
    Fails closed when any higher-precedence Chrome/Edge policy would block
    the exact Support Hub origin, covering both the Local/Loopback Network
    Access block-policy family and the generic navigation-level
    URLBlocklist/URLAllowlist pair. URLAllowlist takes precedence over
    URLBlocklist per vendor documentation, so a URLBlocklist match is only a
    conflict when no URLAllowlist entry also matches the exact origin.
    #>
    param(
        [Parameter(Mandatory)]
        [object]$Contract,
        [Parameter(Mandatory)]
        [string]$SupportHubOrigin
    )

    foreach ($policyName in @($Contract.BlockPolicyNames)) {
        $subKey = "$($Contract.PolicyRoot)\$policyName"
        if (Test-PosUrlListPolicyMatchesOrigin $subKey $SupportHubOrigin "$($Contract.Browser) $policyName") {
            throw "$($Contract.Browser) policy $policyName blocks the exact Support Hub origin. RMS+ will not override a higher-precedence browser block policy."
        }
    }

    $blocklistKey = "$($Contract.PolicyRoot)\URLBlocklist"
    $blockedByUrlList = Test-PosUrlListPolicyMatchesOrigin $blocklistKey $SupportHubOrigin "$($Contract.Browser) URLBlocklist"
    if (-not $blockedByUrlList) {
        return
    }

    $allowlistKey = "$($Contract.PolicyRoot)\URLAllowlist"
    $allowedByUrlList = Test-PosUrlListPolicyMatchesOrigin $allowlistKey $SupportHubOrigin "$($Contract.Browser) URLAllowlist"
    if (-not $allowedByUrlList) {
        throw "$($Contract.Browser) policy URLBlocklist blocks the exact Support Hub origin and no URLAllowlist entry exempts it. RMS+ will not override a higher-precedence browser block policy."
    }
}

function Add-PosBrowserPolicyStateRecord {
    param(
        [Parameter(Mandatory)]
        [object]$State,
        [Parameter(Mandatory)]
        [object]$Record
    )

    if (-not (Test-PosHasProperty $State 'BrowserPolicies')) {
        Add-Member -InputObject $State -MemberType NoteProperty -Name BrowserPolicies -Value @()
    }

    $records = @($State.BrowserPolicies | Where-Object { $_.Browser -ne $Record.Browser -or $_.PolicyRoot -ne $Record.PolicyRoot })
    $State.BrowserPolicies = @($records + $Record)
}

function Ensure-PosBrowserPolicyRecord {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Chrome', 'Edge')]
        [string]$Browser,
        [Parameter(Mandatory)]
        [object]$Installed,
        [Parameter(Mandatory)]
        [string]$SupportHubOrigin,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CanonicalHost,
        [Parameter(Mandatory)]
        [object]$State,
        [AllowNull()]
        [System.Collections.Generic.List[object]]$RollbackActions
    )

    $contract = Get-PosBrowserPolicyContract $Browser ([int]$Installed.MajorVersion)
    Assert-PosNoBlockingPolicyMatch $contract $SupportHubOrigin

    $authSubKey = $contract.PolicyRoot
    $authKeySnapshot = Get-PosRegistryKeySnapshot $authSubKey
    $authSnapshot = Get-PosRegistryValueSnapshot $authSubKey $contract.AuthPolicyName
    Assert-PosRegistryValueKind $authSnapshot $script:RegistryValueKindString "$Browser $($contract.AuthPolicyName)"

    $existingAuthValue = if ($authSnapshot.Exists) { [string]$authSnapshot.Value } else { $null }
    $authPlan = Merge-PosCommaSeparatedPolicy $existingAuthValue $CanonicalHost
    $authRecord = [pscustomobject]@{
        Browser = $Browser
        Version = $Installed.Version
        MajorVersion = $Installed.MajorVersion
        Executable = $Installed.Path
        PolicyRoot = $contract.PolicyRoot
        AuthPolicyName = $contract.AuthPolicyName
        AuthAdded = $false
        AuthOriginalExists = $authSnapshot.Exists
        AuthOriginalValue = $existingAuthValue
        AuthProvisionedValue = $null
        AuthKeyCreated = -not $authKeySnapshot.Exists
        LoopbackPolicyName = $contract.LoopbackPolicyName
        LoopbackPolicyRoot = "$($contract.PolicyRoot)\$($contract.LoopbackPolicyName)"
        LoopbackKeyCreated = $false
        LoopbackAdded = $false
        LoopbackAddedValueName = $null
        LoopbackAddedValue = $null
    }

    $priorRecord = @($State.BrowserPolicies | Where-Object {
            $_.Browser -eq $Browser -and $_.PolicyRoot -eq $contract.PolicyRoot
        } | Select-Object -First 1)
    if ($priorRecord.Count -gt 0 -and (Test-PosHasProperty $priorRecord[0] 'AuthAdded') -and [bool]$priorRecord[0].AuthAdded) {
        if (-not $authSnapshot.Exists -or [string]$authSnapshot.Value -ne [string]$priorRecord[0].AuthProvisionedValue) {
            throw "$Browser authentication policy no longer matches the RMS+ value recorded in provisioning state; refusing to overwrite an external change."
        }
        $authRecord.AuthAdded = $true
        $authRecord.AuthOriginalExists = [bool]$priorRecord[0].AuthOriginalExists
        $authRecord.AuthOriginalValue = $priorRecord[0].AuthOriginalValue
        $authRecord.AuthProvisionedValue = $priorRecord[0].AuthProvisionedValue
        $authRecord.AuthKeyCreated = [bool]$priorRecord[0].AuthKeyCreated
    }

    $loopbackSubKey = "$($contract.PolicyRoot)\$($contract.LoopbackPolicyName)"
    $loopbackKeySnapshot = Get-PosRegistryKeySnapshot $loopbackSubKey
    $loopbackEntries = @(Get-PosListPolicyEntries $loopbackSubKey)
    $loopbackPlan = New-PosListPolicyMergePlan $loopbackEntries $SupportHubOrigin

    if ($authPlan.Added) {
        if ($PSCmdlet.ShouldProcess("HKLM:\$authSubKey\$($contract.AuthPolicyName)", "Add the exact $CanonicalHost Windows authentication allowlist entry for $Browser")) {
            Set-PosRegistryValue $authSubKey $contract.AuthPolicyName $authPlan.Value $script:RegistryValueKindString
            Add-PosProvisioningRollbackAction `
                -RollbackActions $RollbackActions `
                -SubKey $authSubKey `
                -ValueName $contract.AuthPolicyName `
                -ExpectedKind $script:RegistryValueKindString `
                -ExpectedValue $authPlan.Value `
                -OriginalExists $authSnapshot.Exists `
                -OriginalValue $existingAuthValue `
                -RemoveKeyIfEmpty:(-not $authKeySnapshot.Exists)
            $authRecord.AuthAdded = $true
            $authRecord.AuthProvisionedValue = $authPlan.Value
        }
    }

    if ($loopbackPlan.Added) {
        if ($PSCmdlet.ShouldProcess("HKLM:\$loopbackSubKey\$($loopbackPlan.AddedValueName)", "Allow only the exact Support Hub origin for $Browser loopback access")) {
            Set-PosRegistryValue $loopbackSubKey $loopbackPlan.AddedValueName $loopbackPlan.AddedValue $script:RegistryValueKindString
            Add-PosProvisioningRollbackAction `
                -RollbackActions $RollbackActions `
                -SubKey $loopbackSubKey `
                -ValueName $loopbackPlan.AddedValueName `
                -ExpectedKind $script:RegistryValueKindString `
                -ExpectedValue $loopbackPlan.AddedValue `
                -OriginalExists $false `
                -OriginalValue $null `
                -RemoveKeyIfEmpty:(-not $loopbackKeySnapshot.Exists)
            $authRecord.LoopbackAdded = $true
            $authRecord.LoopbackAddedValueName = $loopbackPlan.AddedValueName
            $authRecord.LoopbackAddedValue = $loopbackPlan.AddedValue
            $authRecord.LoopbackKeyCreated = -not $loopbackKeySnapshot.Exists
        }
    }

    if ($priorRecord.Count -gt 0 -and (Test-PosHasProperty $priorRecord[0] 'LoopbackAdded') -and [bool]$priorRecord[0].LoopbackAdded) {
        $ownedName = [string]$priorRecord[0].LoopbackAddedValueName
        $ownedSnapshot = Get-PosRegistryValueSnapshot "$($contract.PolicyRoot)\$($contract.LoopbackPolicyName)" $ownedName
        if (-not $ownedSnapshot.Exists -or [string]$ownedSnapshot.Value -ne [string]$priorRecord[0].LoopbackAddedValue) {
            throw "$Browser loopback policy no longer matches the RMS+ value recorded in provisioning state; refusing to overwrite an external change."
        }
        $authRecord.LoopbackKeyCreated = [bool]$priorRecord[0].LoopbackKeyCreated
        $authRecord.LoopbackAdded = $true
        $authRecord.LoopbackAddedValueName = $ownedName
        $authRecord.LoopbackAddedValue = $priorRecord[0].LoopbackAddedValue
    }

    Add-PosBrowserPolicyStateRecord $State $authRecord

    return [pscustomobject]@{
        Browser = $Browser
        Version = $Installed.Version
        MajorVersion = $Installed.MajorVersion
        Installed = $true
        AuthServerAllowlist = 'Present'
        AuthServerAllowlistChanged = [bool]$authPlan.Added
        LoopbackPolicy = $contract.LoopbackPolicyName
        LoopbackOrigin = $SupportHubOrigin
        LoopbackPolicyChanged = [bool]$loopbackPlan.Added
        BlockPolicyConflict = $false
    }
}

function Ensure-PosBackConnectionHostName {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CanonicalHost,
        [Parameter(Mandatory)]
        [object]$State,
        [AllowNull()]
        [System.Collections.Generic.List[object]]$RollbackActions
    )

    $subKey = 'SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0'
    $valueName = 'BackConnectionHostNames'
    $snapshot = Get-PosRegistryValueSnapshot $subKey $valueName
    Assert-PosRegistryValueKind $snapshot $script:RegistryValueKindMultiString 'BackConnectionHostNames'
    $existingValues = @()
    if ($snapshot.Exists) {
        $existingValues = @($snapshot.Value)
    }
    $plan = Merge-PosMultiStringHostnames $existingValues $CanonicalHost

    if (-not (Test-PosHasProperty $State 'BackConnection') -or $null -eq $State.BackConnection) {
        Add-Member -InputObject $State -MemberType NoteProperty -Name BackConnection -Value ([pscustomobject]@{})
    }

    $priorBackConnection = $State.BackConnection
    if ((Test-PosHasProperty $priorBackConnection 'Added') -and [bool]$priorBackConnection.Added) {
        $ownedValues = @($priorBackConnection.ProvisionedValues)
        if (-not $snapshot.Exists -or $ownedValues.Count -ne $existingValues.Count -or (Compare-Object -ReferenceObject $ownedValues -DifferenceObject $existingValues)) {
            throw 'BackConnectionHostNames no longer matches the RMS+ value recorded in provisioning state; refusing to overwrite an external loopback policy change.'
        }

        return [pscustomobject]@{
            Hostname = $CanonicalHost
            Present = $true
            Changed = $false
            RegistryType = 'REG_MULTI_SZ'
            DisableLoopbackCheck = 'Not modified'
        }
    }

    $State.BackConnection = [pscustomobject]@{
        RegistryPath = $subKey
        ValueName = $valueName
        Added = $false
        OriginalExists = $snapshot.Exists
        OriginalValues = @($existingValues)
        ProvisionedValues = @()
    }

    if ($plan.Added) {
        if ($PSCmdlet.ShouldProcess("HKLM:\$subKey\$valueName", "Add only the exact $CanonicalHost Windows loopback authentication hostname")) {
            Set-PosRegistryValue $subKey $valueName $plan.Values $script:RegistryValueKindMultiString
            Add-PosProvisioningRollbackAction `
                -RollbackActions $RollbackActions `
                -SubKey $subKey `
                -ValueName $valueName `
                -ExpectedKind $script:RegistryValueKindMultiString `
                -ExpectedValue $plan.Values `
                -OriginalExists $snapshot.Exists `
                -OriginalValue $existingValues
            $State.BackConnection.Added = $true
            $State.BackConnection.ProvisionedValues = @($plan.Values)
        }
    }

    return [pscustomobject]@{
        Hostname = $CanonicalHost
        Present = $true
        Changed = [bool]$plan.Added
        RegistryType = 'REG_MULTI_SZ'
        DisableLoopbackCheck = 'Not modified'
    }
}

function Get-PosDisableLoopbackCheckSnapshot {
    $snapshot = Get-PosRegistryValueSnapshot 'SYSTEM\CurrentControlSet\Control\Lsa' 'DisableLoopbackCheck'
    if ($snapshot.Exists) {
        throw 'DisableLoopbackCheck exists. RMS+ refuses to alter or work around an existing loopback security policy; remove the conflicting policy through the approved enterprise channel.'
    }

    return [pscustomobject]@{ Present = $false }
}

function Ensure-PosAgentBrowserProvisioning {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$SupportHubOrigin,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CanonicalHost,
        [Parameter(Mandatory)]
        [object]$State
    )

    $normalizedOrigin = Normalize-PosExactOrigin $SupportHubOrigin
    if ($null -eq $normalizedOrigin) {
        throw "SupportHubOrigin must be one exact HTTPS origin without a path, query, fragment, credentials, or wildcard: $SupportHubOrigin"
    }

    $CanonicalHost = Normalize-PosExactHostname $CanonicalHost

    # Read and validate every machine policy before the first write. This keeps an
    # unsupported browser, a block-policy conflict, or a malformed registry value
    # from leaving a partially provisioned machine behind.
    $disableLoopbackCheck = Get-PosDisableLoopbackCheckSnapshot
    $backConnectionSnapshot = Get-PosRegistryValueSnapshot 'SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0' 'BackConnectionHostNames'
    Assert-PosRegistryValueKind $backConnectionSnapshot $script:RegistryValueKindMultiString 'BackConnectionHostNames'
    $backConnectionValues = @()
    if ($backConnectionSnapshot.Exists) {
        $backConnectionValues = @($backConnectionSnapshot.Value)
    }
    $null = Merge-PosMultiStringHostnames $backConnectionValues $CanonicalHost

    $browserPreflight = @()
    foreach ($browser in @('Chrome', 'Edge')) {
        $installed = Get-PosInstalledBrowser $browser
        if (-not $installed.Installed) {
            continue
        }

        $contract = Get-PosBrowserPolicyContract $browser ([int]$installed.MajorVersion)
        Assert-PosNoBlockingPolicyMatch $contract $normalizedOrigin

        $authSnapshot = Get-PosRegistryValueSnapshot $contract.PolicyRoot $contract.AuthPolicyName
        Assert-PosRegistryValueKind $authSnapshot $script:RegistryValueKindString "$browser $($contract.AuthPolicyName)"
        $existingAuthValue = if ($authSnapshot.Exists) { [string]$authSnapshot.Value } else { $null }
        $null = Merge-PosCommaSeparatedPolicy $existingAuthValue $CanonicalHost

        $loopbackSubKey = "$($contract.PolicyRoot)\$($contract.LoopbackPolicyName)"
        $loopbackEntries = @(Get-PosListPolicyEntries $loopbackSubKey)
        $null = New-PosListPolicyMergePlan $loopbackEntries $normalizedOrigin

        $browserPreflight += [pscustomobject]@{
            Browser = $browser
            Installed = $installed
            Contract = $contract
        }
    }

    $rollbackActions = [System.Collections.Generic.List[object]]::new()
    try {
        if (-not (Test-PosHasProperty $State 'BrowserProvisioning') -or $null -eq $State.BrowserProvisioning) {
            Add-Member -InputObject $State -MemberType NoteProperty -Name BrowserProvisioning -Value ([pscustomobject]@{})
        }
        if (-not (Test-PosHasProperty $State.BrowserProvisioning 'BrowserPolicies')) {
            Add-Member -InputObject $State.BrowserProvisioning -MemberType NoteProperty -Name BrowserPolicies -Value @()
        }
        foreach ($property in @(
                @{ Name = 'SupportHubOrigin'; Value = $normalizedOrigin },
                @{ Name = 'CanonicalHost'; Value = $CanonicalHost },
                @{ Name = 'DisableLoopbackCheck'; Value = $false })) {
            if (-not (Test-PosHasProperty $State.BrowserProvisioning $property.Name)) {
                Add-Member -InputObject $State.BrowserProvisioning -MemberType NoteProperty -Name $property.Name -Value $property.Value
            } else {
                $State.BrowserProvisioning.($property.Name) = $property.Value
            }
        }

        $results = @()
        foreach ($browser in @('Chrome', 'Edge')) {
            $preflight = @($browserPreflight | Where-Object { $_.Browser -eq $browser }) | Select-Object -First 1
            if ($null -eq $preflight) {
                $results += [pscustomobject]@{ Browser = $browser; Installed = $false; Result = 'NotInstalled' }
                continue
            }

            $record = Ensure-PosBrowserPolicyRecord `
                $browser `
                $preflight.Installed `
                $normalizedOrigin `
                $CanonicalHost `
                $State.BrowserProvisioning `
                -RollbackActions $rollbackActions
            $results += $record
        }

        $backConnection = Ensure-PosBackConnectionHostName `
            $CanonicalHost `
            $State.BrowserProvisioning `
            -RollbackActions $rollbackActions
        $State.BrowserProvisioning.DisableLoopbackCheck = $disableLoopbackCheck.Present

        return [pscustomobject]@{
            Browsers = @($results)
            BackConnection = $backConnection
            DisableLoopbackCheckPresent = $disableLoopbackCheck.Present
        }
    } catch {
        $rollbackErrors = New-Object System.Collections.Generic.List[string]
        for ($index = $rollbackActions.Count - 1; $index -ge 0; $index--) {
            try {
                $rollback = $rollbackActions[$index]
                Restore-PosRegistryValueAfterProvisioning `
                    -SubKey $rollback.SubKey `
                    -ValueName $rollback.ValueName `
                    -ExpectedKind $rollback.ExpectedKind `
                    -ExpectedValue $rollback.ExpectedValue `
                    -OriginalExists $rollback.OriginalExists `
                    -OriginalValue $rollback.OriginalValue `
                    -RemoveKeyIfEmpty:$rollback.RemoveKeyIfEmpty
            } catch {
                [void]$rollbackErrors.Add($_.Exception.Message)
            }
        }

        if ($rollbackErrors.Count -gt 0) {
            throw "RMS+ browser/Windows policy provisioning failed and rollback was incomplete: $($_.Exception.Message) Rollback detail: $($rollbackErrors -join '; ')"
        }

        throw
    }
}

function Remove-PosBrowserAuthPolicy {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [object]$Record
    )

    if (-not [bool]$Record.AuthAdded) {
        return
    }

    $snapshot = Get-PosRegistryValueSnapshot $Record.PolicyRoot $Record.AuthPolicyName
    Assert-PosRegistryValueKind $snapshot $script:RegistryValueKindString "$($Record.Browser) $($Record.AuthPolicyName)"
    if (-not $snapshot.Exists -or [string]$snapshot.Value -ne [string]$Record.AuthProvisionedValue) {
        throw "$($Record.Browser) authentication policy changed after RMS+ provisioning; refusing to restore over an external policy change."
    }

    if ($Record.AuthOriginalExists) {
        if ($PSCmdlet.ShouldProcess("HKLM:\$($Record.PolicyRoot)\$($Record.AuthPolicyName)", "Restore the pre-existing $($Record.Browser) authentication allowlist")) {
            Set-PosRegistryValue $Record.PolicyRoot $Record.AuthPolicyName $Record.AuthOriginalValue $script:RegistryValueKindString
        }
    } elseif ($PSCmdlet.ShouldProcess("HKLM:\$($Record.PolicyRoot)\$($Record.AuthPolicyName)", "Remove the RMS+ $($Record.Browser) authentication allowlist value")) {
        Remove-PosRegistryValue $Record.PolicyRoot $Record.AuthPolicyName
    }

    if ([bool]$Record.AuthKeyCreated -and $PSCmdlet.ShouldProcess("HKLM:\$($Record.PolicyRoot)", "Remove the empty RMS+ $($Record.Browser) policy root")) {
        Remove-PosRegistryKeyIfEmpty $Record.PolicyRoot
    }
}

function Remove-PosBrowserLoopbackPolicy {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [object]$Record
    )

    if (-not [bool]$Record.LoopbackAdded) {
        return
    }

    $snapshot = Get-PosRegistryValueSnapshot $Record.LoopbackPolicyRoot ([string]$Record.LoopbackAddedValueName)
    Assert-PosRegistryValueKind $snapshot $script:RegistryValueKindString "$($Record.Browser) $($Record.LoopbackPolicyName)"
    if (-not $snapshot.Exists -or [string]$snapshot.Value -ne [string]$Record.LoopbackAddedValue) {
        throw "$($Record.Browser) loopback policy entry changed after RMS+ provisioning; refusing to remove an external value."
    }

    if ($snapshot.Exists -and $PSCmdlet.ShouldProcess("HKLM:\$($Record.LoopbackPolicyRoot)\$($Record.LoopbackAddedValueName)", "Remove only the RMS+ $($Record.Browser) loopback allowlist entry")) {
        Remove-PosRegistryValue $Record.LoopbackPolicyRoot ([string]$Record.LoopbackAddedValueName)
    }

    if ([bool]$Record.LoopbackKeyCreated -and $PSCmdlet.ShouldProcess("HKLM:\$($Record.LoopbackPolicyRoot)", "Remove the empty RMS+ $($Record.Browser) loopback policy key")) {
        Remove-PosRegistryKeyIfEmpty $Record.LoopbackPolicyRoot
    }

    if ((Test-PosHasProperty $Record 'AuthKeyCreated') -and [bool]$Record.AuthKeyCreated -and $PSCmdlet.ShouldProcess("HKLM:\$($Record.PolicyRoot)", "Remove the empty RMS+ $($Record.Browser) policy root")) {
        Remove-PosRegistryKeyIfEmpty $Record.PolicyRoot
    }
}

function Remove-PosBackConnectionHostName {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [object]$Record
    )

    if (-not [bool]$Record.Added) {
        return
    }

    $snapshot = Get-PosRegistryValueSnapshot ([string]$Record.RegistryPath) ([string]$Record.ValueName)
    Assert-PosRegistryValueKind $snapshot $script:RegistryValueKindMultiString 'BackConnectionHostNames'
    $current = @($snapshot.Value)
    $expected = @($Record.ProvisionedValues)
    if (-not $snapshot.Exists -or $current.Count -ne $expected.Count -or (Compare-Object -ReferenceObject $expected -DifferenceObject $current)) {
        throw 'BackConnectionHostNames changed after RMS+ provisioning; refusing to restore over an external loopback policy change.'
    }

    if ([bool]$Record.OriginalExists) {
        if ($PSCmdlet.ShouldProcess("HKLM:\$($Record.RegistryPath)\$($Record.ValueName)", 'Restore the pre-existing BackConnectionHostNames value')) {
            Set-PosRegistryValue $Record.RegistryPath $Record.ValueName @($Record.OriginalValues) $script:RegistryValueKindMultiString
        }
    } elseif ($PSCmdlet.ShouldProcess("HKLM:\$($Record.RegistryPath)\$($Record.ValueName)", 'Remove the RMS+ BackConnectionHostNames value')) {
        Remove-PosRegistryValue $Record.RegistryPath $Record.ValueName
    }
}

function Remove-PosAgentBrowserProvisioning {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [object]$State
    )

    if (-not (Test-PosHasProperty $State 'BrowserProvisioning') -or $null -eq $State.BrowserProvisioning) {
        return
    }

    if (Test-PosHasProperty $State.BrowserProvisioning 'BrowserPolicies') {
        foreach ($record in @($State.BrowserProvisioning.BrowserPolicies)) {
            Remove-PosBrowserLoopbackPolicy $record
            Remove-PosBrowserAuthPolicy $record
        }
    }

    if (Test-PosHasProperty $State.BrowserProvisioning 'BackConnection') {
        Remove-PosBackConnectionHostName $State.BrowserProvisioning.BackConnection
    }
}

function Get-PosAgentBrowserProvisioningVerification {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$SupportHubOrigin,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string]$CanonicalHost
    )

    $normalizedOrigin = Normalize-PosExactOrigin $SupportHubOrigin
    if ($null -eq $normalizedOrigin) {
        throw "SupportHubOrigin must be one exact HTTPS origin: $SupportHubOrigin"
    }

    $CanonicalHost = Normalize-PosExactHostname $CanonicalHost

    $results = @()
    foreach ($browser in @('Chrome', 'Edge')) {
        $installed = Get-PosInstalledBrowser $browser
        if (-not $installed.Installed) {
            $results += [pscustomobject]@{ Browser = $browser; Installed = $false; Result = 'NotInstalled' }
            continue
        }

        $contract = Get-PosBrowserPolicyContract $browser ([int]$installed.MajorVersion)
        Assert-PosNoBlockingPolicyMatch $contract $normalizedOrigin
        $auth = Get-PosRegistryValueSnapshot $contract.PolicyRoot $contract.AuthPolicyName
        Assert-PosRegistryValueKind $auth $script:RegistryValueKindString "$browser $($contract.AuthPolicyName)"
        $authValue = if ($auth.Exists) { [string]$auth.Value } else { $null }
        $authPlan = Merge-PosCommaSeparatedPolicy $authValue $CanonicalHost
        if (-not $authPlan.RequiredEntryPresent) {
            throw "$browser AuthServerAllowlist does not contain the exact Agent hostname."
        }

        $listEntries = @(Get-PosListPolicyEntries "$($contract.PolicyRoot)\$($contract.LoopbackPolicyName)")
        $listPlan = New-PosListPolicyMergePlan $listEntries $normalizedOrigin
        if (-not $listPlan.RequiredOriginPresent) {
            throw "$browser $($contract.LoopbackPolicyName) does not contain the exact Support Hub origin."
        }

        $results += [pscustomobject]@{
                Browser = $browser
                Installed = $true
                Version = $installed.Version
                MajorVersion = $installed.MajorVersion
                AuthServerAllowlist = 'Present'
                LoopbackPolicy = $contract.LoopbackPolicyName
                ExactOrigin = 'Present'
                BlockPolicyConflict = $false
        }
    }

    $backConnection = Get-PosRegistryValueSnapshot 'SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0' 'BackConnectionHostNames'
    Assert-PosRegistryValueKind $backConnection $script:RegistryValueKindMultiString 'BackConnectionHostNames'
    $backConnectionValues = @()
    if ($backConnection.Exists) {
        $backConnectionValues = @($backConnection.Value)
    }
    $null = Merge-PosMultiStringHostnames $backConnectionValues $CanonicalHost
    $hostPresent = if ($backConnection.Exists) {
        @($backConnectionValues | Where-Object { Test-PosEntryEquals $_ $CanonicalHost }).Count -gt 0
    } else {
        $false
    }
    if (-not $hostPresent) {
        throw 'BackConnectionHostNames does not contain the exact Agent hostname.'
    }

    $null = Get-PosDisableLoopbackCheckSnapshot
    return [pscustomobject]@{
        Browsers = @($results)
        BackConnectionHostNames = 'Present'
        DisableLoopbackCheck = 'Absent'
    }
}

Export-ModuleMember -Function @(
    'Normalize-PosExactOrigin',
    'Merge-PosCommaSeparatedPolicy',
    'Merge-PosMultiStringHostnames',
    'Get-PosBrowserPolicyContract',
    'New-PosListPolicyMergePlan',
    'Assert-PosRegistryValueKind',
    'Get-PosInstalledBrowser',
    'Ensure-PosAgentBrowserProvisioning',
    'Remove-PosAgentBrowserProvisioning',
    'Get-PosAgentBrowserProvisioningVerification'
)
