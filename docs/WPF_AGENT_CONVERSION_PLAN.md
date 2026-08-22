# WPF / Windows Agent Conversion Plan

**Product:** RMS+ Support Hub
**Architecture Rebaseline:** Post PR #30
**Status:** Approved for planning; implementation gated by architecture/backlog acceptance
**Date:** 2026-08-22
**Authority:** GPT-5.6 Sol

---

## 1. Executive Summary

This document establishes the phased implementation roadmap for transitioning the POS Maintenance capability from the historical browser-to-loopback architecture (E07–E09) to the owner-approved **Dual Control-Surface Architecture** (CR-001, ADR-0029).

### Core Architectural Principles
1. **Central Support Hub:** ASP.NET Core backend + Angular administrator dashboard providing fleet supervision and controlled typed remote support.
2. **Local POS Machine:** Always-running Windows Agent Service (`RmsSupportAgent.Service`) + complete standalone WPF Desktop Application (`RmsSupportAgent.Desktop.Wpf`).
3. **Shared Capability Seam:** Local WPF actions and remote Hub commands execute the **same** underlying command/query handlers in the Agent application layer. Privileged logic is never duplicated.
4. **Autonomous Local Operation:** The WPF application and Agent Service operate with full capability even when the central Hub or store network is offline.
5. **Secure Transports:** Windows Named Pipes with bounded local identity ACLs (SYSTEM, Administrators, dedicated RMS Support Operators group) and per-command Agent authorization for local IPC; Agent-initiated persistent SignalR over HTTPS for Hub connectivity.
6. **Strictly Typed Remote Operations:** No generic shell, arbitrary PowerShell, generic SQL, or arbitrary process execution.

---

## 2. Phased Implementation Roadmap

```text
+-----------------------------------------------------------------------------------+
| Phase 0: Architecture Rebaseline & Backlog Reconciliation (CR-001, ADR-0029, E16-E19)|
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 1: Shared Agent Capability Layer (Extract decoupled application seam)       |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 2: Local WPF-Agent IPC Foundation (Named Pipes, Windows Auth, Slice WPF-01)  |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 3: WPF Standalone App / Local Parity (Full native UI for retained features)  |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 4: Agent-Hub Connectivity & Device Identity (Outbound SignalR & Registration)|
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 5: Angular Admin Fleet Supervision (Central dashboard, alerts, WPF telemetry)|
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 6: Typed Remote Operations (Allowlisted remote diagnostics, restart, update) |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 7: Backup & Support Artifact Delivery (Secure streaming & Hub integration)   |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 8: Side-by-Side Migration & Parity Validation (Representative machine proof) |
+-----------------------------------------------------------------------------------+
                                          |
                                          v
+-----------------------------------------------------------------------------------+
| Phase 9: Cutover & Deprecation of Browser-Direct Path (Pilot -> Full Rollout)      |
+-----------------------------------------------------------------------------------+
```

---

### Phase 0 — Architecture Rebaseline
- **Deliverables:**
  - CR-001 specification (`docs/CR-001_WPF_AGENT_ADMIN_SUPERVISION.md`).
  - Architecture ADR-0029 (`.ai/decisions/ADR-0029-wpf-agent-dual-control-and-admin-supervision.md`).
  - BRD v1.1 update incorporating BR-027 through BR-040.
  - Azure DevOps Epics E16, E17, E18, E19 creation and child story mapping.
  - Reconciliation of E11 (superseded) and E12 (updated target state).
  - Backlog blueprint and traceability matrix synchronization.
- **Exit Gate:** Architecture and backlog approved by GPT-5.6 Sol; zero implementation ambiguity.

---

### Phase 1 — Shared Agent Capability Layer
- **Goal:** One unified, transport-agnostic application layer for all privileged logic.
- **Work:**
  - Inventory existing Agent endpoints and handlers in `RmsSupportHub.Pos.Agent`.
  - Extract reusable C# commands, queries, and validators into `RmsSupportAgent.Application`.
  - Decouple business handlers from Kestrel/HTTP infrastructure.
  - Preserve mutation leases, idempotency guards, bounded redaction, timeouts, and durable audit logs.
  - Define `InvocationContext` capturing caller identity (`LocalWpf` vs `RemoteHub`), Windows principal, local operator/admin role, device identity, admin identity, correlation ID, and authorization level.
  - Define unified progress reporting and cooperative cancellation token contracts.
- **Exit Gate:** Shared application layer passes unit and integration tests; existing HTTP endpoints delegate to shared handlers with zero regression.

---

### Phase 2 — Local WPF-Agent IPC Foundation
- **Goal:** Authenticated, secure local IPC transport using Windows Named Pipes under a two-layer authorization model.
- **Work:**
  - Implement Named Pipe server in `RmsSupportAgent.Service` (`\\.\pipe\RmsSupportAgent.Ipc`).
  - Enforce Windows Security Descriptors / ACLs restricting pipe connection to `LocalSystem`, `NT AUTHORITY\Administrators`, and the dedicated local Windows group `RMS Support Operators` (Layer A).
  - Strictly reject unauthorized callers (`Everyone`, `Guests`, anonymous, unrestricted `Authenticated Users`) fail closed.
  - Validate and authenticate caller Windows identity on connection and per-message.
  - Implement per-command Agent application layer authorization verifying required privilege (operator vs administrator) for each typed operation (Layer B).
  - Implement lightweight .NET IPC client library (`RmsSupportAgent.LocalIpc`).
  - Implement protocol handshake, version negotiation, and serialization.
  - Implement local Agent health query and one non-destructive typed diagnostic command.
  - Create integration test harness verifying unauthorized callers fail closed and per-command authorization gates hold.
- **Exit Gate:** Synthetic client / test harness can execute typed commands over Named Pipes with verified Windows authorization; unauthorized identities rejected at IPC connection; non-admin callers attempting elevated operations rejected by per-command authorization fail-closed.

---

### Phase 3 — WPF Standalone App / Local Feature Parity
- **Goal:** Complete, native WPF desktop application covering all retained POS capabilities.
- **UI Delivery Sequence:**
  1. **Shell & Dashboard:** Modern desktop shell, navigation, design tokens, machine status summary, Agent connection indicator.
  2. **Agent & RMS Health:** Real-time service status, component health, connection latency.
  3. **RMS Service Control:** Approved service restart/control workflows with confirmation prompts.
  4. **Database Diagnostics:** Connection test, storage usage, integrity checks without exposing connection strings.
  5. **Database Backup & Download:** On-demand backup creation, artifact inspection, local download.
  6. **Guarded Database Restore:** Pre-flight checks, mandatory safety snapshot, confirmation modal, mutation lease.
  7. **Logs & Safe Support Bundle:** Bounded log viewer, severity filtering, credential-redacted Support Bundle ZIP generation.
  8. **Safety Snapshots & Timeline:** Manual/scheduled snapshot creation, chronological system event timeline.
  9. **Cleanup & Branch Reset:** Purge preview, pre-reset snapshot, guarded branch reset strictly preserving native RMS services.
  10. **Package Lifecycle:** Installed version inspection, package signature verification, install, upgrade, repair, uninstall.
  11. **Rollback & Recovery:** PreviousVersion checkpoint inspection, health-committed rollback execution.
  12. **Local Activity & Audit:** Searchable chronological audit viewer for all machine maintenance actions.
- **Exit Gate:** Functional parity matrix verifies 100% of retained E07–E09 capabilities in WPF; full offline functionality proved.

---

### Phase 4 — Agent-Hub Connectivity & Device Identity
- **Goal:** Outbound, persistent SignalR connection from Agent to Support Hub with cryptographic device identity.
- **Work:**
  - Implement Agent-side SignalR client (`RmsSupportAgent.HubClient`) connecting over outbound HTTPS.
  - Implement per-device registration and cryptographic identity verification.
  - Implement periodic health heartbeats, status updates, and WPF process telemetry.
  - Implement automatic reconnect with exponential backoff and connection state tracking.
  - Implement bounded local event queue buffering telemetry/audit during Hub outages with FIFO replay on reconnect.
  - Implement duplicate prevention and message idempotency.
- **Exit Gate:** Registered Testing machines appear online in Support Hub; unauthenticated Agents fail closed; network drop and reconnect synchronize cleanly.

---

### Phase 5 — Angular Admin Fleet Supervision
- **Goal:** Centralized administration views in Support Hub Angular SPA for fleet visibility.
- **Admin Views:**
  - **Machine Inventory:** Fleet table showing machine names, branch, IP, Agent version, WPF state, online/offline status.
  - **Health Dashboard:** Real-time aggregated heartbeat status, fleet health KPI cards.
  - **WPF Health & Crash Monitoring:** Tracks WPF running state, PID, heartbeats, crash summaries, and Agent/WPF version drift.
  - **Central Issues Board:** Aggregated alerts feed (Agent offline, WPF crash, RMS service stopped, backup failed, low disk).
  - **Machine Detail & Timeline:** Full hardware/OS metrics, RMS state, services, recent jobs, and correlated audit timeline.
  - **Fleet Version Matrix:** Distribution of Agent and WPF versions across all stores, identifying update candidates.
  - **Central Audit Search:** Cross-machine audit search by user, branch, operation, or correlation ID.
- **Exit Gate:** Support engineers can diagnose store issues, monitor WPF health, and track fleet status centrally without remote desktop.

---

### Phase 6 — Typed Remote Operations
- **Goal:** Governed execution of allowlisted remote maintenance commands by authorized administrators.
- **Staged Capability Delivery:**
  1. *Low-Risk / Read-Only:* On-demand health refresh, diagnostics query, sanitized log collection.
  2. *Support Artifacts:* Remote Support Bundle generation, remote database backup trigger.
  3. *Mutating Operations:* Approved RMS service restart, package upgrade/repair, rollback execution.
- **Enforcement Rules:**
  - Server-enforced RBAC (`FleetAdmin` role required for mutations).
  - Strictly typed command catalogue—zero generic shell, PowerShell, SQL, or arbitrary process execution.
  - Real-time SignalR progress streaming, cancellation token propagation, and idempotency guarantees.
  - Correlated audit logging on both Hub and Agent.
- **Exit Gate:** Remote commands execute safely with real-time feedback; unauthorized remote attempts fail closed.

---

### Phase 7 — Backup & Support Artifact Delivery
- **Goal:** Robust delivery and management of database backups and Support Bundles.
- **Work:**
  - Unified backup/bundle creation handlers in shared application layer.
  - Secure chunked/streaming upload of artifacts from Agent to Support Hub.
  - Local-only retention policy when Hub is unreachable.
  - SHA-256 integrity verification and artifact expiration policies.
- **Exit Gate:** Large backup files and Support Bundles upload reliably; local backups operate seamlessly offline.

---

### Phase 8 — Side-by-Side Migration & Parity Validation
- **Goal:** Comprehensive comparative testing on representative Testing POS machines.
- **Work:**
  - Run Agent Service with dual transports (legacy loopback HTTPS + Named Pipes + SignalR).
  - Execute identical test suites through browser-direct, WPF desktop, and Admin remote paths.
  - Verify identical output data, error handling, mutation leasing, and audit records.
  - Perform chaos/resilience testing: forceful WPF termination, network disruption, SQL server failure.
  - Validate WPF crash recovery (Agent remains online and reporting).
- **Exit Gate:** Signed functional and security parity matrix; zero open Critical/High defects; Sol acceptance.

---

### Phase 9 — Cutover & Deprecation
- **Goal:** Transition production support to WPF desktop and Admin supervision, retiring legacy loopback path.
- **Work:**
  - Build unified installer deploying Windows Service + WPF desktop app + desktop shortcuts.
  - Execute in-place migration script for existing Agent installations.
  - Update operational runbooks, user manuals, and training materials.
  - Pilot rollout to initial store group with active telemetry monitoring.
  - Disable and retire legacy Kestrel loopback listener and browser certificate generation in Agent.
  - Complete fleet-wide phased deployment with automated health-gated rollback procedures.
- **Exit Gate:** Full estate running WPF desktop + Agent Service; legacy browser-direct route removed; final migration sign-off.

---

## 3. Recommended Project Structure

```text
pos/
├── RmsSupportAgent.Domain/           # Entities, value objects, domain interfaces
├── RmsSupportAgent.Application/      # Shared command/query handlers, validators, DTOs
├── RmsSupportAgent.Infrastructure/   # SQL repos, service control, backup, package trust
├── RmsSupportAgent.Contracts/        # Typed IPC and SignalR message contracts
├── RmsSupportAgent.LocalIpc/         # Named Pipe client/server communication library
├── RmsSupportAgent.HubClient/        # SignalR outbound client for Hub communication
├── RmsSupportAgent.Service/          # Always-running Windows Service (Composition root)
└── RmsSupportAgent.Desktop.Wpf/      # Standalone native WPF desktop application
```

---

## 4. First Implementation Slice

### Slice: `WPF-01 — Shared Agent Application + Local IPC Foundation`

> [!CAUTION]
> **Implementation Hard Stop:** Do **NOT** start implementation of `WPF-01` until GPT-5.6 Sol reviews and formally accepts this Architecture Rebaseline PR.

#### Scope of WPF-01:
1. **Application Seam Extraction:**
   - Inspect existing `RmsSupportHub.Pos.Agent` endpoints and handlers.
   - Extract core query/command handlers into `RmsSupportAgent.Application`.
   - Implement `InvocationContext` distinguishing local from remote callers.
   - Preserve all existing mutation leases, idempotency checks, and audit semantics.
2. **Local IPC Infrastructure:**
   - Implement secure Windows Named Pipe listener in `RmsSupportAgent.Service`.
   - Enforce Windows Security Descriptors (`LocalSystem` + `Administrators` + dedicated `RMS Support Operators` group).
   - Authenticate caller Windows identity and enforce per-command authorization in Agent layer.
   - Implement lightweight client library in `RmsSupportAgent.LocalIpc`.
3. **Foundation Handlers:**
   - Implement Agent health/readiness query over Named Pipes.
   - Implement ONE non-destructive typed diagnostic command (e.g., RMS installation check).
4. **Transport Preservation:**
   - Maintain existing Kestrel loopback HTTPS endpoints so existing tests and browser workflows remain 100% green.
5. **Validation:**
   - Automated integration tests verifying Named Pipe authentication, ACL rejection for unauthorized identities, per-command authorization checks, and shared handler execution.
   - Zero UI migration or SignalR remote mutations in this first slice.

---

## 5. Traceability & Governance

| Requirement | Description | Target Phase | Azure Epic / Story |
|---|---|:---:|---|
| **BR-027** | Dual Control Surfaces | Phase 1–3 | E16, E17, E18, E19 |
| **BR-028** | Standalone Local Operation | Phase 2, 3 | E16, E17 |
| **BR-029** | Central Fleet Supervision | Phase 4, 5 | E18 |
| **BR-030** | Shared Capability Authority | Phase 1 | E16 (`US-E16-02`) |
| **BR-031** | WPF Health Telemetry | Phase 4, 5 | E16, E18 (`US-E18-03`) |
| **BR-032** | Secure Outbound Agent Comm | Phase 4 | E16 (`US-E16-05`) |
| **BR-033** | Device Identity | Phase 4 | E16 (`US-E16-06`) |
| **BR-034** | Secure Local IPC | Phase 2 | E16 (`US-E16-04`) |
| **BR-035** | Typed Remote Commands | Phase 6 | E16, E18 |
| **BR-036** | Offline Resilience | Phase 2, 3, 4 | E16, E17 |
| **BR-037** | Unified Audit | Phase 1, 4, 5 | E16, E17, E18 |
| **BR-038** | WPF/Agent Version Management | Phase 3, 5, 9 | E16, E17, E18, E19 |
| **BR-039** | Migration Safety | Phase 8, 9 | E17, E19 |
| **BR-040** | Admin-only Fleet Control | Phase 5, 6 | E18 (`US-E18-11`) |
