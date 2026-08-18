# Current Project State

- **Updated:** 2026-08-18
- **Active branch:** `feat/staging-release-candidate-pipeline`, based on merged P0-A `main` baseline `ae4712cd0280c6f5b48797233f6574bec9ccea88`.
- **Status:** P0-A server-owned Testing/Staging environment authority is complete and PR #23 is merged. Initial Opus review of P0-B at `bfbbc71a0885f7f60d567ab2635cd50b4f65a3d9` was `REQUEST CHANGES` with 0 Critical, 1 High, 3 Medium, and 4 Low findings. The bounded remediation is complete in implementation commits `6ec14dc`, `30d3339`, and `6ead799`; Support Hub CI and the existing POS CI are green at the exact CI-validated head `6ead7992d4b1350edc8fb1a99c6955eea5d270cc`. PR #24 remains open, draft, and unmerged, awaiting independent Opus re-review and Sol acceptance. No IIS deployment, Production/customer mutation, RMS gateway probe, or order mutation was performed.
- **Next task:** hand the review-only Opus re-review prompt in `TASK.md` to the independent reviewer. This memory reconciliation is a follow-on documentation commit; read `git rev-parse HEAD` and PR metadata again before any acceptance because the exact branch head advances with this update.

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
- P0-B adds `.github/workflows/support-hub-ci.yml` for the backend/frontend paths and release-candidate gates. The remediation checks out the durable PR head, pins .NET SDK 10.0.302 / Node.js 24.18.0 / npm 12.0.1, asserts artifact identity, scans all emitted web text formats with exact URL allowances, and verifies sanitized Testing package configuration. Existing POS CI scope is unchanged.

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
- Local evidence at implementation head `30d33395e5f279d883c9c9c2c4b9a90e27ea43dc`:
  backend Release 253/253 tests and 0-warning/0-error build, frontend
  362/362 tests in 59 files, production build, Riyal verifier, 37-file
  PowerShell quality gate, context/memory/diff checks, offline negative cases,
  two byte-identical RC ZIPs, fresh-extraction integrity verification,
  package-safety rejection, and packaged runtime smoke all passed. The
  repository `scripts/build.ps1` wrapper reached its tests but its implicit
  restore hit this machine's inaccessible `C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.Offline.config`; the equivalent pinned Release build with the user NuGet config passed. Exact pushed-head artifact evidence remains an external CI gate.
- Exact CI-validated head `6ead7992d4b1350edc8fb1a99c6955eea5d270cc` passed
  Support Hub CI end-to-end, including the explicit npm 12.0.1 pin, RC
  generation, fresh extraction, identity, sanitized-package safety, and
  packaged smoke; all applicable POS CI jobs also passed at that same SHA.

## External release gates (unresolved, outside repository scope)

Real Production Code Signing signer; enterprise PKI issuance/renewal/revocation; representative elevated Windows lifecycle execution; representative LocalSystem CNG key ACL evidence; multiprocess H-3 contention evidence; managed Chrome/Edge and BackConnectionHostNames policy evidence; fleet deployment/enrollment plan; customer/environment approval; authorized Production execution window.

Repository merge does not authorize Production signer installation, PKI mutation, customer SCM activation, fleet browser-policy deployment, or RMS/Main Server live mutation.
