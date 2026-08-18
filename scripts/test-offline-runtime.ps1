param(
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$verifier = Join-Path $RepositoryRoot 'scripts\verify-offline-runtime.ps1'
if (-not (Test-Path -LiteralPath $verifier -PathType Leaf)) {
  throw "Offline verifier is missing: $verifier"
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
  $utf8 = [System.Text.UTF8Encoding]::new($false)
  [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Invoke-OfflineCase {
  param(
    [Parameter(Mandatory)]
    [string]$Name,
    [Parameter(Mandatory)]
    [hashtable]$Files,
    [Parameter(Mandatory)]
    [bool]$ExpectedPass
  )

  $root = Join-Path ([System.IO.Path]::GetTempPath()) ('rms-offline-case-' + [Guid]::NewGuid().ToString('N'))
  $webRoot = Join-Path $root 'wwwroot'
  New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
  try {
    foreach ($relativePath in $Files.Keys) {
      $path = Join-Path $webRoot ($relativePath -replace '/', '\')
      $parent = Split-Path -Parent $path
      New-Item -ItemType Directory -Path $parent -Force | Out-Null
      Write-Utf8NoBom $path ([string]$Files[$relativePath])
    }

    $actualPass = $false
    $output = ''
    try {
      $output = (& $verifier -PackageRoot $root 2>&1 | Out-String)
      $actualPass = $true
    } catch {
      $output = ($_ | Out-String)
    }

    if ($actualPass -ne $ExpectedPass) {
      throw "Offline verifier case '$Name' expected pass=$ExpectedPass but got pass=$actualPass.`n$output"
    }
    Write-Output "[PASS] $Name (expected pass=$ExpectedPass)"
  } finally {
    if (Test-Path -LiteralPath $root) {
      Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
  }
}

Invoke-OfflineCase -Name 'legitimate emitted metadata and internal POS origins' -ExpectedPass $true -Files @{
  'index.html' = '<html><body><app-root></app-root></body></html>'
  'main.js' = 'const agent = "https://rms-pos-agent.localhost:5001"; const hub = "https://support-hub.integration.test:4443"; const angular = "https://angular.dev/best-practices/security#preventing-cross-site-scripting-xss"; const citation = "https://jcgt.org/published/0007/04/01/";'
  'namespace.svg' = '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"></svg>'
  'metadata.json' = '{"namespace":"http://www.w3.org/2000/svg"}'
}

Invoke-OfflineCase -Name 'protocol-relative external script' -ExpectedPass $false -Files @{
  'index.html' = '<script src="//evil.example.com/payload.js"></script>'
}

Invoke-OfflineCase -Name 'protocol-relative CSS resource' -ExpectedPass $false -Files @{
  'styles.css' = '.hero { background-image: url(//evil.example.com/x.png); }'
}

Invoke-OfflineCase -Name 'external SVG resource' -ExpectedPass $false -Files @{
  'image.svg' = '<svg xmlns="http://www.w3.org/2000/svg"><image href="https://evil.example.com/x.png" /></svg>'
}

Invoke-OfflineCase -Name 'protocol-relative SVG xlink resource' -ExpectedPass $false -Files @{
  'image.svg' = '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><image xlink:href="//evil.example.com/x.png" /></svg>'
}

Invoke-OfflineCase -Name 'external JSON API resource' -ExpectedPass $false -Files @{
  'runtime.json' = '{"apiBase":"https://evil.example.com/api"}'
}

Invoke-OfflineCase -Name 'public CDN and font' -ExpectedPass $false -Files @{
  'index.html' = '<link href="https://fonts.googleapis.com/css2?family=Inter" rel="stylesheet"><script src="https://cdn.example.com/app.js"></script>'
}

Invoke-OfflineCase -Name 'public executable URL on an allowed metadata host' -ExpectedPass $false -Files @{
  'index.html' = '<script src="https://angular.dev/payload.js"></script>'
}

Write-Output 'Offline runtime regression cases passed against the real verifier.'
