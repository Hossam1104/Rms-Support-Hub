param(
  [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$assetPath = Join-Path $RepositoryRoot 'frontend/public/assets/Saudi_Riyal.svg'
$expectedSha1 = '02b0fe79a4c8f39f6344682e7ef4dcb5f21cf938'

if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
  throw "Missing Riyal asset: $assetPath"
}

$bytes = [System.IO.File]::ReadAllBytes($assetPath)
$markup = [System.Text.Encoding]::UTF8.GetString($bytes).TrimEnd()
$canonicalBytes = [System.Text.Encoding]::UTF8.GetBytes($markup)
$sha1 = ([System.BitConverter]::ToString([System.Security.Cryptography.SHA1]::Create().ComputeHash($canonicalBytes))).Replace('-', '').ToLowerInvariant()
if ($sha1 -ne $expectedSha1) {
  throw "Unexpected Riyal asset SHA-1 '$sha1'; expected the SAMA vector '$expectedSha1'."
}

$svg = [xml]$markup
$root = $svg.DocumentElement
if ($root.LocalName -ne 'svg' -or $root.GetAttribute('viewBox') -ne '0 0 1124.14 1256.39') {
  throw 'Riyal asset must be the approved SVG viewBox.'
}

$pathNodes = @($svg.SelectNodes("//*[local-name()='path']"))
$emptyPaths = @($pathNodes | Where-Object { [string]::IsNullOrWhiteSpace($_.GetAttribute('d')) })
if ($pathNodes.Count -ne 2 -or $emptyPaths.Count -gt 0) {
  throw 'Riyal asset must contain two non-empty vector paths.'
}

$contentForReferenceCheck = $markup -replace 'xmlns="http://www\.w3\.org/2000/svg"', ''
$legacyArabicLabels = [string]::Concat([char]0x631, '.', [char]0x633, '|', [char]0x631, [char]0x633)
if ($contentForReferenceCheck -match $legacyArabicLabels) {
  throw 'Riyal asset contains a legacy textual currency label.'
}
if ($contentForReferenceCheck -match '<text\b|<script\b|<image\b|<clipPath\b|href\s*=|xlink:href|url\(|https?://|\bSAR\b|ر\.س|رس') {
  throw 'Riyal asset contains textual fallback, script content, clipping, or an external reference.'
}

Write-Output "Riyal asset verified: $assetPath ($($bytes.Length) bytes, SHA-1 $sha1, 2 paths, no text/external references)."
