# Current Project State

- **Updated:** 2026-08-24
- **Repository baseline:** WPF-06 started from accepted WPF-05 at
  `ee2d62c030a2266ed410f91604ce8524df61e50b`, merged through PR #36 as
  `e0c82cdefaac47c1ab9d0649d249cbf85f4d8837`.
- **Working branch:** `feat/wpf-06-database-backup-artifact-delivery`.
- **Status:** WPF-06 implementation and final security/correctness remediation
  are present in the Draft PR candidate. It adds fixed Branch/Cashier backup
  creation, principal-scoped bounded inventory, and one shared Agent-owned
  local artifact-delivery path for database backups and Support Bundles.
  Restore is deliberately not implemented and formal Sol acceptance is still
  pending. Delivered in commit `f548477265d3718a3e568fa62768b0dbb471a0d2`;
  PR #37 remains open and Draft.
- **Authority:** CR-001 and ADR-0029 remain accepted; ADR-0030 is Proposed
  pending GPT-5.6 Sol acceptance. Sol is the acceptance authority. The WPF-06
  PR must remain Draft and must not be marked ready or merged by this
  execution.

## WPF-06 durable facts

- `RmsDatabaseBackupQueryHandler` is transport-neutral and exposes only fixed
  Branch/Cashier metadata. It bounds inventory, orders newest-first, scopes
  reads by the authenticated principal, and never projects server paths.
- `LocalRmsDatabaseBackupRuntime` reuses the existing typed RMS backup
  workflow and machine-wide concurrency gate. It derives authority and
  principal from the trusted invocation context, audits once, revokes an
  artifact when audit fails, and supports cancellation without claiming an
  ambiguous success.
- `ArtifactDeliveryService` is the shared local export seam for database
  backups and Support Bundles. It enforces administrator-only local authority,
  principal binding, fixed extension/output roots, expiry/checksum/size
  validation, pre-write and final outcome audit, per-destination bounded
  conflict control, bounded streaming, temporary-file cleanup, and atomic
  finalization with overwrite rollback compensation when final audit is
  unavailable.
- Production destination roots are resolved from the authenticated caller SID
  through the machine-owned Windows ProfileList mapping to fixed Desktop,
  Documents, and Downloads directories. The Agent does not use LocalSystem's
  interactive-folder environment. Source reads remain Agent-owned and only
  destination-side export operations run under the caller token through the
  dedicated export authority.
- RMS backup catalog access is principal-bound in every transport. The explicit
  legacy compatibility mode permits exact-owner records plus historical
  null-owner records only; null is never unrestricted. Retention is scoped by
  database and owner principal. Artifact requests require Branch/Cashier +
  `.bak` or Support Bundle + null target + `.zip`.
- WPF owns only typed Local IPC adapters, bounded response validation, safe
  inventory rendering, native Save As destination selection, overwrite
  confirmation, and cancellation. It has no Agent/Infrastructure/SQL/HTTP/
  process/PowerShell/Named Pipe/filesystem-reader dependency.
- Local operators can inspect backup inventory but cannot create or export.
  Local administrators can create backups and export approved artifacts.
  RemoteHub and unauthenticated callers are denied. No restore control or
  restore implementation was added.
- WPF-05 intentionally exposes bounded redacted stack-frame labels, not raw or
  unbounded traces. No universal customer-data/PII-free guarantee is claimed;
  Task #13116 remains the privacy-policy task.

## Validation evidence

- `dotnet restore pos/RmsSupportHub.Pos.slnx`: passed; all projects up to date.
- Strict Testing-origin Release solution build with `--warnaserror`: passed,
  0 warnings and 0 errors.
- Release POS tests: aggregate 622/622 passed with no failures or skips.
- Focused WPF-06 coverage includes Application authorization/inventory,
  Agent backup runtime and shared artifact delivery, WPF workspace behavior,
  architecture boundaries, principal isolation, cancellation, audit failure,
  expiry/checksum states, unsafe destinations, overwrite confirmation, and
  same-destination conflict control.
- PowerShell quality: 37/37 files parsed with no dangling continuations.
  Pester: 172 passed, 0 failed, 0 skipped, 0 pending.
- Repository build gate: backend 342/342, backend Release build 0/0, and
  Angular production build passed. `python .ai/scripts/context.py` and
  `python .ai/scripts/check_memory.py` passed. `git diff --check` passed;
  line-ending normalization warnings are Git working-copy warnings only.
- Exact-head GitHub CI for final head `53233d4` passed all seven checks.
  An earlier hosted run exposed a transient ACL-fixture failure; its rerun
  passed the POS Infrastructure job 156/156 and the complete workflow is
  green.

## Azure and backlog

- Live state: E16 #13017 Active/P2; E17 #13018 Active/P1; E18 #13019 New/P2;
  E19 #13020 New/P2.
- WPF children: #13022/#13024/#13031/#13033/#13035 Closed/P1;
  #13023/#13029/#13030 Active/P2; #13032/#13034 Active/P1; #13116 New/P2.
  Remaining WPF local/future children retain their live New priorities.
- #13072-#13076 remain New in the Online Order integrated-testing backlog;
  #12900-#12902 remain New/P3 conditional and #12949 remains New/P3 deferred
  Production acceptance.
- #13035 records WPF-05 acceptance, PR #36, and its merge. #13034 records
  WPF-06 as the active story while guarded restore remains separately gated.
  Azure child #13129 (`Implement guarded RMS database restore in WPF`) is
  New/P2 under #13034. #13032 remains Active/P1 as the next recommended
  guarded service-control slice; WPF-07 is not started.

## Runtime and environment boundary

- Final WPF runtime verification is complete for PR #37 commit `f548477` at
  `pos/src/RmsSupportHub.Pos.Desktop.Wpf/bin/Release/net10.0-windows10.0.19041.0/RmsSupportHub.Pos.Desktop.Wpf.exe`:
  PID 44076, title `RMS Support Hub`, `Responding=True`, exactly one process,
  started `2026-08-24T12:23:48.0785680+03:00`, Session 2. It is left running.
- `scripts/dev.ps1` runtime probes returned API live 200/healthy, API ready
  200/ready with Testing tier, and Angular `http://localhost:4200/` 200 HTML.
  Current project-owned API PID is 41932 and Angular PID is 44228; both remain
  running. WPF was launched with process-local `WINDIR=C:\WINDOWS`.
- The current machine has no authorized Agent service/operator-group/database
  mutation session. No live backup, restore, service mutation, Production
  contact, machine provisioning, fleet/remote work, or customer-data mutation
  is claimed.
- No screenshot/click evidence is claimed because Computer Use visual access
  was unavailable; process/title/responding and endpoint evidence are factual.

`.ai/HANDOFF.md` remains `Empty` while this execution completes.
