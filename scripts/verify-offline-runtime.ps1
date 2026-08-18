param(
  [Parameter(Mandatory)]
  [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) {
  throw "Package root does not exist: $PackageRoot"
}

$webRoot = Join-Path $PackageRoot 'wwwroot'
if (-not (Test-Path -LiteralPath $webRoot -PathType Container)) {
  # Also accept the Angular browser output directly for the focused frontend
  # gate; packaged verification passes the IIS application root.
  if (Test-Path -LiteralPath (Join-Path $PackageRoot 'index.html') -PathType Leaf) {
    $webRoot = $PackageRoot
  } else {
    throw "Package does not contain wwwroot: $webRoot"
  }
}

# These are deliberately narrow. The two POS origins are configured internal
# application dependencies; they are not public CDN/font dependencies. The
# test-domain URLs are generated-client examples that may survive minification
# as documentation strings, not network targets used by the Support Hub. The
# W3C, Angular, and Three.js URLs are framework namespace/license/error-help
# metadata embedded in vendor code, not fetch/import targets.
$allowedHosts = @(
  'rms-pos-agent.localhost',
  'support-hub.integration.test',
  'rms-api.test',
  'rms-downloader.test',
  'www.w3.org',
  'angular.dev',
  'jcgt.org'
)
$publicDependencyPattern = '(?i)(fonts\.googleapis\.com|fonts\.gstatic\.com|unpkg\.com|jsdelivr\.net|cdnjs\.cloudflare\.com|ajax\.googleapis\.com|use\.fontawesome\.com|cdn\.)'
$urlPattern = '(?i)https?://[A-Za-z0-9][A-Za-z0-9.-]*(?::\d+)?(?:/[^\s''"<>)]*)?'
$files = @(Get-ChildItem -LiteralPath $webRoot -Recurse -File -Force | Where-Object {
  $_.Extension.ToLowerInvariant() -in @('.html', '.css', '.js')
})

if ($files.Count -eq 0) {
  throw 'No HTML, CSS, or JavaScript files were found under wwwroot.'
}

$unexpected = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
  $content = [System.IO.File]::ReadAllText($file.FullName)
  if ($content -match $publicDependencyPattern) {
    $unexpected.Add("$($file.FullName): public CDN/font dependency marker")
  }

  foreach ($match in [regex]::Matches($content, $urlPattern)) {
    $urlText = $match.Value.TrimEnd('.', ',', ';', ':')
    $uri = [Uri]$urlText
    $urlHost = $uri.Host.ToLowerInvariant()
    if ($urlHost -notin $allowedHosts) {
      $unexpected.Add("$($file.FullName): unexpected runtime URL '$urlText'")
    }
  }
}

if ($unexpected.Count -gt 0) {
  throw "Offline runtime verification failed:`n$($unexpected -join "`n")"
}

Write-Output "Offline runtime verification passed: scanned $($files.Count) HTML/CSS/JS files; no public runtime URL or CDN/font dependency found."
