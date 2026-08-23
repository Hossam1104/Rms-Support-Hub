# WPF-03 - Local RMS / Windows Service Health

MODEL: Implementation and validation executor
AUTHORITY: GPT-5.6 Sol remains Planner, Architect, and Acceptance Authority
PROGRAMME: POS Dual Control-Surface Architecture (CR-001 / ADR-0029)
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/wpf-03-local-rms-service-health`
EPIC: E17 - WPF Standalone Local Operations (#13018)
PRIMARY STORY: US-E17-02 - Agent/RMS service health and approved service control (#13032)
STATUS: Implemented read-only health slice; Draft PR delivery and GPT-5.6 Sol review pending

## WPF-03 completed implementation

WPF-03 adds a server-owned fixed RMS/Agent service-health projection. The
existing Windows service manager remains the only native inspection adapter;
the shared Application reader/handler maps it to bounded states and safe
codes. The legacy `/api/v1/services` response keeps its existing three RMS
rows while using the same reader/handler path as Local IPC.

The typed Local IPC operation is `rms.services.health`. It accepts no service
name or display-name input, preserves protocol v1 framing, request/correlation
matching, message bounds, timeouts, server identity verification, caller
Windows identity, and the existing LocalWpf operator/administrator authority
matrix. Health polling is non-audited; service mutation remains future work.

The WPF Services workspace is functional and uses only the typed Local IPC
client. It shows fixed catalog rows, installation/runtime state, overall
Healthy/Degraded/Unknown health, bounded transport failures, a coordinated
single-flight Refresh, and the existing 30-second refresh lifecycle. The
Dashboard also shows the same service snapshot summary. No WPF service,
HTTPS, SCM, registry, filesystem, or process access was added.

## WPF-03 validation evidence

- `dotnet restore pos/RmsSupportHub.Pos.slnx`: passed.
- Strict Release solution build with Testing-only
  `PosAgentSecurity__SupportHubOrigin=https://localhost:4443`: 0 warnings,
  0 errors.
- Full POS Release tests: Domain 12/12, Application 98/98, Infrastructure
  155/155, Agent Integration 235/235, WPF 25/25; 525/525 total.
- Focused service-health/application, Local IPC/Agent, and WPF suites passed
  at the same counts above for their affected projects.
- PowerShell quality gate: 37/37 tracked PowerShell files parse cleanly.
- Pester 3.4.0: 8/8 passed, 0 failed, 0 skipped, 0 pending.
- `python .ai/scripts/check_memory.py`, `python .ai/scripts/context.py`, and
  `git diff --check`: passed before delivery documentation synchronization.

## Azure reconciliation

- #13018 remains Active/P1.
- #13031 remains Closed/P1 with WPF-02 merge evidence.
- #13032 remains Active/P1; read-only WPF-03 health is implemented and
  start/stop/restart/service mutation is intentionally deferred.
- #13033 remains New/P1.
- #13072-#13076 remain unchanged in the preserved Online Order backlog.

## Delivery gate

The implementation commit is `e00447b` (`feat: add local RMS service health
to WPF`). Create and keep the pull request Draft; do not merge or mark it
ready. Do not contact Production, provision the operator group, install or
mutate a Windows service, or mutate native RMS/database state.

## Next bounded executable prompt: WPF-04 - Database Health & Diagnostics

Primary Azure story: #13033 - US-E17-03.

Scope the next slice to a read-only, Agent-owned database-health projection
through shared Application logic, typed Local IPC, and a WPF workspace. Reuse
the repository SQL/database contracts; do not accept caller-selected server,
database, connection string, credentials, table names, or arbitrary SQL.
Preserve the WPF-03 single-flight/cancellation lifecycle, typed protocol
bounds, server identity verification, LocalWpf authorization, safe error
mapping, and the WPF token/theme system. Add only deterministic health probes
and tests for healthy, degraded, unavailable, timeout, malformed, protocol,
security, and shutdown outcomes.

Out of scope: database backup/restore, cleanup/reset, arbitrary diagnostics,
logs, Support Bundle, packages, repair, remote Hub, SignalR, fleet/device
supervision, Production, native RMS mutation, service mutation, installer,
operator-group provisioning, and Online Order work. OPUS-14 rate limiting and
OPUS-16 representative-machine/operator-group E2E remain deferred.

HARD STOP - DO NOT EXECUTE WPF-04 until GPT-5.6 Sol reviews and accepts
WPF-03. Do not start WPF-04 during this delivery.
