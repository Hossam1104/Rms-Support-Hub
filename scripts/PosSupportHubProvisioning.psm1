Set-StrictMode -Version Latest

function Test-PosStateProperty {
    param(
        [AllowNull()]
        [object]$Object,
        [Parameter(Mandatory)]
        [string]$Name
    )

    return $null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]
}

function Ensure-PosStateProperty {
    param(
        [Parameter(Mandatory)]
        [object]$State,
        [Parameter(Mandatory)]
        [string]$Name,
        [AllowNull()]
        [object]$Value
    )

    if (-not (Test-PosStateProperty $State $Name)) {
        Add-Member -InputObject $State -MemberType NoteProperty -Name $Name -Value $Value
    }
}

function Get-PosCertificateDnsNames {
    param([Parameter(Mandatory)]$Certificate)

    return @($Certificate.DnsNameList | ForEach-Object { $_.Unicode } | Where-Object { $_ })
}

function Test-PosExactCertificateDnsName {
    param(
        [AllowNull()]
        $Certificate,
        [Parameter(Mandatory)]
        [string]$Hostname
    )

    if ($null -eq $Certificate) {
        return $false
    }

    $names = @(Get-PosCertificateDnsNames $Certificate)
    return $names.Count -eq 1 -and $names[0] -eq $Hostname
}

function Test-PosExactCertificate {
    param(
        [AllowNull()]
        $Certificate,
        [Parameter(Mandatory)]
        [string]$Hostname
    )

    if ($null -eq $Certificate -or -not $Certificate.HasPrivateKey) {
        return $false
    }

    return Test-PosExactCertificateDnsName $Certificate $Hostname
}

function Test-PosServerAuthenticationEku {
    param([Parameter(Mandatory)]$Certificate)

    return @($Certificate.EnhancedKeyUsageList | Where-Object {
        $_.ObjectId -eq '1.3.6.1.5.5.7.3.1'
    }).Count -eq 1
}

function Assert-PosSupportHubCertificateProperties {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Certificate,
        [Parameter(Mandatory)]
        [string]$Hostname,
        [Parameter(Mandatory)]
        [string]$FriendlyName
    )

    $invalid = $Certificate.FriendlyName -ne $FriendlyName `
        -or -not (Test-PosExactCertificate $Certificate $Hostname) `
        -or -not (Test-PosServerAuthenticationEku $Certificate) `
        -or $Certificate.NotBefore -gt (Get-Date).AddMinutes(1) `
        -or $Certificate.NotAfter -le (Get-Date)
    if ($invalid) {
        throw 'The Testing Support Hub certificate does not match its exact SAN, ownership, EKU, or validity requirements.'
    }

    $privateKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    if ($null -eq $privateKey) {
        throw 'The Testing Support Hub certificate does not expose an RSA private key.'
    }
    try {
        $expectedProvider = $privateKey -is [Security.Cryptography.RSACng]
            -and $privateKey.Key.Provider.Provider -eq 'Microsoft Software Key Storage Provider'
        $nonExportable = [string]$privateKey.Key.ExportPolicy -eq 'None'
        if (-not $expectedProvider -or -not $nonExportable) {
            throw 'The Testing Support Hub certificate must use the Microsoft Software Key Storage Provider with a non-exportable private key.'
        }
    } finally {
        if ($null -ne $privateKey) {
            $privateKey.Dispose()
        }
    }
}

function Get-PosSupportHubCertificates {
    param([Parameter(Mandatory)][string]$Hostname)

    return @(Get-ChildItem Cert:\LocalMachine\My -ErrorAction Stop | Where-Object {
        Test-PosExactCertificateDnsName $_ $Hostname
    })
}

function Get-PosSupportHubCertificate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Thumbprint,
        [Parameter(Mandatory)]
        [string]$Hostname,
        [Parameter(Mandatory)]
        [string]$FriendlyName
    )

    if ($Thumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'The recorded Support Hub certificate thumbprint is malformed.'
    }

    $certificate = Get-ChildItem "Cert:\LocalMachine\My\$Thumbprint" -ErrorAction SilentlyContinue
    if ($null -eq $certificate) {
        return $null
    }

    Assert-PosSupportHubCertificateProperties $certificate $Hostname $FriendlyName
    return $certificate
}

function Ensure-PosSupportHubHostEntry {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Hostname,
        [Parameter(Mandatory)]
        [string]$HostsPath,
        [Parameter(Mandatory)]
        [string]$Marker,
        [Parameter(Mandatory)]
        [object]$State
    )

    Ensure-PosStateProperty $State 'SupportHubHostEntryCreated' $false
    $lines = if (Test-Path -LiteralPath $HostsPath -PathType Leaf) {
        @([IO.File]::ReadAllLines($HostsPath))
    } else {
        throw "Windows hosts file is missing: $HostsPath"
    }

    $escapedHost = [Regex]::Escape($Hostname)
    $managedPattern = "^\s*127\.0\.0\.1\s+$escapedHost\s+$([Regex]::Escape($Marker))\s*$"
    $hostConflictPattern = "(?i)(^|\s)$escapedHost(\s|$)"
    $managed = @($lines | Where-Object { $_ -match $managedPattern })
    $conflicts = @($lines | Where-Object {
        $_ -notmatch '^\s*#' -and $_ -match $hostConflictPattern
    })

    $unownedConflicts = @($conflicts | Where-Object { $_ -notmatch $managedPattern })
    if ($conflicts.Count -gt 0 -and $unownedConflicts.Count -gt 0) {
        throw "The Testing Support Hub host already has an unowned hosts-file entry. Refusing to overwrite it: $($conflicts -join ' | ')"
    }

    if ($managed.Count -gt 1) {
        throw "The Testing Support Hub host has duplicate owned hosts-file entries. Refusing to normalize them automatically: $Hostname"
    }

    if ($managed.Count -eq 1) {
        if (-not [bool]$State.SupportHubHostEntryCreated) {
            throw 'The exact Testing Support Hub hosts entry exists but is not owned by the current provisioning state.'
        }
        return
    }

    if ($PSCmdlet.ShouldProcess($HostsPath, "Add the exact loopback-only entry for $Hostname")) {
        $existing = [IO.File]::ReadAllText($HostsPath)
        $separator = if ($existing.Length -eq 0 -or $existing.EndsWith("`n") -or $existing.EndsWith("`r")) {
            ''
        } else {
            [Environment]::NewLine
        }
        [IO.File]::AppendAllText(
            $HostsPath,
            "$separator$([string]::Join("`t", @('127.0.0.1', $Hostname, $Marker)))$([Environment]::NewLine)",
            [Text.Encoding]::ASCII)
        $State.SupportHubHostEntryCreated = $true
    }
}

function Remove-PosSupportHubHostEntry {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Hostname,
        [Parameter(Mandatory)]
        [string]$HostsPath,
        [Parameter(Mandatory)]
        [string]$Marker,
        [Parameter(Mandatory)]
        [object]$State
    )

    $missingEntry = -not (Test-PosStateProperty $State 'SupportHubHostEntryCreated') `
        -or -not [bool]$State.SupportHubHostEntryCreated `
        -or -not (Test-Path -LiteralPath $HostsPath -PathType Leaf)
    if ($missingEntry) {
        return
    }

    $lines = [IO.File]::ReadAllLines($HostsPath)
    $escapedHost = [Regex]::Escape($Hostname)
    $managedPattern = "^\s*127\.0\.0\.1\s+$escapedHost\s+$([Regex]::Escape($Marker))\s*$"
    $hostConflictPattern = "(?i)(^|\s)$escapedHost(\s|$)"
    $managed = @($lines | Where-Object { $_ -match $managedPattern })
    $conflicts = @($lines | Where-Object {
        $_ -notmatch '^\s*#' -and $_ -match $hostConflictPattern
    })
    $unownedConflicts = @($conflicts | Where-Object { $_ -notmatch $managedPattern })
    if ($unownedConflicts.Count -gt 0) {
        throw 'The Testing Support Hub hosts entry has an external change; refusing to remove it.'
    }
    if ($managed.Count -eq 0) {
        return
    }

    $remaining = @($lines | Where-Object { $_ -notmatch $managedPattern })
    if ($PSCmdlet.ShouldProcess($HostsPath, "Remove only the owned $Hostname loopback entry")) {
        [IO.File]::WriteAllLines($HostsPath, $remaining, [Text.Encoding]::ASCII)
    }
}

function Ensure-PosSupportHubCertificate {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Hostname,
        [Parameter(Mandatory)]
        [string]$FriendlyName,
        [Parameter(Mandatory)]
        [int]$CertificateDays,
        [Parameter(Mandatory)]
        [object]$State
    )

    foreach ($property in @(
            @{ Name = 'SupportHubCertificateCreated'; Value = $false },
            @{ Name = 'SupportHubCertificateTrusted'; Value = $false },
            @{ Name = 'SupportHubCertificateThumbprint'; Value = $null })) {
        Ensure-PosStateProperty $State $property.Name $property.Value
    }

    $certificate = $null
    if (-not [bool]$State.SupportHubCertificateCreated -and -not [string]::IsNullOrWhiteSpace([string]$State.SupportHubCertificateThumbprint)) {
        throw 'The Support Hub certificate state has a thumbprint without INT-13D creation ownership. Refusing to adopt it.'
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$State.SupportHubCertificateThumbprint)) {
        $certificate = Get-PosSupportHubCertificate `
            -Thumbprint ([string]$State.SupportHubCertificateThumbprint) `
            -Hostname $Hostname `
            -FriendlyName $FriendlyName
        if ($null -eq $certificate) {
            throw 'The Support Hub certificate recorded by INT-13D is missing from the LocalMachine certificate store.'
        }
    } else {
        $existing = @(Get-PosSupportHubCertificates $Hostname)
        if ($existing.Count -gt 0) {
            throw 'A matching LocalMachine Support Hub certificate already exists without Testing ownership. Refusing to replace it.'
        }

        if ($PSCmdlet.ShouldProcess('Cert:\LocalMachine\My', "Create a $CertificateDays-day non-exportable Testing certificate for $Hostname")) {
            $certificate = New-SelfSignedCertificate `
                -Subject "CN=$Hostname" `
                -DnsName $Hostname `
                -Type Custom `
                -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.1') `
                -KeyUsage DigitalSignature,KeyEncipherment `
                -KeyLength 2048 `
                -Provider 'Microsoft Software Key Storage Provider' `
                -KeyExportPolicy NonExportable `
                -HashAlgorithm SHA256 `
                -NotBefore (Get-Date).AddMinutes(-5) `
                -NotAfter (Get-Date).AddDays($CertificateDays) `
                -FriendlyName $FriendlyName `
                -CertStoreLocation 'Cert:\LocalMachine\My'

            Assert-PosSupportHubCertificateProperties $certificate $Hostname $FriendlyName
            $State.SupportHubCertificateCreated = $true
            $State.SupportHubCertificateThumbprint = $certificate.Thumbprint
        }
    }

    if ($null -eq $certificate) {
        return [pscustomobject]@{
            Certificate = $null
            Thumbprint = $State.SupportHubCertificateThumbprint
            Trusted = [bool]$State.SupportHubCertificateTrusted
        }
    }

    $trusted = Get-ChildItem "Cert:\LocalMachine\Root\$($certificate.Thumbprint)" -ErrorAction SilentlyContinue
    if ($null -eq $trusted) {
        if ($PSCmdlet.ShouldProcess("Cert:\LocalMachine\Root\$($certificate.Thumbprint)", 'Trust the owned Testing Support Hub certificate in the LocalMachine root store')) {
            $temporaryCertificateFile = Join-Path $env:TEMP "rms-int13d-$($certificate.Thumbprint).cer"
            try {
                Export-Certificate -Cert $certificate -FilePath $temporaryCertificateFile -Force | Out-Null
                Import-Certificate -FilePath $temporaryCertificateFile -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
            } finally {
                if (Test-Path -LiteralPath $temporaryCertificateFile) {
                    Remove-Item -LiteralPath $temporaryCertificateFile -Force
                }
            }
            $State.SupportHubCertificateTrusted = $true
        }
    } elseif (-not [bool]$State.SupportHubCertificateTrusted) {
        throw 'The Support Hub certificate thumbprint is trusted by a pre-existing LocalMachine root entry; refusing to claim ownership of that trust material.'
    }

    return [pscustomobject]@{
        Certificate = $certificate
        Thumbprint = $certificate.Thumbprint
        Trusted = [bool]$State.SupportHubCertificateTrusted
    }
}

function Remove-PosSupportHubCertificate {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory)]
        [string]$Hostname,
        [Parameter(Mandatory)]
        [string]$FriendlyName,
        [Parameter(Mandatory)]
        [object]$State
    )

    $missingCertificate = -not (Test-PosStateProperty $State 'SupportHubCertificateCreated') `
        -or -not [bool]$State.SupportHubCertificateCreated `
        -or [string]::IsNullOrWhiteSpace([string]$State.SupportHubCertificateThumbprint)
    if ($missingCertificate) {
        return
    }

    $thumbprint = [string]$State.SupportHubCertificateThumbprint
    if ($thumbprint -notmatch '^[0-9A-Fa-f]{40}$') {
        throw 'The recorded Support Hub certificate thumbprint is malformed; refusing cleanup.'
    }
    $certificate = Get-ChildItem "Cert:\LocalMachine\My\$thumbprint" -ErrorAction SilentlyContinue
    if ($null -ne $certificate) {
        Assert-PosSupportHubCertificateProperties $certificate $Hostname $FriendlyName
        if ($PSCmdlet.ShouldProcess("Cert:\LocalMachine\My\$thumbprint", 'Remove the owned Testing Support Hub certificate and private key')) {
            Remove-Item -LiteralPath "Cert:\LocalMachine\My\$thumbprint" -Force
        }
    }

    $trusted = Get-ChildItem "Cert:\LocalMachine\Root\$thumbprint" -ErrorAction SilentlyContinue
    $removeTrust = $null -ne $trusted `
        -and [bool]$State.SupportHubCertificateTrusted `
        -and $PSCmdlet.ShouldProcess("Cert:\LocalMachine\Root\$thumbprint", 'Remove the owned Testing Support Hub trust entry')
    if ($removeTrust) {
        Remove-Item -LiteralPath "Cert:\LocalMachine\Root\$thumbprint" -Force
    }
}

Export-ModuleMember -Function @(
    'Ensure-PosSupportHubHostEntry',
    'Remove-PosSupportHubHostEntry',
    'Ensure-PosSupportHubCertificate',
    'Remove-PosSupportHubCertificate',
    'Get-PosSupportHubCertificates',
    'Get-PosSupportHubCertificate',
    'Assert-PosSupportHubCertificateProperties'
)
