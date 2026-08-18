Set-StrictMode -Version Latest

function Test-ReleasePlaceholderOrEmpty {
  param([AllowNull()][object]$Value)

  if ($null -eq $Value) { return $true }
  $text = [string]$Value
  return [string]::IsNullOrWhiteSpace($text) -or $text -match '^<[^<>]+>$'
}

function Assert-ReleaseSafeStringValues {
  param(
    [AllowNull()][object]$Value,
    [string]$Path = 'configuration'
  )

  if ($null -eq $Value) { return }
  if ($Value -is [string]) {
    if ($Value -match '(?i)^https?://') {
      throw "Testing release configuration contains a network URL at '$Path'."
    }
    if (($Path -match '(?i)(password|pwd|secret|token|private.?key)') -and -not (Test-ReleasePlaceholderOrEmpty $Value)) {
      throw "Testing release configuration contains a secret-like value at '$Path'."
    }
    return
  }

  if ($Value -is [System.Collections.IDictionary]) {
    foreach ($key in $Value.Keys) {
      Assert-ReleaseSafeStringValues $Value[$key] "$Path.$key"
    }
    return
  }

  if ($Value.PSObject -and $Value.PSObject.Properties) {
    foreach ($property in @($Value.PSObject.Properties)) {
      Assert-ReleaseSafeStringValues $property.Value "$Path.$($property.Name)"
    }
  }
}

function Assert-SanitizedTestingConfiguration {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)]
    [string]$Path
  )

  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Testing release configuration does not exist: $Path"
  }

  try {
    $configuration = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
  } catch {
    throw "Testing release configuration is not valid JSON: $Path. $($_.Exception.Message)"
  }

  if ($null -eq $configuration.SupportHub) {
    throw 'Testing release configuration is missing SupportHub.'
  }
  if ($configuration.SupportHub.DeploymentTier -ne 'Testing') {
    throw "Packaged Testing configuration must set SupportHub:DeploymentTier to Testing, got '$($configuration.SupportHub.DeploymentTier)'."
  }
  if ($configuration.SupportHub.AllowCustomEndpoints -ne $false) {
    throw 'Packaged Testing configuration must disable SupportHub custom endpoints.'
  }
  if ($null -ne $configuration.Outbound -and $configuration.Outbound.VerifyTls -ne $true) {
    throw 'Packaged Testing configuration must enable outbound TLS verification by default.'
  }

  foreach ($sectionName in @('ConnectionStrings', 'ModuleEndpoints', 'ModuleCancelEndpoints')) {
    $section = $configuration.$sectionName
    if ($null -eq $section) { continue }
    foreach ($property in @($section.PSObject.Properties)) {
      if (-not (Test-ReleasePlaceholderOrEmpty $property.Value)) {
        throw "Packaged Testing configuration contains a concrete value at '${sectionName}:$($property.Name)'; inject customer configuration outside the package."
      }
    }
  }

  $registrations = $configuration.SupportHub.Environments
  if ($null -ne $registrations) {
    foreach ($moduleProperty in @($registrations.PSObject.Properties)) {
      foreach ($environmentProperty in @($moduleProperty.Value.PSObject.Properties)) {
        $registration = $environmentProperty.Value
        if ($null -eq $registration) { continue }
        if ($environmentProperty.Name -match '(?i)production' -and $registration.Enabled -eq $true) {
          throw "Packaged Testing configuration enables Production registration '$($moduleProperty.Name)/$($environmentProperty.Name)'."
        }
        $customEndpointProperty = $registration.PSObject.Properties |
          Where-Object { $_.Name -eq 'AllowCustomEndpoint' } |
          Select-Object -First 1
        if ($null -ne $customEndpointProperty -and $customEndpointProperty.Value -eq $true) {
          throw "Packaged Testing configuration enables a custom endpoint for '$($moduleProperty.Name)/$($environmentProperty.Name)'."
        }
        $databaseOverrideProperty = $registration.PSObject.Properties |
          Where-Object { $_.Name -eq 'DatabaseOverride' } |
          Select-Object -First 1
        if (($null -ne $databaseOverrideProperty) -and (-not [string]::IsNullOrWhiteSpace([string]$databaseOverrideProperty.Value))) {
          throw "Packaged Testing configuration contains a database override for '$($moduleProperty.Name)/$($environmentProperty.Name)'."
        }
      }
    }
  }

  Assert-ReleaseSafeStringValues $configuration
  return $configuration
}

Export-ModuleMember -Function Assert-SanitizedTestingConfiguration, Test-ReleasePlaceholderOrEmpty
