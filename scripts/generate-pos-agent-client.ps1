[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$generatorRoot = Join-Path $repositoryRoot 'tools/pos-agent-client-generator'
$packageJsonPath = Join-Path $generatorRoot 'package.json'
$lockfilePath = Join-Path $generatorRoot 'package-lock.json'
$generatorPackagePath = Join-Path $generatorRoot 'node_modules/openapi-typescript/package.json'
$openApiPath = Join-Path $repositoryRoot 'pos/openapi/RmsSupportHub.Pos.Agent.json'
$generatedPath = Join-Path $repositoryRoot 'frontend/src/app/core/pos-agent/generated/pos-agent-api.generated.ts'

foreach ($requiredPath in @($packageJsonPath, $lockfilePath, $openApiPath)) {
  if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
    throw "Required POS Agent client generation file is missing: $requiredPath"
  }
}

if (-not (Test-Path -LiteralPath $generatorPackagePath -PathType Leaf)) {
  throw "Isolated generator dependencies are not installed. Run 'npm ci --prefix tools/pos-agent-client-generator' first."
}

$exitCode = 0
Push-Location $generatorRoot
try {
  & npm run generate
  $exitCode = $LASTEXITCODE
}
finally {
  Pop-Location
}

if ($exitCode -ne 0) {
  exit $exitCode
}

if (-not (Test-Path -LiteralPath $generatedPath -PathType Leaf)) {
  throw "POS Agent client generation completed without producing: $generatedPath"
}
