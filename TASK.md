# CLAUDE OPUS 5 HIGH
## Bounded Re-Review — P0-B Release Pipeline Remediation
MODEL: Claude Opus 5 HIGH
ROLE: Review only
PROGRAMME: Staging-Safe Release Candidate v1
MILESTONE: P0-B remediation re-review
Repository: `D:\AI Tools\DBS\Rms-Support-Hub`
Branch: `feat/staging-release-candidate-pipeline`
PR: `https://github.com/Hossam1104/Rms-Support-Hub/pull/24`
Base: `main@ae4712cd0280c6f5b48797233f6574bec9ccea88`
## Contract

Independently review the current branch, exact PR head, task diff, `.ai/STATE.md`,
`.ai/HISTORY.md`, generated package evidence, and repository tests. Do not use
chat history as evidence. This is review only: do not edit files, commit, push,
merge, deploy IIS, contact Production/RMS/Main Server/customer systems or
databases, send/cancel/resend orders, mutate Testing data, or start P0-C/OMS/
Call Center/POS architecture work. Do not reopen P0-A L-1/L-2/L-3 unless a
  direct regression is proven.
## Objective

Decide whether PR #24 is acceptable for merge as a deterministic, offline-
verifiable Testing/Staging IIS release-candidate pipeline. Separate repository
proof from unavailable live-environment evidence. Challenge claims with concrete
  file/line, artifact, test, and exact-head CI evidence.
## Required review

1. H-1 identity: confirm pull-request checkout uses
   `github.event.pull_request.head.sha`, push/main uses `github.sha`, the
   checked-out HEAD equals the intended SHA, and the identity assertion compares
   Git HEAD, `release-manifest.json.sourceCommit`, and
   `wwwroot/build-identity.json.commit`. Reject any `refs/pull/*/merge` artifact
   identity.
2. M-1 determinism: confirm exact .NET/Node/npm pins, recorded manifest
   `dotnetSdkVersion`, `nodeVersion`, and `npmVersion`, accurate same-source+
   same-toolchain reproducibility scope, deterministic timestamp/hash/input/ZIP
   behavior, and two same-toolchain byte-identical artifacts.
3. M-2 offline gate: inspect and run the real verifier. It must scan emitted
   HTML/HTM/CSS/JS/MJS/JSON/SVG and any emitted web-manifest/XML text assets,
   reject absolute and resource-bearing protocol-relative external targets,
   reject CDN/fonts, and allow only exact documented framework metadata and the
   exact approved internal POS origins. Prove negative cases for external HTML,
   CSS, SVG, JSON, CDN/font, and an executable URL on an inertly allowed host.
4. M-3 package safety: confirm root `appsettings.json` is an exact sanitized
   Testing template copy; DeploymentTier is Testing; custom endpoints are off;
   every Production registration is disabled; Production database overrides,
   real endpoint topology, secrets, and customer Testing values are absent.
   Confirm the package verifier and artifact safety regression reject prohibited
   mutations, while authorized Testing configuration remains external.
5. Package/integrity: confirm ZIP root shape, path safety, sidecar, required
   backend/frontend/web.config/manifest/schema/docs files, source/build identity,
   toolchain manifest, every integrity hash, fresh extraction, exclusions, and
   no `.env`, local/development settings, certificates/keys, maps/PDBs,
   `node_modules`, `.angular`, `bin`, `obj`, or runtime `var`.
6. Packaged smoke: when evidence is available, inspect or rerun the fresh-
   extraction smoke for startup, `/`, live/ready with writable `var/drafts`,
   `/api/modules`, SPA deep link, served identity, hashed main JS, and local
   assets. Confirm it does not contact RMS gateways or databases.
7. Safety boundary: confirm Testing remains server-owned, omitted tier defaults
   to Testing, Production is denied under Testing, the browser exposes no raw
   connection strings/probe URLs/custom endpoint authority, capability truth is
   used, and no POS architecture or P0-A scope was expanded.
8. CI/delivery: inspect the workflow for actual paths, pinned actions/toolchains,
   Release tests/build, frontend tests/production build, Riyal, PowerShell,
   memory/diff, RC generation, fresh verification, identity assertion, offline
   negatives, safety test, and smoke. Check the exact final PR head and confirm
   existing POS CI was not disturbed. Do not require artifact retention for P0-B.
9. Operations boundary: confirm deployment/rollback/smoke docs state that this
   task did not deploy IIS, require the .NET 10 Hosting Bundle, IIS module/pool,
   server-owned configuration, and `var/drafts` ACL, and make no Production or
   live customer/RMS acceptance claim.
10. L-1/L-2/L-3/L-4: confirm durable state is reconciled; dead/overbroad
    allowlist entries and inaccurate rationale are closed; PowerShell dead
  `$LASTEXITCODE` checks are closed or explicitly deferred with evidence.
## Evidence commands
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
If a final ZIP/sidecar/evidence set is unavailable, state exactly why; never
call an unavailable check passed. Review only the task-related diff and tests.
## Required output
Return only:
### Result
`APPROVE`, `REQUEST CHANGES`, or `BLOCKED`, with a concise reason.

### Findings
Concrete Critical/High/Medium/Low findings with file/line evidence and
criterion; explicitly state zero findings at empty severities.

### Determinism / Artifact
Exact head, toolchain, reproducibility scope, identity, manifest, hash, sidecar,
fresh extraction, package shape, and exclusion evidence.

### Offline / Runtime Smoke
Scanner coverage, exact allowances, negative tests, local assets, startup,
health, catalogue, fallback, served identity, and unavailable evidence.

### Testing / Safety
N-2/default, Testing authority, Production denial, sanitized configuration,
browser authority, and no-mutation evidence.

### CI / Exact Head
Reviewed commit, PR state, workflow source SHA, RC identity equality, check
status, and stale or missing gates.

### Remaining
Only unresolved P0-B findings, unavailable external evidence, and deferred
external gates. Do not create implementation work. Stop after this bounded
review; do not merge, deploy, or begin P0-C.
