# Current Project State

- **Updated:** 2026-08-20
- **Current repository:** `main@1cfe651ea4d6a2ab914f2d32e8006f6c55c023be` (working tree clean).
- **Deployed HOSSAM Testing RC source:** `13d590674e894ea720d2f4e3407c23d560923273` (PR #25 merge commit).
- **Status:** P0-D Testing Integration & Operational Readiness preparation in progress; local IIS baseline verified healthy on `http://localhost:8080/`; POS secure-origin closure diagnosed and blocked at the elevated HOSSAM Testing machine-mutation gate.
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
  - **POS D0 secure-origin diagnosis (2026-08-20):** Repository navigation and direct-Agent contracts are intact and tested: the Hub card targets `https://support-hub.integration.test:4443/tools/pos-maintenance`, the wrong-origin guard preserves the exact secure handoff, and the Agent origin remains `https://rms-pos-agent.localhost:5001`. On HOSSAM, the secure hostname resolves to `127.0.0.1`, but TCP `4443` has no listener; both secure root/deep-link probes fail with connection refused. The matching `C:\ProgramData\DBS\RmsSupportHub\Int13Testing\provisioning.json` is missing while its old `SupportHubRuntime` staging tree remains, with a stale 2026-08-15 build identity for commit `03e2c0254e309059f82b287f293efedb5e4f29b7` / source state `modified`. The existing Agent service on loopback `5001` is healthy (`/health/live` and `/health/ready` HTTP 200), but current INT-13 ownership state is incomplete. POS HOSSAM runtime is **NOT ACCEPTED**; no machine state was changed.
  - **Readiness:** REPOSITORY READY = YES, HOSTING PREREQUISITE = READY, EXTERNAL CONFIG SUPPORT = ACCEPTED, TESTING DEPLOYED = YES, TESTING ACCEPTED = YES, PRODUCTION READY = NO.
  - **Non-blocking Low backlog:**
    - `L-P0C-1`: External-config path validation rejects UNC/URL paths but does not independently classify mapped drive letters as DriveType.Network. Local `C:\ProgramData` approved for HOSSAM target.
    - `L-P0C-2`: No dedicated permission-denied filesystem regression independently exercises the UnauthorizedAccessException branch, although implementation fails closed.

## Validation baseline

- .NET SDK: 10.0.400 repository-wide pin.
- Backend Release build: 0 warnings, 0 errors (`--warnaserror`).
- Backend Release tests: 281 passed / 0 failed.
- Frontend tests: 362 passed / 0 failed across 59 test files.
- Frontend production build: passed cleanly (`npm run build -- --configuration production`).
- Riyal asset verifier: passed (`Saudi_Riyal.svg`, 924 bytes).
- Offline runtime negative tests: passed (`test-offline-runtime.ps1`).
- PowerShell quality: passed (`test-powershell-quality.ps1`).
- Packaged runtime smoke: passed (`smoke-test-release-candidate.ps1`).
- Package safety tests: passed (`test-release-candidate-safety.ps1`).

## Safety and environment boundary

- External Production/POS/fleet/PKI gates remain open and unclosed.
- External Testing configuration contains `{}` (sanitized defaults preserved; no fake or real production secrets).
- No Production, customer, RMS gateway, or database mutation performed.
- No repository product/script implementation was required by the proven root cause; no feature branch, commit, push, or PR was created.
