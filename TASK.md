# WPF-05 - Logs, Safe Diagnostic Evidence, and Support Bundle

ROLE: Implement
AUTHORITY: GPT-5.6 Sol is Planner, Architect, and Acceptance Authority
BRANCH: `feat/wpf-05-logs-support-bundle`
PRIMARY STORY: US-E17-05 / #13035
STATUS: Implemented in Draft PR #36; exact-head CI green; Sol acceptance pending

## WPF-05 truth

- Started from accepted WPF-04 PR #35 main `0b9d0b678cfb33a3828876fb0a980fa8fdeb7676`; WPF-01/#32 through WPF-04/#35 are merged.
- `WPF -> rms.logs.evidence` typed Local IPC -> shared Application handler -> existing Agent analyzer/evidence reader. Application models are transport-neutral; Agent maps to V1 DTOs.
- Agent owns three fixed services, authorization, 15-second timeout, 12 records, 8 unknown reasons, 4 recommendations, redaction, and response bounds. Collection is only on Logs open/Refresh; no polling, arbitrary payload, caller-selected source file, raw log, SQL, path, or mutation.
- Secrets, credentials, connection-string secret values, host paths, Windows identities, SIDs, and raw/unbounded stack traces do not cross IPC. Bounded redacted stack-frame labels are intentionally visible; WPF-05 does not claim universal customer-data/PII-free output. Task #13116 defines remaining policy validation.
- `WPF -> support.bundle.generate` -> shared `SupportBundleExecutor`; Local Administrator only; principal/correlation bound; fixed-root generator; one audit; revoke on audit failure; successful SupportBundle timeline only after audit. WPF receives opaque metadata only; export/download is deferred to WPF-06.
- WPF has no Agent/Infrastructure reference, direct log reader/EventLog/SQL/HTTP/process/PowerShell/Named Pipe client, filesystem reader, mutation, backup/restore, fleet/SignalR, Production, provisioning, or Online Order work.

## Validation and live truth

- Restore passed; strict Release solution build with process-local `PosAgentSecurity__SupportHubOrigin=https://localhost:4443` passed with 0 warnings/0 errors.
- Release POS tests: Domain 12/12, Application 116/116, Infrastructure 156/156, Agent Integration 242/242, WPF 57/57; total 583/583. PowerShell 37/37; Pester 172/172, 0 failed/skipped/pending.
- Context, diff-check, and focused remediation tests passed; `check_memory.py` must pass after this compact task record is finalized.
- Azure 2026-08-23: #13017 Active/P2; #13018 Active/P1; #13031/#13033 Closed/P1; #13032/#13035 Active/P1; #13034/#13116 New/P2; #13019/#13020 New/P2; #13072-#13076 preserved New with live P1/P2 priorities.
- PR #36 remains Draft/open; no ready/merge/deploy/provision/Production/RMS mutation; WPF-06 not started.
- Runtime truth is recorded in `.ai/STATE.md`; no visual claim is made without Computer Use.

## Full next prompt - WPF-06

SOL_ACCEPTED_WPF05_SHA=<provided by Sol after acceptance>

Inert until Sol supplies the exact accepted SHA above; do not execute WPF-06 during WPF-05.

### 1. Exact-head merge automation (first)

Run first: `gh pr view 36 --json number,state,isDraft,baseRefName,headRefName,headRefOid,mergeCommit`. Verify PR #36 is open/Draft, base `main`, and `headRefOid` equals `SOL_ACCEPTED_WPF05_SHA`; verify exact-head POS and Support Hub CI are green. If moved, missing, or red, abort. Then run `gh pr ready 36`, `gh pr merge 36 --squash --match-head-commit <accepted SHA>`, verify `merged=true`, capture squash SHA, `git checkout main`, `git pull --ff-only`, and verify clean main with `HEAD==origin/main` containing that merge. Update #13035 Closed/P1 with PR/accepted SHA/merge SHA/CI evidence only after this proof. Create `git switch -c feat/wpf-06-database-backup-artifact-delivery` only then.

### 2. Baseline, Azure, and scope

Use WPF-04 main `0b9d0b678cfb33a3828876fb0a980fa8fdeb7676`; verify WPF-01..05 from code/tests. Keep #13018 Active/P1, #13032 Active/P1, #13031/#13033 Closed/P1, #13035 Closed/P1 after merge, #13034 New/P2, #13017 Active/P2, #13019/#13020 New/P2, #13116, and #13072-#13076 unchanged except required evidence. Implement only fixed RMS backup creation, bounded inventory/metadata, local artifact delivery/export, and Support Bundle export. Restore is separately gated; do not implement it or start snapshots, cleanup/reset, package, rollback, SignalR/fleet, remote, installer, Online Order, or Production work.

### 3. Architecture and authorization

Keep Domain independent; Application has no Contracts; Agent is composition/mapping root; WPF owns only typed Local IPC adapters. Preserve Core -> Data -> API layering and `IOrderModule.Capabilities` gates. Only `LocalIpcClient` owns WPF IPC. WPF must not use Agent/Infrastructure, EventLog, direct log readers, `SqlConnection`, `HttpClient`, `Process.Start`, PowerShell, `NamedPipeClientStream`, filesystem readers, or native RMS APIs. Add regression searches/tests.

Derive authority from trusted Windows invocation context, never payload role/SID/target claims. LocalOperator may read bounded inventory/metadata; LocalAdministrator is required for backup creation, Support Bundle export, and mutation. Bind artifact, capability, audit, correlation, and cancellation to the authenticated principal; wrong principals cannot read, download, revoke, or learn paths.

### 4. Fixed targets and input boundary

Use only the server-owned Branch/Cashier RMS target catalog and opaque target IDs. Reject caller-selected SQL, connection strings, database names, source paths, UNC paths, filenames, shell, PowerShell, arbitrary files, and caller-selected filters. No request/IPC/URL/query field carries a caller-selected source SQL/path; no server path is returned.

### 5. Artifact, audit, and delivery security

Reuse fixed Agent roots, `ArtifactCatalog`, opaque IDs, principal scope, retention, size/time limits, expiry, checksum, cancellation, and fail-closed missing-artifact behavior. Exclude/redact credentials, secret connection values, keys, host paths, identities, SIDs, raw logs, customer payloads, and unbounded exception/stack data. Record exactly one durable audit per attempted operation and claim success only after acceptance. Audit/checksum/size/registration failure deletes/revokes the capability and file and records no successful timeline. Support Bundle export uses this same mechanism, never an ad hoc ZIP/path route.

Destination UX is destination-only: if a save chooser is allowed, Agent validates it against an allowed local policy; source remains an opaque Agent artifact. Reject traversal, ADS, reparse/symlink escape, UNC/network, protected locations, and out-of-policy destinations without path leakage. Overwrite requires explicit admin confirmation, audit, atomic validated temp-file replacement, checksum/size verification, and temp cleanup; never silently overwrite.

### 6. Expiry, cancellation, concurrency, and WPF UI

Verify checksum and expiry before metadata/bytes; enforce bounded size/time, cooperative cancellation, retention cleanup, and truthful canceled/failed outcomes. Prevent concurrent backup/export/restore for one target with the existing typed lease; prove a second request cannot delete a valid first artifact. Reject malformed/oversized/mismatched-correlation/unsupported-version/invalid-enum/missing-file/wrong-principal requests without exception or path detail.

Add only approved WPF-06 target, metadata, admin-confirmation, progress, cancel, checksum/expiry, destination, overwrite, and safe-state UI. Health polling stays read-only; backup/export is explicit. LocalOperator controls are read-only/disabled. Do not claim export, backup, restore, or remote/fleet behavior without evidence.

### 7. Tests and required searches

Add deterministic tests for fixed targets, no caller SQL/path, operator/admin authorization, principal-scoped read/revoke, opaque metadata, checksum/expiry/retention, size/time, cancel, audit failure/revocation, overwrite/reparse/traversal, concurrency, malformed responses, and WPF-05 regression. Preserve WPF-05 3-service/12-record/8-unknown/4-recommendation/15-second/on-demand bounds, no arbitrary input, secret/drive/UNC/credential/SID/user redaction, bounded redacted frames, audit-once/timeline-once success, audit-failure revocation, and HTTP origin + one-use-token tests.

Architecture tests must prove Application has no Contracts reference/import, Domain has no Contracts, WPF has no Agent/Infrastructure/direct log reader/EventLog/SQL/HTTP/process/PowerShell/NamedPipe access beyond `LocalIpcClient`. Run all POS test projects, record actual counts, PowerShell quality, Pester, restore, strict Release `dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --warnaserror` with Testing-only origin, `python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`, `git diff --check`, and prohibited-reference/security searches.

### 8. Docs, Azure, runtime, Git, and report

Update only affected tracked Markdown and live Azure evidence. State WPF-01..05 merged only after proof, WPF-06 current, #13035 closed after accepted merge, export scope, and the precise bounded-redaction caveat; never say "no customer data" or "no stack trace" when bounded redacted frames are exposed. Do not bulk-edit unrelated Azure items.

Stop only stale project-owned Support Hub processes; build Release; launch final WPF with process-local `WINDIR=C:\WINDOWS` only if needed. Verify actual path/PID/title/responding/exactly one instance and actual frontend/backend probes; make no visual claim without Computer Use; preserve healthy API/Angular; never provision Agent/group or contact Production.

Review diff, remove temporary files, commit, push without force, create/update a Draft PR, wait for exact-head POS and Support Hub CI, and report branch/commit/PR, Azure states, actual counts, runtime evidence, unavailable dependencies, and remaining gates. Do not ready or merge the WPF-06 PR.

HARD STOP - DO NOT EXECUTE WPF-06 until GPT-5.6 Sol reviews and accepts WPF-05. Do not start WPF-06 now. Do not merge WPF-05 now. Leave the final WPF application running.
