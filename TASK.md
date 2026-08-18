# CLAUDE OPUS 5 HIGH
## Final Bounded Re-Review — P0-B M-1 Closure

MODEL: Claude Opus 5 HIGH
ROLE: Review only
PROGRAMME: Staging-Safe Release Candidate v1
MILESTONE: P0-B final M-1 closure re-review

Repository: `D:\AI Tools\DBS\Rms-Support-Hub`
Branch: `feat/staging-release-candidate-pipeline`
PR: `https://github.com/Hossam1104/Rms-Support-Hub/pull/24`
Base: `main@ae4712cd0280c6f5b48797233f6574bec9ccea88`

LAST REVIEWED: `ccdaa8db05f3b1e8db67bce08d5bb4911e55660b`
COMPARE THROUGH: FINAL NEW HEAD (see `.ai/STATE.md` for the exact SHA recorded
after this session's delivery)

## Contract

Independently review the current branch, exact PR head, task diff,
`.ai/STATE.md`, `.ai/HISTORY.md`, generated package evidence, and repository
tests. Do not use chat history as evidence. This is review only: do not edit
files, commit, push, merge, deploy IIS, contact Production/RMS/Main
Server/customer systems or databases, send/cancel/resend orders, mutate
Testing data, or start P0-C/OMS/Call Center/POS architecture work. Do not
reopen P0-A L-1/L-2/L-3 unless a direct regression is proven.

## Objective

Decide whether PR #24 is acceptable for merge now that M-1 has been
remediated and an unreviewed SDK toolchain change has been reconciled.
Challenge claims with concrete file/line, artifact, test, and exact-head CI
evidence.

## Required review

1. **SDK drift resolution.** Confirm commit `cb60d7f` ("ci(sdk): update SDK
   version to 10.0.400 and enable latestPatch rollForward") was reverted at
   `9af49d6`, and that a later commit re-applied a .NET SDK 10.0.400 pin only
   after explicit owner direction (recorded in `.ai/STATE.md` and `.ai/HISTORY.md`).
   Confirm no other hidden or partial SDK/toolchain upgrade remains: `global.json`
   must read `"version": "10.0.400"`, `"rollForward": "disable"`,
   `"allowPrerelease": false`; `.github/workflows/support-hub-ci.yml` and every
   `setup-dotnet` step in `.github/workflows/pos-ci.yml` must install `10.0.400`;
   `scripts/build-release-candidate.ps1` and `scripts/verify-release-candidate.ps1`
   must set `$ExpectedDotnetSdkVersion = '10.0.400'`. Confirm the literal string
   `10.0.302` no longer appears anywhere in the repository.
2. **M-1 closure — reproducibility contract.** Confirm the old unconditional
   claim ("Byte identity is guaranteed for the same source commit, recorded
   toolchain, and pipeline logic.") is gone, and that
   `scripts/build-release-candidate.ps1` (manifest writer),
   `scripts/verify-release-candidate.ps1` (manifest verifier — must reject a
   manifest whose `reproducibility` field does not match exactly), and
   `docs/release/DEPLOYMENT.md` all state the identical narrowed contract:
   byte identity is verified for repeated builds from the same source commit
   using the recorded toolchain in an equivalent build environment, including
   checkout byte materialization; cross-environment byte identity is not
   guaranteed. Confirm no conflicting reproducibility claim exists elsewhere
   (builder says A / verifier expects B / docs claim C is not acceptable).
   Confirm the manifest's `dotnetSdkVersion`/`nodeVersion`/`npmVersion` fields
   and `frontend/scripts/build-identity.mjs` were not altered beyond the SDK
   version literal.
3. **Same-environment determinism proof.** Confirm two release candidates
   generated from the same final head, in the same local environment, using
   the pinned 10.0.400/24.18.0/12.0.1 toolchain, produce identical ZIP
   SHA-256, identical Build ID, identical `release-manifest.json`
   reproducibility strings, and byte-identical `file-integrity.sha256`.
   Confirm cross-environment byte equality (e.g., against a prior GitHub CI
   artifact hash) is explicitly not claimed or required.
4. **H-1 identity** remains closed: pull-request checkout uses
   `github.event.pull_request.head.sha`, push/main uses `github.sha`, and the
   identity assertion compares Git HEAD, `release-manifest.json.sourceCommit`,
   and `wwwroot/build-identity.json.commit`.
5. **M-2 offline gate** and **M-3 package safety** remain closed: rerun or
   inspect the real verifier and package-safety regression; no regression
   introduced by the SDK or contract-wording changes.
6. **CI/delivery.** Confirm the exact final PR head passed both Support Hub CI
   (backend/frontend tests, RC generation, fresh verification, identity
   assertion, offline negatives, safety test, packaged smoke) and POS CI, and
   that POS CI's SDK version bump did not disturb any other POS CI scope or
   behavior.
7. **Low findings.** Confirm L-4 (PowerShell `$LASTEXITCODE` checks),
   L-5 (`docs/release/SMOKE.md` operator `PackageRoot` example), L-6 (offline
   scanner `ws://`/`wss://` coverage), and L-7 (stale local publish evidence,
   removed as hygiene this session) are explicitly recorded as non-blocking/
   deferred, not silently dropped.
8. **npm advisories.** Confirm `npm audit` (5 dev/build findings) vs.
   `npm audit --omit=dev` (0 findings) remains accurately recorded, and no
   dependency file was modified.
9. **Operations/safety boundary.** Confirm no IIS deployment, Production/
   customer mutation, RMS gateway probe, or order mutation occurred.

## Evidence commands

```powershell
git status --short --branch
git rev-parse HEAD
git diff main...HEAD --check
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
Get-Content global.json
Get-Content .github/workflows/support-hub-ci.yml
Get-Content .github/workflows/pos-ci.yml
Get-Content scripts/build-release-candidate.ps1
Get-Content scripts/verify-release-candidate.ps1
Get-Content docs/release/DEPLOYMENT.md
```

If a final ZIP/sidecar/evidence set is unavailable, state exactly why; never
call an unavailable check passed. Review only the task-related diff and
tests.

## Required output

Return only:

### Result
`APPROVE`, `REQUEST CHANGES`, or `BLOCKED`, with a concise reason.

### Findings
Concrete Critical/High/Medium/Low findings with file/line evidence and
criterion; explicitly state zero findings at empty severities.

### SDK Toolchain
Confirm 10.0.400 is consistently pinned repo-wide with no stale 10.0.302
reference and no hidden further drift.

### M-1
CLOSED or OPEN, with the exact contract wording found in each of the three
locations and whether they match.

### Determinism / Artifact
Exact head, toolchain, reproducibility scope, identity, manifest, hash,
sidecar, fresh extraction, package shape, and exclusion evidence.

### CI / Exact Head
Reviewed commit, PR state, workflow source SHA, RC identity equality, check
status for both Support Hub CI and POS CI.

### Remaining
Only unresolved P0-B findings, unavailable external evidence, and deferred
external gates.

### P0-B
`ACCEPTED FOR MERGE` or `DO NOT MERGE`.

No modifications. No commit. No push. No merge. No P0-C.
