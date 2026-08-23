# Current Project State

- **Updated:** 2026-08-24
- **Repository baseline:** WPF-06 started from accepted WPF-05 at
  `ee2d62c030a2266ed410f91604ce8524df61e50b`, merged through PR #36 as
  `e0c82cdefaac47c1ab9d0649d249cbf85f4d8837`.
- **Working branch:** `feat/wpf-06-database-backup-artifact-delivery`.
- **Status:** WPF-06 is implemented in a Draft PR candidate. It adds fixed
  Branch/Cashier backup creation, principal-scoped bounded inventory, and one
  shared Agent-owned local artifact-delivery path for database backups and
  Support Bundles. Restore is deliberately not implemented.
- **Authority:** CR-001, ADR-0029, and ADR-0030 are accepted; GPT-5.6 Sol is
  the acceptance authority. The WPF-06 PR must remain Draft and must not be
  marked ready or merged by this execution.

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
  validation, pre-write audit, per-destination conflict control, bounded
  streaming, temporary-file cleanup, and atomic finalization.
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
- Release POS tests: Domain 12/12, Application 122/122, Infrastructure
  156/156, Agent Integration 261/261, WPF 62/62; aggregate 613/613.
- Focused WPF-06 coverage includes Application authorization/inventory,
  Agent backup runtime and shared artifact delivery, WPF workspace behavior,
  architecture boundaries, principal isolation, cancellation, audit failure,
  expiry/checksum states, unsafe destinations, overwrite confirmation, and
  same-destination conflict control.
- PowerShell quality: 37/37 files parsed with no dangling continuations.
  Pester: 172 passed, 0 failed, 0 skipped, 0 pending.
- `python .ai/scripts/context.py` and `python .ai/scripts/check_memory.py`
  passed. `git diff --check` passed; line-ending normalization warnings are
  Git working-copy warnings only.

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

## Runtime and environment boundary

- Final runtime verification must launch the Release WPF executable from the
  current committed head and leave that process running. Record its exact path,
  PID, title, responding state, and start time here before completion.
- Preserve healthy project-owned API/Angular processes. Probe only actual
  responding endpoints; do not infer URLs from configuration alone.
- The current machine has no authorized Agent service/operator-group/database
  mutation session. No live backup, restore, service mutation, Production
  contact, machine provisioning, fleet/remote work, or customer-data mutation
  is claimed.
- Computer Use visual evidence is not claimed unless the native pipe is
  available during final verification.

`.ai/HANDOFF.md` remains `Empty` while this execution completes.
