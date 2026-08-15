[CmdletBinding()]
param(
    [switch]$IUnderstandTestingOnly,
    [string]$SupportHubOrigin,
    # CI and automated harnesses opt out of the interactive UAC handoff and are
    # expected to already be elevated.
    [switch]$NoSelfElevate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosTestingConfiguration.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PosSupportHubProvisioning.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PosSupportHubRuntime.psm1') -Force

# Captured before any build so a staged frontend produced by an earlier run can
# never satisfy this run's freshness proof.
$startedUtc = [DateTime]::UtcNow

$testingConfiguration = Get-PosTestingConfiguration $SupportHubOrigin
$SupportHubOrigin = $testingConfiguration.SupportHubOrigin
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$setupScript = Join-Path $PSScriptRoot 'setup-pos-agent-testing.ps1'
$backendProject = Join-Path $repositoryRoot 'backend/src/RmsSupportHub.Api/RmsSupportHub.Api.csproj'
$frontendRoot = Join-Path $repositoryRoot 'frontend'
$buildIdentityScript = Join-Path $frontendRoot 'scripts/build-identity.mjs'
$programDataRoot = Join-Path $env:ProgramData 'DBS/RmsSupportHub/Int13Testing'
$runtimeRoot = Join-Path $programDataRoot 'SupportHubRuntime'
$frontendStageRoot = Join-Path $runtimeRoot 'frontend'
$apiStageRoot = Join-Path $runtimeRoot 'api'
$wwwrootPath = Join-Path $apiStageRoot 'wwwroot'
$apiDll = Join-Path $apiStageRoot 'RmsSupportHub.Api.dll'
$stdoutPath = Join-Path $runtimeRoot 'support-hub.stdout.log'
$stderrPath = Join-Path $runtimeRoot 'support-hub.stderr.log'
$statePath = Join-Path $programDataRoot 'provisioning.json'
$supportHubCertificateFriendlyName = "RmsSupportHub INT-13D Testing Support Hub ($($testingConfiguration.SupportHubHost))"
$buildEnvironment = 'Testing'

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

<#
Re-launches this exact script elevated. Only this script's own known typed
parameters are carried across; the argument line is built by
New-PosSelfElevationArgumentList, which refuses any other script, any
non-exact origin, and any unquoted path. The repository working directory is
set explicitly so a path containing spaces survives the handoff.
#>
function Invoke-SelfElevation {
    $hostPath = Get-PosElevationHostPath
    $arguments = New-PosSelfElevationArgumentList `
        -ScriptPath $PSCommandPath `
        -IUnderstandTestingOnly ([bool]$IUnderstandTestingOnly) `
        -SupportHubOrigin $SupportHubOrigin

    Write-Host 'INT-13D Testing startup requires Administrator rights. Requesting elevation (accept the UAC prompt).' -ForegroundColor Yellow
    try {
        $elevated = Start-Process `
            -FilePath $hostPath `
            -ArgumentList ($arguments -join ' ') `
            -WorkingDirectory $repositoryRoot `
            -Verb RunAs `
            -PassThru
    } catch {
        throw 'Elevation was declined or could not be started. Re-run this command from an elevated Administrator PowerShell session, or pass -NoSelfElevate from an already elevated session.'
    }

    Write-Host "[PASS] Elevated INT-13D Testing startup handed off to PID $($elevated.Id) in $repositoryRoot." -ForegroundColor Green
    Write-Host 'This unelevated session is now finished; follow the elevated window.' -ForegroundColor Yellow
}

function Assert-Administrator {
    if (-not (Test-Administrator)) {
        throw 'INT-13D Testing startup requires an elevated Administrator PowerShell session.'
    }
}

function Assert-TestingAuthorization {
    if (-not $IUnderstandTestingOnly) {
        throw 'Pass -IUnderstandTestingOnly to acknowledge that this command changes only the current representative Testing machine.'
    }

    Write-Warning 'INT-13D TESTING ONLY: this command provisions and starts the real Support Hub application on the current representative Testing machine.'
    Write-Warning 'It must never be run against a Production/customer machine. The secure origin remains loopback-only and uses the configured Testing certificate.'
}

function Get-FullPath([string]$path) {
    return [IO.Path]::GetFullPath($path)
}

function Assert-NoReparsePoint([string]$path) {
    if (Test-Path -LiteralPath $path) {
        $item = Get-Item -LiteralPath $path -Force
        if ($item.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to use reparse-point path: $path"
        }
    }
}

function Ensure-StateProperty($state, [string]$name, $value) {
    if ($null -eq $state.PSObject.Properties[$name]) {
        Add-Member -InputObject $state -MemberType NoteProperty -Name $name -Value $value
    }
}

function Read-State {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        throw "INT-13P/INT-13D provisioning state is missing: $statePath"
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.OwnershipMarker -ne 'RmsSupportHub INT-13P Testing Provisioning v1' -or $state.SchemaVersion -ne 1) {
        throw "Provisioning state is not owned by the expected INT-13 Testing workflow: $statePath"
    }
    if ($state.SupportHubOrigin -ne $SupportHubOrigin -or $state.SupportHubHost -ne $testingConfiguration.SupportHubHost) {
        throw 'Provisioning state and the requested exact Support Hub Testing origin do not match.'
    }
    $certificateIncomplete = -not [bool]$state.SupportHubCertificateCreated `
        -or [string]::IsNullOrWhiteSpace([string]$state.SupportHubCertificateThumbprint) `
        -or -not [bool]$state.SupportHubCertificateTrusted
    if ($certificateIncomplete) {
        throw 'The owned Testing Support Hub certificate is not fully provisioned. Rerun the authorized setup command.'
    }

    foreach ($runtimeProperty in @(
            'SupportHubRuntimeContentRoot',
            'SupportHubRuntimeHost',
            'SupportHubRuntimePort',
            'SupportHubRuntimeCertificateThumbprint',
            'SupportHubRuntimeBuildId',
            'SupportHubRuntimeCommit',
            'SupportHubRuntimeStartedUtc')) {
        Ensure-StateProperty $state $runtimeProperty $null
    }
    return $state
}

function Write-State($state) {
    $state | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Ensure-OwnedRuntimeRoot($state) {
    Assert-NoReparsePoint $programDataRoot
    if (-not (Test-Path -LiteralPath $programDataRoot -PathType Container)) {
        throw "The INT-13 Testing state directory is missing: $programDataRoot"
    }

    if (Test-Path -LiteralPath $runtimeRoot -PathType Container) {
        Assert-NoReparsePoint $runtimeRoot
        if (-not [bool]$state.SupportHubRuntimeRootCreated -or $state.SupportHubRuntimeRoot -ne $runtimeRoot) {
            throw "The Support Hub runtime directory exists without INT-13D ownership: $runtimeRoot"
        }
        return
    }

    New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
    $state.SupportHubRuntimeRootCreated = $true
    $state.SupportHubRuntimeRoot = $runtimeRoot
    Write-State $state
}

function Get-RelativeManifest([string]$root) {
    $fullRoot = (Get-FullPath $root).TrimEnd('\') + '\'
    return @(Get-ChildItem -LiteralPath $root -Recurse -File -Force | ForEach-Object {
        $_.FullName.Substring($fullRoot.Length)
    })
}

<#
Resolves the recorded runtime PID to a live process only when it is still the
owned dotnet host running the owned published assembly. A reused or repurposed
PID fails closed instead of being controlled.
#>
function Get-OwnedRuntimeProcess($state) {
    $processIdText = [string]$state.SupportHubRuntimeProcessId
    if ([string]::IsNullOrWhiteSpace($processIdText) -or $processIdText -notmatch '^[0-9]+$') {
        return $null
    }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$processIdText" -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        $state.SupportHubRuntimeProcessId = $null
        Write-State $state
        return $null
    }

    $recordedApiDll = [string]$state.SupportHubRuntimeApiDll
    if ([string]::IsNullOrWhiteSpace($recordedApiDll)) {
        throw 'The owned Support Hub runtime has a PID but no recorded API assembly. Refusing to control an unverifiable process.'
    }

    $expectedApiDll = Get-FullPath $recordedApiDll
    $commandLine = [string]$process.CommandLine
    $unownedProcess = [string]::IsNullOrWhiteSpace($commandLine) `
        -or ([string]$process.Name) -ne 'dotnet.exe' `
        -or $commandLine.IndexOf($expectedApiDll, [StringComparison]::OrdinalIgnoreCase) -lt 0
    if ($unownedProcess) {
        throw 'The recorded Support Hub runtime PID no longer points to the owned API assembly. Refusing to control it.'
    }
    return $process
}

function Assert-NoUnownedSupportHubListener($state) {
    $listeners = @(Get-NetTCPConnection -LocalPort $testingConfiguration.SupportHubPort -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -eq 0) {
        return
    }

    # Resolving the owned process first collapses a stale recorded PID to $null,
    # so a listener that survives is provably not ours.
    $ownedProcess = Get-OwnedRuntimeProcess $state
    $ownedProcessId = if ($null -eq $ownedProcess) { $null } else { $ownedProcess.ProcessId }
    Assert-PosSupportHubOwnedListener `
        -Port $testingConfiguration.SupportHubPort `
        -OwnedProcessId $ownedProcessId `
        -Listener $listeners | Out-Null
}

function Stop-OwnedRuntimeProcess($state) {
    $process = Get-OwnedRuntimeProcess $state
    if ($null -eq $process) {
        return
    }

    Stop-Process -Id ([int]$process.ProcessId) -Force
    Wait-Process -Id ([int]$process.ProcessId) -Timeout 30 -ErrorAction SilentlyContinue
    $state.SupportHubRuntimeProcessId = $null
    Write-State $state
}

function Invoke-Checked([string]$FilePath, [string[]]$Arguments, [string]$Label) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

function Get-RequiredCommandPath([string]$name, [string]$label) {
    $command = Get-Command -Name $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command) {
        throw "$label could not be resolved on PATH: $name"
    }
    return $command.Source
}

function Get-CurrentCommit {
    $commit = (& git -C $repositoryRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace([string]$commit)) {
        throw 'The current repository commit could not be resolved; refusing to publish an unidentifiable build.'
    }
    return ([string]$commit).Trim()
}

function Get-CurrentSourceState {
    $status = & git -C $repositoryRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw 'The current repository source state could not be resolved.'
    }
    return $(if (@($status | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count -eq 0) { 'clean' } else { 'modified' })
}

function Clear-StagingDirectory([string]$path) {
    $fullPath = Get-FullPath $path
    $runtimePrefix = (Get-FullPath $runtimeRoot).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($runtimePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the owned Support Hub runtime root: $path"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

<#
Rebuilds the CURRENT Angular frontend and the CURRENT Support Hub backend into
the owned runtime root on every authorized start, then stamps and verifies an
immutable build identity so the secure origin cannot silently keep serving a
previously staged frontend.
#>
function Invoke-SupportHubBuild($state) {
    foreach ($path in @($frontendStageRoot, $apiStageRoot)) {
        Clear-StagingDirectory $path
    }
    New-Item -ItemType Directory -Path $frontendStageRoot, $apiStageRoot -Force | Out-Null

    $npmPath = Get-RequiredCommandPath 'npm.cmd' 'The npm launcher'
    $dotnetPath = Get-RequiredCommandPath 'dotnet.exe' 'The .NET CLI'
    $nodePath = Get-RequiredCommandPath 'node.exe' 'The Node.js runtime'
    if (-not (Test-Path -LiteralPath $buildIdentityScript -PathType Leaf)) {
        throw 'The frontend build-identity generator is missing; refusing to stage an unidentifiable build.'
    }

    Invoke-Checked $npmPath @('ci', '--prefix', $frontendRoot) 'Frontend dependency installation'
    Invoke-Checked $npmPath @('run', 'build', '--prefix', $frontendRoot, '--', '--configuration', 'production', '--output-path', $frontendStageRoot) 'Secure Support Hub Angular build'

    $indexFiles = @(Get-ChildItem -LiteralPath $frontendStageRoot -Recurse -Filter 'index.html' -File | Where-Object {
        $_.DirectoryName -like '*\browser'
    })
    if ($indexFiles.Count -eq 0) {
        $indexFiles = @(Get-ChildItem -LiteralPath $frontendStageRoot -Recurse -Filter 'index.html' -File)
    }
    if ($indexFiles.Count -ne 1) {
        throw "The secure Support Hub build did not produce exactly one Angular index.html under $frontendStageRoot."
    }
    $browserOutput = $indexFiles[0].DirectoryName

    $commit = Get-CurrentCommit
    $identityJson = & $nodePath $buildIdentityScript 'finalize' `
        '--output' $browserOutput `
        '--environment' $buildEnvironment `
        '--commit' $commit `
        '--source-state' (Get-CurrentSourceState)
    if ($LASTEXITCODE -ne 0) {
        throw "Frontend build-identity generation failed with exit code $LASTEXITCODE."
    }
    $identity = ($identityJson | Select-Object -Last 1) | ConvertFrom-Json

    Invoke-Checked $dotnetPath @('restore', $backendProject, '--nologo') 'Support Hub API restore'
    Invoke-Checked $dotnetPath @('publish', $backendProject, '-c', 'Release', '--no-restore', '--nologo', '-o', $apiStageRoot) 'Support Hub API publish'

    # Recreate wwwroot so the staged content root is exactly the frontend that
    # was just built; a leftover asset would break the manifest comparison.
    Clear-StagingDirectory $wwwrootPath
    New-Item -ItemType Directory -Path $wwwrootPath -Force | Out-Null
    Copy-Item -Path (Join-Path $browserOutput '*') -Destination $wwwrootPath -Recurse -Force

    if (-not (Test-Path -LiteralPath (Join-Path $wwwrootPath 'index.html') -PathType Leaf)) {
        throw 'The secure Support Hub staging directory is missing wwwroot/index.html.'
    }
    if (-not (Get-ChildItem -LiteralPath $wwwrootPath -Recurse -File | Where-Object { $_.Extension -in @('.js', '.css') })) {
        throw 'The secure Support Hub staging directory contains no JavaScript or CSS assets.'
    }

    $stagedManifestHash = Get-PosFrontendAssetManifestHash -Root $wwwrootPath
    Assert-PosFrontendBuildIdentity `
        -Identity $identity `
        -ExpectedCommit $commit `
        -ExpectedEnvironment $buildEnvironment `
        -ExpectedAssetManifestHash $stagedManifestHash `
        -NotBeforeUtc $startedUtc
    Write-Host "[PASS] Staged frontend build $($identity.buildId.Substring(0, 12)) for commit $($identity.commitShort) matches its $($identity.assetCount)-asset manifest." -ForegroundColor Green

    $state.SupportHubRuntimeApiDll = $apiDll
    $state.SupportHubRuntimeContentRoot = $apiStageRoot
    $state.SupportHubRuntimeHost = $testingConfiguration.SupportHubHost
    $state.SupportHubRuntimePort = $testingConfiguration.SupportHubPort
    $state.SupportHubRuntimeCertificateThumbprint = [string]$state.SupportHubCertificateThumbprint
    $state.SupportHubRuntimeBuildId = $identity.buildId
    $state.SupportHubRuntimeCommit = $identity.commit
    $state.SupportHubRuntimePublishedFiles = @(Get-RelativeManifest $runtimeRoot)
    $state.SupportHubRuntimeLogFiles = @($stdoutPath, $stderrPath)
    Write-State $state

    return $identity
}

function Invoke-SecureOriginRequest([string]$Uri) {
    $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 10
    if ([int]$response.StatusCode -ne 200) {
        throw "Secure Support Hub endpoint returned HTTP $([int]$response.StatusCode): $Uri"
    }
    return $response
}

function Wait-ForSecureSupportHub($state) {
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    $rootUrl = "$SupportHubOrigin/"
    $maintenanceUrl = "$SupportHubOrigin/tools/pos-maintenance"
    do {
        try {
            $rootResponse = Invoke-SecureOriginRequest $rootUrl
            $maintenanceResponse = Invoke-SecureOriginRequest $maintenanceUrl
            if ($rootResponse.Content -notmatch '<app-root\b' -or $maintenanceResponse.Content -notmatch '<app-root\b') {
                throw 'The secure origin did not return the real Angular Support Hub application shell.'
            }

            $listeners = @(Get-NetTCPConnection -LocalPort $testingConfiguration.SupportHubPort -State Listen -ErrorAction Stop)
            $ownership = Assert-PosSupportHubOwnedListener `
                -Port $testingConfiguration.SupportHubPort `
                -OwnedProcessId $state.SupportHubRuntimeProcessId `
                -Listener $listeners
            if ($ownership -ne 'owned') {
                throw 'The secure Support Hub listener is not the runtime this startup launched.'
            }

            return
        } catch {
            if ([DateTime]::UtcNow -ge $deadline) {
                $details = @()
                if (Test-Path -LiteralPath $stderrPath -PathType Leaf) {
                    $details += (Get-Content -LiteralPath $stderrPath -Tail 20 -ErrorAction SilentlyContinue)
                }
                throw "The secure Support Hub origin did not become healthy: $($_.Exception.Message)`n$($details -join [Environment]::NewLine)"
            }
            Start-Sleep -Milliseconds 500
        }
    } while ($true)
}

<#
Proves the bytes the secure origin serves are the exact bytes just staged.
HTTP 200 plus an <app-root> element is not evidence of freshness, so the
identity document, the application shell, the canonical POS deep link, and the
hashed main bundle are all compared against the staged build.
#>
function Assert-ServedFrontendIsCurrentBuild($identity) {
    $stagedIdentityPath = Join-Path $wwwrootPath 'build-identity.json'
    $stagedIdentityHash = Get-PosFileSha256Hex $stagedIdentityPath
    $servedIdentityHash = Get-PosSha256Hex (Get-PosHttpResourceBytes "$SupportHubOrigin/build-identity.json")
    if ($servedIdentityHash -ne $stagedIdentityHash) {
        throw 'The secure Support Hub origin served a different build-identity document than the one staged by this startup.'
    }

    foreach ($url in @("$SupportHubOrigin/", "$SupportHubOrigin/tools/pos-maintenance")) {
        $servedShellHash = Get-PosSha256Hex (Get-PosHttpResourceBytes $url)
        if ($servedShellHash -ne [string]$identity.indexHtmlSha256) {
            throw "The secure Support Hub origin served a stale application shell for $url."
        }
    }

    $mainBundleUrl = "$SupportHubOrigin/$([string]$identity.mainBundle.file)"
    $servedBundleHash = Get-PosSha256Hex (Get-PosHttpResourceBytes $mainBundleUrl)
    if ($servedBundleHash -ne [string]$identity.mainBundle.sha256) {
        throw 'The secure Support Hub origin served a stale main application bundle.'
    }

    Write-Host "[PASS] Served build identity, application shell, canonical POS route, and main bundle all match staged build $($identity.buildId.Substring(0, 12))." -ForegroundColor Green
}

if (-not (Test-Administrator)) {
    if ($NoSelfElevate) {
        Assert-Administrator
    }

    Invoke-SelfElevation
    return
}

Assert-Administrator
Assert-TestingAuthorization

if (-not (Test-Path -LiteralPath $setupScript -PathType Leaf)) {
    throw "The authorized INT-13P prerequisite setup script is missing: $setupScript"
}
& $setupScript -IUnderstandTestingOnly -SupportHubOrigin $SupportHubOrigin -Confirm:$false
if (-not $?) {
    throw 'The authorized INT-13P prerequisite setup did not complete successfully.'
}

$state = Read-State
Ensure-OwnedRuntimeRoot $state
Assert-NoUnownedSupportHubListener $state

$certificate = Get-PosSupportHubCertificate `
    -Thumbprint ([string]$state.SupportHubCertificateThumbprint) `
    -Hostname $testingConfiguration.SupportHubHost `
    -FriendlyName $supportHubCertificateFriendlyName
if ($null -eq $certificate) {
    throw 'The exact trusted Testing Support Hub certificate could not be loaded from LocalMachine.'
}

$addresses = @([Net.Dns]::GetHostAddresses($testingConfiguration.SupportHubHost) | ForEach-Object { $_.IPAddressToString })
if ($addresses.Count -eq 0 -or @($addresses | Where-Object { $_ -notin @('127.0.0.1', '::1') }).Count -gt 0) {
    throw "The Support Hub Testing host did not resolve only to loopback: $($addresses -join ', ')"
}
Write-Host "[PASS] $($testingConfiguration.SupportHubHost) resolves only to $($addresses -join ', ')" -ForegroundColor Green

# A live owned process may still serve a previous Angular/API publish. Refresh the owned
# Testing runtime on every authorized start so the canonical secure origin cannot drift from
# the current repository source while localhost:4200 and the secure route expose two UIs.
Stop-OwnedRuntimeProcess $state

$buildIdentity = Invoke-SupportHubBuild $state

if (Test-Path -LiteralPath $stdoutPath) { Remove-Item -LiteralPath $stdoutPath -Force }
if (Test-Path -LiteralPath $stderrPath) { Remove-Item -LiteralPath $stderrPath -Force }

$environmentNames = @(
    'DOTNET_ENVIRONMENT',
    'ASPNETCORE_ENVIRONMENT',
    'AllowedHosts',
    'Kestrel__Endpoints__Https__Url',
    'Kestrel__Endpoints__Https__Protocols',
    'Kestrel__Endpoints__Https__Certificate__Subject',
    'Kestrel__Endpoints__Https__Certificate__Store',
    'Kestrel__Endpoints__Https__Certificate__Location',
    'Kestrel__Endpoints__Https__Certificate__AllowInvalid'
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$dotnetHostPath = Get-RequiredCommandPath 'dotnet.exe' 'The .NET CLI'
try {
    $env:DOTNET_ENVIRONMENT = 'Testing'
    $env:ASPNETCORE_ENVIRONMENT = 'Testing'
    $env:AllowedHosts = $testingConfiguration.SupportHubHost
    $env:Kestrel__Endpoints__Https__Url = "https://127.0.0.1:$($testingConfiguration.SupportHubPort)"
    $env:Kestrel__Endpoints__Https__Protocols = 'Http1'
    $env:Kestrel__Endpoints__Https__Certificate__Subject = $testingConfiguration.SupportHubHost
    $env:Kestrel__Endpoints__Https__Certificate__Store = 'My'
    $env:Kestrel__Endpoints__Https__Certificate__Location = 'LocalMachine'
    $env:Kestrel__Endpoints__Https__Certificate__AllowInvalid = 'false'

    $process = Start-Process `
        -FilePath $dotnetHostPath `
        -ArgumentList ('"{0}"' -f (Get-FullPath $apiDll)) `
        -WorkingDirectory $apiStageRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru
} finally {
    foreach ($name in $environmentNames) {
        $value = $previousEnvironment[$name]
        if ($null -eq $value) {
            Remove-Item "Env:$name" -ErrorAction SilentlyContinue
        } else {
            Set-Item "Env:$name" $value
        }
    }
}

$state.SupportHubRuntimeProcessId = $process.Id
$state.SupportHubRuntimeStartedUtc = $startedUtc.ToString('O')
Write-State $state

try {
    Wait-ForSecureSupportHub $state

    # The launched process must still be the owned runtime, and the state must
    # still bind every identity it claims, before freshness is asserted.
    $launchedProcess = Get-OwnedRuntimeProcess $state
    if ($null -eq $launchedProcess -or [int]$launchedProcess.ProcessId -ne [int]$process.Id) {
        throw 'The secure Support Hub runtime that answered is not the process this startup launched.'
    }
    Assert-PosSupportHubRuntimeStateBinding `
        -State $state `
        -ExpectedRuntimeRoot $runtimeRoot `
        -ExpectedApiDll $apiDll `
        -ExpectedContentRoot $apiStageRoot `
        -ExpectedHost $testingConfiguration.SupportHubHost `
        -ExpectedPort $testingConfiguration.SupportHubPort `
        -ExpectedCertificateThumbprint ([string]$state.SupportHubCertificateThumbprint)

    Assert-ServedFrontendIsCurrentBuild $buildIdentity
} catch {
    Stop-OwnedRuntimeProcess $state
    throw
}

Write-Host '[PASS] Secure Support Hub root and /tools/pos-maintenance returned the real Angular application over HTTPS.' -ForegroundColor Green
Write-Host "[PASS] Secure Support Hub Testing origin is live at $SupportHubOrigin" -ForegroundColor Green
Write-Host "[PASS] API runtime PID $($process.Id) is owned by the INT-13D staging assembly." -ForegroundColor Green
Write-Host "[PASS] Serving commit $($buildIdentity.commitShort) build $($buildIdentity.buildId.Substring(0, 12)) ($($buildIdentity.environment), source $($buildIdentity.sourceState))." -ForegroundColor Green
