/**
 * Mirrors OnlineOrderTool.Core.DTOs.EnvironmentDto (see docs/api-spec.md §1).
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
}

/** Mirrors OnlineOrderTool.Core.Modules.ModuleCapabilities, exposed on
 * ModuleDto so routes/UI can gate on real capability data instead of
 * hardcoded module-key comparisons (see remediation_plan.md B21). */
export interface ModuleCapabilities {
  draftKind: string | null;
  itemLookup: boolean;
  consumerLookup: boolean;
  orderRequests: boolean;
  cancel: boolean;
  resend: boolean;
}

/** Mirrors OnlineOrderTool.Core.DTOs.ModuleDto. */
export interface ModuleDto {
  key: string;
  label: string;
  client: string;
  available: boolean;
  environments: EnvironmentDto[];
  capabilities: ModuleCapabilities;
}
