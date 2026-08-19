# P0-D — Testing Integration & Operational Readiness

MODEL: Gemini 3.7 | ROLE: Implementation Preparation / Operational Readiness
PROGRAMME: Staging-Safe Release Candidate v1 | MILESTONE: P0-D Testing Integration & Operational Readiness
Repository: `D:\AI Tools\DBS\Rms-Support-Hub` | Target: `HOSSAM` (Local Windows IIS)

### 1. OBJECTIVE & EXECUTION PREFERENCE
Prepare and verify operational readiness of the deployed `RmsSupportHub.Testing` IIS site on host `HOSSAM` with authorized Testing external configuration and complete end-to-end integration workflows against authorized Testing endpoints.

Executor preference while current quotas apply:
- Gemini 3.7 for bounded preparation/validation/docs/config work.
- Do not consume Luna unless Sol explicitly escalates a genuinely high-risk cross-cutting implementation problem.
- Claude remains unavailable until quota resets.

### 2. THREE ENVIRONMENT TIERS & BOUNDARIES
P0-D explicitly distinguishes:
A. **Local HOSSAM Support Hub work**: Local IIS site/pool, local config files, local development servers, local builds/tests.
B. **Shared/customer Testing environment access**: External Testing RMS APIs, databases, gateways (requires explicit operation authorization packets).
C. **Production**: Strictly forbidden; completely outside P0-D authorization.

### 3. CURRENT DEPLOYED LOCAL BASELINE
- Host: `HOSSAM`
- IIS Site: `RmsSupportHub.Testing`
- Application Pool: `RmsSupportHub.Testing` (No Managed Code, Integrated, ApplicationPoolIdentity)
- URL: `http://localhost:8080/`
- Physical Path: `C:\inetpub\RmsSupportHub.Testing`
- External Configuration: `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json`
- Current External Config: `{}`
- Deployed RC Source: `13d590674e894ea720d2f4e3407c23d560923273`
- Build ID: `c372ecaee6a7d7bc0f026599b1d793f4f1d342f5ce479fa1809e37fa7b900a46`
- Current Repository Head: `72793b878430a6ec57f2ca4ae79c2a73715ffba6`

### 4. MANDATORY SAFETY BOUNDARY & AUTHORIZATION GATES
- Do NOT guess or discover real Testing values by probing RMS gateways or databases.
- Real Testing endpoints, connection strings, credentials, module registrations, database overrides, or similar customer/shared-environment values remain **OWNER INPUT REQUIRED**.
- Adding owner-provided configuration values to the external server-owned JSON does NOT by itself authorize downstream mutation.
- Before ANY shared/customer Testing action that can mutate state, require an explicit operation packet identifying:
  - environment
  - system
  - method/action
  - route/query where applicable
  - body/data where applicable
  - expected effect
  - reason
  - rollback/recovery
  - whether the operation is read-only or mutating
- Without separate explicit authorization, strictly PROHIBIT:
  - order creation/send
  - order cancellation
  - order resend
  - DB INSERT/UPDATE/DELETE
  - stored procedure execution with side effects
  - Main Server mutation
  - native RMS service mutation (`RMS.BranchService`, `RMS.CashierService`, `RMSServiceManager`)
  - customer configuration mutation
- Read-only access to a shared/customer Testing RMS API or database also requires explicit authorization before first contact.
- Production remains completely outside P0-D authorization.

### 5. INITIAL EXECUTABLE SCOPE
1. Verify current local HOSSAM IIS baseline remains healthy (`http://localhost:8080/api/health/ready`).
2. Inventory required external Testing configuration keys from repository contracts only.
3. Produce an OWNER INPUT REQUIRED matrix for missing real Testing values.
4. Validate provided values structurally/offline without contacting downstream systems where possible.
5. Prepare exact read-only and mutating integration approval packets separately.
6. Do not execute shared-environment probes or mutations until the applicable packet is explicitly authorized.
7. Preserve external config outside Git/package.
8. Preserve `SupportHub:DeploymentTier = Testing`, `AllowCustomEndpoints = false`, and all Production registrations disabled.
9. Update durable state only with facts actually established.
10. Maintain: REPOSITORY READY = YES, TESTING DEPLOYED = YES, TESTING ACCEPTED = YES, PRODUCTION READY = NO.
11. Keep all external Production gates open.
12. Leave Git clean and STOP at the authorization boundary.
