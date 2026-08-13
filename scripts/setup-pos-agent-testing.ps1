[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$IUnderstandTestingOnly,
    [string]$SupportHubOrigin = 'https://support-hub.integration.test:4443'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosAgentWindowsProvisioning.psm1') -Force

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$canonicalHost = 'rms-pos-agent.localhost'
$agentServiceName = 'RmsSupportHub.Pos.Agent'
$testServiceName = 'RmsSupportHub.Pos.Int13.TestService'
$agentProject = Join-Path $repositoryRoot 'pos/src/RmsSupportHub.Pos.Agent/RmsSupportHub.Pos.Agent.csproj'
$testServiceProject = Join-Path $repositoryRoot 'pos/tools/RmsSupportHub.Pos.Int13.TestService/RmsSupportHub.Pos.Int13.TestService.csproj'
$programDataRoot = Join-Path $env:ProgramData 'DBS/RmsSupportHub/Int13Testing'
$agentInstallRoot = Join-Path $env:ProgramFiles 'DBS/RmsSupportHub/PosAgent'
$testInstallRoot = Join-Path $env:ProgramData 'DBS/RmsSupportHub/Int13Testing/TestService'
$agentConfigurationRoot = Join-Path $env:ProgramData 'DBS/PosAdminTool'
$agentConfigurationFile = Join-Path $agentConfigurationRoot 'configuration.json'
$agentAppSettingsFileName = 'appsettings.json'
$statePath = Join-Path $programDataRoot 'provisioning.json'
$hostsPath = Join-Path $env:SystemRoot 'System32/drivers/etc/hosts'
$ownershipMarker = 'RmsSupportHub INT-13P Testing Provisioning v1'
$hostsMarker = '# RmsSupportHub INT-13P'
$certificateFriendlyName = "RmsSupportHub INT-13P Testing Agent ($canonicalHost)"
$certificateDays = 30

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'INT-13P requires an elevated Administrator PowerShell session.'
    }
}

function Assert-TestingAuthorization {
    if (-not $IUnderstandTestingOnly) {
        throw 'Pass -IUnderstandTestingOnly to acknowledge that this script changes only the current representative Testing machine.'
    }

    Write-Warning 'INT-13P TESTING ONLY: this script changes loopback hosts resolution, LocalMachine certificate trust, service-owned configuration, and two explicitly named Testing Windows Services on this machine.'
    Write-Warning 'It must never be run against a Production/customer machine. No credentials, private keys, PFX files, or browser-policy bypasses are created by this script.'
}

function Assert-ExactOrigin([string]$origin) {
    $uri = $null
    $parsed = [Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$uri)
    $invalid = [string]::IsNullOrWhiteSpace($origin) -or $origin.IndexOf('*', [StringComparison]::Ordinal) -ge 0 -or -not $parsed -or $uri.Scheme -ne 'https' -or [string]::IsNullOrWhiteSpace($uri.Host) -or -not [string]::IsNullOrEmpty($uri.UserInfo) -or -not [string]::IsNullOrEmpty($uri.Query) -or -not [string]::IsNullOrEmpty($uri.Fragment) -or ($uri.AbsolutePath -ne '' -and $uri.AbsolutePath -ne '/')
    if ($invalid) {
        throw "SupportHubOrigin must be one exact HTTPS origin without a path, query, fragment, credentials, or wildcard: $origin"
    }
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

function Read-ProvisioningState {
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        return $null
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.OwnershipMarker -ne $ownershipMarker -or $state.SchemaVersion -ne 1) {
        throw "Provisioning state exists but is not owned by this INT-13P script: $statePath"
    }

    return $state
}

function Write-ProvisioningState($state) {
    $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function New-InitialState {
    return [pscustomobject]@{
        SchemaVersion = 1
        OwnershipMarker = $ownershipMarker
        CreatedUtc = [DateTime]::UtcNow.ToString('O')
        SupportHubOrigin = $SupportHubOrigin
        CanonicalHost = $canonicalHost
        StateDirectoryCreated = $false
        AgentServiceName = $agentServiceName
        TestServiceName = $testServiceName
        HostEntryCreated = $false
        CertificateCreated = $false
        CertificateTrusted = $false
        CertificateThumbprint = $null
        AgentConfigurationDirectoryCreated = $false
        AgentConfigurationFileCreated = $false
        AgentConfigurationAdopted = $false
        AgentConfigurationAclChanged = $false
        AgentConfigurationBackupPath = $null
        AgentConfigurationAclBackupPath = $null
        AgentConfigurationProvisionedHash = $null
        AgentInstallDirectoryCreated = $false
        AgentServiceCreated = $false
        TestInstallDirectoryCreated = $false
        TestServiceCreated = $false
        AgentPublishedFiles = @()
        TestPublishedFiles = @()
    }
}

function Assert-CleanStateRoot {
    if (-not (Test-Path -LiteralPath $programDataRoot)) {
        return
    }

    Assert-NoReparsePoint $programDataRoot
    $entries = @(Get-ChildItem -LiteralPath $programDataRoot -Force)
    if ($entries.Count -gt 0 -and -not (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        throw "The INT-13P state directory is occupied without an ownership marker: $programDataRoot"
    }
}

function Assert-PreexistingResourceConflicts($state) {
    if (Test-Path -LiteralPath $agentConfigurationRoot) {
        Assert-NoReparsePoint $agentConfigurationRoot
        if (-not (Test-Path -LiteralPath $agentConfigurationFile -PathType Leaf)) {
            throw "The Agent configuration directory exists without its expected configuration file: $agentConfigurationRoot"
        }
        $existingConfiguration = Get-Content -LiteralPath $agentConfigurationFile -Raw | ConvertFrom-Json
        $existingServices = @($existingConfiguration.Services)
        if ($existingServices.Count -eq 0 -or @($existingServices | Where-Object { $_ -isnot [string] }).Count -gt 0) {
            throw 'The existing Agent configuration does not have a valid server-owned Services list.'
        }
        if (-not $state.AgentConfigurationDirectoryCreated -and -not $state.AgentConfigurationAdopted) {
            if (@($existingServices | Where-Object { $_ -eq $testServiceName }).Count -gt 0) {
                throw 'The disposable Testing service target is already present in the existing Agent configuration without INT-13P ownership.'
            }
            $state.AgentConfigurationAdopted = $true
        }
    }
    if ((Test-Path -LiteralPath $agentInstallRoot) -and -not $state.AgentInstallDirectoryCreated) {
        throw "The Agent install directory already exists without INT-13P ownership: $agentInstallRoot"
    }
    if ((Test-Path -LiteralPath $testInstallRoot) -and -not $state.TestInstallDirectoryCreated) {
        throw "The disposable-service install directory already exists without INT-13P ownership: $testInstallRoot"
    }

    $existingAgentService = Get-ServiceRecord $agentServiceName
    if ($null -ne $existingAgentService -and -not $state.AgentServiceCreated) {
        throw "The Agent Windows Service already exists without INT-13P ownership: $agentServiceName"
    }
    $existingTestService = Get-ServiceRecord $testServiceName
    if ($null -ne $existingTestService -and -not $state.TestServiceCreated) {
        throw "The disposable Testing Windows Service already exists without INT-13P ownership: $testServiceName"
    }

    $existingListeners = @(Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue)
    if ($existingListeners.Count -gt 0 -and -not $state.AgentServiceCreated) {
        throw 'TCP port 5001 already has a listener without INT-13P ownership. Refusing to interfere with it.'
    }
}

function Get-HostLines {
    if (-not (Test-Path -LiteralPath $hostsPath -PathType Leaf)) {
        throw "Windows hosts file is missing: $hostsPath"
    }

    return @([IO.File]::ReadAllLines($hostsPath))
}

function Get-HostConflictLines {
    param([string[]]$lines)

    return @($lines | Where-Object {
            $_ -match "^\s*(?!#)(\S+)\s+$([Regex]::Escape($canonicalHost))(?:\s+#.*)?\s*$"
        })
}

function Add-ManagedHostEntry {
    param($state)

    $lines = Get-HostLines
    $conflicts = @(Get-HostConflictLines $lines)
    $managed = "127.0.0.1`t$canonicalHost`t$hostsMarker"
    if ($conflicts.Count -gt 0) {
        if ($conflicts.Count -eq 1 -and $conflicts[0] -eq $managed) {
            if (-not $state.HostEntryCreated) {
                throw "The canonical host entry already exists but is not owned by this provisioning state."
            }
            return
        }

        throw "The canonical host already has an unowned hosts-file entry. Refusing to overwrite it: $($conflicts -join ' | ')"
    }

    if ($PSCmdlet.ShouldProcess($hostsPath, "Add the exact loopback-only entry for $canonicalHost")) {
        $existing = [IO.File]::ReadAllText($hostsPath)
        $separator = if ($existing.Length -eq 0 -or $existing.EndsWith("`n") -or $existing.EndsWith("`r")) { '' } else { [Environment]::NewLine }
        [IO.File]::AppendAllText($hostsPath, "$separator$managed$([Environment]::NewLine)", [Text.Encoding]::ASCII)
        $state.HostEntryCreated = $true
        Write-ProvisioningState $state
    }
}

function Get-CertificateDnsNames($certificate) {
    return @($certificate.DnsNameList | ForEach-Object { $_.Unicode } | Where-Object { $_ })
}

function Test-ExactCertificate($certificate) {
    if ($null -eq $certificate -or -not $certificate.HasPrivateKey) {
        return $false
    }

    $names = @(Get-CertificateDnsNames $certificate)
    return $names.Count -eq 1 -and $names[0] -eq $canonicalHost
}

function Get-MatchingAgentCertificates {
    return @(Get-ChildItem Cert:\LocalMachine\My -ErrorAction Stop | Where-Object { Test-ExactCertificate $_ })
}

function Test-ServerAuthenticationEku($certificate) {
    return @($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq '1.3.6.1.5.5.7.3.1' }).Count -eq 1
}

function Assert-TestingCertificateProperties($certificate) {
    $certificateValid = Test-ExactCertificate $certificate
    $serverAuthenticationEku = Test-ServerAuthenticationEku $certificate
    $validityValid = $certificate.NotBefore -le (Get-Date).AddMinutes(1) -and $certificate.NotAfter -gt (Get-Date)
    $invalidCertificate = -not $certificateValid -or -not $serverAuthenticationEku -or -not $validityValid
    if ($invalidCertificate) {
        throw 'The Testing certificate did not meet the exact SAN, private-key, validity, or server-authentication requirements.'
    }

    $privateKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
    try {
        $expectedProvider = $privateKey -is [Security.Cryptography.RSACng] -and $privateKey.Key.Provider.Provider -eq 'Microsoft Software Key Storage Provider'
        $nonExportable = [string]$privateKey.Key.ExportPolicy -eq 'None'
        if (-not $expectedProvider -or -not $nonExportable) {
            throw 'The Testing certificate did not use the Microsoft Software Key Storage Provider with a non-exportable private key.'
        }
    } finally {
        if ($null -ne $privateKey) {
            $privateKey.Dispose()
        }
    }
}

function Get-CertificateKeyFile($certificate) {
    $privateKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
    if ($privateKey -isnot [Security.Cryptography.RSACng]) {
        throw 'The Testing certificate did not use the expected Windows CNG provider.'
    }

    $uniqueName = $privateKey.Key.UniqueName
    if ([string]::IsNullOrWhiteSpace($uniqueName)) {
        throw 'The Testing certificate private-key unique name could not be resolved.'
    }

    $keyPath = Join-Path $env:ProgramData "Microsoft/Crypto/Keys/$uniqueName"
    if (-not (Test-Path -LiteralPath $keyPath -PathType Leaf)) {
        throw "The Testing certificate private-key file could not be found: $keyPath"
    }

    return $keyPath
}

function Grant-LocalSystemCertificateRead($certificate) {
    $keyPath = Get-CertificateKeyFile $certificate
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = Get-Acl -LiteralPath $keyPath
    $rules = @($acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
    $hasRead = @($rules | Where-Object { ($_.IdentityReference -eq $systemSid) -and ($_.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow) -and ((($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::Read) -eq [Security.AccessControl.FileSystemRights]::Read)) }).Count -gt 0

    if (-not $hasRead -and $PSCmdlet.ShouldProcess($keyPath, 'Grant LocalSystem read access to the Agent private key')) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $systemSid,
            [Security.AccessControl.FileSystemRights]::Read,
            [Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
        Set-Acl -LiteralPath $keyPath -AclObject $acl
    }

    $verified = @( (Get-Acl -LiteralPath $keyPath).GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]) | Where-Object { ($_.IdentityReference -eq $systemSid) -and ($_.AccessControlType -eq [Security.AccessControl.AccessControlType]::Allow) -and ((($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::Read) -eq [Security.AccessControl.FileSystemRights]::Read)) }).Count -gt 0
    if (-not $verified) {
        throw 'LocalSystem private-key read access could not be verified.'
    }
}

function Ensure-TestingCertificate {
    param($state)

    $certificate = $null
    if ($state.CertificateThumbprint) {
        $certificate = Get-ChildItem "Cert:\LocalMachine\My\$($state.CertificateThumbprint)" -ErrorAction SilentlyContinue
        if ($null -eq $certificate -or $certificate.FriendlyName -ne $certificateFriendlyName) {
            throw 'The certificate recorded by INT-13P is missing or no longer matches its ownership metadata.'
        }
        Assert-TestingCertificateProperties $certificate
    } else {
        $existing = @(Get-MatchingAgentCertificates)
        if ($existing.Count -gt 0) {
            throw 'A matching LocalMachine Agent certificate already exists without INT-13P ownership. Refusing to replace it.'
        }

        if ($PSCmdlet.ShouldProcess('Cert:\LocalMachine\My', "Create a $certificateDays-day non-exportable Testing certificate for $canonicalHost")) {
            $certificate = New-SelfSignedCertificate `
                -Subject "CN=$canonicalHost" `
                -DnsName $canonicalHost `
                -Type Custom `
                -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.1') `
                -KeyUsage DigitalSignature,KeyEncipherment `
                -KeyLength 2048 `
                -KeyExportPolicy NonExportable `
                -HashAlgorithm SHA256 `
                -NotBefore (Get-Date).AddMinutes(-5) `
                -NotAfter (Get-Date).AddDays($certificateDays) `
                -FriendlyName $certificateFriendlyName `
                -CertStoreLocation 'Cert:\LocalMachine\My'

            Assert-TestingCertificateProperties $certificate

            $state.CertificateCreated = $true
            $state.CertificateThumbprint = $certificate.Thumbprint
            Write-ProvisioningState $state
        }
    }

    if ($null -eq $certificate) {
        return
    }

    if ($PSCmdlet.ShouldProcess("Cert:\LocalMachine\Root\$($certificate.Thumbprint)", 'Trust the created Testing certificate in the LocalMachine root store')) {
        $trusted = Get-ChildItem "Cert:\LocalMachine\Root\$($certificate.Thumbprint)" -ErrorAction SilentlyContinue
        if ($null -eq $trusted) {
            Import-Certificate -FilePath (Export-Certificate -Cert $certificate -FilePath (Join-Path $env:TEMP "rms-int13p-$($certificate.Thumbprint).cer") -Force).FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
            $temporaryCertificateFile = Join-Path $env:TEMP "rms-int13p-$($certificate.Thumbprint).cer"
            if (Test-Path -LiteralPath $temporaryCertificateFile) {
                Remove-Item -LiteralPath $temporaryCertificateFile -Force
            }
            $state.CertificateTrusted = $true
            Write-ProvisioningState $state
        } elseif (-not $state.CertificateTrusted) {
            throw 'The certificate thumbprint is trusted by a pre-existing LocalMachine root entry; refusing to claim ownership of that trust material.'
        }
    }

    Grant-LocalSystemCertificateRead $certificate
}

function Set-RestrictedDirectoryAcl([string]$path) {
    Assert-NoReparsePoint $path
    $directory = Get-Item -LiteralPath $path -Force
    $administrators = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    $system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetAccessRuleProtection($true, $false)
    $security.SetOwner($administrators)
    $security.SetAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            $administrators,
            [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow))
    $security.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
            $system,
            [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow))
    $directory.SetAccessControl($security)
}

function Ensure-AgentConfiguration {
    param($state)

    if (-not (Test-Path -LiteralPath $agentConfigurationRoot)) {
        if ($PSCmdlet.ShouldProcess($agentConfigurationRoot, 'Create the Agent service-owned configuration directory')) {
            New-Item -ItemType Directory -Path $agentConfigurationRoot -Force | Out-Null
            Set-RestrictedDirectoryAcl $agentConfigurationRoot
            $state.AgentConfigurationDirectoryCreated = $true
            Write-ProvisioningState $state
        }
    } elseif (-not $state.AgentConfigurationDirectoryCreated -and -not $state.AgentConfigurationAdopted) {
        throw "The Agent configuration directory is not owned by the current INT-13P state: $agentConfigurationRoot"
    }

    if (Test-Path -LiteralPath $agentConfigurationFile -PathType Leaf) {
        if ($state.AgentConfigurationAdopted) {
            if (-not $state.AgentConfigurationBackupPath) {
                $state.AgentConfigurationBackupPath = Join-Path $programDataRoot 'configuration.original.json'
                $state.AgentConfigurationAclBackupPath = Join-Path $programDataRoot 'configuration-root.acl.xml'
                if ($PSCmdlet.ShouldProcess($state.AgentConfigurationBackupPath, 'Back up the pre-existing Agent configuration before adding the disposable target')) {
                    Copy-Item -LiteralPath $agentConfigurationFile -Destination $state.AgentConfigurationBackupPath -Force
                    (Get-Acl -LiteralPath $agentConfigurationRoot) | Export-Clixml -LiteralPath $state.AgentConfigurationAclBackupPath
                    Write-ProvisioningState $state
                }
            }

            if ($PSCmdlet.ShouldProcess($agentConfigurationRoot, 'Restrict the existing Agent configuration directory to Administrators and LocalSystem')) {
                Set-RestrictedDirectoryAcl $agentConfigurationRoot
                $state.AgentConfigurationAclChanged = $true
                Write-ProvisioningState $state
            }

            $configuration = Get-Content -LiteralPath $agentConfigurationFile -Raw | ConvertFrom-Json
            $services = @($configuration.Services)
            if (@($services | Where-Object { $_ -eq $testServiceName }).Count -eq 0) {
                $configuration.Services = @($services) + $testServiceName
                if ($PSCmdlet.ShouldProcess($agentConfigurationFile, 'Add only the opaque INT-13P disposable service target to the existing Agent configuration')) {
                    $configuration | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $agentConfigurationFile -Encoding UTF8
                    $state.AgentConfigurationProvisionedHash = (Get-FileHash -LiteralPath $agentConfigurationFile -Algorithm SHA256).Hash
                    Write-ProvisioningState $state
                }
            } else {
                $state.AgentConfigurationProvisionedHash = (Get-FileHash -LiteralPath $agentConfigurationFile -Algorithm SHA256).Hash
                Write-ProvisioningState $state
            }
            return
        }

        if (-not $state.AgentConfigurationFileCreated) {
            throw "The Agent configuration file already exists outside the expected INT-13P state: $agentConfigurationFile"
        }

        $configuration = Get-Content -LiteralPath $agentConfigurationFile -Raw | ConvertFrom-Json
        $services = @($configuration.services)
        if ($services.Count -ne 1 -or $services[0] -ne $testServiceName) {
            throw 'The owned Agent configuration no longer contains exactly the INT-13P disposable service target.'
        }
        return
    }

    if ($PSCmdlet.ShouldProcess($agentConfigurationFile, 'Create server-owned Agent configuration with one disposable Testing service target')) {
        $configuration = [ordered]@{
            Version = 1
            Services = @($testServiceName)
        }
        $configuration | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $agentConfigurationFile -Encoding UTF8
        $state.AgentConfigurationFileCreated = $true
        $state.AgentConfigurationProvisionedHash = (Get-FileHash -LiteralPath $agentConfigurationFile -Algorithm SHA256).Hash
        Write-ProvisioningState $state
    }
}

function Get-ServiceRecord([string]$name) {
    return Get-CimInstance -ClassName Win32_Service -Filter "Name='$name'" -ErrorAction SilentlyContinue
}

function Assert-ExpectedService($service, [string]$expectedExecutable, [string]$name) {
    if ($null -eq $service) {
        return
    }

    $actual = [Environment]::ExpandEnvironmentVariables([string]$service.PathName).Trim()
    $expected = '"' + (Get-FullPath $expectedExecutable) + '"'
    if ($actual -ne $expected -or $service.StartName -ne 'LocalSystem') {
        throw "Existing service $name does not match the exact INT-13P-owned executable and LocalSystem identity."
    }
}

function Publish-Project([string]$project, [string]$outputPath, [string]$label) {
    if ($PSCmdlet.ShouldProcess($outputPath, "Publish the Release $label outside the repository")) {
        if (-not (Test-Path -LiteralPath $outputPath)) {
            New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
        }

        $previousOrigin = $env:PosAgentSecurity__SupportHubOrigin
        $env:PosAgentSecurity__SupportHubOrigin = $SupportHubOrigin
        try {
            & dotnet restore $project -r win-x64 --nologo
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet restore failed for $label with exit code $LASTEXITCODE."
            }
            & dotnet publish $project -c Release -r win-x64 --self-contained false --no-restore --nologo -o $outputPath
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet publish failed for $label with exit code $LASTEXITCODE. Restore the project and retry."
            }
        } finally {
            if ($null -eq $previousOrigin) {
                Remove-Item Env:PosAgentSecurity__SupportHubOrigin -ErrorAction SilentlyContinue
            } else {
                $env:PosAgentSecurity__SupportHubOrigin = $previousOrigin
            }
        }
    }
}

function Ensure-OwnedInstallRoot([string]$path, [string]$stateProperty, $state) {
    Assert-NoReparsePoint $path
    if (Test-Path -LiteralPath $path -PathType Container) {
        if (-not $state.$stateProperty) {
            throw "The install directory already exists without INT-13P ownership: $path"
        }
        return
    }

    if ($PSCmdlet.ShouldProcess($path, 'Create the stable machine-local INT-13P install directory')) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
        $state.$stateProperty = $true
        Write-ProvisioningState $state
    }
}

function Assert-ExistingServiceOwnership($state, [string]$name, [string]$expectedExecutable, [string]$stateProperty) {
    $service = Get-ServiceRecord $name
    if ($null -eq $service) {
        return
    }

    if (-not $state.$stateProperty) {
        throw "The Windows Service already exists without INT-13P ownership: $name"
    }
    Assert-ExpectedService $service $expectedExecutable $name
}

function Stop-OwnedServiceForPublish($state, [string]$name, [string]$expectedExecutable, [string]$stateProperty) {
    if (-not $state.$stateProperty) {
        return
    }

    $service = Get-ServiceRecord $name
    if ($null -eq $service) {
        return
    }
    Assert-ExpectedService $service $expectedExecutable $name
    $controller = Get-Service -Name $name -ErrorAction Stop
    if ($controller.Status -ne 'Stopped' -and $PSCmdlet.ShouldProcess($name, 'Stop the owned service before replacing its published binaries')) {
        Stop-Service -Name $name -Force
        $controller.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
}

function Get-RelativeManifest([string]$root) {
    if (-not (Test-Path -LiteralPath $root)) {
        return @()
    }

    $fullRoot = (Get-FullPath $root).TrimEnd('\') + '\'
    return @(Get-ChildItem -LiteralPath $root -Recurse -Force | ForEach-Object {
            $_.FullName.Substring($fullRoot.Length)
        })
}

function Install-Service([string]$name, [string]$executable, [string]$displayName, [string]$description, $state, [string]$stateProperty) {
    $service = Get-ServiceRecord $name
    Assert-ExpectedService $service $executable $name
    if ($null -eq $service -and $PSCmdlet.ShouldProcess($name, "Install the bounded LocalSystem Testing Windows Service")) {
        New-Service -Name $name -BinaryPathName ('"' + (Get-FullPath $executable) + '"') -DisplayName $displayName -Description $description -StartupType Manual | Out-Null
        $state.$stateProperty = $true
        Write-ProvisioningState $state
    }
}

function Ensure-AgentAppSettings {
    param($state, [string]$installRoot)

    $path = Join-Path $installRoot $agentAppSettingsFileName
    $expected = [ordered]@{
        PosAgentSecurity = [ordered]@{
            SupportHubOrigin = $SupportHubOrigin
            CertificateThumbprint = $state.CertificateThumbprint
        }
    }

    if ((Test-Path -LiteralPath $path -PathType Leaf) -and -not $state.AgentInstallDirectoryCreated) {
        throw "The Agent appsettings file exists outside INT-13P ownership: $path"
    }

    if ($PSCmdlet.ShouldProcess($path, 'Write the deployment-owned non-secret Agent security configuration')) {
        $expected | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
    }
}

function Start-AndVerify-Service([string]$name) {
    Start-Service -Name $name
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $service = Get-Service -Name $name -ErrorAction Stop
        if ($service.Status -eq 'Running') {
            return
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Windows Service did not reach Running state: $name"
}

function Invoke-Http11HealthCheck {
    foreach ($path in @('/health/live', '/health/ready')) {
        $request = [Net.HttpWebRequest]::Create("https://$canonicalHost`:5001$path")
        $request.Method = 'GET'
        $request.ProtocolVersion = [Version]::new(1, 1)
        $request.AllowAutoRedirect = $false
        $request.Timeout = 10000
        $response = $null
        $reader = $null
        try {
            $response = [Net.HttpWebResponse]$request.GetResponse()
            if ($response.StatusCode -ne [Net.HttpStatusCode]::OK -or $response.ProtocolVersion -ne [Version]::new(1, 1)) {
                throw "Agent health check failed for ${path}: HTTP $([int]$response.StatusCode), protocol $($response.ProtocolVersion)."
            }
            $reader = [IO.StreamReader]::new($response.GetResponseStream())
            $body = $reader.ReadToEnd()
            Write-Host "[PASS] $path -> HTTP $([int]$response.StatusCode), HTTP/$($response.ProtocolVersion), body status recorded as $((ConvertFrom-Json $body).status)" -ForegroundColor Green
        } finally {
            if ($null -ne $reader) { $reader.Dispose() }
            if ($null -ne $response) { $response.Dispose() }
        }
    }
}

Assert-Administrator
Assert-TestingAuthorization
Assert-ExactOrigin $SupportHubOrigin
Assert-CleanStateRoot

$state = Read-ProvisioningState
if ($null -eq $state) {
    if (-not (Test-Path -LiteralPath $programDataRoot)) {
        if ($PSCmdlet.ShouldProcess($programDataRoot, 'Create the INT-13P local ownership state directory')) {
            New-Item -ItemType Directory -Path $programDataRoot -Force | Out-Null
            $stateDirectoryCreated = $true
        } else {
            $stateDirectoryCreated = $false
        }
    } else {
        $stateDirectoryCreated = $false
    }
    $state = New-InitialState
    $state.StateDirectoryCreated = $stateDirectoryCreated
    if (-not $WhatIfPreference) {
        Write-ProvisioningState $state
    }
}

Assert-PreexistingResourceConflicts $state
if (-not $WhatIfPreference) {
    Write-ProvisioningState $state
}

if ($WhatIfPreference) {
    $browserPlan = Ensure-PosAgentBrowserProvisioning `
        -SupportHubOrigin $SupportHubOrigin `
        -CanonicalHost $canonicalHost `
        -State $state `
        -WhatIf:$WhatIfPreference
    Write-Host "[WHATIF] Would provision $canonicalHost, the LocalMachine certificate, the Agent, the exact browser IWA/LNA policies, the BackConnectionHostNames entry, the opaque allow-list target, and $testServiceName." -ForegroundColor Yellow
    foreach ($browserResult in @($browserPlan.Browsers)) {
        if ($browserResult.Installed) {
            Write-Host "[WHATIF] $($browserResult.Browser) $($browserResult.Version): $($browserResult.LoopbackPolicy) would allow only $SupportHubOrigin; AuthServerAllowlist would include $canonicalHost." -ForegroundColor Yellow
        } else {
            Write-Host "[WHATIF] $($browserResult.Browser) is not installed; no policy would be written." -ForegroundColor Yellow
        }
    }
    Write-Host '[WHATIF] BackConnectionHostNames would remain REG_MULTI_SZ and DisableLoopbackCheck would remain absent.' -ForegroundColor Yellow
    return
}

$browserProvisioning = Ensure-PosAgentBrowserProvisioning `
    -SupportHubOrigin $SupportHubOrigin `
    -CanonicalHost $canonicalHost `
    -State $state `
    -WhatIf:$WhatIfPreference
Write-ProvisioningState $state

Add-ManagedHostEntry $state
Ensure-TestingCertificate $state
Ensure-AgentConfiguration $state

$agentExecutable = Join-Path $agentInstallRoot 'RmsSupportHub.Pos.Agent.exe'
$testExecutable = Join-Path $testInstallRoot 'RmsSupportHub.Pos.Int13.TestService.exe'
Stop-OwnedServiceForPublish $state $agentServiceName $agentExecutable 'AgentServiceCreated'
Ensure-OwnedInstallRoot $agentInstallRoot 'AgentInstallDirectoryCreated' $state
Publish-Project $agentProject $agentInstallRoot 'POS Agent'
Ensure-AgentAppSettings $state $agentInstallRoot
$state.AgentPublishedFiles = @(Get-RelativeManifest $agentInstallRoot)
Write-ProvisioningState $state

Ensure-OwnedInstallRoot $testInstallRoot 'TestInstallDirectoryCreated' $state
Stop-OwnedServiceForPublish $state $testServiceName $testExecutable 'TestServiceCreated'
Publish-Project $testServiceProject $testInstallRoot 'INT-13 disposable test service'
$state.TestPublishedFiles = @(Get-RelativeManifest $testInstallRoot)
Write-ProvisioningState $state

if (-not (Test-Path -LiteralPath $agentExecutable -PathType Leaf) -or -not (Test-Path -LiteralPath $testExecutable -PathType Leaf)) {
    throw 'The published Agent or disposable Testing service executable is missing.'
}

Assert-ExistingServiceOwnership $state $agentServiceName $agentExecutable 'AgentServiceCreated'
Assert-ExistingServiceOwnership $state $testServiceName $testExecutable 'TestServiceCreated'
Install-Service $agentServiceName $agentExecutable 'RMS+ POS Agent (Testing)' 'RMS+ POS Agent loopback service for the authorized Testing machine.' $state 'AgentServiceCreated'
Install-Service $testServiceName $testExecutable 'RMS+ INT-13 Disposable Testing Service' 'INT-13P disposable service. Testing only; no business, network, SQL, or child-process behavior.' $state 'TestServiceCreated'

Start-AndVerify-Service $testServiceName
Start-AndVerify-Service $agentServiceName

$addresses = @([Net.Dns]::GetHostAddresses($canonicalHost) | ForEach-Object { $_.IPAddressToString })
if ($addresses.Count -eq 0 -or @($addresses | Where-Object { $_ -notin @('127.0.0.1', '::1') }).Count -gt 0) {
    throw "Canonical host did not resolve only to loopback: $($addresses -join ', ')"
}
Write-Host "[PASS] $canonicalHost resolves only to $($addresses -join ', ')" -ForegroundColor Green

$listeners = @(Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction Stop)
if ($listeners.Count -eq 0 -or @($listeners | Where-Object { $_.LocalAddress -notin @('127.0.0.1', '::1') }).Count -gt 0) {
    throw 'The Agent port 5001 listener was not loopback-only.'
}
Write-Host '[PASS] Agent port 5001 listener is loopback-only.' -ForegroundColor Green

Invoke-Http11HealthCheck

$browserVerification = Get-PosAgentBrowserProvisioningVerification `
    -SupportHubOrigin $SupportHubOrigin `
    -CanonicalHost $canonicalHost
foreach ($browserResult in @($browserVerification.Browsers)) {
    if ($browserResult.Installed) {
        Write-Host "[PASS] $($browserResult.Browser) $($browserResult.Version): exact AuthServerAllowlist hostname and $($browserResult.LoopbackPolicy) origin policy verified." -ForegroundColor Green
    } else {
        Write-Host "[INFO] $($browserResult.Browser) is not installed; browser policy verification was not applicable." -ForegroundColor Yellow
    }
}
Write-Host '[PASS] BackConnectionHostNames contains only the owned exact hostname addition; DisableLoopbackCheck is absent.' -ForegroundColor Green

Write-Host "INT-13P provisioning completed. Owned state: $statePath" -ForegroundColor Green
Write-Host "Agent service: $agentServiceName; disposable service: $testServiceName" -ForegroundColor Green
Write-Host "Certificate thumbprint (metadata only): $($state.CertificateThumbprint)" -ForegroundColor Green
