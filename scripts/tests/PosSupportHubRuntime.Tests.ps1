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
    $output = & node $generator 'finalize' '--output' $root '--environment' $environmentName '--commit' $commit '--source-state' 'clean'
    if ($LASTEXITCODE -ne 0) {
        throw "The build-identity generator failed with exit code $LASTEXITCODE."
    }
    return ($output | Select-Object -Last 1) | ConvertFrom-Json
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

Describe 'Secure Support Hub runtime state binding' {
    $completeState = [pscustomobject]@{
        SupportHubRuntimeRoot = 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime'
        SupportHubRuntimeApiDll = 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime\api\RmsSupportHub.Api.dll'
        SupportHubRuntimeContentRoot = 'C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime\api'
        SupportHubRuntimeHost = 'support-hub.integration.test'
        SupportHubRuntimePort = 4443
        SupportHubRuntimeCertificateThumbprint = '0123456789ABCDEF0123456789ABCDEF01234567'
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
            -ExpectedCertificateThumbprint $thumbprint
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
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
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
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
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
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
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
                    -ExpectedAssetManifestHash (Get-PosFrontendAssetManifestHash -Root $root) `
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
        # The repository itself lives under a path with a space, so this is the
        # real failure the owner hit rather than a synthetic case.
        ($startupScript.IndexOf(' ') -ge 0) | Should Be $true
        $arguments = New-PosSelfElevationArgumentList -ScriptPath $startupScript -IUnderstandTestingOnly $true -SupportHubOrigin ''
        $arguments[5] | Should Be ('"{0}"' -f $startupScript)
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
