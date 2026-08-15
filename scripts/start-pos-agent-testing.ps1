[CmdletBinding()]
param(
    [switch]$IUnderstandTestingOnly,
    [string]$SupportHubOrigin
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosTestingConfiguration.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'PosSupportHubProvisioning.psm1') -Force

$testingConfiguration = Get-PosTestingConfiguration $SupportHubOrigin
$SupportHubOrigin = $testingConfiguration.SupportHubOrigin
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$setupScript = Join-Path $PSScriptRoot 'setup-pos-agent-testing.ps1'
$backendProject = Join-Path $repositoryRoot 'backend/src/RmsSupportHub.Api/RmsSupportHub.Api.csproj'
$frontendRoot = Join-Path $repositoryRoot 'frontend'
$programDataRoot = Join-Path $env:ProgramData 'DBS/RmsSupportHub/Int13Testing'
$runtimeRoot = Join-Path $programDataRoot 'SupportHubRuntime'
$frontendStageRoot = Join-Path $runtimeRoot 'frontend'
$apiStageRoot = Join-Path $runtimeRoot 'api'
$apiDll = Join-Path $apiStageRoot 'RmsSupportHub.Api.dll'
$stdoutPath = Join-Path $runtimeRoot 'support-hub.stdout.log'
$stderrPath = Join-Path $runtimeRoot 'support-hub.stderr.log'
$statePath = Join-Path $programDataRoot 'provisioning.json'
$supportHubCertificateFriendlyName = "RmsSupportHub INT-13D Testing Support Hub ($($testingConfiguration.SupportHubHost))"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
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

function Get-OwnedRuntimeProcess($state) {
    $processIdText = [string]$state.SupportHubRuntimeProcessId
    if ([string]::IsNullOrWhiteSpace($processIdText)) {
        return $null
    }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$processIdText" -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        $state.SupportHubRuntimeProcessId = $null
        Write-State $state
        return $null
    }

    $expectedApiDll = Get-FullPath ([string]$state.SupportHubRuntimeApiDll)
    $unownedProcess = [string]::IsNullOrWhiteSpace([string]$process.CommandLine) `
        -or [string]$process.CommandLine.IndexOf($expectedApiDll, [StringComparison]::OrdinalIgnoreCase) -lt 0
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

    $ownedProcess = Get-OwnedRuntimeProcess $state
    if ($null -eq $ownedProcess) {
        throw "TCP port $($testingConfiguration.SupportHubPort) already has a listener without INT-13D ownership. Refusing to interfere with it."
    }

    if (@($listeners | Where-Object { $_.LocalAddress -notin @('127.0.0.1', '::1') }).Count -gt 0) {
        throw "The Support Hub Testing listener is not loopback-only on port $($testingConfiguration.SupportHubPort)."
    }
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

function Invoke-SupportHubBuild($state) {
    foreach ($path in @($frontendStageRoot, $apiStageRoot)) {
        $fullPath = Get-FullPath $path
        $runtimePrefix = (Get-FullPath $runtimeRoot).TrimEnd('\') + '\'
        if (-not $fullPath.StartsWith($runtimePrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean a path outside the owned Support Hub runtime root: $path"
        }
        if (Test-Path -LiteralPath $fullPath) {
            Remove-Item -LiteralPath $fullPath -Recurse -Force
        }
    }
    New-Item -ItemType Directory -Path $frontendStageRoot,$apiStageRoot -Force | Out-Null

    Invoke-Checked 'npm' @('ci', '--prefix', $frontendRoot) 'Frontend dependency installation'
    Invoke-Checked 'npm' @('run', 'build', '--prefix', $frontendRoot, '--', '--configuration', 'production', '--output-path', $frontendStageRoot) 'Secure Support Hub Angular build'

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

    Invoke-Checked 'dotnet' @('restore', $backendProject, '--nologo') 'Support Hub API restore'
    Invoke-Checked 'dotnet' @('publish', $backendProject, '-c', 'Release', '--no-restore', '--nologo', '-o', $apiStageRoot) 'Support Hub API publish'

    $wwwroot = Join-Path $apiStageRoot 'wwwroot'
    New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null
    Copy-Item -Path (Join-Path $browserOutput '*') -Destination $wwwroot -Recurse -Force
    if (-not (Test-Path -LiteralPath (Join-Path $wwwroot 'index.html') -PathType Leaf)) {
        throw 'The secure Support Hub staging directory is missing wwwroot/index.html.'
    }
    if (-not (Get-ChildItem -LiteralPath $wwwroot -Recurse -File | Where-Object { $_.Extension -in @('.js', '.css') })) {
        throw 'The secure Support Hub staging directory contains no JavaScript or CSS assets.'
    }

    $state.SupportHubRuntimeApiDll = $apiDll
    $state.SupportHubRuntimePublishedFiles = @(Get-RelativeManifest $runtimeRoot)
    $state.SupportHubRuntimeLogFiles = @($stdoutPath, $stderrPath)
    Write-State $state
}

function Invoke-SecureOriginRequest([string]$Uri) {
    $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 10
    if ([int]$response.StatusCode -ne 200) {
        throw "Secure Support Hub endpoint returned HTTP $([int]$response.StatusCode): $Uri"
    }
    return $response
}

function Wait-ForSecureSupportHub($state) {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
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
            if ($listeners.Count -eq 0 -or @($listeners | Where-Object { $_.LocalAddress -notin @('127.0.0.1', '::1') }).Count -gt 0) {
                throw 'The secure Support Hub listener is not loopback-only.'
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

Assert-Administrator
Assert-TestingAuthorization

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

$existingRuntime = Get-OwnedRuntimeProcess $state
if ($null -ne $existingRuntime) {
    # A live owned process may still serve a previous Angular/API publish. Refresh the owned
    # Testing runtime on every authorized start so the canonical secure origin cannot drift from
    # the current repository source while localhost:4200 and the secure route expose two UIs.
    Stop-OwnedRuntimeProcess $state
}

Invoke-SupportHubBuild $state

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
        -FilePath 'dotnet.exe' `
        -ArgumentList @($apiDll) `
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
Write-State $state

try {
    Wait-ForSecureSupportHub $state
} catch {
    Stop-OwnedRuntimeProcess $state
    throw
}

Write-Host '[PASS] Secure Support Hub root and /tools/pos-maintenance returned the real Angular application over HTTPS.' -ForegroundColor Green
Write-Host "[PASS] Secure Support Hub Testing origin is live at $SupportHubOrigin" -ForegroundColor Green
Write-Host "[PASS] API runtime PID $($process.Id) is owned by the INT-13D staging assembly." -ForegroundColor Green
