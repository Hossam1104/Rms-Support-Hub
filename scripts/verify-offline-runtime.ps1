param(
  [Parameter(Mandatory)]
  [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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

# These are exact emitted occurrences, not public-host allowlists. The first
# two are the fixed POS application origins used by the frontend. The other
# entries are current framework/license/namespace metadata observed in the
# emitted package and are allowed only at their exact documented paths.
$allowedUrlPatterns = @(
  '^https://rms-pos-agent\.localhost:5001$',
  '^https://support-hub\.integration\.test:4443$',
  '^http://www\.w3\.org/(?:1998/Math/MathML|1999/xhtml|1999/xlink|2000/svg|2000/xmlns/|XML/1998/namespace)$',
  '^https://angular\.dev/best-practices/security#preventing-cross-site-scripting-xss$',
  '^https://jcgt\.org/published/0007/04/01/$'
)

# Public dependency markers are kept explicit for readable diagnostics. Any
# other absolute URL is also rejected unless it matches an exact allowance.
$publicDependencyPattern = '(?i)(?:https?:)?//(?:fonts\.googleapis\.com|fonts\.gstatic\.com|unpkg\.com|jsdelivr\.net|cdnjs\.cloudflare\.com|ajax\.googleapis\.com|use\.fontawesome\.com)(?::\d+)?(?:[/?#]|$)'
$absoluteUrlPattern = '(?i)(?<url>https?://[A-Za-z0-9][A-Za-z0-9.-]*(?::\d+)?(?:/[^\s''"<>)]*)?)'
$protocolRelativePattern = '(?i)(?:\b(?:src|srcset|href|xlink:href|action|poster|data)\s*=\s*(?:["'']|)|url\(\s*["'']?|["''])(?<url>//[A-Za-z0-9][A-Za-z0-9.-]*(?::\d+)?(?:/[^\s''"<>)]*)?)'
$textExtensions = @('.html', '.htm', '.css', '.js', '.mjs', '.json', '.svg', '.webmanifest', '.xml')
$files = @(Get-ChildItem -LiteralPath $webRoot -Recurse -File -Force | Where-Object {
  $textExtensions -contains $_.Extension.ToLowerInvariant()
})

if ($files.Count -eq 0) {
  throw 'No runtime-delivered text assets were found under wwwroot.'
}

$unexpected = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
  $content = [System.IO.File]::ReadAllText($file.FullName)

  if ($content -match $publicDependencyPattern) {
    $unexpected.Add("$($file.FullName): public CDN/font dependency marker")
  }

  foreach ($match in [regex]::Matches($content, $absoluteUrlPattern)) {
    $urlText = $match.Groups['url'].Value.TrimEnd('.', ',', ';', ':')
    $allowed = $false
    foreach ($allowedPattern in $allowedUrlPatterns) {
      if ($urlText -match $allowedPattern) {
        $allowed = $true
        break
      }
    }
    if (-not $allowed) {
      $unexpected.Add("$($file.FullName): unexpected runtime URL '$urlText'")
    }
  }

  foreach ($match in [regex]::Matches($content, $protocolRelativePattern)) {
    $urlText = $match.Groups['url'].Value.TrimEnd('.', ',', ';', ':')
    $unexpected.Add("$($file.FullName): protocol-relative runtime URL '$urlText'")
  }
}

if ($unexpected.Count -gt 0) {
  throw "Offline runtime verification failed:`n$($unexpected -join "`n")"
}

Write-Output "Offline runtime verification passed: scanned $($files.Count) HTML/CSS/JS/MJS/JSON/SVG/web-manifest/XML text assets; no public runtime URL, protocol-relative resource, or CDN/font dependency found."
