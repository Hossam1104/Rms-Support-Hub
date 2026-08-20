# Current Project State

- **Updated:** 2026-08-20
- **Current repository:** `main@9daa6d549f546ffc6bea4629cc6ea95a17406666` (equal to `origin/main`; task/state/handoff have intentional unstaged documentation updates).
- **Deployed HOSSAM Testing RC source:** `13d590674e894ea720d2f4e3407c23d560923273` (PR #25 merge commit).
- **Status:** P0-D Testing Integration & Operational Readiness preparation in progress; local IIS baseline verified healthy on `http://localhost:8080/`; P0-D0T machine package-trust discovery is blocked because HOSSAM has no established package signer identities or canonical trust file.
  - **PR #25 Merge:** Accepted technical head `ef5e459`, final PR head `c6f2ab9`, merge commit `13d590674e894ea720d2f4e3407c23d560923273`.
  - **CI Verification:** Support Hub CI run `32308207050` (SUCCESS) and POS CI run `32308207074` (SUCCESS) verified on merged `main@13d5906`.
  - **Release Candidate:** Build ID `c372ecaee6a7d7bc0f026599b1d793f4f1d342f5ce479fa1809e37fa7b900a46`, ZIP SHA-256 `664e7db24224788a1dddc4a4ffdb5b874f78299477e53cbab52e25b9c9a661ab`.
  - **HOSSAM IIS Deployment:**
    - Site: `RmsSupportHub.Testing` (bound to `http://*:8080/`).
    - App Pool: `RmsSupportHub.Testing` (No Managed Code, Integrated, ApplicationPoolIdentity).
    - Physical path: `C:\inetpub\RmsSupportHub.Testing`.
    - External configuration: `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json` (`{}`).
    - External config authority: `SUPPORTHUB_EXTERNAL_CONFIG_PATH`.
    - Runtime storage: `C:\inetpub\RmsSupportHub.Testing\var\drafts` (ACL: Modify granted).
  - **Acceptance Evidence:** All 7 read-only acceptance probes on `http://localhost:8080` passed (GET `/`, `/api/health/live`, `/api/health/ready`, `/api/modules`, `/build-identity.json`, SPA deep link, `/assets/Saudi_Riyal.svg`).
  - **Development Runtime:** Backend active on `http://localhost:5200` (live), Frontend active on `http://localhost:4200` (live).
  - **POS D0/P0-D0R/P0-D0T (2026-08-20):** Repository navigation and direct-Agent contracts remain intact: the Hub card targets `https://support-hub.integration.test:4443/tools/pos-maintenance` and the Agent origin remains `https://rms-pos-agent.localhost:5001`. Authorized Path B recovery recreated valid INT-13P Testing ownership, fresh HTTPS certificates, and stopped Agent/TestService services; ports `5001` and `4443` are unbound. Agent startup fails closed at `machine_trust_file_missing` because `C:\ProgramData\DBS\RmsSupportAgent\Trust\package-trust.json` is absent. HOSSAM has no `RmsSupportAgent` trust root, no fixed-root package archive/manifest, no LocalMachine Code Signing EKU candidate, and no TrustedPublisher entry. The fresh certificates are Server Authentication only and are not signer pins. Genuine Production/Testing signer identities remain unestablished; no trust file or pins were fabricated. POS HOSSAM runtime is **NOT ACCEPTED**.
  - **Readiness:** REPOSITORY READY = YES, HOSTING PREREQUISITE = READY, EXTERNAL CONFIG SUPPORT = ACCEPTED, TESTING DEPLOYED = YES, TESTING ACCEPTED = YES, PRODUCTION READY = NO.
  - **Non-blocking Low backlog:**
    - `L-P0C-1`: External-config path validation rejects UNC/URL paths but does not independently classify mapped drive letters as DriveType.Network. Local `C:\ProgramData` approved for HOSSAM target.
    - `L-P0C-2`: No dedicated permission-denied filesystem regression independently exercises the UnauthorizedAccessException branch, although implementation fails closed.

## P0-D1 Testing integration result (2026-08-20)

- **Configuration:** The deployed `C:\inetpub\RmsSupportHub.Testing\appsettings.json` is the sanitized Testing package configuration; the external override at `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json` parses as `{}`. `SupportHub:DeploymentTier=Testing`, `Outbound:VerifyTls=true`, `SupportHub:AllowCustomEndpoints=false`, and all deployed Production registrations are disabled.
- **Owner input required:** Missing live values are `ConnectionStrings:GhcEcommerce`, `ModuleEndpoints:GhcTesting`, `ModuleCancelEndpoints:GhcTesting`, `ConnectionStrings:UpcEcommerceTest`, `ModuleEndpoints:UpcTesting`, and `ModuleCancelEndpoints:UpcTesting`. No values were guessed, printed, logged, or committed. `ConnectionStrings:GhcUnicommerce` remains an optional disabled placeholder in the current contract, not a required active P0-D1 value.
- **External contact:** NONE. No shared Testing RMS API, gateway, TCP, database, Production, Main Server, customer configuration, or order mutation was contacted.
- **Local/deployed acceptance:** IIS readiness returned HTTP 200 with `deploymentTier=Testing`; module health classified Production as `policy_disabled` and GHC/UPC Testing as `unconfigured`; module DTOs exposed no endpoint topology or connection names; Online Orders root/deep links returned the SPA; unconfigured endpoint access failed closed without secret disclosure.
- **GHC:** Module discovery is present; Testing is unavailable until the three owner values exist. TCP probe and DB read-only acceptance are pending authorization and values. Item/consumer SQL is explicitly unverified against a live GHC schema; OrderRequests capability is false.
- **UPC:** Module discovery is present; Testing is unavailable until the three owner values exist. TCP probe and bounded read-only DB workflows (connection health, branches, item/consumer lookup, and OrderRequests SELECT/aggregate reads) are prepared but not authorized or executed.
- **GHC Uni-Commerce:** Registered but disabled placeholder. It has offline payload validation only; no Testing API/DB endpoint or read-only workflow is currently defined. Do not claim integration acceptance.
- **Authorization packets:** Full next-executable read-only and prohibited-operation packets are in `TASK.md`; execution stops at the authorization boundary.

## Validation baseline

- .NET SDK: 10.0.400 repository-wide pin.
- Backend Release build: 0 warnings, 0 errors (`--warnaserror`).
- Backend Release tests: 281 passed / 0 failed.
- Frontend tests: 362 passed / 0 failed across 59 test files.
- Frontend production build: passed cleanly (`npm run build -- --configuration production`).
- Riyal asset verifier: passed (`Saudi_Riyal.svg`, 924 bytes).
- Offline runtime negative tests: passed (`test-offline-runtime.ps1`).
- PowerShell quality: passed (`test-powershell-quality.ps1`).
- Focused P0-D1 configuration/routing/health tests: 116 passed / 0 failed.
- Packaged runtime smoke: passed (`smoke-test-release-candidate.ps1`).
- Package safety tests: passed against `C:\inetpub\RmsSupportHub.Testing` (`test-release-candidate-safety.ps1`).

## Safety and environment boundary

- External Production/POS/fleet/PKI gates remain open and unclosed.
- External Testing configuration contains `{}` (sanitized defaults preserved; no fake or real production secrets).
- No Production, customer, RMS gateway, or database contact or mutation performed.
- No repository product/script implementation was required by the proven root cause; no feature branch, commit, push, or PR was created.
