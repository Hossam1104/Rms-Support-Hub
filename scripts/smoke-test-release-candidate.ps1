param(
  [Parameter(Mandatory)]
  [string]$PackageRoot,
  [ValidateRange(1024, 65000)]
  [int]$Port = 5270,
  [ValidateRange(5, 180)]
  [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
  if (-not $Condition) { throw $Message }
}

function Get-JsonFile([string]$Path) {
  return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Http([string]$BaseUrl, [string]$Path) {
  return Invoke-WebRequest -Uri "$BaseUrl$Path" -UseBasicParsing -MaximumRedirection 0 -ErrorAction Stop
}

if (-not (Test-Path -LiteralPath $PackageRoot -PathType Container)) {
  throw "Package root does not exist: $PackageRoot"
}
$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$apiDll = Join-Path $PackageRoot 'RmsSupportHub.Api.dll'
$manifestPath = Join-Path $PackageRoot 'release-manifest.json'
$identityPath = Join-Path $PackageRoot 'wwwroot\build-identity.json'
foreach ($required in @($apiDll, $manifestPath, $identityPath, (Join-Path $PackageRoot 'wwwroot\index.html'))) {
  Assert-True (Test-Path -LiteralPath $required -PathType Leaf) "Packaged runtime smoke input is missing: $required"
}

$manifest = Get-JsonFile $manifestPath
$identity = Get-JsonFile $identityPath
Assert-True ($manifest.environment -eq 'Testing') 'Packaged smoke requires a Testing release candidate.'
Assert-True ($manifest.sourceCommit -eq $identity.commit) 'Build identity commit does not match release manifest.'
Assert-True ($manifest.buildId -eq $identity.buildId) 'Build identity buildId does not match release manifest.'

$baseUrl = "http://127.0.0.1:$Port"
$logRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('rms-support-hub-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$process = $null

try {
  $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
  $startInfo.FileName = 'dotnet'
  $startInfo.Arguments = '"' + $apiDll.Replace('"', '\"') + '"'
  $startInfo.WorkingDirectory = $PackageRoot
  $startInfo.UseShellExecute = $false
  $startInfo.CreateNoWindow = $true
  $startInfo.Environment['ASPNETCORE_ENVIRONMENT'] = 'Testing'
  $startInfo.Environment['ASPNETCORE_URLS'] = $baseUrl
  $startInfo.Environment['DOTNET_NOLOGO'] = '1'

  $process = [System.Diagnostics.Process]::new()
  $process.StartInfo = $startInfo
  Assert-True $process.Start() 'Could not start dotnet for packaged runtime smoke testing.'

  $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
  $live = $null
  while ([DateTime]::UtcNow -lt $deadline) {
    if ($process.HasExited) { break }
    try {
      $live = Get-Http $baseUrl '/api/health/live'
      if ($live.StatusCode -eq 200) { break }
    } catch {
      Start-Sleep -Milliseconds 250
    }
  }
  $exitDetail = if ($process.HasExited) { " Process exited with code $($process.ExitCode)." } else { '' }
  Assert-True ($null -ne $live -and $live.StatusCode -eq 200) "Packaged API did not become live within $TimeoutSeconds seconds.$exitDetail"

  $liveBody = $live.Content | ConvertFrom-Json
  Assert-True ($liveBody.status -eq 'healthy') 'API liveness response was not healthy.'

  $rootResponse = Get-Http $baseUrl '/'
  Assert-True ($rootResponse.StatusCode -eq 200 -and $rootResponse.Content -match '<app-root') 'Packaged root did not serve Angular index.html.'

  $readyResponse = Get-Http $baseUrl '/api/health/ready'
  $readyBody = $readyResponse.Content | ConvertFrom-Json
  Assert-True ($readyResponse.StatusCode -eq 200 -and $readyBody.status -eq 'ready') 'Packaged readiness endpoint did not report ready.'
  Assert-True ($readyBody.deploymentTier -eq 'Testing') 'Packaged readiness endpoint did not preserve Testing tier.'

  $catalogResponse = Get-Http $baseUrl '/api/modules'
  $catalog = @($catalogResponse.Content | ConvertFrom-Json)
  Assert-True ($catalogResponse.StatusCode -eq 200 -and $catalog.Count -gt 0) 'Packaged module catalogue did not return modules.'

  $deepLinkResponse = Get-Http $baseUrl '/tools/online-orders'
  Assert-True ($deepLinkResponse.StatusCode -eq 200 -and $deepLinkResponse.Content -match '<app-root') 'Packaged SPA deep-link fallback did not serve index.html.'

  $servedIdentityResponse = Get-Http $baseUrl '/build-identity.json'
  $servedIdentity = $servedIdentityResponse.Content | ConvertFrom-Json
  Assert-True ($servedIdentity.commit -eq $manifest.sourceCommit) 'Served build identity commit is stale or mismatched.'
  Assert-True ($servedIdentity.buildId -eq $manifest.buildId) 'Served build identity buildId is stale or mismatched.'

  $mainFile = [string]$identity.mainBundle.file
  $mainPath = Join-Path $PackageRoot ("wwwroot\$mainFile")
  $mainDownload = Join-Path $logRoot $mainFile
  $mainResponse = Invoke-WebRequest -Uri "$baseUrl/$mainFile" -UseBasicParsing -OutFile $mainDownload -PassThru -ErrorAction Stop
  Assert-True ($mainResponse.StatusCode -eq 200) "Packaged main static asset '$mainFile' was not served."
  Assert-True ((Get-FileHash -LiteralPath $mainDownload -Algorithm SHA256).Hash.ToLowerInvariant() -eq $identity.mainBundle.sha256) 'Served main bundle hash does not match build identity.'

  foreach ($assetPath in @('/assets/Saudi_Riyal.svg', '/assets/CompanyLogos/Rms_Plus_Dark.svg')) {
    $assetResponse = Get-Http $baseUrl $assetPath
    Assert-True ($assetResponse.StatusCode -eq 200 -and $assetResponse.Content.Length -gt 0) "Packaged static asset '$assetPath' was not served."
  }

  Write-Output 'Packaged runtime smoke passed: process start, root, liveness, readiness, module catalogue, SPA deep link, build identity, and static assets.'
  Write-Output "Source commit: $($manifest.sourceCommit)"
  Write-Output "Build ID: $($manifest.buildId)"
}
catch {
  Write-Host $_.Exception.Message -ForegroundColor Red
  exit 1
}
finally {
  if ($null -ne $process) {
    try {
      if (-not $process.HasExited) {
        # Windows PowerShell 5.1 binds to the .NET Framework Process API,
        # which has Kill() but not the .NET Core Kill(entireProcessTree)
        # overload. The packaged API has no child process, so Kill() is the
        # precise and portable cleanup operation here.
        $process.Kill()
        [void]$process.WaitForExit(5000)
      }
    } catch { }
    $process.Dispose()
  }
  if (Test-Path -LiteralPath $logRoot) { Remove-Item -LiteralPath $logRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
