# ADR-0029: WPF Standalone Agent, Dual Control Surfaces, and Admin Fleet Supervision

- Status: Accepted for architecture / implementation pending
- Affected area: POS Agent architecture, WPF desktop application, Angular admin dashboard, local IPC transport, SignalR outbound connectivity, device identity, fleet supervision
- Supersedes (as future target architecture only): ADR-0015, ADR-0016

> [!IMPORTANT]
> **Supersession Notice:** ADR-0029 supersedes ADR-0015 (*Separate Windows POS Agent and direct browser trust boundary*) and ADR-0016 (*POS browser transport and cross-origin security boundary*) **exclusively as the future target POS architecture**. ADR-0015 and ADR-0016 remain authoritative, permanent historical records of the delivered, verified browser-to-loopback implementation (E07–E09) and remain active in side-by-side compatibility mode during the migration period.

---

## 1. Context

The delivered POS Maintenance architecture (E07–E09) established a machine-local Windows Agent (`RmsSupportAgent.Service`) listening on an HTTPS loopback origin (`https://rms-pos-agent.localhost:5001`), invoked directly by the browser-hosted Support Hub SPA using Windows Negotiate authentication, exact-origin CORS, Local Network Access (LNA) policies, and short-lived mutation tokens (ADR-0015, ADR-0016).

While technically sound and verified on Testing instances, operational requirements and store environment constraints have evolved:
1. **Offline Autonomy:** Store technicians and POS operators must be able to perform diagnostics, service recovery, database backups, guarded restores, and branch resets even when store WAN connectivity or the central Support Hub is offline.
2. **Retail Desktop Usability:** Local users require a native Windows desktop experience without browser-specific certificate installation, loopback trust warnings, or varying browser Local Network Access (LNA) permissions.
3. **Fleet Supervision:** Central engineering and support administrators need centralized visibility over thousands of store endpoints—tracking Agent heartbeats, WPF crashes, service states, and database health—and the ability to issue typed remote maintenance commands without remote desktop sessions.
4. **Architectural Convergence:** Privileged logic must not be duplicated between local desktop and central web surfaces.

---

## 2. Decision

The future POS target architecture is re-baselined into a **Dual Control-Surface Architecture** with a single shared Agent capability seam:

```text
================================================================================
                           TARGET CONTROL MODEL
================================================================================

+-------------------------------------+      +--------------------------------+
|      RMS Support WPF Desktop        |      |     Angular Admin Dashboard    |
|   (Complete standalone local app)   |      |   (Central fleet supervision)  |
+-------------------------------------+      +--------------------------------+
                  |                                           |
                  | [Windows Named Pipes]                     | [HTTPS Web Request]
                  | Local System / Admin ACL                  | Admin RBAC Gated
                  v                                           v
+-------------------------------------+      +--------------------------------+
|       RmsSupportAgent.Service       |      |     RMS Support Hub Backend    |
|   (Machine privilege authority)     |      |      (ASP.NET Core Server)     |
+-------------------------------------+      +--------------------------------+
                  ^                                           |
                  |                                           |
                  +===========================================+
                     Agent-Initiated Persistent SignalR (HTTPS)
                     Per-Device Cryptographic Identity & Trust
                                      |
                                      v
       +-------------------------------------------------------------+
       |             SHARED AGENT CAPABILITY SEAM                    |
       |  - Typed Command/Query Application Handlers                 |
       |  - Machine-wide Mutation Leases & Idempotency Guards        |
       |  - Bounded Diagnostic Redaction & Unified Audit Repository  |
       |  - Native RMS Service Protection & Guarded DB Recovery      |
       +-------------------------------------------------------------+
```

### 2.1 Agent Service Remains Privileged Authority
The always-running Windows Service (`RmsSupportAgent.Service`) remains the sole privileged execution boundary on target POS machines. It executes under `LocalSystem` (or designated privileged service account), maintaining its existing proven service identity. It executes typed SQL recovery, Windows service management, filesystem operations, Support Bundle generation, and package lifecycle actions.

### 2.2 WPF Desktop as Complete Standalone Local Application
A native WPF desktop application (`RmsSupportAgent.Desktop.Wpf`) is installed locally beside the Agent Service. It provides full standalone local access to all retained POS capabilities:
- Machine and RMS health overview
- RMS service status and approved service restart/control
- Database health and diagnostics
- Database backup/download and guarded restore
- Logs viewer and safe Support Bundle creation
- Safety Snapshots and incident timeline
- Cleanup and branch-reset workflows preserving native RMS services
- Package install, upgrade, repair, and rollback checkpoints
- Local activity and durable audit history

The WPF application operates 100% autonomously when the central Hub or store network is offline. It contains **no direct privileged logic**; all operations delegate to the local Agent Service over IPC.

### 2.3 Angular Hub as Admin Fleet Supervision Surface
The central Angular Support Hub POS area transitions into an administrator-only fleet supervision and remote support surface:
- Ingesting Agent heartbeats and health telemetry
- Monitoring WPF installation state, running state, heartbeat, and unhandled crashes
- Aggregating fleet-wide issues (Agent offline, RMS service stopped, DB degraded, backup failure, low disk)
- Displaying machine detail, hardware stats, and operational timelines
- Issuing allowlisted, typed remote commands (diagnostics, logs, support bundle, backup, approved service restarts, package updates)
- Enforcing server-side admin RBAC and audit correlation

### 2.4 Shared Capability Authority (Zero Privilege Duplication)
Both local WPF calls and remote Hub commands converge on a single, transport-agnostic Agent command/query application layer. Handlers enforce machine-wide mutation leases, idempotency, bounded redaction, error handling, and durable audit logs identically regardless of invocation channel.

### 2.5 Windows Named Pipes for Local IPC
Communication between the WPF desktop application and the Agent Service uses Windows Named Pipes (`\\.\pipe\RmsSupportAgent.Ipc`). Security is enforced via Windows Security Descriptors and ACLs restricting connection rights to `LocalSystem` and `NT AUTHORITY\Administrators`. The Agent validates the caller's Windows identity on connect and per-message.

### 2.6 Agent-Initiated SignalR for Hub Connectivity
Agent-to-Hub communication is strictly outbound, persistent SignalR over HTTPS initiated by the Agent. No inbound listening ports are opened on store firewalls or POS machines. Reconnection uses exponential backoff.

### 2.7 Per-Device Cryptographic Identity
Device identity is decoupled from browser session cookies (`oot_sid`). Each POS machine has a registered Device ID and cryptographic credential/certificate stored outside application packages. The Hub authenticates device credentials upon connection negotiation.

### 2.8 WPF Crash Isolation & Agent Continuity
The Agent Service and WPF Desktop run as separate Windows processes. If the WPF application crashes, freezes, or is terminated, the Agent Service continues running uninterrupted, logs the crash event, reports crash telemetry to the Hub, and maintains continuous remote management.

### 2.9 Strictly Allowlisted Typed Remote Commands
Under NO circumstances shall arbitrary or generic command execution be introduced. The architecture strictly prohibits:
- Arbitrary PowerShell execution
- Generic shell / CMD execution
- Generic SQL execution
- Generic filesystem browsing or script upload
- Arbitrary process launch
- Arbitrary service name manipulation

Only explicitly typed, compiled, allowlisted handlers may be invoked.

### 2.10 Side-by-Side Migration & Controlled Deprecation
During migration, the Agent Service retains the legacy HTTPS loopback transport alongside Named Pipes and SignalR. The legacy browser-direct path is deprecated and retired only after formal acceptance of the WPF desktop app and Admin supervision on representative Testing machines.

---

## 3. Consequences

### Positive
- **Store Autonomy:** Retail POS maintenance operates reliably during network outages.
- **Zero Duplication:** Local and remote operations share 100% of privileged business logic and validation.
- **Enhanced Security:** Eliminates browser CORS, LNA policy friction, and loopback certificate installation in retail browsers.
- **Centralized Visibility:** Administrators gain real-time fleet health, crash monitoring, and typed remote support capabilities.
- **Process Resilience:** WPF crashes cannot take down the privileged Agent Service.

### Negative / Trade-offs
- **Packaging & Deployment:** Requires maintaining a dual-binary installer (Windows Service + WPF Desktop) and version compatibility contracts.
- **Migration Effort:** Requires side-by-side testing and formal parity validation across all retained E07–E09 capabilities.

---

## 4. Acceptance and Traceability

- **CR:** [`docs/CR-001_WPF_AGENT_ADMIN_SUPERVISION.md`](../docs/CR-001_WPF_AGENT_ADMIN_SUPERVISION.md)
- **Business Requirements:** BR-027 through BR-040
- **Azure DevOps Epics:** E16 (Agent Re-Architecture), E17 (WPF Desktop), E18 (Admin Supervision), E19 (Migration & Rollout)
- **Conversion Plan:** [`docs/WPF_AGENT_CONVERSION_PLAN.md`](../docs/WPF_AGENT_CONVERSION_PLAN.md)
- **First Implementation Slice:** WPF-01 — Shared Agent Application + Local IPC Foundation
