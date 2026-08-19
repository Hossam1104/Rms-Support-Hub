# P0-D — TESTING INTEGRATION & OPERATIONAL READINESS

MODEL: Claude Sonnet 5 HIGH | ROLE: Implement / Operational Verification
PROGRAMME: Staging-Safe Release Candidate v1 | MILESTONE: P0-D Testing Integration & Operational Readiness
Repository: `D:\AI Tools\DBS\Rms-Support-Hub` | Target: `HOSSAM` (Local Windows IIS)

### 1. OBJECTIVE
Verify operational readiness of the deployed `RmsSupportHub.Testing` IIS site on host `HOSSAM` with authorized Testing external configuration and complete end-to-end integration workflows against authorized Testing endpoints.

### 2. DEPLOYED BASELINE ON HOSSAM
- IIS Site: `RmsSupportHub.Testing` (http://localhost:8080)
- App Pool: `RmsSupportHub.Testing` (No Managed Code, Integrated, ApplicationPoolIdentity)
- Physical Path: `C:\inetpub\RmsSupportHub.Testing`
- External Config: `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json` (SUPPORTHUB_EXTERNAL_CONFIG_PATH)
- Writable Storage: `C:\inetpub\RmsSupportHub.Testing\var\drafts`
- Release Candidate: Built from merged `main@13d590674e894ea720d2f4e3407c23d560923273` (Build ID: `c372ecaee6a7d7bc0f026599b1d793f4f1d342f5ce479fa1809e37fa7b900a46`)

### 3. SCOPE & SAFETY BOUNDARIES
- Inject authorized Testing endpoints/secrets into `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json` only when explicitly provided/authorized by the owner.
- Maintain `SupportHub:DeploymentTier = Testing`.
- Production registrations and custom endpoints must remain disabled unless separately authorized.
- Do NOT perform live order mutation, cancellation, or resend against Production.
- Do NOT commit credentials, connection strings, or customer data to Git.
