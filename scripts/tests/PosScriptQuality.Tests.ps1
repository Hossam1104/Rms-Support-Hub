$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Import-Module (Join-Path $PSScriptRoot '..\PosScriptQuality.psm1') -Force

function New-TemporaryScriptFile([string]$content) {
    $path = Join-Path ([IO.Path]::GetTempPath()) ("pos-quality-{0}.ps1" -f ([Guid]::NewGuid().ToString('N')))
    [IO.File]::WriteAllText($path, $content, [Text.UTF8Encoding]::new($false))
    return $path
}

Describe 'Support Hub PowerShell quality gate' {
    It 'parses every tracked script and module without findings' {
        $findings = @(Invoke-PosScriptQualityAnalysis -RepositoryRoot $repositoryRoot)
        $detail = ($findings | ForEach-Object { "$($_.Path):$($_.Line) $($_.Rule)" }) -join '; '
        $detail | Should Be ''
    }

    It 'analyses the real provisioning surface rather than an empty set' {
        $paths = @(Get-PosTrackedScriptPath -RepositoryRoot $repositoryRoot)
        @($paths | Where-Object { $_ -like '*PosSupportHubProvisioning.psm1' }).Count | Should Be 1
        @($paths | Where-Object { $_ -like '*start-pos-agent-testing.ps1' }).Count | Should Be 1
        ($paths.Count -ge 10) | Should Be $true
    }

    It 'detects the observed multiline certificate-validation defect shape' {
        # This is the exact shape that shipped: it parses cleanly, but the second
        # line becomes a command named -and and the provider check is lost.
        $path = New-TemporaryScriptFile @'
$expectedProvider = $privateKey -is [Security.Cryptography.RSACng]
    -and $privateKey.Key.Provider.Provider -eq 'Microsoft Software Key Storage Provider'
'@
        try {
            $findings = @(Test-PosScriptFile -Path $path)
            @($findings | Where-Object { $_.Rule -eq 'OperatorParsedAsCommand' }).Count | Should Be 1
            @($findings | Where-Object { $_.Rule -eq 'DanglingOperatorContinuation' }).Count | Should Be 1
        } finally {
            Remove-Item -LiteralPath $path -Force
        }
    }

    It 'accepts the same expression with a real line continuation' {
        $path = New-TemporaryScriptFile @'
$expectedProvider = $privateKey -is [Security.Cryptography.RSACng] `
    -and $privateKey.Key.Provider.Provider -eq 'Microsoft Software Key Storage Provider'
'@
        try {
            @(Test-PosScriptFile -Path $path).Count | Should Be 0
        } finally {
            Remove-Item -LiteralPath $path -Force
        }
    }

    It 'reports a backtick that is followed by trailing whitespace' {
        $path = New-TemporaryScriptFile "`$value = 1 ``   `r`n    -and `$true`r`n"
        try {
            $findings = @(Test-PosScriptFile -Path $path)
            @($findings | Where-Object { $_.Rule -eq 'TrailingWhitespaceAfterContinuation' }).Count | Should Be 1
        } finally {
            Remove-Item -LiteralPath $path -Force
        }
    }

    It 'reports a genuine parse error' {
        $path = New-TemporaryScriptFile "function Broken {`r`n"
        try {
            @(Test-PosScriptFile -Path $path | Where-Object { $_.Rule -eq 'ParseError' }).Count -ge 1 | Should Be $true
        } finally {
            Remove-Item -LiteralPath $path -Force
        }
    }

    It 'runs from the command line under both invocation forms' {
        # -File binds parameter defaults before $PSScriptRoot is available, so a
        # gate that only works under -Command would silently never run in CI.
        $gate = Join-Path $repositoryRoot 'scripts\test-powershell-quality.ps1'
        foreach ($invocation in @('-File', '-Command')) {
            if ($invocation -eq '-File') {
                & powershell -NoProfile -ExecutionPolicy Bypass -File $gate -SkipScriptAnalyzer | Out-Null
            } else {
                & powershell -NoProfile -ExecutionPolicy Bypass -Command "& '$gate' -SkipScriptAnalyzer" | Out-Null
            }
            $LASTEXITCODE | Should Be 0
        }
    }

    It 'does not flag the defect shape when it only appears inside a comment' {
        $path = New-TemporaryScriptFile @'
<#
    $expectedProvider = $privateKey -is [Security.Cryptography.RSACng]
        -and $privateKey.Key.Provider.Provider -eq 'Microsoft Software Key Storage Provider'
#>
$value = $true
'@
        try {
            @(Test-PosScriptFile -Path $path).Count | Should Be 0
        } finally {
            Remove-Item -LiteralPath $path -Force
        }
    }
}
