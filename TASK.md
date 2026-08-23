# WPF-04 - Database Health & Diagnostics

MODEL: Implementation and validation executor
AUTHORITY: GPT-5.6 Sol remains Planner, Architect, and Acceptance Authority
PROGRAMME: POS Dual Control-Surface Architecture (CR-001 / ADR-0029)
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/wpf-04-database-health-diagnostics`
EPIC: E17 - WPF Standalone Local Operations (#13018)
PRIMARY STORY: US-E17-03 - Database health and diagnostics (#13033)
STATUS: Implemented locally in `fca899d`; Draft PR and Sol acceptance remain pending

## WPF-03 acceptance baseline

WPF-03 was accepted, squash merged, and synced to `main` at
`803dc85c60bc3662f78b041c8c99499195656e08` (merged PR #34). The former
WPF-03 Draft/hard-stop wording is obsolete and is superseded by this WPF-04
task.

## WPF-04 implementation truth

WPF-04 adds a read-only, Agent-owned health projection for the fixed Branch
and Cashier RMS databases. The flow is:

`WPF -> typed Local IPC -> shared Application handler -> existing IRmsDatabaseDiagnostics`

The `rms.databases.health` operation accepts no payload. The Agent owns the
fixed database set, canonical names, caller identity, authorization, timeout,
safe status mapping, and response validation. WPF uses only the typed
`LocalIpcClient.GetDatabaseHealthAsync` adapter; it does not read RMS files or
registry, open SQL connections, parse connection strings, or call HTTPS.

The workspace preserves `NotConfigured`, `ConfigurationInvalid`,
`DatabaseNameMismatch`, `Reachable`, `AuthenticationFailed`,
`DatabaseUnavailable`, and `Unreachable`. Overall state is deterministic:
both reachable is `Healthy`, all definitively unavailable statuses is
`Unavailable`, and all other complete two-database results are `Degraded`.
Incomplete or contradictory transport data fails closed as a safe error.

The Dashboard and Database workspace share one coordinated refresh snapshot.
Agent availability is kept separate from database availability. There are no
backup, restore, cleanup, reset, repair, schema, arbitrary SQL, service
mutation, logs, Support Bundle, package, or provisioning controls.

## Validation evidence

- `dotnet restore pos/RmsSupportHub.Pos.slnx`: passed.
- Strict Release solution build with the Testing-only
  `PosAgentSecurity__SupportHubOrigin=https://localhost:4443`: 0 warnings,
  0 errors.
- Final POS Release tests: Domain 12/12, Application 110/110,
  Infrastructure 155/155, Agent Integration 237/237, WPF 48/48;
  562/562 total.
- PowerShell quality: 37/37 tracked files parse cleanly.
- Pester: 172/172 passed, 0 failed, 0 skipped, 0 pending.
- `git diff --check`: passed. Memory/context checks are run after the final
  documentation update.

## Runtime evidence

The final Release executable was launched from the exact workspace path:

`pos\src\RmsSupportHub.Pos.Desktop.Wpf\bin\Release\net10.0-windows10.0.19041.0\RmsSupportHub.Pos.Desktop.Wpf.exe`

PID 18836 remained alive and responsive with window title `RMS Support Hub`
after a second probe. The executor required process-local `WINDIR=C:\WINDOWS`
for WPF font initialization; this is not a product setting. Computer Use was
unavailable after its required retry, so Database navigation visibility,
Dashboard summary visibility, and an actual Refresh click are not claimed.
The machine was not provisioned with an Agent service or operator group.

## Azure reconciliation

Live Azure read before reconciliation found #13018 Active/P1, #13031 Closed/P1,
#13032 Active/P1, #13033 New/P1, #13034 New/P2, #13035 New/P1, and #13072-
#13076 unchanged in New state. After implementation, #13033 was reconciled to
Active/P1 with the WPF-04 evidence note and must not be closed before Sol
acceptance and the Draft PR merge. Keep #13032 Active/P1, #13035 P1, #13034
P2, and #13072-#13076 unchanged.

## Next bounded executable prompt: WPF-05 - Logs & Safe Support Bundle

Primary Azure story: #13035 - Logs and safe Support Bundle.

Objective: add a read-only, Agent-owned local support view and bounded safe
support-bundle projection through shared Application logic, typed Local IPC,
and WPF. Reuse existing redaction, fixed-root, opaque-artifact, authorization,
audit, size, retention, and cancellation boundaries. Prove that raw logs,
credentials, connection strings, arbitrary paths, stack traces, customer data,
and caller-selected files never reach WPF or the bundle.

Required follow-up coverage includes authorization, safe redaction, fixed
roots, bounded size/time, malformed and unavailable Agent responses,
correlation, retry, shutdown cancellation, and no direct filesystem/process/
HTTPS access from WPF. Keep support-bundle generation read-only from the WPF
perspective and separately gated from database/service mutation.

Out of scope: database backup/restore, arbitrary SQL, schema browsing, service
control, cleanup/reset, branch reset, packages, repair, remote Hub, SignalR,
fleet/device supervision, Production, native RMS mutation, provisioning,
installer, auto-update, and Online Order work. OPUS-14 durable-audit rate
limiting and OPUS-16 representative Testing-machine/operator-group E2E remain
deferred.

HARD STOP - DO NOT EXECUTE WPF-05 until GPT-5.6 Sol reviews and accepts WPF-04.
Do not start WPF-05 during this delivery.

## Delivery guardrails

Keep the PR Draft; do not merge or mark it ready. Do not contact Production,
provision the operator group, install or mutate a Windows service, or mutate
native RMS/database state. Preserve the WPF-01 through WPF-03 Named Pipe
trust boundary and the Online Order backlog.
