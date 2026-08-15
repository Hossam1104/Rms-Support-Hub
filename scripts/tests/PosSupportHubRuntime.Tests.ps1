$ErrorActionPreference = 'Stop'

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $scriptsRoot
Import-Module (Join-Path $scriptsRoot 'PosSupportHubRuntime.psm1') -Force

function New-TestListener([string]$address, [int]$owningProcess) {
    return [pscustomobject]@{ LocalAddress = $address; OwningProcess = $owningProcess }
}

function New-TestBuildOutput {
    $root = Join-Path ([IO.Path]::GetTempPath()) ("pos-build-{0}" -f ([Guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $root 'media') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $root 'index.html'), "<html><body><app-root></app-root></body></html>`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $root 'main-ABC123.js'), "console.log('pos');`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $root 'styles-DEF456.css'), ":root{--x:1}`n", [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $root 'media/logo.svg'), "<svg/>`n", [Text.UTF8Encoding]::new($false))
    return $root
}

function Invoke-BuildIdentityGenerator([string]$root, [string]$commit, [string]$environmentName) {
    $generator = Join-Path $repositoryRoot 'frontend/scripts/build-identity.mjs'
    $output = @(& node $generator 'finalize' '--output' $root '--environment' $environmentName '--commit' $commit '--source-state' 'clean')
    if ($LASTEXITCODE -ne 0) {
        throw "The build-identity generator failed with exit code $LASTEXITCODE."
    }
    return ConvertFrom-PosBuildIdentityGeneratorOutput -Output $output
}

function Invoke-TestFrontendIdentityValidation($identity, [string]$root, [DateTime]$notBeforeUtc) {
    Assert-PosFrontendBuildIdentity `
        -Identity $identity `
        -ExpectedCommit 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' `
        -ExpectedEnvironment 'Testing' `
        -ExpectedSourceState 'clean' `
        -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
        -AssetRoot $root `
        -NotBeforeUtc $notBeforeUtc
}

Describe 'Build identity generator output boundary' {
    $validJson = '{"schemaVersion":1}'

    It 'accepts exactly one JSON object record' {
        $parsed = ConvertFrom-PosBuildIdentityGeneratorOutput -Output @($validJson)
        $parsed.schemaVersion | Should Be 1
    }

    It 'rejects the demonstrated LAST_LINE_ACCEPTED warning-plus-JSON shape' {
        { ConvertFrom-PosBuildIdentityGeneratorOutput -Output @('LAST_LINE_ACCEPTED', $validJson) } |
            Should Throw 'exactly one JSON identity record'
    }

    It 'rejects JSON followed by warning text instead of selecting the first record' {
        { ConvertFrom-PosBuildIdentityGeneratorOutput -Output @($validJson, 'warning text') } |
            Should Throw 'exactly one JSON identity record'
    }

    It 'rejects zero, blank, and whitespace-only output' {
        foreach ($output in @(@(), @(''), @('  `t  '))) {
            { ConvertFrom-PosBuildIdentityGeneratorOutput -Output $output } |
                Should Throw
        }
    }

    It 'rejects malformed and mixed records' {
        foreach ($output in @(
                @('{'),
                @($validJson, '{'),
                @('warning text', $validJson),
                @($validJson, 'warning text'))) {
            { ConvertFrom-PosBuildIdentityGeneratorOutput -Output $output } |
                Should Throw
        }
    }

    It 'rejects arrays, scalars, null, and non-text success-stream objects' {
        foreach ($output in @(
                @('[{"schemaVersion":1}]'),
                @('42'),
                @('null'),
                @([pscustomobject]@{ schemaVersion = 1 }))) {
            { ConvertFrom-PosBuildIdentityGeneratorOutput -Output $output } |
                Should Throw
        }
    }
}

Describe 'Secure Support Hub listener ownership' {
    It 'accepts an empty port' {
        Assert-PosSupportHubOwnedListener -Port 4443 -OwnedProcessId 1234 -Listener @() | Should Be 'none'
    }

    It 'rejects a listener when no runtime PID is owned' {
        { Assert-PosSupportHubOwnedListener -Port 4443 -OwnedProcessId $null -Listener @(New-TestListener '127.0.0.1' 999) } |
            Should Throw 'without INT-13D ownership'
    }

    It 'rejects a listener owned by another process instead of adopting or killing it' {
        { Assert-PosSupportHubOwnedListener -Port 4443 -OwnedProcessId 1234 -Listener @(New-TestListener '127.0.0.1' 4321) } |
            Should Throw 'owned by another process'
    }

    It 'allows an owned stale listener to be replaced' {
        Assert-PosSupportHubOwnedListener -Port 4443 -OwnedProcessId 1234 -Listener @(New-TestListener '127.0.0.1' 1234) |
            Should Be 'owned'
    }

    It 'rejects an owned listener that is not loopback-only' {
        { Assert-PosSupportHubOwnedListener -Port 4443 -OwnedProcessId 1234 -Listener @(New-TestListener '0.0.0.0' 1234) } |
            Should Throw 'not loopback-only'
    }
}

Describe 'Service listener readiness wait' {
    It 'rejects an empty listener set' {
        { Assert-PosLoopbackOnlyListener -Port 5001 -Listener @() } | Should Throw 'No listener is bound'
    }

    It 'rejects a routable listener regardless of ownership' {
        { Assert-PosLoopbackOnlyListener -Port 5001 -Listener @(New-TestListener '0.0.0.0' 4321) } |
            Should Throw 'not loopback-only'
    }

    It 'accepts a loopback-only listener set' {
        { Assert-PosLoopbackOnlyListener -Port 5001 -Listener @(
                (New-TestListener '127.0.0.1' 4321),
                (New-TestListener '::1' 4321)) } | Should Not Throw
    }

    It 'waits for a service that reaches Running before it binds its socket' {
        # This is the live failure: the SCM reported Running and the port was
        # sampled once, so an ordinary startup race aborted provisioning.
        $script:waitAttempts = 0
        $provider = {
            param($p)
            $script:waitAttempts++
            if ($script:waitAttempts -lt 3) { return @() }
            return @(New-TestListener '127.0.0.1' 4321)
        }

        $listeners = Wait-PosLoopbackOnlyListener -Port 5001 -TimeoutSeconds 30 -ListenerProvider $provider

        @($listeners).Count | Should Be 1
        $script:waitAttempts | Should Be 3
    }

    It 'fails closed on a routable listener without waiting out the deadline' {
        $provider = { param($p) @(New-TestListener '0.0.0.0' 4321) }
        $started = [DateTime]::UtcNow

        { Wait-PosLoopbackOnlyListener -Port 5001 -TimeoutSeconds 600 -ListenerProvider $provider } |
            Should Throw 'not loopback-only'

        ([DateTime]::UtcNow - $started).TotalSeconds -lt 30 | Should Be $true
    }

    It 'fails closed when the socket is never bound' {
        $provider = { param($p) @() }
        { Wait-PosLoopbackOnlyListener -Port 5001 -TimeoutSeconds 1 -ListenerProvider $provider } |
            Should Throw 'No listener reached TCP port 5001'
    }
}

Describe 'Secure Support Hub runtime state binding' {
    $completeState = [pscustomobject]@{
        SupportHubRuntimeRoot = 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime'
        SupportHubRuntimeApiDll = 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime\api\RmsSupportHub.Api.dll'
        SupportHubRuntimeContentRoot = 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime\api'
        SupportHubRuntimeHost = 'support-hub.integration.test'
        SupportHubRuntimePort = 4443
        SupportHubRuntimeCertificateThumbprint = '0123456789ABCDEF0123456789ABCDEF01234567'
        SupportHubRuntimeProcessId = 1234
        SupportHubRuntimeBuildId = ('a' * 64)
        SupportHubRuntimeCommit = 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1'
    }

    $bind = {
        param($state, $thumbprint)
        Assert-PosSupportHubRuntimeStateBinding `
            -State $state `
            -ExpectedRuntimeRoot 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime' `
            -ExpectedApiDll 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime\api\RmsSupportHub.Api.dll' `
            -ExpectedContentRoot 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime\api' `
            -ExpectedHost 'support-hub.integration.test' `
            -ExpectedPort 4443 `
            -ExpectedCertificateThumbprint $thumbprint `
            -ExpectedProcessId 1234 `
            -ExpectedBuildId ('a' * 64) `
            -ExpectedCommit 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1'
    }

    It 'accepts a fully bound state' {
        { & $bind $completeState '0123456789ABCDEF0123456789ABCDEF01234567' } | Should Not Throw
    }

    It 'fails closed when a binding is missing' {
        $partial = $completeState | Select-Object -Property * -ExcludeProperty SupportHubRuntimeContentRoot
        { & $bind $partial '0123456789ABCDEF0123456789ABCDEF01234567' } | Should Throw 'missing its SupportHubRuntimeContentRoot'
    }

    It 'fails closed when the certificate identity drifts' {
        { & $bind $completeState 'FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF' } |
            Should Throw 'does not match the current authorized Testing configuration'
    }

    It 'rejects a stale PID, an unrelated PID, and every other runtime identity drift' {
        $cases = @(
            @{ Property = 'SupportHubRuntimeProcessId'; Value = 9999 },
            @{ Property = 'SupportHubRuntimeProcessId'; Value = 4321 },
            @{ Property = 'SupportHubRuntimeBuildId'; Value = ('b' * 64) },
            @{ Property = 'SupportHubRuntimeCommit'; Value = '1111111111111111111111111111111111111111' },
            @{ Property = 'SupportHubRuntimeContentRoot'; Value = 'C:\foreign\content' },
            @{ Property = 'SupportHubRuntimeApiDll'; Value = 'C:\foreign\api.dll' },
            @{ Property = 'SupportHubRuntimeHost'; Value = 'foreign.test' },
            @{ Property = 'SupportHubRuntimePort'; Value = 5443 },
            @{ Property = 'SupportHubRuntimeCertificateThumbprint'; Value = ('F' * 40) })

        foreach ($case in $cases) {
            $drifted = $completeState | Select-Object -Property *
            $drifted.($case.Property) = $case.Value
            { & $bind $drifted '0123456789ABCDEF0123456789ABCDEF01234567' } |
                Should Throw 'does not match the current authorized Testing configuration'
        }
    }

    It 'rejects a missing PID, build ID, or commit binding' {
        foreach ($property in @('SupportHubRuntimeProcessId', 'SupportHubRuntimeBuildId', 'SupportHubRuntimeCommit')) {
            $partial = $completeState | Select-Object -Property * -ExcludeProperty $property
            { & $bind $partial '0123456789ABCDEF0123456789ABCDEF01234567' } |
                Should Throw "missing its $property"
        }
    }

    It 'rejects an invalid expected PID, build ID, or commit before trusting state' {
        { Assert-PosSupportHubRuntimeStateBinding `
                -State $completeState `
                -ExpectedRuntimeRoot $completeState.SupportHubRuntimeRoot `
                -ExpectedApiDll $completeState.SupportHubRuntimeApiDll `
                -ExpectedContentRoot $completeState.SupportHubRuntimeContentRoot `
                -ExpectedHost $completeState.SupportHubRuntimeHost `
                -ExpectedPort 4443 `
                -ExpectedCertificateThumbprint $completeState.SupportHubRuntimeCertificateThumbprint `
                -ExpectedProcessId 0 `
                -ExpectedBuildId ('a' * 64) `
                -ExpectedCommit $completeState.SupportHubRuntimeCommit } |
            Should Throw 'expected Support Hub runtime PID is invalid'
        { Assert-PosSupportHubRuntimeStateBinding `
                -State $completeState `
                -ExpectedRuntimeRoot $completeState.SupportHubRuntimeRoot `
                -ExpectedApiDll $completeState.SupportHubRuntimeApiDll `
                -ExpectedContentRoot $completeState.SupportHubRuntimeContentRoot `
                -ExpectedHost $completeState.SupportHubRuntimeHost `
                -ExpectedPort 4443 `
                -ExpectedCertificateThumbprint $completeState.SupportHubRuntimeCertificateThumbprint `
                -ExpectedProcessId 1234 `
                -ExpectedBuildId 'not-a-hash' `
                -ExpectedCommit $completeState.SupportHubRuntimeCommit } |
            Should Throw 'expected Support Hub runtime build ID'
        { Assert-PosSupportHubRuntimeStateBinding `
                -State $completeState `
                -ExpectedRuntimeRoot $completeState.SupportHubRuntimeRoot `
                -ExpectedApiDll $completeState.SupportHubRuntimeApiDll `
                -ExpectedContentRoot $completeState.SupportHubRuntimeContentRoot `
                -ExpectedHost $completeState.SupportHubRuntimeHost `
                -ExpectedPort 4443 `
                -ExpectedCertificateThumbprint $completeState.SupportHubRuntimeCertificateThumbprint `
                -ExpectedProcessId 1234 `
                -ExpectedBuildId ('a' * 64) `
                -ExpectedCommit 'not-a-commit' } |
            Should Throw 'expected Support Hub runtime commit'
    }
}

Describe 'Frontend build identity' {
    It 'produces a manifest hash PowerShell reproduces byte for byte' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            Get-PosFrontendAssetManifestHash -Root $root | Should Be $identity.buildId
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'accepts a build produced by the current run for the current commit' {
        $root = New-TestBuildOutput
        try {
            $notBefore = [DateTime]::UtcNow.AddSeconds(-5)
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            {
                Assert-PosFrontendBuildIdentity `
                    -Identity $identity `
                    -ExpectedCommit 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' `
                    -ExpectedEnvironment 'Testing' `
                    -ExpectedSourceState 'clean' `
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
                    -AssetRoot $root `
                    -NotBeforeUtc $notBefore
            } | Should Not Throw
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'fails closed when a staged asset no longer matches the identity document' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            [IO.File]::WriteAllText((Join-Path $root 'main-ABC123.js'), "console.log('stale');`n", [Text.UTF8Encoding]::new($false))
            {
                Assert-PosFrontendBuildIdentity `
                    -Identity $identity `
                    -ExpectedCommit 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' `
                    -ExpectedEnvironment 'Testing' `
                    -ExpectedSourceState 'clean' `
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
                    -AssetRoot $root `
                    -NotBeforeUtc ([DateTime]::UtcNow.AddSeconds(-5))
            } | Should Throw 'do not hash to the build identity'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'fails closed for a build staged before this startup run' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            {
                Assert-PosFrontendBuildIdentity `
                    -Identity $identity `
                    -ExpectedCommit 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' `
                    -ExpectedEnvironment 'Testing' `
                    -ExpectedSourceState 'clean' `
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
                    -AssetRoot $root `
                    -NotBeforeUtc ([DateTime]::UtcNow.AddMinutes(5))
            } | Should Throw 'Refusing to serve a stale build'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'fails closed for a build produced from a different commit' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root '1111111111111111111111111111111111111111' 'Testing'
            {
                Assert-PosFrontendBuildIdentity `
                    -Identity $identity `
                    -ExpectedCommit 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' `
                    -ExpectedEnvironment 'Testing' `
                    -ExpectedSourceState 'clean' `
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
                    -AssetRoot $root `
                    -NotBeforeUtc ([DateTime]::UtcNow.AddSeconds(-5))
            } | Should Throw 'does not name the current repository commit'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'names the hashed main bundle and the application shell' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            $identity.mainBundle.file | Should Be 'main-ABC123.js'
            $identity.mainBundle.sha256 | Should Be (Get-PosFileSha256Hex (Join-Path $root 'main-ABC123.js'))
            $identity.indexHtmlSha256 | Should Be (Get-PosFileSha256Hex (Join-Path $root 'index.html'))
            $identity.assetCount | Should Be 4
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'does not expose a filesystem path in the identity document' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            ($identity | ConvertTo-Json -Depth 5) | Should Not Match '([A-Za-z]:\\|ProgramData|Users)'
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'rejects incorrect schemaVersion, environment, commitShort, and sourceState values' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            foreach ($mutation in @(
                    @{ Property = 'schemaVersion'; Value = '1' },
                    @{ Property = 'schemaVersion'; Value = 2 },
                    @{ Property = 'environment'; Value = 'Production' },
                    @{ Property = 'commitShort'; Value = '0000000' },
                    @{ Property = 'sourceState'; Value = 'dirty' })) {
                $malformed = $identity | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                $malformed.($mutation.Property) = $mutation.Value
                { Invoke-TestFrontendIdentityValidation $malformed $root ([DateTime]::UtcNow.AddSeconds(-5)) } |
                    Should Throw
            }
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'rejects invalid buildId, assetCount, index hash, and main bundle hash values' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            foreach ($mutation in @(
                    @{ Property = 'buildId'; Value = ('A' * 64) },
                    @{ Property = 'buildId'; Value = ('b' * 64) },
                    @{ Property = 'assetCount'; Value = '4' },
                    @{ Property = 'assetCount'; Value = 0 },
                    @{ Property = 'assetCount'; Value = 100001 },
                    @{ Property = 'indexHtmlSha256'; Value = 'not-a-hash' })) {
                $malformed = $identity | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                $malformed.($mutation.Property) = $mutation.Value
                { Invoke-TestFrontendIdentityValidation $malformed $root ([DateTime]::UtcNow.AddSeconds(-5)) } |
                    Should Throw
            }

            $malformedBundle = $identity | ConvertTo-Json -Depth 5 | ConvertFrom-Json
            $malformedBundle.mainBundle.sha256 = ('c' * 64)
            { Invoke-TestFrontendIdentityValidation $malformedBundle $root ([DateTime]::UtcNow.AddSeconds(-5)) } |
                Should Throw
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'rejects invalid builtAtUtc timestamps and timestamps outside the startup window' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            foreach ($timestamp in @('not-a-timestamp', '2026-08-15T09:30:00+00:00', '2020-01-01T00:00:00Z', ([DateTime]::UtcNow.AddMinutes(10).ToString('O')))) {
                $malformed = $identity | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                $malformed.builtAtUtc = $timestamp
                { Invoke-TestFrontendIdentityValidation $malformed $root ([DateTime]::UtcNow.AddSeconds(-5)) } |
                    Should Throw
            }
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'rejects every unsafe main bundle filename form' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            foreach ($file in @(
                    '..\\main-ABC123.js',
                    '../main-ABC123.js',
                    'C:\\main-ABC123.js',
                    '\\server\\share\\main-ABC123.js',
                    'main-ABC123.js?cache=1',
                    'main-ABC123.js#fragment',
                    'nested/main-ABC123.js',
                    'main-ABC123.css',
                    'main-ABC123.js/extra')) {
                $malformed = $identity | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                $malformed.mainBundle.file = $file
                { Invoke-TestFrontendIdentityValidation $malformed $root ([DateTime]::UtcNow.AddSeconds(-5)) } |
                    Should Throw
            }
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }

    It 'rejects missing or non-object mainBundle documents' {
        $root = New-TestBuildOutput
        try {
            $identity = Invoke-BuildIdentityGenerator $root 'b8e8f0fab1e730dc08e73bbb28058f9f5cc8f8b1' 'Testing'
            foreach ($value in @($null, 'main-ABC123.js')) {
                $malformed = $identity | ConvertTo-Json -Depth 5 | ConvertFrom-Json
                $malformed.mainBundle = $value
                { Invoke-TestFrontendIdentityValidation $malformed $root ([DateTime]::UtcNow.AddSeconds(-5)) } |
                    Should Throw
            }
        } finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }
}

Describe 'Testing startup self-elevation boundary' {
    $startupScript = Join-Path $scriptsRoot 'start-pos-agent-testing.ps1'

    It 'forwards only the known typed parameters' {
        $arguments = New-PosSelfElevationArgumentList `
            -ScriptPath $startupScript `
            -IUnderstandTestingOnly $true `
            -SupportHubOrigin 'https://support-hub.integration.test:4443'
        ($arguments -join ' ') | Should Be ('-NoProfile -ExecutionPolicy Bypass -NoExit -File "{0}" -IUnderstandTestingOnly -SupportHubOrigin "https://support-hub.integration.test:4443"' -f $startupScript)
    }

    It 'omits the acknowledgement switch when it was not passed' {
        $arguments = New-PosSelfElevationArgumentList -ScriptPath $startupScript -IUnderstandTestingOnly $false -SupportHubOrigin ''
        ($arguments -contains '-IUnderstandTestingOnly') | Should Be $false
        ($arguments -contains '-SupportHubOrigin') | Should Be $false
    }

    It 'quotes a script path that contains spaces' {
        # The owner's repository lives under a path with a space, but a CI
        # checkout does not, so the space is staged rather than assumed.
        $spacedRoot = Join-Path ([IO.Path]::GetTempPath()) ('RMS Support Hub {0}' -f [guid]::NewGuid())
        New-Item -ItemType Directory -Path $spacedRoot -Force | Out-Null
        try {
            $spacedScript = Join-Path $spacedRoot 'start-pos-agent-testing.ps1'
            Copy-Item -LiteralPath $startupScript -Destination $spacedScript -Force
            $spacedScript.IndexOf(' ') -ge 0 | Should Be $true

            $arguments = New-PosSelfElevationArgumentList -ScriptPath $spacedScript -IUnderstandTestingOnly $true -SupportHubOrigin ''

            $arguments[4] | Should Be '-File'
            $arguments[5] | Should Be ('"{0}"' -f $spacedScript)
        } finally {
            Remove-Item -LiteralPath $spacedRoot -Recurse -Force
        }
    }

    It 'refuses to re-launch any script other than the Testing startup script' {
        { New-PosSelfElevationArgumentList -ScriptPath (Join-Path $scriptsRoot 'build.ps1') -IUnderstandTestingOnly $true -SupportHubOrigin '' } |
            Should Throw 'may only re-launch'
    }

    It 'refuses an injected argument smuggled through the origin' {
        foreach ($origin in @(
                'https://support-hub.integration.test:4443 -Command Start-Process',
                'https://support-hub.integration.test:4443/tools',
                'https://support-hub.integration.test:4443"; rm -rf /',
                'http://localhost:4200')) {
            { New-PosSelfElevationArgumentList -ScriptPath $startupScript -IUnderstandTestingOnly $true -SupportHubOrigin $origin } |
                Should Throw 'not an exact scheme/host/port origin'
        }
    }

    It 'resolves the elevation host only from the PowerShell installation directory' {
        $hostPath = Get-PosElevationHostPath
        (Split-Path -Parent $hostPath) | Should Be $PSHOME
        ([IO.Path]::GetFileName($hostPath) -in @('pwsh.exe', 'powershell.exe')) | Should Be $true
    }

    It 'refuses an elevation host outside the PowerShell installation directory' {
        { Get-PosElevationHostPath -PowerShellHome (Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))) } |
            Should Throw 'No PowerShell host executable'
    }
}
