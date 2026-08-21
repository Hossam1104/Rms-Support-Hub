# Current Project State

- **Updated:** 2026-08-21
- **Repository:** feature branch `feat/p0-d-ghc-unicommerce-local-pos`, based on `origin/main@87d4673edbb95b5400d9a632e3795bde12df4960`.
- **Status:** P0-D bounded GHC E-Commerce and GHC Uni-Commerce Testing implementation is complete locally; Draft PR #26 is open against `main`, with hosted CI required for each pushed head.

## Testing configuration and schema evidence

- The external Testing override is populated at `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json`; it is outside the repository and is not tracked.
- Effective policy is Testing-only: `DeploymentTier=Testing`, `Outbound:VerifyTls=true`, `SupportHub:AllowCustomEndpoints=false`, all Production registrations disabled, GHC Testing enabled, Uni-Commerce Testing enabled, and UPC left unconfigured/disabled.
- Read-only SQL verification succeeded for GHC E-Commerce `RmsMainStg` and Uni-Commerce `RmsEcommerceStg` on the approved Testing SQL host. No credentials are stored in project memory.
- GHC item lookup now uses the verified live `Items`/branch-UOM/barcode/price/branch/tax schema. GHC consumer lookup uses the verified consumer/address schema. Uni consumer lookup uses the verified `Consumers.PrimaryPhoneNumber` schema.
- Uni-Commerce has no item master/catalog table in the verified database. Item lookup remains capability-gated and returns HTTP 501; this is an evidence-backed boundary, not a guessed query.
- Uni-Commerce `ExternalInvoiceRequests` is not the generic `OrderRequests` workflow; Uni `OrderRequests=false` remains explicit until a separately reviewed adapter is designed.

## Runtime and POS state

- Secure Testing Support Hub is running and verified at `https://support-hub.integration.test:4443`.
- Canonical Agent service is `RmsSupportHub.Pos.Agent`, verified at `https://rms-pos-agent.localhost:5001`; the disposable INT-13 TestService is also retained for Testing validation.
- Local Testing code-signing certificates and the protected canonical trust file under `C:\ProgramData\DBS\RmsSupportAgent\Trust\package-trust.json` were created for this local Testing session only. They are not Production PKI and do not authorize Production mode.
- No `RMS.BranchService`, `RMS.CashierService`, `RMSServiceManager`, Production service, customer system, or native RMS service was changed.
- Synthetic GHC and Uni sends used QA-only order data. GHC reached the Testing route and was rejected with downstream HTTP 400; Uni reached the Testing route and was rejected with downstream status 502. Neither was accepted, and no cancellation was attempted.

## Validation evidence

- Backend focused tests: 285 passed.
- Frontend tests: 367 passed across 60 files.
- POS Domain: 12 passed; Application: 82 passed; Infrastructure: 155 passed; Agent Integration/OpenAPI: 171 passed in the intended clean-trust validation run.
- PowerShell provisioning/lifecycle tests: 172 passed.
- Production frontend build passed. `scripts/build.ps1` passed its backend tests, Release build with 0 warnings/0 errors, production Angular build, and final checks.
- Runtime probes passed for Hub readiness, module catalog/health, Agent readiness, GHC/Uni endpoint and database checks, and the configured local secure origins.

## Remaining boundary

- Uni item lookup and generic Uni order history remain intentionally unavailable because the verified Uni schema does not provide compatible tables.
- Both synthetic Testing sends were rejected downstream; no accepted synthetic order exists to cancel.
- Production remains disabled and is not ready for use without real Production authority, PKI, and explicit approval.
