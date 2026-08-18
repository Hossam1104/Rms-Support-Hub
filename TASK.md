# CLAUDE OPUS 5 HIGH
## Independent Review — P0-B Deterministic Release Candidate Pipeline
MODEL: Claude Opus 5 HIGH
ROLE: Review only
PROGRAMME: Staging-Safe Release Candidate v1
MILESTONE: P0-B Release Candidate Pipeline
Repository: `D:\AI Tools\DBS\Rms-Support-Hub`
Branch: `feat/staging-release-candidate-pipeline`
Base: `main`
Expected baseline: `ae4712cd0280c6f5b48797233f6574bec9ccea88`

## Contract
Independently review the final branch and draft PR using the repository,
task-related diff, `.ai/STATE.md`, `.ai/HISTORY.md`, and generated evidence as
the source of truth. Do not reconstruct work from chat history.
This is REVIEW ONLY. Do not edit files, commit, push, merge, deploy IIS, or
open another PR. Do not contact Production, RMS/Main Server, customer systems,
or databases; do not send/cancel/resend orders. Do not start P0-C, OMS, Call
Center, or POS architecture work. Read `TASK.md`, `.ai/STATE.md`, and run
`python .ai/scripts/context.py` first; read `.ai/HANDOFF.md` only if its status
is `In Progress` or `Blocked`.

## Objective
Decide whether P0-B is acceptable for merge as a deterministic,
offline-verifiable Testing/Staging IIS Release Candidate pipeline. Challenge
claims with concrete evidence and separate repository proof from unavailable
live-environment evidence.

## Required review
1. Determinism: confirm clean-source enforcement, source commit and Testing
   binding, deterministic build timestamp, sorted/hash-defined package inputs,
   fixed ZIP entry times, repeatable ZIP bytes, and exact ZIP sidecar hash.
2. Integrity: inspect `scripts/build-release-candidate.ps1` and
   `scripts/verify-release-candidate.ps1`; confirm ZIP path safety, sidecar,
   required files, manifest schema/identity, every package hash, and genuinely
   fresh extraction verification.
3. Package shape: confirm backend publish output, Angular `wwwroot`,
   `web.config`, build identity, release manifest, integrity manifest,
   configuration schema/template, deployment/rollback/smoke docs, and no
   unexpected nested package root.
4. Exclusions: confirm absence of `.env`, local/development settings,
   certificates/private keys, source maps, compiler symbols, `node_modules`,
   `.angular`, `bin`, `obj`, and runtime `var`; confirm no secrets, tokens,
   connection-string values, or personal data are introduced.
5. Offline independence: inspect `scripts/verify-offline-runtime.ps1`; confirm
   emitted HTML/CSS/JS has no Google Fonts/CDN/public runtime dependency and
   serves required assets locally. Confirm its allowlist is limited to
   documented framework metadata and approved internal POS origins. Confirm
   RMS gateway URLs remain explicit server-side configuration and are not
   exposed to the browser or misclassified as CDN dependencies.
6. Packaged smoke: inspect and, when evidence is available, rerun
   `scripts/smoke-test-release-candidate.ps1` against a fresh extraction.
   Confirm packaged startup, `/`, `/api/health/live`, `/api/health/ready` with
   writable `var/drafts`, `/api/modules`, SPA deep-link fallback,
   `/build-identity.json`, hashed main JS, and representative static assets.
   Confirm the smoke does not contact RMS gateways or databases.
7. Testing safety: confirm the host-level N-2 test removes normal config
   sources and proves omitted `SupportHub:DeploymentTier` defaults to Testing;
   Testing remains server-owned; Production remains denied under Testing; no
   browser raw connection strings/probe URLs/custom endpoint authority or
   module-key string gating was added; P0-A L-1/L-2/L-3 and optional N-1 were
   not reopened or expanded.
8. CI and delivery: inspect `.github/workflows/support-hub-ci.yml` for actual
   backend/frontend paths, pinned actions, backend Release tests/build,
   frontend tests/production build, Riyal verification, PowerShell quality,
   memory/diff checks, RC generation, fresh verification, and smoke. Confirm
   checks ran on the exact final PR head and existing POS CI was not disturbed.
9. Operations boundary: confirm deployment, rollback, and smoke instructions
   say this task did not deploy IIS; confirm .NET 10 Hosting Bundle, IIS
   module/pool, server-owned config, and `var/drafts` ACL prerequisites; reject
   unsupported Production or live customer/RMS acceptance claims.

## Evidence commands
Use focused read-only checks first:
```powershell
git status --short --branch
git rev-parse HEAD
git diff main...HEAD --check
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
Get-Content .github/workflows/support-hub-ci.yml
Get-Content scripts/build-release-candidate.ps1
Get-Content scripts/verify-release-candidate.ps1
Get-Content scripts/verify-offline-runtime.ps1
Get-Content scripts/smoke-test-release-candidate.ps1
```
If a final ZIP/sidecar is available, verify it without silently overwriting
owner evidence. If a check cannot run, state the exact environmental reason;
never call an unavailable check passed.

## Required output
Return only:
### Result
`APPROVE`, `REQUEST CHANGES`, or `BLOCKED`, with a concise reason.
### Findings
Concrete findings ordered Critical/High/Medium/Low, with file/line evidence
and affected criterion; explicitly state zero findings at empty severities.
### Determinism / Artifact
Reproducibility, identity, manifest, hash, sidecar, extraction, and exclusion
evidence.
### Offline / Runtime Smoke
Dependency scan, local assets, startup, health, catalogue, fallback, identity,
and static-asset evidence.
### Testing / Safety
N-2, Testing authority, Production denial, and no-mutation evidence.
### CI / Exact Head
Reviewed commit, PR/check status, and stale or missing gates.
### Remaining
Only unresolved findings, unavailable external evidence, or explicit deferred
gates. Do not create implementation work in this session.
STOP after the bounded review. Do not merge, deploy, or run P0-C.
