# Current Project State

- **Updated:** 2026-08-22
- **Repository baseline:** `main` was verified clean at `bd83e3b2c223e807f40e684fe61a5281c915674b` before implementation.
- **Working branch:** `feat/wpf-02-wpf-shell-local-health`; Draft PR pending delivery.
- **Architecture authority:** CR-001 and ADR-0029 were accepted and merged by
  architecture PR #31. GPT-5.6 Sol remains the acceptance authority.
- **Status:** WPF-01 is accepted and merged at `c09e4ec`; WPF-02 implementation and runtime verification are complete on the feature branch and await Sol review/acceptance. WPF-03 must not start until that acceptance.

## WPF-02 durable facts
- `RmsSupportHub.Pos.Desktop.Wpf` is a native WPF `WinExe` targeting
  `net10.0-windows10.0.19041.0` and references only `RmsSupportHub.Pos.LocalIpc`.
  It is included in `pos/RmsSupportHub.Pos.slnx` beside, and without changes
  to, `PosAdminTool.WinUI`.
- The shell opens before IPC work completes and provides token-owned dark
  graphite surfaces, a cyan action accent, a header Agent indicator, Dashboard
  navigation, bounded future-work placeholders, health cards, safe metadata,
  and a keyboard-reachable Refresh action.
- `LocalAgentHealthClient` wraps the existing typed
  `LocalIpcClient.GetHealthAsync` call. It maps unavailable, timeout, malformed
  response, protocol mismatch, security verification, and unknown failures to
  safe fixed UI values. No WPF named-pipe, HTTPS, service, database, or RMS
  implementation exists.
- `DashboardViewModel` owns explicit health states, single-flight refresh,
  cancellation, a single bounded 30-second `PeriodicTimer`, shutdown disposal,
  last-success tracking, and transport-focused display properties.
- `LocalIpcClient` now exposes a typed protocol-version mismatch exception after
  validating the response envelope; accepted server identity, ACL, PID, SCM,
  impersonation, bounds, correlation, and authorization controls are otherwise
  unchanged.
- The current development machine has no visible `RmsSupportAgent` service and
  no `RMS Support Operators` local group. IPC remained disabled/unavailable;
  no prerequisite was provisioned and no security mode was weakened.

## WPF-01 durable facts
- The existing `RmsSupportHub.Pos.Application` project now owns a transport-
  agnostic `InvocationContext`, fail-closed operation authorization, and the
  shared `RmsInstallationDiscoveryQueryHandler`.
- The legacy `/api/v1/rms/diagnostics` path still composes the same dashboard,
  while `/api/v1/rms/installation` is a typed HTTPS adapter over the shared
  discovery handler.
- `RmsSupportHub.Pos.LocalIpc` provides protocol version 1, newline-delimited
  JSON envelopes, typed health and installation-discovery calls, strict
  request/correlation matching, bounded request/response sizes, timeouts, and
  client concurrency.
- The Agent Named Pipe listener is disabled by default. When enabled it uses an
  explicit ACL for LocalSystem and Built-in Administrators FullControl, and
  explicit duplex-client rights (`ReadData`, `WriteData`, attributes,
  `ReadPermissions`, and `Synchronize`) for the configured `RMS Support
  Operators` group. An explicit NETWORK deny establishes the local-only pipe
  boundary; no broad-principal allow or operator security/ownership/server-
  instance rights exist. Missing group resolution produces an unavailable/no-
  listener state; no broad-principal fallback exists.
- The only initial IPC operations are `agent.health` and
  `rms.installation.discovery`. Client payloads do not provide identity or
  privilege authority. No WPF UI, SignalR, Production configuration, native
  RMS service, or customer database was changed.
- `LocalIpcClient` verifies the connected Named Pipe server by matching
  `GetNamedPipeServerProcessId` to the currently running PID returned by
  read-only SCM `QueryServiceStatusEx` for the immutable
  `AgentServiceIdentity.PermanentServiceName` (`RmsSupportAgent`) before it
  writes a request. The SCM resolver requests only `SC_MANAGER_CONNECT` and
  `SERVICE_QUERY_STATUS`; no process-token, `OpenProcessToken`, or
  `SeDebugPrivilege` path remains. Pipe PID and service PID resolution are
  injected behind small bounded interfaces for deterministic tests and future
  service-account migration. Local group resolution machine-qualifies
  unqualified names and rejects domain/foreign authorities.
- Shared authorization now binds source to authority: LegacyLoopbackHttp is
  local-admin only, LocalWpf supports local operator/admin according to risk,
  RemoteHub and AgentInternal fail closed in WPF-01, and unknown combinations
  are denied. Diagnostics, Support Bundle evidence, and Safety Snapshot
  evidence receive the real invocation context; no synthetic admin overload
  remains.
- Durable audit writes return a persistence result. Installation discovery
  returns `audit_unavailable` when its mandatory audit record is not durable;
  `agent.health` remains non-audited to avoid high-frequency audit spam.
- The final bounded remediation closes OPUS-01 through OPUS-13 and OPUS-15:
  exact `OpenSCManagerW`/`OpenServiceW` imports and real SCM identity tests;
  capped cancellation-aware listener recovery; server-owned semaphore and pipe
  instance lifetime with safe shutdown; effective correlation fallback; a real
  Local WPF authority classifier with fail-closed identity/role errors;
  production ACL/PID verification without process-token APIs; local group
  account-type and broad-SID rejection; Domain/Contracts decoupling; typed
  `audit_unavailable` 503 responses for audited diagnostics, bundles, and
  snapshots; first-instance namespace ownership; Identification impersonation;
  orphan interface removal; and exact max/max+1 newline bounds.
- OPUS-14 rate limiting and OPUS-16 representative-machine/operator-group E2E
  are explicitly deferred. WPF-02 remains blocked on Sol acceptance.
- The mandated stable `System.IO.Pipes.AccessControl` 5.0.0 attempt exposed
  NU1510 because the API is already provided by the .NET 10 BCL; the explicit
  reference is removed so strict CI (`--warnaserror`) stays clean. No preview
  remains.

## Validation evidence
- Release solution build: 0 warnings, 0 errors, with the required Testing-only
  `PosAgentSecurity__SupportHubOrigin` environment variable.
- POS Release tests: Domain 12/12, Application 89/89, Infrastructure 155/155,
  Agent Integration 234/234 (490/490 total).
- Focused bounded remediation tests remain green: lifecycle 6/6,
  audit/authority/architecture 25/25, and exact protocol bounds 2/2.
- PowerShell quality: 37 tracked files parse cleanly; PSScriptAnalyzer was not
  installed. Pester 3.4.0: 172 passed, 0 failed, 0 skipped, 0 pending.
- TestServer HTTPS and in-process Windows Named Pipe integration exercised the
  selected diagnostic, health, invalid-operation, malformed/oversized-request,
  unauthorized-connection, missing-group, and HTTP/IPC parity paths.
- The checked-in POS OpenAPI document remains content-identical after the
  validation build; frontend/OpenAPI sources were not changed by WPF-02.
- Standalone Agent startup was not attempted because the real Kestrel listener
  requires the machine-owned Testing certificate. No runtime URL is claimed
  from configuration alone.

## Safety and next work
- Production readiness remains **NO**. No Production contact or native RMS
  mutation was authorized or performed.
- Azure mapping remains E16 #13017, US-E16-02 #13022, US-E16-04 #13024, with
  related authorization story US-E16-03 #13023. No broad Azure administration
  was performed during WPF-01.
- `.ai/HANDOFF.md` is `Empty` because implementation and runtime verification
  are complete and the next action is review, not unfinished coding.
