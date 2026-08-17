# Current Project State

- **Updated:** 2026-08-18
- **Active branch:** `feat/staging-environment-safety` (P0-A accepted and merge-authorized; PR #23; based on `f24e2fef1818f6aea3655fc7255dd894a4b53e71`).
- **Status:** P0-A server-owned Testing/Staging environment authority is accepted for merge by GPT-5.6 Sol following Claude Opus 5 HIGH bounded re-review (0 Critical / 0 High / 0 Medium). M-1 is closed. P0-B is the next programme phase. No external deployment or customer/Production mutation was performed.
- **Next task:** see `TASK.md` for the full `GPT-5.6 LUNA MAX HIGH` P0-B release candidate pipeline prompt.

## P0-A acceptance, M-1 closure, and merge handoff

- **Scope:** Server-owned `SupportHub:DeploymentTier` (defaults to `Testing`), server-side environment endpoint and database configuration resolution, Testing-tier Production denial, browser authority removal (no raw connection strings, probe URLs, or custom endpoint overrides), safe error envelopes, and policy-aware health projection.
- **Opus review & M-1 closure:** Initial Opus review at `04304ed` (0 Crit / 0 High / 1 Med / 3 Low). Sol required M-1 fixed before merge. Remediation at `500a8b3` introduced `DeploymentTierParser.TryParseExact` strict textual allowlist in Core used by validator and composition root. Final bounded Opus re-review: 0 Critical / 0 High / 0 Medium; M-1 CLOSED; P0-A ACCEPTED. Sol: MERGE AUTHORIZED.
- **Deferred findings & observations:**
  - P0-A L-1: URL safety validation completeness for optional endpoint templates.
  - P0-A L-2: legacy policy-free `IOrderModule.GetEnvironment`/`ModuleEnvironmentResolver` path.
  - P0-A L-3: connection string participates in in-process branch cache key.
  - N-1: host-startup failure test does not independently exercise redundant `Program.cs` fallback guard.
  - N-2: no automated test directly proves omitted `DeploymentTier` defaults to Testing (carried to P0-B).
- **External boundary:** No Testing/Staging IIS deployment occurred; no Release Candidate produced; no Production/customer mutation occurred; Production is not ready.

## POS and PR #21 / #22 foundation

- Sole normal C# package-trust authority is `%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`, non-configurable from config/env/CLI/API/browser. Mandatory distinct 40-hex Production/Testing signer pins in C# and PowerShell.
- OpenAPI host is metadata-only with no trust/lifecycle authority; normal startup without canonical trust fails closed.
- Rollback/recovery resolves target identity from checkpoint `PreviousVersion`; retained slots hold signed manifest+archive only, re-extracted and re-verified before activation, health-gated before success. Security-control files and ancestors are ACL/ownership-verified.
- SCM identity is `RmsSupportAgent` (`LocalSystem`); typed Windows lifecycle uses one mutation lease, fixed ACL roots, atomic checkpoints, HTTPS `/health/live` and `/health/ready` gates. Certificate prerequisite requires exact `rms-pos-agent.localhost` SAN, non-exportable CNG machine key, and LocalSystem private key ACL evidence.

## Validation baseline

- Merged baseline validation: Backend 252 passed / 0 failed (206 baseline + 46 M-1 tests); Frontend 362 passed / 0 failed across 59 files; Production frontend build passed; POS 420 passed; Pester 172 passed; PowerShell quality 29 files clean; `build.ps1`, `context.py`, and `check_memory.py` clean.
- Integrated Support Hub backend/frontend CI is absent (P0-B deliverable). POS CI workflows remain green.

## External release gates (unresolved, outside repository scope)

Real Production Code Signing signer; enterprise PKI issuance/renewal/revocation; representative elevated Windows lifecycle execution; representative LocalSystem CNG key ACL evidence; multiprocess H-3 contention evidence; managed Chrome/Edge and BackConnectionHostNames policy evidence; fleet deployment/enrollment plan; customer/environment approval; authorized Production execution window.

Repository merge does not authorize Production signer installation, PKI mutation, customer SCM activation, fleet browser-policy deployment, or RMS/Main Server live mutation.
