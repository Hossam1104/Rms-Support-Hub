[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishRoot,
    [Parameter(Mandatory)]
    [string]$OutputRoot,
    [ValidateSet('Testing', 'Production')]
    [string]$Channel = 'Testing',
    [Parameter(Mandatory)]
    [ValidatePattern('^(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})$')]
    [string]$Version,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$SignerThumbprint,
    [string]$PackageId = 'rms-support-agent',
    [string]$SignerDisplayName = 'DBS RMS Support Agent Code Signing'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'PosSupportAgentDeployment.psm1') -Force

function Test-PublisherPath {
    param([Parameter(Mandatory)][string]$Path)

    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.IndexOfAny([char[]]@("`0", "`r", "`n")) -ge 0) { throw 'Publisher paths must be absolute and free of control characters.' }
    $full = [IO.Path]::GetFullPath($Path)
    if (Test-Path -LiteralPath $full) {
        if ([IO.File]::GetAttributes($full).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Publisher paths may not be reparse points.' }
    }
    return $full.TrimEnd('\')
}

function Test-PublisherToken {
    param([AllowNull()][string]$Value)

    return ((-not [string]::IsNullOrWhiteSpace($Value)) -and $Value.Length -le 128 -and $Value -match '^[A-Za-z0-9][A-Za-z0-9._-]*$')
}

function Get-PublisherFiles {
    param([Parameter(Mandatory)][string]$Root)

    $files = @(Get-ChildItem -LiteralPath $Root -Recurse -Force -File | Sort-Object FullName)
    if ($files.Count -lt 1 -or $files.Count -gt 256) { throw 'The publish output must contain between one and 256 files.' }
    $result = [System.Collections.Generic.List[object]]::new()
    $logicalNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in $files) {
        if ($file.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The publish output contains a reparse point.' }
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $unsafeSegments = @($relative.Split('/') | Where-Object { $_ -in @('', '.', '..') })
        if ([IO.Path]::IsPathFullyQualified($relative) -or $unsafeSegments.Count -gt 0) { throw 'The publish output contains an unsafe relative path.' }
        if ($relative -ieq 'manifest.json') { throw 'The publish output must not contain a package manifest.' }
        if ($file.Length -gt 64MB) { throw 'A publish output file exceeds the bounded package file size.' }
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $logicalName = [IO.Path]::GetFileNameWithoutExtension($relative) -replace '[^A-Za-z0-9._-]', '-'
        if ($logicalName -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') { throw 'The publish output contains a file with no safe logical name.' }
        if (-not $logicalNames.Add($logicalName)) { throw 'The publish output contains duplicate logical file names.' }
        $result.Add([pscustomobject]@{
            SourcePath = $file.FullName
            LogicalName = $logicalName
            RelativePath = $relative
            SizeBytes = [int64]$file.Length
            Sha256 = $hash
            Required = $true
        })
    }
    return @($result)
}

function Get-PublisherSignerCertificate {
    param([Parameter(Mandatory)][string]$Thumbprint)

    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Package signing is Windows LocalMachine-only.' }
    $normalized = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    $certificate = Get-Item -LiteralPath ("Cert:\LocalMachine\My\$normalized") -ErrorAction Stop
    if ($certificate.Thumbprint -replace '\s' -ne $normalized -or -not $certificate.HasPrivateKey) { throw 'The pinned LocalMachine signer certificate is unavailable.' }
    if ($certificate.NotBefore.ToUniversalTime() -gt [DateTime]::UtcNow -or $certificate.NotAfter.ToUniversalTime() -lt [DateTime]::UtcNow) { throw 'The pinned signer certificate is not currently valid.' }
    if (@($certificate.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.5.5.7.3.3' }).Count -ne 1) { throw 'The signer certificate lacks the Code Signing EKU.' }
    $keyUsage = @($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.15' })
    if ($keyUsage.Count -eq 1 -and -not ([Security.Cryptography.X509Certificates.X509KeyUsageExtension]$keyUsage[0]).KeyUsages.HasFlag([Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature)) { throw 'The signer certificate lacks Digital Signature key usage.' }
    return $certificate
}

$publishRootFull = Test-PublisherPath -Path $PublishRoot
$outputRootFull = Test-PublisherPath -Path $OutputRoot
if (-not (Test-Path -LiteralPath $publishRootFull -PathType Container)) { throw 'The publish output directory does not exist.' }
if (-not (Test-PublisherToken -Value $PackageId)) { throw 'The package ID is invalid.' }
$outputInsideInput = $outputRootFull.Equals($publishRootFull, [StringComparison]::OrdinalIgnoreCase) -or $outputRootFull.StartsWith($publishRootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
if ($outputInsideInput) {
    throw 'The package output directory must be separate from the publish input directory.'
}

$contract = Get-RmsSupportAgentDeploymentContract -Channel $Channel
$files = @(Get-PublisherFiles -Root $publishRootFull)
$totalBytes = ($files | Measure-Object -Property SizeBytes -Sum).Sum
if ([int64]$totalBytes -gt 256MB) { throw 'The publish output exceeds the bounded package size.' }

if (-not (Test-Path -LiteralPath $outputRootFull -PathType Container)) { New-Item -ItemType Directory -Path $outputRootFull -Force | Out-Null }
if ([IO.File]::GetAttributes($outputRootFull).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'The package output directory is a reparse point.' }

$archiveName = "$PackageId-$Version.zip"
$archivePath = Join-Path $outputRootFull $archiveName
$archiveTemporary = Join-Path $outputRootFull ('.' + $archiveName + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
$manifestPath = Join-Path $outputRootFull 'manifest.json'
$manifestTemporary = Join-Path $outputRootFull ('.manifest.' + [Guid]::NewGuid().ToString('N') + '.tmp')

try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($archiveTemporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in @($files | Sort-Object RelativePath)) {
            $entry = $archive.CreateEntry($file.RelativePath, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
            $source = [IO.File]::OpenRead($file.SourcePath)
            $destination = $entry.Open()
            try { $source.CopyTo($destination) } finally { $destination.Dispose(); $source.Dispose() }
        }
    } finally { $archive.Dispose() }

    $archiveHash = (Get-FileHash -LiteralPath $archiveTemporary -Algorithm SHA256).Hash.ToLowerInvariant()
    $archiveSize = (Get-Item -LiteralPath $archiveTemporary).Length
    $manifest = [pscustomobject]([ordered]@{
        PackageId = $PackageId
        Version = $Version
        SupportedOperatingSystem = 'Windows'
        SupportedRuntime = 'net10.0-windows'
        ServiceName = $contract.ServiceName
        ServiceDisplayName = $contract.DisplayName
        ServiceDescription = $contract.Description
        ServiceIdentity = 'LocalSystem'
        ScmName = $contract.ServiceName
        SignatureAlgorithm = 'SHA256withRSA'
        SignerDisplayName = $SignerDisplayName
        PackageSha256 = $archiveHash
        Signature = ''
        Files = @($files | ForEach-Object { [pscustomobject]([ordered]@{ LogicalName = $_.LogicalName; RelativePath = $_.RelativePath; SizeBytes = $_.SizeBytes; Sha256 = $_.Sha256; Required = $_.Required }) })
        AclRequirements = @('AgentServiceReadWrite', 'AgentServiceExecute')
        CertificateRequirements = @('AgentHttpsCertificate')
        PreviousVersion = $null
        RollbackAvailable = $false
        PackageSizeBytes = [int64]$archiveSize
        SchemaVersion = 1
        ProductId = $contract.ProductId
        Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
        ReleaseChannel = $Channel
        Environment = $Channel
    })

    $certificate = Get-PublisherSignerCertificate -Thumbprint $SignerThumbprint
    $payload = Get-RmsCanonicalPackagePayload -Manifest $manifest
    $rsa = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($certificate)
    if ($null -eq $rsa) { throw 'The pinned signer certificate did not expose a usable private key.' }
    try { $manifest.Signature = [Convert]::ToBase64String($rsa.SignData($payload, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)) } finally { $rsa.Dispose() }

    $policy = Test-RmsSupportAgentPackageManifest -Manifest $manifest -Channel $Channel -CurrentArchitecture $manifest.Architecture -CryptographicSignature
    if (-not $policy.Valid) { throw ('The generated package manifest failed policy: ' + ($policy.Blockers -join ', ')) }
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestTemporary -Encoding UTF8
    foreach ($target in @($archivePath, $manifestPath)) {
        if (Test-Path -LiteralPath $target) {
            if ([IO.File]::GetAttributes($target).HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'A package publication target is a reparse point.' }
        }
    }
    Move-Item -LiteralPath $archiveTemporary -Destination $archivePath -Force
    Move-Item -LiteralPath $manifestTemporary -Destination $manifestPath -Force
    [pscustomobject]@{
        State = 'Published'
        Channel = $Channel
        PackageId = $PackageId
        Version = $Version
        ArchivePath = $archivePath
        ManifestPath = $manifestPath
        PackageSha256 = $archiveHash
        PackageSizeBytes = $archiveSize
        SignerThumbprint = ($SignerThumbprint -replace '\s', '').ToUpperInvariant()
        PrivateKeyExported = $false
    } | ConvertTo-Json -Depth 8
} finally {
    if (Test-Path -LiteralPath $archiveTemporary -PathType Leaf) { Remove-Item -LiteralPath $archiveTemporary -Force }
    if (Test-Path -LiteralPath $manifestTemporary -PathType Leaf) { Remove-Item -LiteralPath $manifestTemporary -Force }
}
