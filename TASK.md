# POS Slice B - Main Server Profiles, Safe Diagnostic Console Run, Safety Snapshot, Repair and Agent Package Boundary

**Executor:** GPT-5.6 Luna Max, High. **Role:** Implement / Validate / Deliver. One active executor; continue from the repository; do not start another planning cycle.

## Baseline and startup

Slice A is the contract baseline: fixed Product Release and Client discovery, component drift, the real `RMSServiceManager` catalog, typed Health/DB-capacity/backup health, bounded redacted failure evidence, principal-scoped Incident Timeline, Support Bundle, responsive POS workspace, and all PR #10 Downloader/Artifact/Cleanup/Branch Reset behavior. Consume those seams; do not replace them without concrete evidence.

At session start read `TASK.md`, `.ai/STATE.md`, run `python .ai/scripts/context.py`, read `.ai/HANDOFF.md` only when In Progress or Blocked, then read only task-named source/tests/docs and relevant changed files. Read `.ai/PROJECT.md` or `.ai/DECISIONS.md` only when needed. This is implementation, validation, and delivery, not planning.

## Safety and environment boundaries

- Testing is the only live environment. Never use Production or a customer device.
- Main Server GET is pre-authorized only when genuinely read-only. Do not issue POST/PUT/PATCH/DELETE or a state-changing GET. If a mutation is required, stop and report exact method, route, parameters/body, effect, environment, and rollback implications; ask the Owner.
- Implement the boundaries below, but do not run RMS executables, installers, uninstallers, repair, package activation, or Main Server mutations during validation.
- No arbitrary executable, command, shell, script, environment, path, log provider, SQL, database, service target, URL, credential, token, or browser-supplied target may cross the Agent seam. Never proxy `InstallBranchDto` or any secret-bearing DTO; use fixed typed projections and redact before storage, logs, bundles, or Angular.
- Preserve exact-origin HTTPS/HTTP/1.1, Negotiate/local-Administrators authorization, fail-closed SID handling, one-use mutation tokens, principal scoping, bounded idempotency/concurrency, authenticated artifacts, SSE terminal truth, and explicit accepted/partial/notAttempted/recovery/unknown outcomes. Keep Domain/Core -> Application -> Infrastructure -> Agent and do not edit generated/runtime paths.

## Objective

Deliver one vertical Slice B across Domain/Application/Infrastructure/Agent, OpenAPI/generated Angular types, operator UI, tests, and docs:

1. Agent-owned allow-listed Main Server profiles and read-only Branch/POS installation-state evidence.
2. A bounded Safe Diagnostic Console Run with a real process boundary that cannot become arbitrary code execution.
3. A pre-maintenance Safety Snapshot with safe identity, drift, service, database, capacity, backup, and configuration evidence.
4. Typed Repair Installation and Guided Repair with preview, confirmation, rollback, and honest failure/recovery truth.
5. A real versioned Agent package boundary for install, upgrade, uninstall, rollback, integrity, ACL, service, and certificate ownership.

## Main Server profiles and state evidence

- Define server-owned named Testing/future-Production profiles without exposing credentials, bearer material, connection strings, or a free URL. Validate an allow-list, environment, and binding to discovered Branch/POS identity and client.
- Add typed Agent projections for documented, genuinely read-only Main Server capabilities and Branch/POS state. Angular requests a logical read only; it cannot supply URL, host, branch, POS, headers, credentials, or arbitrary query. Redact nested unknowns and reject stale/ambiguous identity as Unknown or ActionRequired.
- Treat existing Branch/POS PUTs as installation-state acknowledgements until a proven package, operation, polling, cancellation, idempotency, and rollback contract exists. Do not call them. Add tests for profile separation, binding, redaction, bounds, auth, and proof that no mutation is issued.

## Safe Diagnostic Console Run

- Add typed preview/start/status/result and operator UI, disabled until server-owned preconditions pass. The browser supplies only an opaque operation token and typed confirmation, never a command line.
- Resolve an executable from a fixed Agent manifest and exact executable/argument templates. No shell, arbitrary working directory/args, inherited secret environment, downloaded code, or user path. Bound wall time, output bytes/lines, result size, concurrency, cancellation, and process-tree termination.
- Capture stdout/stderr separately, redact secrets, paths, users/SIDs, connection data, and key material, and retain only an opaque principal-scoped artifact with authenticated fetch/expiry. Return truthful accepted/running/succeeded/failed/timedOut/cancelled/notAttempted/partial/unknown states; never equate process start with success.
- Do not run it in validation. Use synthetic process seams for allow-list rejection, caps, timeout/cancel, redaction, cleanup, replay, authorization, and terminal truth. Record timeline milestones only after an authorized POST boundary.

## Pre-maintenance Safety Snapshot

- Add preview/capture/verify/retrieve under a fixed Agent root with atomic writes, bounded retention, corruption fail-closed behavior, principal/environment scope, and no arbitrary filename/path.
- Capture only typed safe identity/GUID/client, Product Release/drift, canonical service states, database identity/reachability, fixed-root capacity, approved-backup metadata, consistency, package/version hashes, and health/timeline correlation IDs. Exclude secrets, raw config, credentials, SQL, arbitrary paths, raw logs, private keys, and full events.
- Require a fresh verified snapshot before repair/package mutation; bind it to identity, package/profile, principal, and expiry. Distinguish stale, mismatched, unavailable, and unverifiable. Test atomicity, retention, corruption, expiry, redaction, concurrency, and safe download naming.

## Repair Installation and Guided Repair

- Validate a server-owned package manifest and dry-run plan before confirmation: signature/trust, checksum, compatibility, size, archive traversal, exact files, service identities, ACL/certificates, capacity, and rollback material.
- Touch only the fixed Agent installation boundary and exact file/service/certificate allow-list. Never overwrite arbitrary paths, change Main Server state, alter unrelated services, or silently remove data. Preview safe logical effects and blockers without raw paths or secrets.
- Require exact-origin auth, one-use token, typed confirmation, fresh Snapshot, bounded idempotency, and principal-scoped operation state. Stage atomically, verify health, and roll back on failure. Surface partial, rollbackSucceeded, rollbackFailed, recoveryRequired, and unknown honestly.
- Guided Repair is an evidence-driven typed checkpoint sequence. Each state-changing step is allow-listed and independently confirmed, resumable only from a verified checkpoint, and never generated from browser text or logs. Keep real repair execution out of validation; synthetic seams must cover replacement interruption, ACL/space/signature failure, rollback failure, restart/recovery, replay, and auth.

## Real Agent package boundary

- Define a versioned package and Agent-owned install/upgrade/uninstall/rollback/repair/health lifecycle. Manifest files must include hashes, version, supported OS/runtime, service identity, ACL/certificate requirements, and rollback metadata.
- Acquire/stage only authenticated bounded server-owned artifacts; Angular cannot execute or directly own packages. Verify signature/checksum before staging and again before activation.
- Own service registration and display-name versus SCM-name mapping, LocalSystem/ACL/certificate/private-key requirements, health/startup, migration, uninstall safety, recovery, and a recoverable prior version. Add non-destructive package tests; do not expose fake enabled controls before this boundary exists.

## UI and regression requirements

Preserve Slice A's compact responsive workspace, typed health/drift/database/service evidence, bounded analyzer/timeline/bundle, safe configuration statements, accessibility/reduced-motion/token behavior, and all PR #10 controls. Add profile/state, console preview, Snapshot, repair, Guided Repair, and package status only as truthful typed workflows; Deployment, repair, and console controls must not be fake enabled buttons.

## Validation and delivery

Run targeted checks, affected backend build/tests with `PosAgentSecurity__SupportHubOrigin=https://support-hub.integration.test:4443`, frontend tests/build, OpenAPI/client generation twice with a byte-stable second pass, `git diff --check`, secret/private-path/generated-artifact scans, and `python .ai/scripts/check_memory.py`; run `scripts/build.ps1` when warranted. Use synthetic seams for every process/state boundary and never run an RMS executable, installer, repair, package activation, or Main Server mutation.

Inspect the task diff; update durable `.ai/STATE.md`, concise `.ai/HISTORY.md`, and clear `.ai/HANDOFF.md`; update stable project/decision docs only for contract changes. Commit on a feature branch, push/open a normal PR, merge only after review/check governance, sync `main --ff-only` only after a real remote merge, and leave clean. Runtime restart/probing is last; if authorized, use `scripts/dev.ps1`, report only responding endpoints, and leave successful project-owned frontend/backend processes running.

Do not execute Slice C: M-1 browser policy, M-2 Production certificate lifecycle, durable Production audit, fleet rollout, Whites equivalence, or independent final security/readiness review.

Return only `### Result`, `### Changes`, `### Validation`, and `### Remaining` as required by `AGENTS.md`.
