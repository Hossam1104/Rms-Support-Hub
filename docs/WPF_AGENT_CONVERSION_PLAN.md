# WPF / Windows Agent Conversion Plan

**Product:** RMS+ Support Hub  
**Architecture checkpoint:** Post PR #30  
**Status:** Proposed implementation roadmap

## Target

- Angular remains the central administrator dashboard.
- WPF becomes the complete standalone local POS support application.
- Windows Agent Service remains the machine privilege boundary.
- WPF and Angular remote actions share the same Agent command/query handlers.
- Agent initiates secure outbound Hub connectivity.
- WPF uses secure local IPC to the Agent.
- WPF issues are independently visible to administrators.

## Phase 0 — Architecture Rebaseline

Deliverables:
- CR-001.
- ADR for WPF + Service + Angular Admin model.
- BRD v1.1 requirements BR-027–BR-040.
- Azure Epics E16–E19.
- revised E11/E12 roadmap.
- traceability matrix.
- parity inventory of existing E07–E09 capabilities.

Exit: architecture accepted, backlog synchronized, and no implementation ambiguity for Phase 1.

## Phase 1 — Shared Agent Capability Layer

Goal: one privileged implementation invoked locally or remotely.

Work:
- inventory current Agent endpoints/handlers;
- extract reusable typed commands/queries from HTTP-specific composition;
- preserve authorization, mutation leases, idempotency, timeouts, audit and redaction;
- define invocation context (`LocalWpf` / `RemoteHub`), principal/device/admin identity, correlation and policy;
- define progress/cancellation contracts;
- prove local/remote paths converge on the same behavior.

Exit: shared application layer validated; existing path still works; no duplicated privileged logic.

## Phase 2 — Local WPF ↔ Agent IPC

Preferred transport: Windows Named Pipes.

Work:
- Agent-owned pipe;
- strict ACL;
- Windows client identity validation;
- typed contracts;
- correlation/audit;
- progress/events;
- reconnect behavior;
- service-unavailable UX;
- no arbitrary command surface.

Exit: synthetic WPF/test client can call health plus one non-destructive typed command and unauthorized callers fail closed.

## Phase 3 — WPF Standalone App / Local Feature Parity

Recommended UI order:
1. Shell + machine dashboard.
2. Agent/RMS health.
3. RMS services.
4. DB diagnostics.
5. Logs.
6. Backup/download.
7. Support Bundle.
8. Safety Snapshots.
9. Cleanup/branch reset.
10. Package lifecycle.
11. Restore/rollback.
12. Local activity/history.

Rules:
- WPF does not reimplement privileged logic.
- approved local workflows work when Hub is offline.
- high-risk actions preserve preview/confirm/authorization.
- local audit queues safely for later synchronization.

Exit: parity matrix accepted against retained E07–E09 capabilities.

## Phase 4 — Agent ↔ Hub Connectivity / Device Registration

Preferred transport: Agent-initiated SignalR over HTTPS.

Work:
- machine registration;
- per-machine device identity;
- external certificate/credential provisioning;
- heartbeat;
- Agent version/status;
- WPF version/status/heartbeat;
- reconnect/backoff;
- offline event queue;
- server last-seen/current-state model;
- replay/idempotency protection.

Exit: registered Testing machines appear online/offline correctly and untrusted Agents fail closed.

## Phase 5 — Angular Admin Fleet Supervision

Admin pages:
- Machines
- Machine detail
- Issues
- Operations/jobs
- Audit timeline
- Versions/update status
- Backup/support artifacts

Monitor:
- machine online/offline;
- Agent health/version;
- WPF running/stopped/crashed/heartbeat/version;
- RMS service;
- DB health;
- disk;
- backup;
- package/update;
- recent operation failures.

Exit: admins diagnose WPF/Agent problems centrally without opening the local app.

## Phase 6 — Typed Remote Operations

Start low-risk:
- refresh health;
- collect logs;
- Support Bundle;
- backup;
- diagnostics.

Then controlled higher-risk:
- approved RMS service restart;
- cleanup/branch reset where policy permits;
- package install/repair/upgrade;
- rollback.

Required:
- admin RBAC;
- machine policy;
- typed allowlist;
- idempotency;
- progress/cancel;
- audit;
- command expiry;
- explicit offline queue policy;
- no arbitrary shell/SQL/filesystem/process execution.

## Phase 7 — Backup / Artifact Delivery

- provider abstraction;
- Agent creates artifact locally;
- resumable/retryable upload where practical;
- Hub stores metadata/status/reference, not secrets;
- local WPF and admin-triggered backup use the same handler;
- local-only backup works when Hub is offline.

## Phase 8 — Side-by-Side Migration

Run existing browser-direct POS, WPF local path and Angular Admin supervision in parallel on representative Testing machines.

Compare capability results, authorization, audit, errors, rollback, support evidence, performance and stability.

Exit: signed parity matrix and no unresolved High/Medium security issue.

## Phase 9 — Cutover

- WPF becomes supported local POS maintenance UI.
- Angular POS area becomes admin supervision/control.
- browser-direct privileged POS route is disabled/removed only after acceptance.
- installer/runbooks/support procedures updated.
- pilot then phased rollout.

## Recommended Project Shape

```text
pos/
├── RmsSupportAgent.Domain
├── RmsSupportAgent.Application
├── RmsSupportAgent.Infrastructure
├── RmsSupportAgent.Service
├── RmsSupportAgent.Contracts
├── RmsSupportAgent.HubClient
├── RmsSupportAgent.LocalIpc
└── RmsSupportAgent.Desktop.Wpf
```

Adapt to current project names rather than renaming working projects without need.

## First Implementation Slice

**WPF-01 — Shared Agent Application + Local IPC Foundation**

- extract/reuse command/query seam;
- invocation context;
- Named Pipe endpoint;
- authenticated local test client;
- health query;
- one non-destructive typed command;
- audit/correlation;
- automated tests;
- preserve current browser path.

This proves the architecture before broad UI conversion.
