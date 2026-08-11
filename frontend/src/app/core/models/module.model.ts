/**
 * Mirrors RmsSupportHub.Core.DTOs.EnvironmentDto (see docs/api-spec.md §1).
 * Never carries the real ApiUrl/CancelUrl -- only whether they are
 * configured -- so internal RMS endpoint topology is not published to the
 * browser (see remediation_plan.md B16, fixed server-side in R6).
 */
export interface EnvironmentDto {
  key: string;
  environment: string;
  description: string;
  accent: string;
  cue: string;
  icon: string;
  routeLabel: string;
  visualUrl: string;
  visualAlt: string;
  available: boolean;
  statusLabel: string;
  hasApiUrl: boolean;
  hasCancelUrl: boolean;
  isDefault: boolean;
}

/** Mirrors RmsSupportHub.Core.Modules.ModuleCapabilities, exposed on
 * ModuleDto so routes/UI can gate on real capability data instead of
 * hardcoded module-key comparisons (see remediation_plan.md B21). */
export interface ModuleCapabilities {
  draftKind: string | null;
  itemLookup: boolean;
  consumerLookup: boolean;
  orderRequests: boolean;
  cancel: boolean;
  resend: boolean;
  hasDeliveryFields: boolean;
}

/** Mirrors RmsSupportHub.Core.DTOs.ModuleDto. */
export interface ModuleDto {
  key: string;
  label: string;
  client: string;
  available: boolean;
  environments: EnvironmentDto[];
  capabilities: ModuleCapabilities;
}

/**
 * Mirrors RmsSupportHub.Core.DTOs.EnvironmentHealthDto: whether the API host
 * could open a TCP connection to this environment's send endpoint.
 *
 * This is not the same fact as `EnvironmentDto.statusLabel`. That label says
 * which lane an environment is (Live/Test/Soon) and stays constant while a
 * host is down -- otherwise a failing probe would make a Production lane stop
 * announcing itself as Live and read as safe to send against.
 *
 * `unconfigured` means the environment has no endpoint to probe. `unknown` is
 * client-side only: the sweep has not returned, or the Support Hub's own API
 * could not be reached -- neither of which is evidence the module is down.
 */
export type EnvironmentHealthStatus = 'reachable' | 'unreachable' | 'unconfigured';
export type EnvironmentHealthState = EnvironmentHealthStatus | 'unknown';

export interface EnvironmentHealthDto {
  moduleKey: string;
  environmentKey: string;
  status: EnvironmentHealthStatus;
  checkedAt: string;
}

/** Key for the flattened health lookup; one module owns many environment keys
 * and environment keys are only unique within a module. */
export function environmentHealthKey(moduleKey: string, environmentKey: string): string {
  return `${moduleKey}::${environmentKey}`;
}

export type EnvironmentHealthMap = ReadonlyMap<string, EnvironmentHealthStatus>;
