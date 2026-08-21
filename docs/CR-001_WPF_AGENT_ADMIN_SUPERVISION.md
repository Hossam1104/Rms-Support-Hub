# CR-001 — WPF Standalone Agent and Admin Supervision Re-Architecture

**Product:** RMS+ Support Hub  
**Change Type:** Architecture / Major Capability Re-baseline  
**Status:** Approved for planning; implementation gated by architecture/backlog acceptance  
**Date:** 2026-08-22

## 1. Business Change

The machine-local POS support capability shall be restructured into an always-running Windows Agent Service plus a standalone WPF desktop application. The WPF application shall provide local users with the same supported POS maintenance capabilities on the local machine, even when the central Support Hub is unavailable.

The existing Angular Support Hub remains the central web product and becomes the administrator-only fleet supervision and remote-support surface for machine-local POS capabilities.

The Angular dashboard and WPF application shall not contain separate implementations of privileged business logic. Both shall invoke one shared Agent application/capability layer.

## 2. Target Product Structure

### Central RMS+ Support Hub
- ASP.NET Core backend.
- Angular administrator dashboard.
- Online Orders and QA Prompt Studio remain web capabilities.
- POS becomes an administrator fleet/supervision area.
- Central dashboard can inspect machine, Agent and WPF state and invoke only approved typed remote commands.

### RMS Support Agent Service
Always-running Windows Service installed on each supported target machine. It owns privileged execution, machine identity, Hub connectivity, WPF/Agent health, package/update/rollback, diagnostics, typed commands and bounded audit.

### RMS Support WPF Desktop
Complete standalone local operational UI installed beside the Service. It remains usable without the Angular dashboard for supported local operations, reports its own health/version/heartbeat/crashes through the Agent, and never owns unrestricted service/SQL/filesystem privilege directly.

## 3. Control-Surface Principle

| Capability | Local WPF | Angular Admin |
|---|---:|---:|
| Machine/RMS health | Yes | Yes |
| Service status/control | Yes | Yes |
| DB health | Yes | Yes |
| Backup | Yes | Yes |
| Guarded restore | Yes, policy-controlled | Admin/policy-controlled |
| Logs | Yes | Yes |
| Support Bundle | Yes | Yes |
| Cleanup/branch reset | Yes | Yes where policy allows |
| Package lifecycle | Yes, authorized | Yes, controlled |
| Rollback/recovery | Yes, authorized | Yes, controlled |
| Activity/audit | Local | Fleet-wide |
| WPF health monitoring | Local | Fleet-wide |
| Agent health monitoring | Local | Fleet-wide |

No arbitrary PowerShell, SQL, filesystem, process, service-name, URL or script execution is introduced.

## 4. Approved Architecture Direction

- **Hub ↔ Agent:** Agent-initiated persistent outbound SignalR over HTTPS.
- **Machine identity:** per-machine registered device identity backed by certificate/cryptographic material stored outside application content.
- **WPF ↔ Agent:** authenticated local Windows IPC, preferred Windows Named Pipes.
- **Privilege boundary:** privileged execution remains in Agent Service.
- **Service identity migration:** preserve the current proven service identity during parity migration; any least-privilege identity redesign is a separate hardening decision.
- **Remote commands:** typed allowlisted command catalogue only.
- **Offline behavior:** WPF/local Agent features continue locally; telemetry/events synchronize after reconnect.
- **Admin authorization:** central administrator authorization plus server-owned policy.
- **Local authorization:** Windows identity/role authorization appropriate to each operation.
- **Audit:** local and remote paths share correlation and sanitized audit semantics.

## 5. WPF Health / Issue Monitoring

The Agent shall independently report WPF installed/running state, heartbeat, version, PID/start time where safe, last launch/close, crash summary, current operation, version mismatch and Agent↔WPF IPC health.

Angular Admin shall aggregate WPF crash, heartbeat loss, Agent offline, version mismatch, RMS service stopped, DB unavailable, backup failure, package/update failure, low disk and failed remote operations.

WPF crash must not stop the Agent Service.

## 6. New Business Requirements

- **BR-027 Dual Control Surfaces:** Local WPF and Angular Admin expose approved POS capabilities through one shared Agent capability layer.
- **BR-028 Standalone Local Operation:** WPF remains usable for supported local workflows when Hub is unavailable.
- **BR-029 Central Fleet Supervision:** Admins monitor registered machines, Agent/WPF/RMS health and operational issues centrally.
- **BR-030 Shared Capability Authority:** Local and remote actions use the same typed command/query handlers.
- **BR-031 WPF Health Telemetry:** Agent monitors WPF heartbeat, version and crash state independently.
- **BR-032 Secure Outbound Agent Communication:** Agent-to-Hub control communication is Agent-initiated over authenticated HTTPS.
- **BR-033 Device Identity:** Every remotely manageable Agent has a unique server-recognized identity and registration lifecycle.
- **BR-034 Secure Local IPC:** WPF-to-Agent uses authenticated local IPC with Windows identity/ACL enforcement.
- **BR-035 Typed Remote Commands:** No arbitrary remote shell/SQL/filesystem/process execution.
- **BR-036 Offline Resilience:** Local workflows and bounded audit tolerate temporary Hub disconnection.
- **BR-037 Unified Audit:** Local and remote operations share correlation, authorization and sanitized audit semantics.
- **BR-038 WPF/Agent Version Management:** Admins see Agent/WPF versions and package lifecycle stays signed/rollback-aware/fail-closed.
- **BR-039 Migration Safety:** Browser-direct POS maintenance is not removed until parity and representative-machine acceptance pass.
- **BR-040 Admin-only Fleet Control:** Central supervision and remote privileged operations are restricted to authorized administrators.

## 7. Migration Principles

1. Preserve E07–E09 as historical delivered evidence.
2. Reuse existing typed Agent domain/application/infrastructure capabilities.
3. Do not rewrite privileged logic into WPF.
4. Build the shared application seam first.
5. Add WPF as a local client.
6. Add Hub remote control as another client of the same seam.
7. Run browser-direct and WPF paths side-by-side during parity validation.
8. Deprecate browser-direct privileged POS only after explicit acceptance.
9. Preserve package trust, rollback, audit and native RMS protection rules unless a reviewed ADR supersedes them.
10. No Production rollout is implied by this CR.

## 8. Out of Scope for Initial Migration

- Replacing Angular Online Orders or QA Prompt Studio with WPF.
- Arbitrary remote desktop/control.
- Generic PowerShell, command, SQL or filesystem consoles.
- New Production rollout before controlled acceptance.
- Replacing working Agent internals where reuse is safe.

## 9. Acceptance

The architecture change is accepted for cutover when WPF has local parity, offline local use works, Agent survives WPF crash/close, Angular monitors Agent/WPF independently, local/remote actions share handlers, device identity and authorization fail closed, correlated audit is preserved, package trust/rollback remain intact, representative-machine Testing passes, and the browser-direct privileged path can be removed without capability loss.

## 10. Implementation Gate

Implementation may begin only after:
- this CR and the new architecture ADR are merged to `main`;
- Azure WPF epics/stories exist and traceability is synchronized;
- the first implementation slice is explicitly defined;
- Sol confirms no unresolved architecture decision blocks that slice.

Recommended first slice: **shared Agent application contracts + secure local IPC foundation**, not broad WPF screens.
