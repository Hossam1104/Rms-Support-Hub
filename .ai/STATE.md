# Current Project State

- **Updated:** 2026-08-19
- **Active branch:** `feat/staging-release-candidate-pipeline` (closing into `main` via PR #24 merge baseline `ae4712cd0280c6f5b48797233f6574bec9ccea88`).
- **Status:** P0-B deterministic release candidate pipeline is ACCEPTED FOR MERGE. Independent Opus review concluded APPROVE with 0 Critical, 0 High, 0 Medium, and 0 Low new blocking findings (M-1, H-1, M-2, and M-3 all CLOSED). Sol merge authorized. .NET SDK 10.0.400 repository-wide pin is verified and in place. Both exact-head workflows on the accepted implementation head (`d811c5fa842887e01453a15decbda38f2a509df4`) are green: Support Hub CI run `32184944831` (SUCCESS) and POS CI run `32184944709` (SUCCESS, all jobs including POS Windows build and Infrastructure tests passed). PR #24 is ready for merge commit closure. No IIS deployment, Production/customer mutation, RMS gateway probe, or order mutation was performed.
- **Next milestone:** P0-C — Controlled Testing/Staging IIS Deployment and Read-Only Acceptance Evidence (Claude Sonnet 5 HIGH). NO external write or deployment may occur until the user provides fresh explicit approval for the specific Testing/Staging target.
- **Exact accepted technical head:** `d811c5fa842887e01453a15decbda38f2a509df4`.
- **Workflow trigger paths (L-8 correction):** `.ai/STATE.md`, `.ai/HISTORY.md`, and `TASK.md` are active workflow trigger paths in `.github/workflows/support-hub-ci.yml` and `.github/workflows/pos-ci.yml`. Any commit modifying these files triggers fresh CI runs.
- **POS CI status (L-9 correction):** POS CI run `32184944709` at accepted head `d811c5f` passed all 6 jobs without failure, including POS Windows build and Infrastructure tests.

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

- Merged baseline validation: Backend 253 passed / 0 failed; Frontend 362 passed / 0 failed across 59 files; Production frontend build passed; POS 420 passed; Pester 172 passed; PowerShell quality clean; `build.ps1`, `context.py`, and `check_memory.py` clean.
- P0-B adds `.github/workflows/support-hub-ci.yml` for backend/frontend paths and release-candidate gates. Toolchain is pinned to .NET SDK 10.0.400 / Node.js 24.18.0 / npm 12.0.1 repo-wide across `global.json`, `support-hub-ci.yml`, and `pos-ci.yml`.

## P0-B deterministic release candidate pipeline

- `scripts/build-release-candidate.ps1` performs clean-source Testing builds,
  fixed-timestamp frontend identity generation, framework-dependent .NET 10
  publish, sanitized `appsettings.json` generation from the Testing template,
  package exclusions, local runtime URL scanning, sorted file hashes,
  deterministic ZIP creation, ZIP SHA-256 sidecar creation, and fresh-
  extraction verification. `publish-iis.ps1` delegates to this pipeline and
  never deploys IIS.
- The package carries `release-manifest.json`, `wwwroot/build-identity.json`,
  `file-integrity.sha256`, `web.config`, Angular assets, an exact sanitized
  Testing `appsettings.json`/template pair with disabled registrations and no
  concrete topology, configuration schema identity, and deployment/rollback/
  smoke documentation. Runtime prerequisites include the .NET 10 Hosting
  Bundle and writable `var/drafts` storage.
- `/api/health/live` and `/api/health/ready` are local process/storage checks;
  packaged smoke does not contact RMS gateways or databases. The server-owned
  Testing tier remains the default and Production remains denied under Testing.
- Public Google Fonts/runtime CDN references were removed. The offline scan
  covers emitted HTML/HTM/CSS/JS/MJS/JSON/SVG/web-manifest/XML and allows only
  exact documented framework metadata plus the exact approved internal POS
  origins; configured RMS gateway URLs remain explicit server-side
  dependencies.

## P0-B review, closure, and deferred backlog

- **Review summary:**
  - Opus final review: APPROVE (0 Critical, 0 High, 0 Medium, 0 Low new blocking)
  - M-1 (reproducibility contract): CLOSED (narrowed to same-source/same-toolchain/equivalent-environment including checkout byte materialization)
  - H-1 (CI PR-head identity): CLOSED (`github.event.pull_request.head.sha` checkout and identity assertions verified)
  - M-2 (offline runtime gate): CLOSED (allowlist-strict offline URL scan with negative tests)
  - M-3 (sanitized Testing package configuration): CLOSED (template-driven sanitized `appsettings.json`, Production denial verified)
  - Sol: MERGE AUTHORIZED
- **Accepted reproducibility contract:** "Byte identity is verified for repeated builds from the same source commit using the recorded toolchain in an equivalent build environment, including checkout byte materialization; cross-environment byte identity is not guaranteed."
- **Deferred non-blocking backlog:**
  - P0-B L-4: PowerShell scripts `$LASTEXITCODE` cleanup observations.
  - P0-B L-5: `docs/release/SMOKE.md` operator `PackageRoot` example alignment.
  - P0-B L-6: Offline URL scanner negative coverage for `ws://`/`wss://`.
  - P0-B L-7: Local generated artifact hygiene.
  - L-8 / L-9: Corrected in durable state during closure.
  - npm advisories: 5 dev/build-tooling findings (`npm audit`), 0 production/runtime (`npm audit --omit=dev`).

## External release gates (unresolved, outside repository scope)

Real Production Code Signing signer; enterprise PKI issuance/renewal/revocation; representative elevated Windows lifecycle execution; representative LocalSystem CNG key ACL evidence; multiprocess H-3 contention evidence; managed Chrome/Edge and BackConnectionHostNames policy evidence; fleet deployment/enrollment plan; customer/environment approval; authorized Production execution window.

Repository merge does not authorize Production signer installation, PKI mutation, customer SCM activation, fleet browser-policy deployment, or RMS/Main Server live mutation.
