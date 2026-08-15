[CmdletBinding()]
param(
    [ValidateSet('Hold', 'Compete', 'TryOnce')]
    [string]$ProbeMode,
    [string]$ReadyPath,
    [string]$ResultPath,
    [string]$ReleasePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$semaphoreName = 'Global\RmsSupportHub.Pos.Agent.PrivilegedMutationLease'

function Write-ProofFile([string]$path, [string]$value) {
    [IO.File]::WriteAllText($path, $value, [Text.UTF8Encoding]::new($false))
}

function Wait-ProofValue([string]$path, [string]$expected, [int]$timeoutSeconds = 30) {
    $deadline = [DateTime]::UtcNow.AddSeconds($timeoutSeconds)
    do {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $value = Get-Content -LiteralPath $path -Raw -ErrorAction SilentlyContinue
            if ($value -eq $expected) {
                return
            }
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for $path to contain '$expected'."
}

function Invoke-LeaseProbe {
    if ([string]::IsNullOrWhiteSpace($ResultPath)) {
        throw 'The lease probe result path is missing.'
    }
    if ($ProbeMode -eq 'Hold' -and [string]::IsNullOrWhiteSpace($ReadyPath)) {
        throw 'The lease holder ready path is missing.'
    }

    $createdNew = $false
    $semaphore = [Threading.Semaphore]::new(1, 1, $semaphoreName, [ref]$createdNew)
    try {
        switch ($ProbeMode) {
            'Hold' {
                if (-not $semaphore.WaitOne(0)) {
                    Write-ProofFile $ResultPath 'busy'
                    return 2
                }

                Write-ProofFile $ReadyPath 'acquired'
                Wait-ProofValue -path $ReleasePath -expected 'release' -timeoutSeconds 60
                [void]$semaphore.Release()
                Write-ProofFile $ResultPath 'released'
                return 0
            }
            'Compete' {
                if ($semaphore.WaitOne(0)) {
                    [void]$semaphore.Release()
                    Write-ProofFile $ResultPath 'unexpected-acquired'
                    return 2
                }

                Write-ProofFile $ResultPath 'busy'
                Wait-ProofValue -path $ReleasePath -expected 'release' -timeoutSeconds 60
                if (-not $semaphore.WaitOne(5000)) {
                    Write-ProofFile $ResultPath 'failed-after-release'
                    return 2
                }

                Write-ProofFile $ResultPath 'acquired'
                # The parent terminates this real process after observing the
                # acquisition. That termination is the proof that the OS-owned
                # semaphore handle is released without an application cleanup.
                while ($true) { Start-Sleep -Milliseconds 250 }
            }
            'TryOnce' {
                if (-not $semaphore.WaitOne(0)) {
                    Write-ProofFile $ResultPath 'busy'
                    return 2
                }

                [void]$semaphore.Release()
                Write-ProofFile $ResultPath 'acquired'
                return 0
            }
            default {
                throw 'Unknown internal lease probe mode.'
            }
        }
    } finally {
        $semaphore.Dispose()
    }
}

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not [string]::IsNullOrWhiteSpace($ProbeMode)) {
    exit (Invoke-LeaseProbe)
}

if (-not (Test-Administrator)) {
    throw 'This Testing-only proof requires an elevated Administrator PowerShell. Run once from an elevated window: .\scripts\test-pos-privileged-lease.ps1'
}

$scriptPath = [IO.Path]::GetFullPath($PSCommandPath)
$powerShellPath = Join-Path $PSHOME 'pwsh.exe'
if (-not (Test-Path -LiteralPath $powerShellPath -PathType Leaf)) {
    $powerShellPath = Join-Path $PSHOME 'powershell.exe'
}
if (-not (Test-Path -LiteralPath $powerShellPath -PathType Leaf)) {
    throw 'The current PowerShell installation does not contain a supported child-process host.'
}

$proofRoot = Join-Path ([IO.Path]::GetTempPath()) ('rms-pos-lease-proof-{0}' -f ([Guid]::NewGuid().ToString('N')))
New-Item -ItemType Directory -Path $proofRoot -Force | Out-Null
$aReady = Join-Path $proofRoot 'a.ready'
$aResult = Join-Path $proofRoot 'a.result'
$aRelease = Join-Path $proofRoot 'a.release'
$bResult = Join-Path $proofRoot 'b.result'
$bRelease = Join-Path $proofRoot 'b.release'
$cResult = Join-Path $proofRoot 'c.result'
$children = @()

function Quote-ChildArgument([string]$value) {
    if ($value.IndexOf('"', [StringComparison]::Ordinal) -ge 0) {
        throw 'The lease proof generated an unexpected quote in a child argument.'
    }
    return '"{0}"' -f $value
}

function Start-LeaseProbe([string]$mode, [string]$ready, [string]$result, [string]$release) {
    $arguments = @(
        '-NoProfile',
        '-NonInteractive',
        '-ExecutionPolicy', 'Bypass',
        '-File', (Quote-ChildArgument $scriptPath),
        '-ProbeMode', $mode,
        '-ReadyPath', (Quote-ChildArgument $ready),
        '-ResultPath', (Quote-ChildArgument $result),
        '-ReleasePath', (Quote-ChildArgument $release))

    $stdout = Join-Path $proofRoot "$mode.stdout.log"
    $stderr = Join-Path $proofRoot "$mode.stderr.log"
    $child = Start-Process `
        -FilePath $powerShellPath `
        -ArgumentList ($arguments -join ' ') `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru
    return $child
}

try {
    $processA = Start-LeaseProbe 'Hold' $aReady $aResult $aRelease
    $children += $processA
    Wait-ProofValue -path $aReady -expected 'acquired'
    Write-Host '[PASS] Process A acquired Global\RmsSupportHub.Pos.Agent.PrivilegedMutationLease.' -ForegroundColor Green

    $processB = Start-LeaseProbe 'Compete' '' $bResult $aRelease
    $children += $processB
    Wait-ProofValue -path $bResult -expected 'busy'
    Write-Host '[PASS] Process B was rejected while Process A held the lease.' -ForegroundColor Green

    Write-ProofFile $aRelease 'release'
    Wait-ProofValue -path $aResult -expected 'released'
    Write-Host '[PASS] Process A released the lease.' -ForegroundColor Green
    Wait-ProofValue -path $bResult -expected 'acquired'
    Write-Host '[PASS] Process B acquired the lease after Process A released it.' -ForegroundColor Green

    if ($processB.HasExited) {
        throw 'Process B exited before the termination-release proof could run.'
    }
    Stop-Process -Id $processB.Id -Force
    Wait-Process -Id $processB.Id -Timeout 10 -ErrorAction SilentlyContinue

    $processC = Start-LeaseProbe 'TryOnce' '' $cResult $bRelease
    $children += $processC
    Wait-ProofValue -path $cResult -expected 'acquired'
    Write-Host '[PASS] A subsequent real process acquired the lease after Process B was terminated.' -ForegroundColor Green
    Write-Host '[PASS] H-3 Testing-only two-process Global semaphore proof completed without RMS mutation.' -ForegroundColor Green
} finally {
    foreach ($child in $children) {
        if ($null -ne $child -and -not $child.HasExited) {
            Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
        }
    }
    if (Test-Path -LiteralPath $proofRoot) {
        Remove-Item -LiteralPath $proofRoot -Recurse -Force
    }
}
