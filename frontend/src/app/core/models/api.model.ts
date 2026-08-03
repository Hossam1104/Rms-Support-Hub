/**
 * Unwrapped shape of the R6 error envelope
 * (`{ error: { code, message, details } }`, see docs/api-spec.md and
 * remediation_plan.md B22) as produced by the errorEnvelope interceptor.
 */
export interface ApiError {
  status: number;
  code: string;
  message: string;
  details?: unknown;
}

/** Mirrors OnlineOrderTool.Core.Services.ApiResponseResult -- the shape of
 * send-request / cancel-order / resend responses. */
export interface SendOrderResult {
  success: boolean;
  statusCode: number;
  responseText: string;
  urlSent: string;
}

/** Mirrors OnlineOrderTool.Core.DTOs.LookupResultDto. */
export interface LookupResult<T> {
  success: boolean;
  message?: string | null;
  data?: T | null;
}

/** Mirrors OnlineOrderTool.Core.DTOs.ModuleEndpointDto -- the active
 * environment's resolved send endpoint (U4, UI_Rework_Plan.md D13). Scoped
 * to the single resolved environment; the module catalog still never
 * carries URLs (B16). */
export interface ModuleEndpoint {
  environmentKey: string;
  environment: string;
  apiUrl?: string | null;
}

/** GET /api/modules/{key}/branches option. The code is the only value sent
 * back to the API; the name is display metadata from dbo.Branches. */
export interface BranchOption {
  code: string;
  name: string;
}
