# WPF-07 - Safety Snapshots and Incident Timeline

ROLE: Implement
PRIMARY STORY: choose the accepted Azure child story only after Sol reviews WPF-06; do not close #13034 while guarded restore remains outstanding.
STATUS: WPF-06 is implemented on `feat/wpf-06-database-backup-artifact-delivery`; its Draft PR must remain Draft until Sol acceptance. This file is the next-slice prompt only.

## Current truth

- WPF-05 was accepted at `ee2d62c030a2266ed410f91604ce8524df61e50b`, merged through PR #36 as `e0c82cdefaac47c1ab9d0649d249cbf85f4d8837`, and #13035 is Closed/P1.
- WPF-06 implements fixed Branch/Cashier backup creation, principal-scoped bounded inventory, and one shared Agent-owned local artifact-delivery path for database backups and Support Bundles.
- WPF-06 does not implement database restore. #13034 remains Active/P1 until restore governance is separately reconciled.
- Preserve #13018 Active/P1, #13032 Active/P1, #13033 Closed/P1, #13035 Closed/P1, #13116 New/P2, #13019 New/P2, #13020 New/P2, and Online Order #13072-#13076 without unrelated changes.
- Bounded redacted stack-frame labels are an intentional WPF-05 output. Never claim universal customer-data or PII-free evidence; #13116 remains the privacy-policy task.
- No Production access, machine provisioning, service mutation, real database mutation, fleet/remote work, or Online Order work is authorized.

## Mandatory startup

Read `TASK.md`, `.ai/STATE.md`, run `python .ai/scripts/context.py`, read `.ai/HANDOFF.md` only if it is In Progress or Blocked, then read only task-relevant sources/tests/docs. Inspect current code and tests before challenging accepted work. Do not restart WPF-06 discovery.

## WPF-07 objective

Add the native WPF Safety Snapshots and Incident Timeline workspace through:

`WPF -> typed LocalIpcClient -> Agent Local IPC -> transport-neutral Application/Agent seam -> existing SafetySnapshotService and IncidentTimelineService`.

Reuse the existing fixed-root, atomic, integrity-protected Agent snapshot store and principal-scoped incident timeline. Do not create a second snapshot store, timeline store, filesystem browser, HTTP download route, or direct WPF file reader.

## Required scope

- Add only typed no-arbitrary-path operations for bounded snapshot inventory/metadata, explicit snapshot creation where the existing Agent capability permits it, and principal-scoped timeline read/record behavior.
- Derive authority, Windows identity, principal scope, correlation, and device facts from the trusted Agent invocation context. Never accept role, SID, source path, destination path, SQL, connection string, or filesystem root from payload JSON.
- Keep LocalOperator read-only. Require LocalAdministrator for any snapshot creation or other mutation. RemoteHub and unauthenticated callers are denied.
- Return safe metadata only: opaque snapshot ID, fixed snapshot kind, created time, expiry/retention, integrity state, bounded size/count, timeline category/severity/summary, correlation, and safe failure codes. Never return server paths, raw files, credentials, connection strings, customer payloads, or unbounded exception text.
- Keep operations bounded, cancellation-aware, principal-scoped, fail closed on checksum/reparse/missing/corrupt metadata, and truthful when the Agent or approved storage is unavailable.
- Expose a focused WPF workspace with bounded snapshot status/list and incident timeline records. Use existing design tokens and current shell patterns. Do not add service-control, restore, repair, cleanup, reset, package, remote, fleet, or Production controls.
- Preserve WPF-06 backup inventory/export behavior and the WPF-05 logs/evidence/Support Bundle regression surface. Support Bundle export must continue through the shared artifact-delivery service.

## Required tests

Add deterministic tests for:

- LocalOperator read access; LocalAdministrator mutation access; RemoteHub and unauthenticated denial.
- Fixed snapshot kinds and server-owned roots; no caller path/SQL/credential fields.
- Principal isolation for snapshot and timeline records; wrong-principal requests reveal no existence.
- Atomic write, checksum/integrity mismatch, expiry/retention, missing/reparse/corrupt metadata, bounded inventory, newest-first ordering, cancellation, concurrent create, audit failure, and safe unavailable/timeout/protocol/security states.
- Timeline severity/category bounds, record count bounds, safe summaries, correlation binding, duplicate/concurrent writes, and no raw payload leakage.
- WPF navigation, rendering, operator read-only state, administrator mutation state, malformed response handling, shutdown cancellation, and absence of restore/service-mutation controls.
- Architecture boundaries: Application has no Contracts dependency/import; Domain has no Contracts; WPF has no Agent/Infrastructure/SQL/HTTP/process/PowerShell/Named Pipe/filesystem reader/service controller/EventLog reference beyond typed Local IPC and native output dialogs where already approved.

## Validation and delivery

Run focused tests first, then every POS test project, strict Testing-origin Release build with `--warnaserror`, `.scripts\test-powershell-quality.ps1`, `Invoke-Pester -Path .\scripts\tests -PassThru`, `python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`, `git diff --check`, and prohibited-reference/security searches. Report actual counts and distinguish unavailable live dependencies.

Update only affected tracked Markdown and Azure evidence. Review the final diff, commit, push without force, create a Draft PR with `AB#<accepted-child-id>`, wait for exact-head POS and Support Hub CI, and leave the final current-head WPF process running after authorized runtime verification. Do not merge or mark the PR ready.

HARD STOP - DO NOT EXECUTE WPF-07 UNTIL GPT-5.6 SOL REVIEWS AND ACCEPTS WPF-06.
DO NOT START A DIFFERENT WPF SLICE.
DO NOT IMPLEMENT DATABASE RESTORE IN WPF-07.
