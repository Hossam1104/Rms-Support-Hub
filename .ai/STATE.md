# Current Project State

- **Updated:** 2026-08-22
- **Repository baseline:** `main` was verified clean at
  `bd83e3b2c223e807f40e684fe61a5281c915674b` before implementation.
- **Working branch:** `feat/wpf-01-shared-agent-local-ipc`.
- **Architecture authority:** CR-001 and ADR-0029 were accepted and merged by
  architecture PR #31. GPT-5.6 Sol remains the acceptance authority.
- **Status:** WPF-01 implementation and validation are complete; the branch is
  awaiting Sol review. WPF-02 must not start until that acceptance.

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
  explicit ACL for LocalSystem, Built-in Administrators, and the configured
  `RMS Support Operators` group. Missing group resolution produces an
  unavailable/no-listener state; no broad-principal fallback exists.
- The only initial IPC operations are `agent.health` and
  `rms.installation.discovery`. Client payloads do not provide identity or
  privilege authority. No WPF UI, SignalR, Production configuration, native
  RMS service, or customer database was changed.
- The transport-only ACL package is pinned to the available
  `System.IO.Pipes.AccessControl` `6.0.0-preview.5.21301.5` asset because the
  current feed did not provide a compatible stable/.NET 10 asset. This remains
  a dependency-review item before production packaging.

## Validation evidence

- Release solution build: 0 warnings, 0 errors, with the required Testing-only
  `PosAgentSecurity__SupportHubOrigin` environment variable.
- POS Release tests: Domain 12/12, Application 87/87, Infrastructure 155/155,
  Agent Integration 179/179.
- Focused IPC foundation tests: 8/8. Focused application seam tests are
  included in the Application total; the WPF-01 additions contribute 5 tests.
- PowerShell quality: 37 tracked files parse cleanly; PSScriptAnalyzer was not
  installed. Pester 3.4.0: 172 passed, 0 failed, 0 skipped, 0 pending.
- TestServer HTTPS and in-process Windows Named Pipe integration exercised the
  selected diagnostic, health, invalid-operation, malformed/oversized-request,
  unauthorized-connection, missing-group, and HTTP/IPC parity paths.
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
