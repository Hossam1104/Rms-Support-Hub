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
        [Parameter(Mandatory)][string]$ExpectedCertificateThumbprint
    )

    $bindings = [ordered]@{
        SupportHubRuntimeRoot = $ExpectedRuntimeRoot
        SupportHubRuntimeApiDll = $ExpectedApiDll
        SupportHubRuntimeContentRoot = $ExpectedContentRoot
        SupportHubRuntimeHost = $ExpectedHost
        SupportHubRuntimePort = [string]$ExpectedPort
        SupportHubRuntimeCertificateThumbprint = $ExpectedCertificateThumbprint
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
        [Parameter(Mandatory)][string]$ExpectedAssetManifestHash,
        [Parameter(Mandatory)][DateTime]$NotBeforeUtc
    )

    foreach ($name in @('schemaVersion', 'environment', 'commit', 'buildId', 'builtAtUtc', 'indexHtmlSha256', 'mainBundle')) {
        if ($null -eq $Identity.PSObject.Properties[$name]) {
            throw "The frontend build identity document is missing its $name field."
        }
    }

    if ([int]$Identity.schemaVersion -ne 1) {
        throw 'The frontend build identity document uses an unsupported schema version.'
    }
    if ([string]$Identity.environment -ne $ExpectedEnvironment) {
        throw 'The frontend build identity was produced for a different environment.'
    }
    if ([string]$Identity.commit -ne $ExpectedCommit) {
        throw 'The frontend build identity does not name the current repository commit. Refusing to serve a stale build.'
    }
    if ([string]$Identity.buildId -ne $ExpectedAssetManifestHash) {
        throw 'The staged frontend assets do not hash to the build identity that was generated for them.'
    }

    $builtAtUtc = [DateTime]::MinValue
    $parsed = [DateTime]::TryParse(
        [string]$Identity.builtAtUtc,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AdjustToUniversal -bor [Globalization.DateTimeStyles]::AssumeUniversal,
        [ref]$builtAtUtc)
    if (-not $parsed -or $builtAtUtc -lt $NotBeforeUtc) {
        throw 'The staged frontend build was not produced by this authorized Testing startup. Refusing to serve a stale build.'
    }

    if ($null -eq $Identity.mainBundle -or [string]::IsNullOrWhiteSpace([string]$Identity.mainBundle.file)) {
        throw 'The frontend build identity does not name a hashed main bundle.'
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
    'New-PosSelfElevationArgumentList',
    'Wait-PosLoopbackOnlyListener'
)
