Set-StrictMode -Version Latest

<#
Testing-only helpers for the secure Support Hub runtime.

These exist as a module so the ownership, build-identity, and self-elevation
boundaries can be exercised by Pester without provisioning a machine. The
Testing startup script is the only intended caller.
#>

$script:PosBuildIdentityFileName = 'build-identity.json'
$script:PosStartupScriptName = 'start-pos-agent-testing.ps1'
$script:PosLoopbackAddresses = @('127.0.0.1', '::1')
$script:PosSupportedBuildEnvironments = @('Testing')
$script:PosFrontendAssetCountLimit = 100000
$script:PosSha256Pattern = '^[0-9a-f]{64}$'
$script:PosGitCommitPattern = '^[0-9a-f]{40}$'
$script:PosMainBundlePattern = '^main-[A-Za-z0-9]+\.js$'

function Get-PosSha256Hex {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant()
    } finally {
        $algorithm.Dispose()
    }
}

function Get-PosFileSha256Hex {
    param([Parameter(Mandatory)][string]$Path)

    return Get-PosSha256Hex ([IO.File]::ReadAllBytes($Path))
}

<#
.SYNOPSIS
Recomputes the emitted-asset manifest hash for a staged frontend directory.

.DESCRIPTION
This is the PowerShell side of the algorithm in
`frontend/scripts/build-identity.mjs`: ordinal-sorted forward-slash relative
paths, one `"<path> <sha256>\n"` line each, excluding the identity document,
hashed as UTF-8. Recomputing it locally proves the staged bytes still match the
identity document that was generated for them, so a partially copied or
hand-edited staging directory fails before the runtime is started.
#>
function Get-PosFrontendAssetManifestHash {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Root,
        [string[]]$ExcludeRelativePath = @($script:PosBuildIdentityFileName)
    )

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw 'The staged frontend directory to hash does not exist.'
    }

    $prefix = ([IO.Path]::GetFullPath($Root)).TrimEnd('\') + '\'
    $relativePaths = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Force | ForEach-Object {
        $_.FullName.Substring($prefix.Length).Replace('\', '/')
    } | Where-Object { $_ -notin $ExcludeRelativePath })

    if ($relativePaths.Count -eq 0) {
        throw 'The staged frontend directory contains no hashable assets.'
    }

    # Ordinal sort so PowerShell and Node agree byte for byte; Sort-Object is
    # culture-sensitive even with -CaseSensitive and would drift here.
    $ordered = [string[]]$relativePaths
    [Array]::Sort($ordered, [StringComparer]::Ordinal)

    $builder = [Text.StringBuilder]::new()
    foreach ($relativePath in $ordered) {
        $hash = Get-PosFileSha256Hex (Join-Path $Root ($relativePath -replace '/', '\'))
        [void]$builder.Append("$relativePath $hash`n")
    }

    return Get-PosSha256Hex ([Text.Encoding]::UTF8.GetBytes($builder.ToString()))
}

function ConvertFrom-PosBuildIdentityGeneratorOutput {
    <#
    Parses the success stream from the build-identity generator without
    treating a later output item as authoritative. Native diagnostics are not
    redirected into this parameter; callers keep them on the host/error
    streams, while this helper accepts exactly one stdout record.
    #>
    [CmdletBinding()]
    param(
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]]$Output
    )

    $records = @($Output)
    if ($records.Count -ne 1) {
        throw "The build-identity generator must emit exactly one JSON identity record; received $($records.Count) success-stream records."
    }

    if ($records[0] -isnot [string]) {
        throw 'The build-identity generator emitted a non-text success-stream object; refusing to trust it.'
    }

    $json = [string]$records[0]
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw 'The build-identity generator emitted an empty or whitespace-only success stream.'
    }

    $trimmedJson = $json.Trim()
    if (-not $trimmedJson.StartsWith('{', [StringComparison]::Ordinal) -or -not $trimmedJson.EndsWith('}', [StringComparison]::Ordinal)) {
        throw 'The build-identity generator must emit one JSON object, not an array, scalar, or null value.'
    }

    try {
        $identity = ConvertFrom-Json -InputObject $json -ErrorAction Stop
    } catch {
        throw "The build-identity generator emitted malformed JSON: $($_.Exception.Message)"
    }

    if ($null -eq $identity -or $identity -isnot [pscustomobject]) {
        throw 'The build-identity generator must emit one JSON object, not an array, scalar, or null value.'
    }

    return $identity
}

<#
.SYNOPSIS
Fails closed unless every listener on the port belongs to the owned runtime.

.DESCRIPTION
An unowned listener is never adopted and never stopped; a stale listener owned
by the recorded provisioning PID may be replaced through the normal flow. The
listener collection is a parameter so the decision is directly testable.
#>
function Assert-PosSupportHubOwnedListener {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Port,
        [AllowNull()][object]$OwnedProcessId,
        [AllowNull()][AllowEmptyCollection()][object[]]$Listener
    )

    $listeners = @($Listener | Where-Object { $null -ne $_ })
    if ($listeners.Count -eq 0) {
        return 'none'
    }

    $ownedText = [string]$OwnedProcessId
    if ([string]::IsNullOrWhiteSpace($ownedText) -or $ownedText -notmatch '^[0-9]+$' -or [int]$ownedText -le 0) {
        throw "TCP port $Port already has a listener without INT-13D ownership. Refusing to interfere with it."
    }

    $foreign = @($listeners | Where-Object { [string]$_.OwningProcess -ne $ownedText })
    if ($foreign.Count -gt 0) {
        throw "TCP port $Port has a listener owned by another process. Refusing to stop or adopt it."
    }

    $routable = @($listeners | Where-Object { [string]$_.LocalAddress -notin $script:PosLoopbackAddresses })
    if ($routable.Count -gt 0) {
        throw "The Support Hub Testing listener is not loopback-only on port $Port."
    }

    return 'owned'
}

<#
.SYNOPSIS
Fails closed unless the supplied listeners are all loopback-only.

.DESCRIPTION
Ownership is not asserted here: this is the service-port check, where the
service manages its own process. The listener collection is a parameter so the
decision stays testable without binding a real socket.
#>
function Assert-PosLoopbackOnlyListener {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Port,
        [AllowNull()][AllowEmptyCollection()][object[]]$Listener
    )

    $listeners = @($Listener | Where-Object { $null -ne $_ })
    if ($listeners.Count -eq 0) {
        throw "No listener is bound to TCP port $Port."
    }

    $routable = @($listeners | Where-Object { [string]$_.LocalAddress -notin $script:PosLoopbackAddresses })
    if ($routable.Count -gt 0) {
        throw "The TCP port $Port listener was not loopback-only."
    }
}

<#
.SYNOPSIS
Waits a bounded time for a service to bind its loopback-only port.

.DESCRIPTION
A Windows Service reaches Running as soon as its host starts, which is before
the hosted web server has bound its socket. Sampling the port once therefore
turns an ordinary startup race into a provisioning failure. The wait stays
fail-closed: a routable listener is rejected as soon as it is observed, and an
absent listener still throws once the deadline passes.
#>
function Wait-PosLoopbackOnlyListener {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Port,
        [ValidateRange(1, 600)][int]$TimeoutSeconds = 60,
        [scriptblock]$ListenerProvider
    )

    if ($null -eq $ListenerProvider) {
        $ListenerProvider = { param($p) @(Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue) }
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $listeners = @(& $ListenerProvider $Port | Where-Object { $null -ne $_ })
        if ($listeners.Count -gt 0) {
            Assert-PosLoopbackOnlyListener -Port $Port -Listener $listeners
            return $listeners
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            break
        }
        Start-Sleep -Milliseconds 500
    } while ($true)

    throw "No listener reached TCP port $Port within $TimeoutSeconds seconds."
}

<#
.SYNOPSIS
Verifies the recorded runtime state still binds every identity it claims.
#>
function Assert-PosSupportHubRuntimeStateBinding {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$State,
        [Parameter(Mandatory)][string]$ExpectedRuntimeRoot,
        [Parameter(Mandatory)][string]$ExpectedApiDll,
        [Parameter(Mandatory)][string]$ExpectedContentRoot,
        [Parameter(Mandatory)][string]$ExpectedHost,
        [Parameter(Mandatory)][int]$ExpectedPort,
        [Parameter(Mandatory)][string]$ExpectedCertificateThumbprint,
        [Parameter(Mandatory)][int]$ExpectedProcessId,
        [Parameter(Mandatory)][string]$ExpectedBuildId,
        [Parameter(Mandatory)][string]$ExpectedCommit
    )

    if ($ExpectedProcessId -le 0) {
        throw 'The expected Support Hub runtime PID is invalid.'
    }
    if ($ExpectedBuildId -notmatch $script:PosSha256Pattern) {
        throw 'The expected Support Hub runtime build ID is not a lowercase SHA-256 value.'
    }
    if ($ExpectedCommit -notmatch $script:PosGitCommitPattern) {
        throw 'The expected Support Hub runtime commit is not a lowercase full Git SHA.'
    }

    $bindings = [ordered]@{
        SupportHubRuntimeRoot = $ExpectedRuntimeRoot
        SupportHubRuntimeApiDll = $ExpectedApiDll
        SupportHubRuntimeContentRoot = $ExpectedContentRoot
        SupportHubRuntimeHost = $ExpectedHost
        SupportHubRuntimePort = [string]$ExpectedPort
        SupportHubRuntimeCertificateThumbprint = $ExpectedCertificateThumbprint
        SupportHubRuntimeProcessId = [string]$ExpectedProcessId
        SupportHubRuntimeBuildId = $ExpectedBuildId
        SupportHubRuntimeCommit = $ExpectedCommit
    }

    foreach ($binding in $bindings.GetEnumerator()) {
        $property = $State.PSObject.Properties[$binding.Key]
        if ($null -eq $property) {
            throw "The Support Hub runtime state is missing its $($binding.Key) binding. Refusing to trust it."
        }
        if (([string]$property.Value) -ne ([string]$binding.Value)) {
            throw "The Support Hub runtime state binding $($binding.Key) does not match the current authorized Testing configuration."
        }
    }
}

<#
.SYNOPSIS
Compares a staged build-identity document against the current expectations.

.DESCRIPTION
A build is only accepted when it names the current commit and was produced
during this startup run. That is what turns "HTTP 200 with an <app-root>" into
a real freshness proof: a previously staged build carries an older
`builtAtUtc`, a different `buildId`, or a different commit and fails here.
#>
function Assert-PosFrontendBuildIdentity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Identity,
        [Parameter(Mandatory)][string]$ExpectedCommit,
        [Parameter(Mandatory)][string]$ExpectedEnvironment,
        [Parameter(Mandatory)][ValidateSet('clean', 'modified')][string]$ExpectedSourceState,
        [Parameter(Mandatory)][string]$ExpectedAssetManifestHash,
        [Parameter(Mandatory)][string]$AssetRoot,
        [Parameter(Mandatory)][DateTime]$NotBeforeUtc
    )

    if ($null -eq $Identity -or $Identity -isnot [pscustomobject]) {
        throw 'The frontend build identity must be a JSON object.'
    }

    $expectedFields = @('schemaVersion', 'environment', 'commit', 'commitShort', 'sourceState', 'buildId', 'assetCount', 'builtAtUtc', 'indexHtmlSha256', 'mainBundle')
    $actualFields = @($Identity.PSObject.Properties.Name)
    foreach ($name in $expectedFields) {
        if ($null -eq $Identity.PSObject.Properties[$name]) {
            throw "The frontend build identity document is missing its $name field."
        }
    }
    if (@($actualFields | Where-Object { $_ -notin $expectedFields }).Count -gt 0) {
        throw 'The frontend build identity document contains an unexpected field.'
    }

    if ($Identity.schemaVersion -is [bool] -or $Identity.schemaVersion -is [string] -or ($Identity.schemaVersion -isnot [int] -and $Identity.schemaVersion -isnot [long]) -or [int64]$Identity.schemaVersion -ne 1) {
        throw 'The frontend build identity document uses an unsupported or incorrectly typed schema version.'
    }
    if ($ExpectedEnvironment -notin $script:PosSupportedBuildEnvironments -or $Identity.environment -isnot [string] -or [string]$Identity.environment -ne $ExpectedEnvironment) {
        throw 'The frontend build identity was produced for a different or unsupported environment.'
    }
    if ($ExpectedCommit -notmatch $script:PosGitCommitPattern -or $Identity.commit -isnot [string] -or [string]$Identity.commit -notmatch $script:PosGitCommitPattern) {
        throw 'The frontend build identity does not contain a valid full Git commit SHA.'
    }
    if ([string]$Identity.commit -ne $ExpectedCommit) {
        throw 'The frontend build identity does not name the current repository commit. Refusing to serve a stale build.'
    }
    if ($Identity.commitShort -isnot [string] -or [string]$Identity.commitShort -ne $ExpectedCommit.Substring(0, 7)) {
        throw 'The frontend build identity commitShort does not equal the expected commit prefix.'
    }
    if ($Identity.sourceState -isnot [string] -or [string]$Identity.sourceState -notin @('clean', 'modified') -or [string]$Identity.sourceState -ne $ExpectedSourceState) {
        throw 'The frontend build identity has an unsupported or unexpected sourceState.'
    }
    if ($Identity.buildId -isnot [string] -or [string]$Identity.buildId -notmatch $script:PosSha256Pattern) {
        throw 'The frontend build identity buildId is not a lowercase SHA-256 value.'
    }
    if ([string]$Identity.buildId -ne $ExpectedAssetManifestHash) {
        throw 'The staged frontend assets do not hash to the build identity that was generated for them.'
    }

    if ($Identity.assetCount -is [bool] -or $Identity.assetCount -is [string] -or ($Identity.assetCount -isnot [int] -and $Identity.assetCount -isnot [long])) {
        throw 'The frontend build identity assetCount is not an integer.'
    }
    if ([int64]$Identity.assetCount -le 0 -or [int64]$Identity.assetCount -gt $script:PosFrontendAssetCountLimit) {
        throw 'The frontend build identity assetCount is outside the sane supported range.'
    }

    if (-not (Test-Path -LiteralPath $AssetRoot -PathType Container)) {
        throw 'The staged frontend directory for build-identity validation does not exist.'
    }
    $assetPrefix = ([IO.Path]::GetFullPath($AssetRoot)).TrimEnd('\') + '\'
    $assetFiles = @(Get-ChildItem -LiteralPath $AssetRoot -Recurse -File -Force | Where-Object {
        $_.FullName.Substring($assetPrefix.Length).Replace('\', '/') -ne $script:PosBuildIdentityFileName
    })
    if ($assetFiles.Count -ne [int64]$Identity.assetCount) {
        throw 'The frontend build identity assetCount does not match the staged asset set.'
    }
    if ((Get-PosFrontendAssetManifestHash -Root $AssetRoot) -ne [string]$Identity.buildId) {
        throw 'The staged frontend asset manifest does not match buildId.'
    }

    if ($Identity.indexHtmlSha256 -isnot [string] -or [string]$Identity.indexHtmlSha256 -notmatch $script:PosSha256Pattern) {
        throw 'The frontend build identity indexHtmlSha256 is not a lowercase SHA-256 value.'
    }
    $indexPath = Join-Path $AssetRoot 'index.html'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf) -or (Get-PosFileSha256Hex $indexPath) -ne [string]$Identity.indexHtmlSha256) {
        throw 'The frontend build identity indexHtmlSha256 does not match the staged index.html.'
    }

    if ($null -eq $Identity.mainBundle -or $Identity.mainBundle -isnot [pscustomobject]) {
        throw 'The frontend build identity mainBundle must be a JSON object.'
    }
    $mainFields = @($Identity.mainBundle.PSObject.Properties.Name)
    foreach ($name in @('file', 'sha256')) {
        if ($null -eq $Identity.mainBundle.PSObject.Properties[$name]) {
            throw "The frontend build identity mainBundle is missing its $name field."
        }
    }
    if (@($mainFields | Where-Object { $_ -notin @('file', 'sha256') }).Count -gt 0) {
        throw 'The frontend build identity mainBundle contains an unexpected field.'
    }
    if ($Identity.mainBundle.file -isnot [string] -or [string]$Identity.mainBundle.file -notmatch $script:PosMainBundlePattern) {
        throw 'The frontend build identity mainBundle.file is not a safe hashed main bundle filename.'
    }
    if ($Identity.mainBundle.sha256 -isnot [string] -or [string]$Identity.mainBundle.sha256 -notmatch $script:PosSha256Pattern) {
        throw 'The frontend build identity mainBundle.sha256 is not a lowercase SHA-256 value.'
    }
    $mainBundlePath = Join-Path $AssetRoot ([string]$Identity.mainBundle.file)
    if (-not (Test-Path -LiteralPath $mainBundlePath -PathType Leaf) -or (Get-PosFileSha256Hex $mainBundlePath) -ne [string]$Identity.mainBundle.sha256) {
        throw 'The frontend build identity mainBundle.sha256 does not match the staged main bundle.'
    }

    if ($Identity.builtAtUtc -isnot [string] -or [string]$Identity.builtAtUtc -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$') {
        throw 'The frontend build identity builtAtUtc must be an ISO-8601 UTC timestamp.'
    }
    $builtAtUtc = [DateTime]::MinValue
    $parsed = [DateTime]::TryParse(
        [string]$Identity.builtAtUtc,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind,
        [ref]$builtAtUtc)
    if (-not $parsed -or $builtAtUtc.Kind -ne [DateTimeKind]::Utc -or $builtAtUtc -lt $NotBeforeUtc.ToUniversalTime() -or $builtAtUtc -gt [DateTime]::UtcNow.AddMinutes(5)) {
        throw 'The staged frontend build was not produced by this authorized Testing startup. Refusing to serve a stale build.'
    }
}

<#
.SYNOPSIS
Builds the confined argument line used to re-launch Testing startup elevated.

.DESCRIPTION
Only this script's own known typed parameters are forwarded. The script path
must be the expected Testing startup script, the origin must already have been
normalized to the exact Testing origin, and no caller-supplied token is ever
appended. Quoting is explicit so a repository path containing spaces survives
the ShellExecute round trip.
#>
function New-PosSelfElevationArgumentList {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [Parameter(Mandatory)][bool]$IUnderstandTestingOnly,
        [AllowNull()][AllowEmptyString()][string]$SupportHubOrigin
    )

    $fullScriptPath = [IO.Path]::GetFullPath($ScriptPath)
    if ([IO.Path]::GetFileName($fullScriptPath) -ne $script:PosStartupScriptName) {
        throw "Self-elevation may only re-launch $script:PosStartupScriptName."
    }
    if (-not (Test-Path -LiteralPath $fullScriptPath -PathType Leaf)) {
        throw 'The Testing startup script to re-launch could not be found.'
    }
    if ($fullScriptPath.IndexOf('"', [StringComparison]::Ordinal) -ge 0) {
        throw 'Refusing to self-elevate from a script path containing a quote character.'
    }

    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', ('"{0}"' -f $fullScriptPath))
    if ($IUnderstandTestingOnly) {
        $arguments += '-IUnderstandTestingOnly'
    }
    if (-not [string]::IsNullOrWhiteSpace($SupportHubOrigin)) {
        if ($SupportHubOrigin -notmatch '^https://[A-Za-z0-9.\-]+:[0-9]{1,5}$') {
            throw 'Refusing to forward a Support Hub origin that is not an exact scheme/host/port origin.'
        }
        $arguments += @('-SupportHubOrigin', ('"{0}"' -f $SupportHubOrigin))
    }

    return $arguments
}

<#
.SYNOPSIS
Resolves the PowerShell host used for the elevated re-launch.

.DESCRIPTION
Restricted to the running edition's own executable under $PSHOME so the
elevated process can never be an arbitrary caller-selected binary.
#>
function Get-PosElevationHostPath {
    [CmdletBinding()]
    param([string]$PowerShellHome = $PSHOME)

    foreach ($name in @('pwsh.exe', 'powershell.exe')) {
        $candidate = Join-Path $PowerShellHome $name
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw 'No PowerShell host executable was found in $PSHOME for the elevated re-launch.'
}

<#
.SYNOPSIS
Reads an exact HTTP response body as bytes without following redirects.
#>
function Get-PosHttpResourceBytes {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Uri,
        [int]$TimeoutSeconds = 15
    )

    $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -MaximumRedirection 0 -TimeoutSec $TimeoutSeconds
    if ([int]$response.StatusCode -ne 200) {
        throw "Secure Support Hub endpoint returned HTTP $([int]$response.StatusCode)."
    }

    $stream = $response.RawContentStream
    $stream.Position = 0
    $buffer = [byte[]]::new($stream.Length)
    [void]$stream.Read($buffer, 0, $buffer.Length)
    return $buffer
}

Export-ModuleMember -Function @(
    'Assert-PosFrontendBuildIdentity',
    'Assert-PosLoopbackOnlyListener',
    'Assert-PosSupportHubOwnedListener',
    'Assert-PosSupportHubRuntimeStateBinding',
    'Get-PosElevationHostPath',
    'Get-PosFileSha256Hex',
    'Get-PosFrontendAssetManifestHash',
    'Get-PosHttpResourceBytes',
    'Get-PosSha256Hex',
    'ConvertFrom-PosBuildIdentityGeneratorOutput',
    'New-PosSelfElevationArgumentList',
    'Wait-PosLoopbackOnlyListener'
)
