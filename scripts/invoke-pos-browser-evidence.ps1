[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [switch]$IUnderstandTestingOnly,
    [ValidateSet('chrome', 'edge')]
    [string]$Browser = 'chrome',
    [string]$SupportHubOrigin = 'https://support-hub.integration.test:4443',
    [string]$AgentOrigin = 'https://rms-pos-agent.localhost:5001',
    [string]$StartUrl,
    [switch]$AllowLocalhostDevTest,
    [string]$ServiceId,
    [switch]$AllowDisposableServiceAction,
    [string]$OutputDirectory = (Join-Path ([IO.Path]::GetTempPath()) 'RmsSupportHub\Int13CBrowserEvidence'),
    [ValidateRange(15, 180)]
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This bounded launcher must be run from an elevated Testing-machine shell so it can create a Limited interactive-token task.'
    }
}

function Quote-TaskArgument([string]$Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

if (-not $IUnderstandTestingOnly) {
    throw 'Pass -IUnderstandTestingOnly. This launcher is for the authorized Testing machine only.'
}
Assert-Administrator

if ($AllowDisposableServiceAction -and [string]::IsNullOrWhiteSpace($ServiceId)) {
    throw '-ServiceId is required with -AllowDisposableServiceAction and must be the opaque Agent service ID.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$nodeScript = Join-Path $repositoryRoot 'tools\pos-browser-evidence\run-evidence.mjs'
if (-not (Test-Path -LiteralPath $nodeScript -PathType Leaf)) {
    throw "Browser evidence runner is missing: $nodeScript"
}
$nodeCommand = Get-Command node.exe -CommandType Application -ErrorAction Stop | Select-Object -First 1
$interactiveUser = (Get-CimInstance -ClassName Win32_ComputerSystem -ErrorAction Stop).UserName
if ([string]::IsNullOrWhiteSpace($interactiveUser)) {
    throw 'No interactive desktop user is logged on; refusing to launch a browser outside a normal user session.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = Join-Path $OutputDirectory ("{0}-{1}.json" -f $Browser, ([Guid]::NewGuid().ToString('N')))
$childArguments = @(
    $nodeScript,
    '--browser', $Browser,
    '--support-hub-origin', $SupportHubOrigin,
    '--agent-origin', $AgentOrigin,
    '--output', $outputPath,
    '--timeout-ms', ([string]($TimeoutSeconds * 1000)))
if (-not [string]::IsNullOrWhiteSpace($StartUrl)) {
    $childArguments += @('--start-url', $StartUrl)
}
if ($AllowLocalhostDevTest) {
    $childArguments += '--allow-localhost-dev-test'
}
if ($AllowDisposableServiceAction) {
    $childArguments += @('--allow-disposable-service-action', '--service-id', $ServiceId)
}
$taskArgumentString = ($childArguments | ForEach-Object { Quote-TaskArgument ([string]$_) }) -join ' '

$taskName = 'RmsSupportHub.Int13C.BrowserEvidence.' + [Guid]::NewGuid().ToString('N')
$action = New-ScheduledTaskAction -Execute $nodeCommand.Source -Argument $taskArgumentString
$principal = New-ScheduledTaskPrincipal -UserId $interactiveUser -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -DontStopOnIdleEnd `
    -ExecutionTimeLimit ([TimeSpan]::FromMinutes(5))
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddSeconds(10)
$deadline = (Get-Date).AddSeconds($TimeoutSeconds + 30)

try {
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Trigger $trigger -Settings $settings -Force | Out-Null
    Start-ScheduledTask -TaskName $taskName

    while (-not (Test-Path -LiteralPath $outputPath -PathType Leaf) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 1
    }
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw 'The Limited interactive-token browser task did not produce an evidence file before the deadline.'
    }

    $evidence = Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
    $evidence | ConvertTo-Json -Depth 12
    if ($evidence.result -ne 'pass') {
        exit 2
    }
} finally {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
}
