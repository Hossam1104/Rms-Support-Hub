# Current Project State

- **Updated:** 2026-08-18
- **Active branch:** `feat/staging-release-candidate-pipeline`, based on merged P0-A `main` baseline `ae4712cd0280c6f5b48797233f6574bec9ccea88`.
- **Status:** P0-A server-owned Testing/Staging environment authority is complete and PR #23 is merged. M-1 is closed. P0-B implementation is complete locally; exact-head CI and the draft PR are the remaining Git gates before independent review. No IIS deployment, Production/customer mutation, RMS gateway probe, or order mutation was performed.
- **Next task:** run the final synchronized-branch CI/package evidence and hand the review-only Opus prompt in `TASK.md` to the independent reviewer.

## P0-A acceptance, M-1 closure, and merge handoff

- **Scope:** Server-owned `SupportHub:DeploymentTier` (defaults to `Testing`), server-side environment endpoint and database configuration resolution, Testing-tier Production denial, browser authority removal (no raw connection strings, probe URLs, or custom endpoint overrides), safe error envelopes, and policy-aware health projection.
- **Opus review & M-1 closure:** Initial Opus review at `04304ed` (0 Crit / 0 High / 1 Med / 3 Low). Sol required M-1 fixed before merge. Remediation at `500a8b3` introduced `DeploymentTierParser.TryParseExact` strict textual allowlist in Core used by validator and composition root. Final bounded Opus re-review: 0 Critical / 0 High / 0 Medium; M-1 CLOSED; P0-A COMPLETE and merged as PR #23.
- **Deferred findings & observations:**
  - P0-A L-1: URL safety validation completeness for optional endpoint templates.
  - P0-A L-2: legacy policy-free `IOrderModule.GetEnvironment`/`ModuleEnvironmentResolver` path.
  - P0-A L-3: connection string participates in in-process branch cache key.
  - N-1: host-startup failure test does not independently exercise redundant `Program.cs` fallback guard.
  - N-2: omitted `DeploymentTier` default is now covered by the P0-B host regression.
- **External boundary:** No Testing/Staging IIS deployment occurred; the RC is generated and smoke-verified only from a local clean extraction; no Production/customer mutation occurred; Production is not ready.

## POS and PR #21 / #22 foundation

- Sole normal C# package-trust authority is `%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`, non-configurable from config/env/CLI/API/browser. Mandatory distinct 40-hex Production/Testing signer pins in C# and PowerShell.
- OpenAPI host is metadata-only with no trust/lifecycle authority; normal startup without canonical trust fails closed.
- Rollback/recovery resolves target identity from checkpoint `PreviousVersion`; retained slots hold signed manifest+archive only, re-extracted and re-verified before activation, health-gated before success. Security-control files and ancestors are ACL/ownership-verified.
- SCM identity is `RmsSupportAgent` (`LocalSystem`); typed Windows lifecycle uses one mutation lease, fixed ACL roots, atomic checkpoints, HTTPS `/health/live` and `/health/ready` gates. Certificate prerequisite requires exact `rms-pos-agent.localhost` SAN, non-exportable CNG machine key, and LocalSystem private key ACL evidence.

## Validation baseline

- Merged baseline validation: Backend 252 passed / 0 failed (206 baseline + 46 M-1 tests); Frontend 362 passed / 0 failed across 59 files; Production frontend build passed; POS 420 passed; Pester 172 passed; PowerShell quality 29 files clean; `build.ps1`, `context.py`, and `check_memory.py` clean.
- POS CI workflows remain green. P0-B adds `.github/workflows/support-hub-ci.yml` for the backend/frontend paths and release-candidate gates.

## P0-B deterministic release candidate pipeline

- `scripts/build-release-candidate.ps1` performs clean-source Testing builds,
  fixed-timestamp frontend identity generation, framework-dependent .NET 10
  publish, package exclusions, local runtime URL scanning, sorted file hashes,
  deterministic ZIP creation, ZIP SHA-256 sidecar creation, and fresh-
  extraction verification. `publish-iis.ps1` delegates to this pipeline and
  never deploys IIS.
- The package carries `release-manifest.json`, `wwwroot/build-identity.json`,
  `file-integrity.sha256`, `web.config`, Angular assets, a names/placeholders-
  only Testing configuration template, configuration schema identity, and
  deployment/rollback/smoke documentation. Runtime prerequisites include the
  .NET 10 Hosting Bundle and writable `var/drafts` storage.
- `/api/health/live` and `/api/health/ready` are local process/storage checks;
  packaged smoke does not contact RMS gateways or databases. The server-owned
  Testing tier remains the default and Production remains denied under Testing.
- Public Google Fonts/runtime CDN references were removed. The offline scan
  covers emitted HTML/CSS/JS and allows only documented framework metadata and
  approved internal POS origins; configured RMS gateway URLs remain explicit
  server-side dependencies.
- Local evidence: backend Release 253/253 tests, frontend 362/362 tests in
  59 files, Release build with 0 warnings/0 errors, production build, Riyal
  verifier, broad `scripts/build.ps1`, 33-file PowerShell quality gate,
  fresh-extraction integrity verification, repeated identical ZIP hash, and
  packaged runtime smoke all passed. Exact final artifact identity is reported
  from the synchronized final head.

## External release gates (unresolved, outside repository scope)

Real Production Code Signing signer; enterprise PKI issuance/renewal/revocation; representative elevated Windows lifecycle execution; representative LocalSystem CNG key ACL evidence; multiprocess H-3 contention evidence; managed Chrome/Edge and BackConnectionHostNames policy evidence; fleet deployment/enrollment plan; customer/environment approval; authorized Production execution window.

Repository merge does not authorize Production signer installation, PKI mutation, customer SCM activation, fleet browser-policy deployment, or RMS/Main Server live mutation.
