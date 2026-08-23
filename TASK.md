# WPF-02 - WPF Shell + Local Agent Health Experience

MODEL: Implementation and validation executor
AUTHORITY: GPT-5.6 Sol remains Planner, Architect, and Acceptance Authority
PROGRAMME: POS Dual Control-Surface Architecture (CR-001 / ADR-0029)
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/wpf-02-wpf-shell-local-health`
EPIC: E17 - WPF Standalone Local Operations (#13018)
PRIMARY STORY: US-E17-01 - WPF shell and local machine dashboard (#13031)
STATUS: Implemented; final process runtime verified with visual evidence limited; awaiting GPT-5.6 Sol review/acceptance
## WPF-02 completed implementation

WPF-02 added the native `RmsSupportHub.Pos.Desktop.Wpf` application beside the
legacy `PosAdminTool.WinUI` project. The shell targets
`net10.0-windows10.0.19041.0`, uses pure WPF/BCL resources, and references only
`RmsSupportHub.Pos.LocalIpc` for Agent communication.
The first window opens immediately with a stable shell, branded header,
keyboard-reachable navigation, bounded future-work placeholders, a Dashboard
health view, an Agent connection indicator, safe transport metadata, and a
Refresh/Retry action. Only Agent health is functional in this slice.
## Health boundary and behavior

- Production health calls wrap `LocalIpcClient.GetHealthAsync`; WPF does not
  create named pipes, parse envelopes, call Agent HTTPS, or access services,
  databases, RMS files, or privileged APIs directly.
- The UI state model explicitly represents Loading, Connected, Unavailable,
  TimedOut, ProtocolMismatch, InvalidResponse,
  SecurityVerificationFailed, UnknownError, and Cancelled.
- Local IPC protocol-version mismatches are surfaced through a typed
  `LocalIpcProtocolMismatchException`; no security boundary or authorization
  behavior was weakened.
- Health failures are mapped to fixed safe codes/details. Exception text,
  stack traces, SIDs, paths, security descriptors, and native error material
  never reach visible labels.
- Refresh is single-flight and cancellation-aware. One bounded 30-second
  `PeriodicTimer` lifecycle is created after startup health completes; it stops
  on window close and never creates a timer per retry/navigation.
- Last successful check, safe correlation ID, Agent status, IPC status,
  protocol version, and Hub-connectivity-required status are transport-focused
  display fields only.
## WPF-02 validation evidence

- Release WPF project build: 0 warnings, 0 errors.
- Full POS Release solution build with the Testing-only
  `PosAgentSecurity__SupportHubOrigin=https://localhost:4443` environment:
  0 warnings, 0 errors.
- WPF focused tests: 13/13 passed.
- Full POS tests: Domain 12/12, Application 89/89, Infrastructure 155/155,
  Agent Integration 234/234, WPF 13/13; 503/503 total.
- PowerShell quality gate: 37/37 tracked files parse cleanly; PSScriptAnalyzer
  was not installed.
- Pester 3.4.0: 172/172 passed, 0 failed, 0 skipped, 0 pending.
- `python .ai/scripts/check_memory.py`: passed.
- `git diff --check`: passed.
- The final Release WPF executable was launched in the interactive Windows
  session. The process remained alive and responsive and exposed the expected
  `RMS Support Hub` window title.
- Runtime state remained truthfully Agent unavailable/timed-out because Agent prerequisites were not provisioned.
- Computer Use was unavailable on the final pass because its native pipe was unavailable, so screenshot-based visual inspection and an actual Refresh-button click were not independently completed; visual clipping/overlap assertions and maximized/minimized inspection are also unverified.
- This is an evidence limitation, not an implementation/CI blocker.
- The current machine has no visible `RmsSupportAgent` service and no local
  `RMS Support Operators` group. IPC was not enabled or weakened; the truthful
  runtime result was Agent/IPC unavailable with a bounded timeout and Retry.
## Delivery gate

WPF-01 is accepted and merged at the current main baseline. WPF-02 remains a
Draft feature branch until GPT-5.6 Sol reviews and accepts this slice. Do not
merge or mark the PR ready. No Production contact, native RMS service
mutation, customer database access, operator-group provisioning, installer
  change, or WPF-03 work is authorized by this task.
## Next bounded executable prompt: WPF-03 - Local RMS / Service Health Dashboard

> HARD STOP - DO NOT EXECUTE WPF-03 until GPT-5.6 Sol reviews and accepts
> WPF-02.
Create the next narrow WPF dashboard slice on a new branch from the accepted
WPF-02 head. Keep the WPF shell native and use typed Agent-owned application
operations through `RmsSupportHub.Pos.LocalIpc` only. Extend the shared
transport/application/contracts seam only when a typed read-only contract is
needed; do not duplicate RMS or Windows service logic in WPF.
WPF-03 in scope:

1. Add a read-only local RMS/Windows-service health projection through the
   shared Agent application layer and typed Local IPC contract, with explicit
   protocol bounds, authorization, correlation, and safe error mapping.
2. Add the projection to the existing WPF Dashboard without changing the
   existing health state lifecycle or replacing the browser-direct HTTPS path.
3. Show truthful loading, healthy, degraded, unavailable, timeout, malformed,
   protocol-mismatch, and security-verification outcomes without raw exception,
   connection-string, SID, path, or service-control details.
4. Add deterministic tests for the shared contract/handler, Local IPC adapter,
   and WPF state transitions. Keep health operations non-audited where the
   accepted Agent policy requires it.
5. Preserve design tokens, keyboard access, bounded refresh cadence, shutdown
   cancellation, and the accepted WPF-01 ACL/PID/impersonation/protocol gates.
WPF-03 out of scope:

- Starting, stopping, restarting, installing, removing, or reconfiguring RMS
  or Agent services.
- Database backup/restore, cleanup/reset, package lifecycle, arbitrary
  diagnostics, Support Bundle, RMS installation discovery UI, Hub/SignalR,
  fleet supervision, certificates, installer work, Production contact, or
  customer database access.
- General rate limiting (OPUS-14) and representative-machine/operator-group
  validation (OPUS-16); both remain deferred follow-up work.
WPF-03 delivery requirements:

- Read `TASK.md`, `.ai/STATE.md`, run `python .ai/scripts/context.py`, and
  inspect only task-relevant sources before editing.
- Use a new feature branch; do not work on main, merge, or mark a PR ready.
- Run focused tests, the affected POS Release build/tests, PowerShell gate,
  Pester, memory checks, and `git diff --check`.
- Use only local Testing verification when machine-owned prerequisites are
  already present. Never provision security prerequisites or mutate Production
  or native RMS state.
- Update `.ai/STATE.md`, `.ai/HISTORY.md`, and this prompt with factual
  evidence; set `.ai/HANDOFF.md` to `Empty` only after completion.
Stop after Draft PR exact-head CI and wait for GPT-5.6 Sol review.
