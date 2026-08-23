# Current Project State

- **Updated:** 2026-08-23
- **Repository baseline:** WPF-04 started from accepted WPF-03 main
  `803dc85c60bc3662f78b041c8c99499195656e08`.
- **Working branch:** `feat/wpf-04-database-health-diagnostics`.
- **Implementation commit:** `fca899d` (`feat: add RMS database health to WPF`).
- **Status:** WPF-04 is implemented and locally validated. The branch remains
  Draft/pending Sol review and merge; WPF-05 has not started.
- **Authority:** CR-001 and ADR-0029 remain accepted; GPT-5.6 Sol is the
  acceptance authority.

## WPF-04 durable facts

- `DatabaseHealthQueryHandler` is the shared transport-independent read seam.
  It authorizes LocalWpf LocalOperator/LocalAdministrator callers, invokes the
  existing `IRmsDatabaseDiagnostics` for the fixed Branch/Cashier set in
  parallel, bounds cancellation, validates the projection, and maps all
  failures to fixed safe copies. Polling is non-audited.
- The typed `rms.databases.health` Local IPC operation accepts no payload and
  returns only the bounded snapshot DTO. The existing Local IPC framing,
  request/correlation matching, protocol v1, size/time bounds, Windows caller
  identity, server identity/PID verification, ACL, and impersonation boundary
  are unchanged.
- The legacy RMS diagnostics composition now consumes the same shared database
  projection seam while preserving its existing HTTP contract and backup
  metadata. No second SQL adapter, parser, config reader, or probe was added.
- WPF owns only typed IPC models/adapters. The Database workspace and Dashboard
  summary share the existing single-flight/cancel-aware refresh lifecycle and
  30-second timer. Agent unavailability remains distinct from
  `database_health_unavailable`, timeout, protocol, security, and invalid
  response states.
- Canonical statuses remain `NotConfigured`, `ConfigurationInvalid`,
  `DatabaseNameMismatch`, `Reachable`, `AuthenticationFailed`,
  `DatabaseUnavailable`, and `Unreachable`. The explicit aggregate rule is:
  both reachable = Healthy; all three definitive unavailable statuses =
  Unavailable; otherwise a complete two-row result = Degraded.
- WPF has no `SqlConnection`, connection-string/config/registry access, HTTPS
  path, Agent/Infrastructure reference, arbitrary SQL, backup/restore,
  service mutation, or provisioning surface. Contradictory DTOs and unsafe
  display/detail values fail closed.

## Validation evidence

- Strict Release solution build after restore: 0 warnings, 0 errors, using only
  the Testing origin environment variable
  `https://localhost:4443`.
- Final POS Release tests: Domain 12/12, Application 110/110,
  Infrastructure 155/155, Agent Integration 237/237, WPF 48/48;
  562/562 total.
- PowerShell quality: 37/37 tracked files parse cleanly. Pester: 172/172
  passed, 0 failed, 0 skipped, 0 pending.
- Final Release WPF process: PID 22068, exact Release executable, title
  `RMS Support Hub`, alive and responsive after a second probe. The process
  required only process-local `WINDIR=C:\WINDOWS` for font initialization.
  Computer Use failed its native-pipe retry, so screenshot/navigation/Refresh
  click evidence is intentionally not claimed.
- Current machine has no visible `RmsSupportAgent` service and no `RMS
  Support Operators` local group. No prerequisite or security mode was
  provisioned.

## Azure and backlog

- Live read before reconciliation: #13018 Active/P1; #13031 Closed/P1;
  #13032 Active/P1; #13033 New/P1; #13034 New/P2; #13035 New/P1.
- #13033 is the active WPF-04 story and is to be moved to Active/P1 with
  implementation evidence; it must not be closed before Sol acceptance and
  Draft PR merge. #13032 remains Active/P1 because service mutation is
  deferred. #13035 remains P1, #13034 remains P2, and #13072-#13076 remain
  unchanged in the preserved Online Order backlog.
- OPUS-14 and OPUS-16 remain deferred.

`.ai/HANDOFF.md` remains `Empty`; no incomplete implementation handoff is
needed.
