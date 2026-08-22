# Current Project State

- **Updated:** 2026-08-22
- **Repository baseline:** `main` was verified clean at
  `bd83e3b2c223e807f40e684fe61a5281c915674b` before implementation.
- **Working branch:** `feat/wpf-01-shared-agent-local-ipc`; Draft PR #32.
- **Architecture authority:** CR-001 and ADR-0029 were accepted and merged by
  architecture PR #31. GPT-5.6 Sol remains the acceptance authority.
- **Status:** WPF-01 implementation and the bounded Sol security remediation
  are complete locally; Draft PR #32 remains awaiting Sol review. WPF-02 must
  not start until that acceptance.

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
- `LocalIpcClient` verifies the connected Named Pipe server PID token before it
  writes a request. The default expected identity is LocalSystem, and the
  verifier is injected behind a small interface for test seams and a future
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
- The mandated stable `System.IO.Pipes.AccessControl` 5.0.0 attempt exposed
  NU1510 because the API is already provided by the .NET 10 BCL; the explicit
  reference is removed so strict CI (`--warnaserror`) stays clean. No preview
  remains.

## Validation evidence

- Release solution build: 0 warnings, 0 errors, with the required Testing-only
  `PosAgentSecurity__SupportHubOrigin` environment variable.
- POS Release tests: Domain 12/12, Application 89/89, Infrastructure 155/155,
  Agent Integration 187/187.
- Focused remediation tests: 25/25 for ACL, NETWORK deny, local group
  resolution, server identity, correlation, bounded protocol, source policy,
  audit failure, HTTPS/IPC parity, and context-overload coverage.
- PowerShell quality: 37 tracked files parse cleanly; PSScriptAnalyzer was not
  installed. Pester 3.4.0: 172 passed, 0 failed, 0 skipped, 0 pending.
- TestServer HTTPS and in-process Windows Named Pipe integration exercised the
  selected diagnostic, health, invalid-operation, malformed/oversized-request,
  unauthorized-connection, missing-group, and HTTP/IPC parity paths.
- The final PR validation run passed: POS portable, POS Windows
  build/infrastructure, Windows Agent security, POS OpenAPI/Angular
  generation, POS PowerShell, retained WinUI publish, and Support Hub
  backend/frontend/release candidate.
- Standalone Agent startup was not attempted because the real Kestrel listener
  requires the machine-owned Testing certificate. No runtime URL is claimed
  from configuration alone.

## Safety and next work

- Production readiness remains **NO**. No Production contact or native RMS
  mutation was authorized or performed.
- Azure mapping remains E16 #13017, US-E16-02 #13022, US-E16-04 #13024, with
  related authorization story US-E16-03 #13023. No broad Azure administration
  was performed during WPF-01.
- `.ai/HANDOFF.md` is `Empty` because the implementation is complete and the
  next action is review, not unfinished coding.
