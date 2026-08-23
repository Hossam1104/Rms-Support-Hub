# Current Project State

- **Updated:** 2026-08-23
- **Repository baseline:** `main` was verified clean at
  `e2da601cfad42a324fa49aa68ad14c3008a42605` before WPF-03 work.
- **Working branch:** `feat/wpf-03-local-rms-service-health`.
- **Status:** WPF-03 read-only implementation is committed at `e00447b` and
  delivered in Draft PR #34; GPT-5.6 Sol review remains pending.
- **Authority:** CR-001 and ADR-0029 remain accepted; GPT-5.6 Sol is the
  acceptance authority. WPF-04 is explicitly blocked until that review.

## WPF-03 durable facts

- `ServiceHealthCatalog` owns the fixed RMS identities from `RmsServiceCatalog`
  plus the permanent `RmsSupportAgent` identity. No caller-provided service
  string enters the query.
- `ServiceHealthReader` is the bounded Application read seam. It maps Windows
  service states to `Running`, `Stopped`, `Paused`, `Transitioning`,
  `NotFound`, and `Unknown`, and maps overall health to `Healthy`, `Degraded`,
  or `Unknown`. Lookup timeout is bounded to five seconds by default.
- `ServiceHealthQueryHandler` reuses the accepted fail-closed
  `AgentOperationAuthorization` matrix. LocalWpf operators/administrators
  may query; missing, unauthenticated, invalid, RemoteHub, and AgentInternal
  authorities remain denied. Polling is intentionally non-audited.
- Legacy `/api/v1/services` and Local IPC `rms.services.health` use the same
  typed Application handler/reader; the legacy adapter preserves its existing
  three RMS-row contract. Local IPC returns a separate four-row typed health
  projection including the Agent Windows service identity.
- WPF owns no Windows service implementation. `LocalAgentServiceHealthClient`
  calls `LocalIpcClient.GetServiceHealthAsync`; the Services workspace and
  Dashboard summary share one coordinated `DashboardViewModel` refresh,
  single-flight gate, cancellation source, and 30-second timer.
- WPF remains a WinExe referencing only `RmsSupportHub.Pos.LocalIpc`. Structural
  tests reject WPF ServiceController/SCM/PowerShell/process/HTTPS access.
- Service start/stop/restart and all other mutation controls were not added.
  #13032 remains Active/P1 for this reason and its implementation is in Draft
  PR #34; #13033 remains the next candidate after acceptance.

## Validation evidence

- Strict Release build after restore: 0 warnings, 0 errors, using only the
  Testing origin environment variable `https://localhost:4443`.
- POS Release tests: Domain 12/12, Application 98/98, Infrastructure 155/155,
  Agent Integration 235/235, WPF 25/25; 525/525 total.
- PowerShell quality: 37/37 tracked files parse cleanly. Pester 3.4.0: 8/8
  passed. Memory, context, and `git diff --check` passed.
- Current machine still has no visible `RmsSupportAgent` service or
  `RMS Support Operators` local group. No prerequisite was provisioned and no
  security mode was weakened. Final Release runtime evidence belongs in the
  delivery handoff; visual screenshot/Refresh-click evidence is unavailable
  when Computer Use is unavailable.

## Azure and backlog

- #13018 Active/P1; #13031 Closed/P1; #13032 Active/P1; #13033 New/P1.
- #13072-#13076 remain preserved and unchanged. No Online Order work was
  implemented or reprioritized.
- OPUS-14 and OPUS-16 remain deferred.

`.ai/HANDOFF.md` is `Empty` until a genuinely incomplete or blocked session
requires a delta handoff.
