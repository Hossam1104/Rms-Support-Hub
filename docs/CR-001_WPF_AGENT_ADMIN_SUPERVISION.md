# CR-001 — WPF Standalone Agent and Admin Supervision Re-Architecture

**Product:** RMS+ Support Hub
**Change Type:** Architecture / Major Capability Re-baseline
**Status:** Approved for planning; implementation gated by architecture/backlog acceptance
**Date:** 2026-08-22
**Acceptance Authority:** GPT-5.6 Sol

---

## 1. Business Change

The machine-local POS support capability shall be restructured from a browser-to-loopback architecture into a dual-surface architecture comprising:
1. An always-running **Windows Agent Service** (`RmsSupportAgent.Service`) owning privileged machine execution and device identity.
2. A complete standalone **WPF Desktop Application** (`RmsSupportAgent.Desktop.Wpf`) installed beside the Service, providing local users with full, autonomous access to all supported POS maintenance capabilities—even when the central Support Hub or WAN is offline.
3. A centralized **Angular Administrator Dashboard** in RMS Support Hub providing central fleet supervision, machine health telemetry, WPF issue monitoring, and controlled typed remote support commands.

The Angular dashboard and the WPF desktop application shall **never** contain separate implementations of privileged business logic. Both control surfaces shall invoke one shared Agent capability/application layer.

---

## 2. Business Rationale

- **Store-Level Autonomy:** POS maintenance, diagnostics, database backup/restore, service recovery, and emergency resets must remain 100% operational when store network connectivity or central Support Hub servers are unavailable.
- **Dedicated Desktop UX:** Local POS technicians and cashiers require a fast, native Windows desktop application with Windows-integrated authentication, eliminating browser-specific CORS, Local Network Access (LNA) policy variance, and certificate installation friction in retail browsers.
- **Centralized Fleet Supervision:** Central engineering and support administrators need centralized visibility over thousands of store endpoints, monitoring Agent heartbeats, WPF application crashes, service failures, database health, and version drift without needing remote desktop sessions.
- **Zero Duplication of Privilege:** Maintaining separate logic for local vs remote operations introduces security risks and contract drift. A single shared command/query layer guarantees identical business rules, validation, mutation leasing, and auditing across both channels.
- **Safe Evolutionary Migration:** The transition preserves all delivered capabilities from E07–E09, runs side-by-side during validation, and deprecates the browser-direct loopback path only after proven representative-machine acceptance.

---

## 3. Central Hub Responsibilities

The central RMS+ Support Hub (.NET Core Backend + Angular Admin Dashboard) owns:
- **Central Fleet Supervision:** Aggregating registered POS machine inventory, online/offline status, Agent health, and WPF application status.
- **WPF Health & Crash Monitoring:** Ingesting and alerting on WPF heartbeat loss, unhandled crashes, version mismatches, and execution errors reported by the local Agent.
- **Issues Board:** Centralized categorization and escalation of fleet-wide issues (Agent offline, RMS service stopped, database degradation, backup failures, low disk).
- **Admin Remote Support:** Initiating allowlisted, typed remote diagnostic queries, log collections, Support Bundle generation, and approved service restarts.
- **Admin RBAC & Policy:** Enforcing server-side role-based access control, ensuring only authorized administrators can trigger remote actions.
- **Central Audit Search:** Correlating remote command dispatches with machine-side audit execution records.

---

## 4. Agent Service Responsibilities

The Windows Agent Service (`RmsSupportAgent.Service`) runs continuously under `LocalSystem` (or designated privileged service account) and owns:
- **Privileged Execution Boundary:** Sole authority for machine-level operations: SQL recovery, Windows service control, filesystem maintenance, Support Bundle generation, and package updates.
- **Device Identity & Registration:** Owning per-machine cryptographic identity and certificates used for authenticating to the central Hub.
- **Secure Local IPC Endpoint:** Hosting an authenticated Windows Named Pipe listener restricted by Windows ACLs to explicitly approved local identities (SYSTEM, Administrators, and dedicated RMS Support Operators group) with per-command Agent authorization.
- **Outbound Persistent Hub Connection:** Establishing and maintaining an Agent-initiated SignalR connection over HTTPS to the Support Hub.
- **WPF Process Supervision & Telemetry:** Monitoring local WPF process state, heartbeat, crashes, and protocol version compatibility, reporting telemetry to the Hub.
- **Offline Event Queue:** Buffering health, audit, and diagnostic events during Hub disconnection and replaying them deterministically upon reconnection.
- **Shared Capability Seam:** Hosting transport-agnostic command and query handlers enforcing mutation leases, idempotency, bounded redaction, and durable audit logs.

---

## 5. WPF Standalone Responsibilities

The WPF Desktop Application (`RmsSupportAgent.Desktop.Wpf`) is a rich native Windows application that owns:
- **Standalone Local Operations:** Providing immediate, responsive local access to all supported POS maintenance workflows without browser dependencies.
- **Retained Capabilities Access:**
  - Machine and RMS installation discovery and health overview.
  - RMS service status and approved service restart/control.
  - Database health checks, storage metrics, and integrity diagnostics.
  - Database backup creation, download, and guarded pre-flight restore.
  - Diagnostic logs viewer and safe Support Bundle creation.
  - Safety Snapshots creation and incident timeline inspection.
  - File/cache cleanup previews and guarded branch reset preserving native RMS services.
  - Package installation, upgrade, repair, and rollback checkpoints.
  - Local activity and durable audit history.
- **Local Windows Authorization UX:** Providing distinct authorization flows for Authorized Local Operators (non-destructive operations) and prompting for Administrator/elevated confirmation before executing high-risk mutating operations.
- **WPF Heartbeat & Telemetry:** Sending periodic heartbeats and unhandled exception reports over Named Pipes to the Agent Service.
- **Zero Direct Privilege:** The WPF application never contains direct SQL connections, raw service manipulation code, or unconstrained filesystem access; all actions delegate to the Agent Service over IPC.

---

## 6. Dual Control-Surface Capability Matrix

| Capability | Local WPF Desktop | Angular Admin Dashboard | Shared Authority Seam |
|---|:---:|:---:|:---:|
| Machine & RMS health | Full local view | Fleet-wide aggregation | Shared Query Handler |
| Service status & control | Local view & restart | Fleet view & remote restart | Shared Service Control Handler |
| Database health & diagnostics | Full local metrics | Remote diagnostics | Shared DB Diagnostics Handler |
| Database backup | On-demand local backup | Remote backup request | Shared Backup Handler |
| Guarded database restore | Local guarded restore | Policy-gated remote restore | Shared Restore Handler |
| Diagnostic logs | Local viewer & filter | Remote log fetch | Shared Log Query Handler |
| Support Bundle | Generate & save local | Generate & upload to Hub | Shared Support Bundle Handler |
| Safety Snapshots & timeline | Full local management | Central timeline view | Shared Snapshot Handler |
| Cleanup & branch reset | Local preview & execute | Restricted remote policy | Shared Cleanup Handler |
| Package lifecycle | Install/upgrade/repair | Controlled remote upgrade | Shared Package Handler |
| Rollback & recovery | Local rollback checkpoint | Controlled remote rollback | Shared Rollback Handler |
| Activity & audit | Local machine audit log | Central fleet-wide search | Shared Audit Repository |
| WPF health & telemetry | Heartbeat & self-report | Central monitoring & alerts | Shared Telemetry Model |
| Agent health monitoring | Local connection status | Fleet heartbeat dashboard | Shared Health Model |

---

## 7. Target Communication & Security Architecture

### 7.1 Hub ↔ Agent: Agent-Initiated Persistent SignalR over HTTPS
- The Agent initiates an outbound TLS connection to the central Support Hub SignalR endpoint (`/hubs/agent`).
- No inbound listening ports are opened on store firewalls or POS machines.
- Persistent duplex communication allows real-time telemetry streaming and typed remote command dispatch.
- Reconnection with exponential backoff and message replay protection.

### 7.2 WPF ↔ Agent: Two-Layer Local Authorization Model
Communication between the WPF desktop application and the Agent Service uses an authenticated Windows Named Pipe endpoint (e.g., `\\.\pipe\RmsSupportAgent.Ipc`) under a strict two-layer security model:

#### Layer A — IPC Connection Authorization
- Named Pipe connection rights are restricted using Windows Security Descriptors / ACLs granting access exclusively to explicitly approved local identities:
  - `LocalSystem`
  - `NT AUTHORITY\Administrators` (Local Administrators)
  - Dedicated local Windows group: `RMS Support Operators` (or repository-configured equivalent bounded operator group)
- Access is strictly denied to `Everyone`, `Guests`, anonymous callers, and unrestricted `Authenticated Users`.
- Installer/provisioning owns creation and configuration of the dedicated operator group.
- Client Windows identity is authenticated upon connection.

#### Layer B — Per-Command Authorization
- Connecting to the Named Pipe does **not** grant blanket authorization (`PIPE CONNECTION AUTHORIZATION != COMMAND AUTHORIZATION`).
- The Agent application layer evaluates each typed command/query against the caller's authenticated Windows identity and role:
  - **Authorized Local Operator:** Permitted to execute non-destructive low/medium-risk maintenance (machine/RMS health, database health/read diagnostics, logs inspection, Support Bundle creation, local audit/activity, approved backup creation).
  - **Local Administrator / Elevated Operator:** Required for high-risk mutating actions (privileged Windows service restarts/mutations, database restore, cleanup execution, branch reset, package install/upgrade/repair/uninstall, rollback/recovery).
- Named Pipe transport eliminates browser CORS, loopback HTTPS certificate trust, and LNA policy friction.

### 7.3 Device Identity & Trust
- Every POS machine possesses a unique registered Device ID and cryptographic credential/certificate.
- Credentials are provisioned during installation and stored securely outside tracked repository files.
- The central Hub validates device credentials during connection negotiation; revoked or unlisted devices fail closed.

### 7.4 Authorization Model
- **Local Invocations (WPF):** Authenticated Windows identity verified against Layer A (IPC connection ACLs) and Layer B (Per-command authorization), requiring Administrator elevation and confirmation modals for high-risk mutating actions.
- **Remote Invocations (Hub):** Enforce central administrator authentication, RBAC role validation (`FleetAdmin`), target machine policy, and one-time command execution tokens.
- **Shared Enforcement:** Both invocation channels pass into the shared application layer wrapped in an `InvocationContext` verifying caller identity, authorization level, and command prerequisites.

### 7.5 Typed Remote Commands (Allowlisted Catalogue Only)
Under **NO circumstances** shall arbitrary or generic command execution be introduced. The architecture strictly prohibits:
- Arbitrary PowerShell execution
- Generic shell / CMD execution
- Generic SQL query execution
- Generic filesystem browsing or file upload
- Arbitrary process launch
- Arbitrary Windows service name manipulation
- Arbitrary URLs, webhooks, or script downloads

Every remote command must belong to the strictly typed, compiled, allowlisted catalogue of handlers.

### 7.6 WPF Crash Resilience & Continuity
- The Agent Service and WPF Desktop run in separate Windows processes.
- An unhandled exception, freeze, or crash of the WPF application does **not** terminate or disrupt the Agent Service.
- The Agent detects client disconnection, logs the crash event, reports crash telemetry to the Hub, and maintains continuous remote management.

---

## 8. New Business Requirements (BR-027 through BR-040)

- **BR-027 Dual Control Surfaces:** The platform shall provide dual control surfaces for machine-local POS maintenance: a local native WPF desktop application and a central Angular administrator dashboard, both operating through one shared Agent capability layer.
- **BR-028 Standalone Local Operation:** The WPF desktop application and local Agent Service shall function autonomously for all supported maintenance workflows when disconnected from the central Support Hub or external network.
- **BR-029 Central Fleet Supervision:** The central Angular dashboard shall provide administrators with unified visibility over registered POS machines, Agent heartbeats, WPF application states, RMS health, and aggregated operational issues.
- **BR-030 Shared Capability Authority:** All privileged maintenance logic, validation rules, mutation leases, and diagnostic operations shall be implemented in a single transport-agnostic Agent application layer invoked identically by local IPC and remote Hub requests.
- **BR-031 WPF Health Telemetry:** The Agent Service shall monitor the local WPF process state, heartbeat, version, and unhandled crashes, publishing health telemetry to the central Hub independently of the desktop UI process.
- **BR-032 Secure Outbound Agent Communication:** Agent-to-Hub communication shall be initiated by the Agent as an outbound, authenticated, encrypted SignalR connection over HTTPS, requiring no inbound listening ports on target machines.
- **BR-033 Device Identity:** Each remotely manageable POS machine shall have a unique, server-recognized device identity backed by cryptographic credentials managed outside application packages.
- **BR-034 Secure Local IPC:** WPF-to-Agent communication uses authenticated Windows Named Pipes restricted to explicitly approved local identities/groups; the Agent performs per-command authorization, requiring administrator/elevated authority for high-risk operations.
- **BR-035 Typed Remote Commands:** Remote operations issued from the central Hub shall be restricted to an allowlisted catalogue of strongly-typed commands; generic shell, arbitrary PowerShell, generic SQL, and arbitrary process execution are strictly prohibited.
- **BR-036 Offline Resilience:** Local workflows and durable audit logging shall operate during network outages; pending telemetry and audit records shall be buffered locally and synchronized upon reconnection.
- **BR-037 Unified Audit:** Local and remote operations shall generate durable, sanitized audit records sharing a common correlation model, identifying the originating caller, machine identity, operation parameters, and execution outcome.
- **BR-038 WPF/Agent Version Management:** The platform shall enforce protocol version compatibility between the WPF client and Agent Service, surface version drift centrally, and govern package updates through signed, rollback-capable lifecycles.
- **BR-039 Migration Safety:** The legacy browser-direct loopback POS maintenance path shall remain operational side-by-side during parity migration and shall only be deprecated after full functional and security acceptance on representative machines.
- **BR-040 Admin-only Fleet Control:** Central fleet supervision and remote command execution shall be strictly restricted to authenticated administrators holding verified administrative roles.

---

## 9. Migration Principles & Phased Roadmap

1. **Preserve Historical Delivered Evidence:** Deliverables and tests under E07, E08, and E09 remain permanently closed as historical baseline evidence.
2. **Reuse Proven Seams:** Existing domain models, SQL backup/restore logic, service controls, Support Bundle redaction, and package trust verification code are preserved and reused.
3. **Application Seam First:** Implementation starts by extracting the transport-agnostic application layer before building new UI screens.
4. **Side-by-Side Validation:** During the migration period, the Agent Service supports both the legacy HTTPS loopback transport and the new Named Pipe / SignalR transports.
5. **Controlled Deprecation:** The browser-direct loopback transport is retired only after formal acceptance of the WPF desktop app and Angular admin supervision.
6. **No Production Direct Contact:** Re-architecture and migration activities remain strictly within Development and Testing environments until formal go-live gates are satisfied.

---

## 10. Out of Scope for Initial Migration

- Rebuilding Online Orders or QA Prompt Studio in WPF (they remain central web applications in Angular Support Hub).
- Arbitrary remote desktop / screen sharing functionality.
- Generic PowerShell, CMD, or SQL interactive consoles.
- Automatic or un-gated fleet-wide production deployments before representative-machine acceptance.
- Replacing working internal Agent algorithms where reuse is proven and safe.

---

## 11. Acceptance Criteria

The architecture re-baseline and subsequent migration shall be accepted when:
1. The WPF desktop application achieves 100% functional parity with retained E07–E09 capabilities.
2. Local operations execute completely and deterministically while disconnected from the Hub.
3. The Agent Service continues running and reporting to Hub when the WPF application is closed or crashes.
4. Central Angular dashboard accurately tracks machine status, Agent heartbeats, and WPF health.
5. Local and remote requests converge on the same typed handlers without code duplication.
6. Named Pipe ACLs and SignalR device authentication fail closed against unauthorized callers.
7. Correlated audit logs capture local and remote operations with full fidelity.
8. Representative-machine Testing passes all automated and manual validation scenarios.
9. Architecture Authority (GPT-5.6 Sol) formally reviews and approves the migration outcome.

---

## 12. Implementation Gate

Implementation of Phase 1 (WPF-01) may begin only after:
- CR-001 and ADR-0029 are committed and accepted by GPT-5.6 Sol.
- Azure DevOps Epics E16–E19 and child user stories are established and synchronized in backlog traceability.
- The first implementation slice (`WPF-01 — Shared Agent Application + Local IPC Foundation`) is explicitly accepted for development.
