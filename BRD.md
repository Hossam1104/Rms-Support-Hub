# RMS+ Support Hub — Business Requirements Document (BRD)

**Version:** 1.1
**Status:** Baselined with Approved WPF Agent & Fleet Supervision Re-Architecture (CR-001)
**Product:** RMS+ Support Hub
**Repository:** `Hossam1104/Rms-Support-Hub`
**Prepared:** 2026-08-22
**Acceptance Authority:** GPT-5.6 Sol

---

## 1. Executive Summary

RMS+ Support Hub is an internal engineering and operational support platform that unifies three operational areas:

1. **QA Productivity Tooling:** Governed Prompt Studio authoring for bug reports, user stories, and test cases with multi-format export.
2. **Online Order Integration Operations:** Multi-tenant integration workspace for UPC E-Commerce, GHC E-Commerce, and GHC Uni-Commerce order flows, draft management, server-side validation, exact JSON previews, and server-gated Production mutations.
3. **POS Diagnostics, Maintenance & Fleet Supervision:** Dual control-surface architecture comprising:
   - An always-running **Windows Agent Service** (`RmsSupportAgent.Service`) on target machines owning privileged machine execution and device identity.
   - A standalone native **WPF Desktop Application** (`RmsSupportAgent.Desktop.Wpf`) providing local users with autonomous maintenance capabilities even when disconnected.
   - A centralized **Angular Administrator Dashboard** providing real-time fleet supervision, machine health telemetry, WPF issue monitoring, and typed remote support commands.

The platform eliminates fragmented scripts, manual SQL queries, direct API calls, and unmanaged utilities while guaranteeing that privileged logic is never duplicated across local desktop and central web surfaces.

---

## 2. Business Problem

Before RMS+ Support Hub, QA and support activities were distributed across disparate scripts, manual Postman collections, and operator-specific utilities:
- Inconsistent payload construction and high environment-selection risk.
- Lack of auditability and authorization for privileged machine-level operations.
- Operational dependency on central network connectivity for store-level POS troubleshooting.
- Browser friction in retail environments (CORS, Local Network Access permissions, and local certificate installation).
- Lack of centralized fleet-wide visibility into POS machine health, software versions, and application crashes.

RMS+ Support Hub resolves these challenges by providing one governed workspace with server-owned configuration, strict safety gates, autonomous store-level desktop tooling, and centralized administrator supervision.

---

## 3. Business Objectives

- **BO-01:** Unify QA tooling, Online Order integrations, and POS support capabilities into one cohesive ecosystem.
- **BO-02:** Eliminate unauthorized or accidental Production mutations through fail-closed server-enforced security gates.
- **BO-03:** Standardize QA artifact authoring with deterministic prompt engineering and export utilities.
- **BO-04:** Provide accurate, client-specific payload authoring, validation, submission, and troubleshooting for Online Orders.
- **BO-05:** Guarantee 100% autonomous local POS diagnostics, database recovery, and service maintenance when stores are offline.
- **BO-06:** Provide centralized administrator fleet supervision, health telemetry, and issue monitoring across all store endpoints.
- **BO-07:** Enforce strict auditability and correlation for all privileged local and remote operations without duplicating business logic.
- **BO-08:** Ensure deterministic release candidates, package signature verification, and health-committed rollback lifecycles.
- **BO-09:** Maintain end-to-end traceability from business requirements to Azure DevOps work items, GitHub PRs, automated tests, and deployment evidence.

---

## 4. Stakeholders

| Role | Primary Responsibility & Need | Primary Surface |
|---|---|---|
| **QA Engineer** | Author test payloads, validate schemas, refine bugs/stories, generate test cases | QA Prompt Studio & Online Orders (Hub) |
| **QA Lead** | Govern quality criteria, verify regression suites, maintain requirement traceability | Support Hub & Azure DevOps |
| **Support Engineer** | Diagnose order failures, inspect logs, fetch Support Bundles, monitor fleet health | Angular Admin Fleet Dashboard |
| **Fleet Administrator** | Supervise estate health, monitor WPF crashes, trigger approved remote maintenance | Angular Admin Fleet Dashboard |
| **POS Technician / Operator** | Perform local diagnostics, database backup/restore, service restart, and branch reset on-site | WPF Desktop Application |
| **Architect / Technical Lead** | Protect system trust boundaries, security architecture, and communication contracts | Governance & Architecture (ADRs) |
| **Management** | Track delivery progress, system reliability, and roadmap execution | Azure DevOps & Executive Metrics |

---

## 5. Product Scope

### 5.1 QA Prompt Studio
- Bug refinement and structured defect authoring.
- User story refinement with acceptance criteria generation.
- Test case authoring with step-by-step verification.
- Deterministic export formats: Generic Markdown, Jira, and Azure DevOps.
- Deterministic quality guidance and prompt scoring.
- Client-side privacy boundary with bounded browser-local history.

### 5.2 Online Order Operations
Supported integration modules:
- **UPC E-Commerce:** Flat-order payload, branch/item/consumer lookup, submission, OrderRequests history, cancellation, same-number resend.
- **GHC E-Commerce:** GHC flat-order payload, contact/delivery fields, card/credit payment metadata, Testing submission, request history.
- **GHC Uni-Commerce:** Specialized multi-row invoice payload, invoice/return metadata, draft persistence, consumer lookup, Testing submission, request history adapter.
- **Future Modules:** OMS and Call Center (architecture-ready).

Common capabilities:
- Server-owned environment and connection resolution.
- Server-side totals, VAT calculations, and business rule validation.
- Exact compiled JSON payload preview.
- Server-enforced Production mutation unlock gate over effective HTTPS.
- Capability-aware action buttons with fail-closed status reasons.

### 5.3 POS Maintenance & Fleet Supervision

#### A. Delivered Historical Baseline (E07–E09)
*Direct Browser-to-Loopback Architecture (`https://rms-pos-agent.localhost:5001`):*
- Machine and RMS installation discovery.
- Supported RMS service health and one-use token service restart.
- Database health diagnostics, backup, and guarded restore.
- Cleanup previews and guarded branch reset protecting native RMS services.
- Safety Snapshots, incident timeline, and sanitized Support Bundle generation.
- Machine-pinned package trust, signature verification, SCM lifecycle, and rollback recovery.
- Windows Negotiate authentication, exact-origin CORS, and LNA browser policies.

#### B. Approved Future Target Architecture (CR-001, ADR-0029)
*Dual Control-Surface Architecture (WPF Standalone + Angular Admin Fleet Supervision):*
- **WPF Standalone Desktop:** Native Windows UI providing local autonomous access to all retained maintenance features over authenticated Windows Named Pipes, operating completely offline.
- **Always-Running Agent Service:** Privileged machine execution boundary, owning device identity, Named Pipe IPC listener, outbound SignalR client, WPF supervision, and offline event queue.
- **Angular Admin Fleet Supervision:** Central dashboard aggregating registered machines, Agent heartbeats, WPF application crashes/heartbeats/versions, central issues board, machine operational timelines, and allowlisted typed remote commands.
- **Shared Capability Seam:** Single transport-agnostic command/query application layer shared by local IPC and remote Hub channels—zero duplicated privileged logic.

### 5.4 Platform & Release Governance
- Testing-first default operational tier; Production policy disabled in Testing.
- Server-owned external JSON configuration outside application packages.
- Deterministic release-candidate packaging with build identity and integrity manifests.
- Multi-lane CI workflows (Support Hub CI and POS CI).
- Machine trust configuration, signer pin validation, and rollback checkpoints.

---

## 6. Business Requirements

### Core Platform & Governance (BR-001 – BR-026)
- **BR-001 Unified Workspace:** One application shell shall expose all supported central tools through a consistent navigation experience.
- **BR-002 Operational Safety:** The platform shall strictly prohibit arbitrary SQL execution, generic command shells, arbitrary filesystem browsing, and unconstrained process launching.
- **BR-003 Environment Separation:** Testing and Production shall remain completely isolated; Testing workflows shall never contact Production infrastructure.
- **BR-004 Server-owned Configuration:** Endpoints, database connections, and credentials shall be resolved exclusively server-side.
- **BR-005 Deterministic QA Authoring:** QA Prompt Studio shall produce structured, reproducible bug reports, user stories, and test cases.
- **BR-006 Client-specific Contracts:** Online Order integrations shall faithfully implement verified downstream client contracts without invented fields.
- **BR-007 Payload Preview:** Users shall be able to inspect exact server-compiled JSON payloads before submission.
- **BR-008 Server-side Calculations:** Order totals, VAT, delivery charges, and payment reconciliations shall be computed authoritatively on the backend.
- **BR-009 Controlled Lookup:** Supported modules shall provide bounded item, consumer, and branch lookups using server-owned endpoints.
- **BR-010 Request Visibility:** The platform shall provide searchable request history and detailed payload/response inspection.
- **BR-011 Safe Cancellation:** Order cancellation shall be permitted only for supported integrations and eligible order statuses.
- **BR-012 Safe Resend:** Resend shall re-dispatch authoritative stored payloads using original request identifiers per client contract.
- **BR-013 POS Local Boundary:** Privileged machine operations shall execute through the local Agent Service, not via central API relay.
- **BR-014 Windows Authorization:** POS privileged endpoints shall require Windows authentication and Administrator authorization.
- **BR-015 Secure Transport:** POS communication channels shall be strictly encrypted and authenticated.
- **BR-016 Native Service Protection:** Native RMS services and databases shall be strictly protected from accidental deletion or disruption during maintenance.
- **BR-017 Sanitized Diagnostics:** Diagnostic outputs and Support Bundles shall redact passwords, API keys, private keys, and connection strings.
- **BR-018 Safe Database Recovery:** Database backup and restore shall enforce concurrency leases, pre-flight safety checks, and rollback snapshots.
- **BR-019 Package Trust:** Agent and client packages shall enforce cryptographic signature verification, machine-pinned trust, and rollback checkpoints.
- **BR-020 Auditability:** All privileged operations shall produce durable, sanitized audit records.
- **BR-021 Deterministic Release:** Releases shall carry immutable source commit, build identity, and cryptographic integrity hashes.
- **BR-022 External Deployment Configuration:** Deployment secrets and environment configurations shall remain outside application binaries.
- **BR-023 Fail Closed:** Missing configuration, untrusted certificates, or authorization failures shall block execution rather than fall back to insecure defaults.
- **BR-024 Extensible Modules:** New integration modules shall be introduced through strongly-typed capability interfaces without platform redesign.
- **BR-025 Traceability:** Business requirements shall map bidirectionally to Azure DevOps work items, GitHub PRs, automated tests, and deployment evidence.
- **BR-026 Production Mutation Safety:** Production order mutations (send, cancel, resend) shall remain locked until unlocked via server-verified secret over effective HTTPS, enforcing Secure session cookies, HSTS, and allowlisted proxy trust.

### WPF Standalone & Admin Fleet Supervision (BR-027 – BR-040)
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

## 7. Functional Requirements

### QA Prompt Studio
- Structured bug, user story, and test case authoring.
- Multi-format exports (Markdown, Jira, Azure DevOps).
- Deterministic quality score and refinement tips.
- Browser-local storage isolation.

### Online Order Operations
- Module discovery, server-owned environment resolution, and draft persistence.
- Authoritative server-side totals, VAT calculations, and schema validation.
- JSON preview and sanitized submission dispatch.
- Server-enforced Production unlock ceremony over effective HTTPS.
- Canonical Order Requests history, filtering, detail inspection, and resend.

### POS Standalone WPF Desktop
- Native desktop shell with design tokens and machine status dashboard.
- RMS and Agent service health inspection and approved service restart.
- Database health checks, storage diagnostics, backup creation, and guarded restore.
- Diagnostic log inspection and redacted Support Bundle generation.
- Safety Snapshot management and incident timeline visualization.
- File/data cleanup preview and guarded branch reset protecting RMS services.
- Package installation, upgrade, repair, uninstall, and rollback execution.
- Searchable local audit activity history.
- Authorized Local Operator support workflows with local Windows Administrator elevation verification and confirmation modals for high-risk mutating actions.
- Full autonomous functionality in offline mode.

### POS Admin Fleet Supervision (Angular Hub)
- Registered machine inventory table with real-time online/offline status.
- Central health dashboard aggregating Agent heartbeats and fleet KPIs.
- WPF desktop health monitoring (running state, PID, heartbeat, crash alerts, version mismatch).
- Central issues board categorizing fleet alerts by severity.
- Machine detail view with hardware metrics, RMS configuration, and correlated timeline.
- Allowlisted remote typed commands (diagnostics, log fetch, Support Bundle, backup, service restart, package lifecycle).
- Server-enforced administrator RBAC and remote command policies.
- Real-time command progress streaming, cancellation, and idempotency protection.
- Fleet version distribution matrix and update campaign tracking.
- Centralized cross-machine audit trail search.

---

## 8. Non-Functional Requirements

- **Security & Authorization:** Fail-closed design; bounded Named Pipe ACLs (SYSTEM, Administrators, dedicated RMS Support Operators group) and per-command Agent authorization for local IPC; outbound SignalR for remote control; server-enforced RBAC for admins.
- **Offline Resilience:** Store-level maintenance must operate with 100% capability during network outages.
- **Process Isolation:** WPF desktop crashes must never stop or disrupt the background Agent Service.
- **Zero Privilege Duplication:** Shared application layer ensures 100% identical business rules for local and remote actions.
- **Auditability:** Bounded, durable audit records capturing caller, timestamp, machine, action, and outcome.
- **Performance:** Low CPU/memory footprint for background Agent Service; responsive native WPF UI.
- **Deterministic Deployment:** Release packages carry build identity, SHA-256 manifests, and cryptographic signatures.

---

## 9. Security and Governance Principles

1. No credentials or private keys tracked in Git repository.
2. No browser-selected database connection strings or arbitrary API URLs.
3. No generic shell, arbitrary PowerShell, generic SQL, or arbitrary process execution.
4. Production resources strictly disabled in Testing deployments.
5. Local IPC restricted to `SYSTEM`, `Administrators`, and dedicated `RMS Support Operators` group via Windows Security Descriptors, with per-command Agent authorization for elevated actions.
6. Outbound-only SignalR over HTTPS—no open listening ports on store machines.
7. Unique per-machine cryptographic device identity.
8. Remote mutating commands require verified administrator RBAC permissions.
9. Package trust enforced via canonical machine trust configuration and signer pins.
10. Native RMS services protected from accidental deletion or disruption.

---

## 10. Environment & Rollout Strategy

### Development
Local development and unit/integration test execution.

### Testing
Default operational environment. Side-by-side validation of legacy browser-direct transport, WPF desktop app, and Admin supervision on representative POS machines.

### Production
Separate authorized environment requiring:
- Authoritative external configuration and secrets.
- Real release PKI and production package signers.
- Representative-machine rehearsals for Agent Service + WPF Desktop + Admin Hub.
- Phased fleet rollout with automated rollback procedures.
- Formal go-live acceptance by Owner and Architecture Authority.

---

## 11. Current Delivery Status — 2026-08-22

### Delivered on `main` (Merge `9272041`)
- Unified Support Hub shell and QA Prompt Studio.
- Online Order shared architecture (UPC, GHC, GHC Uni-Commerce).
- Server-enforced Production mutation unlock gate (P0-F, PR #30).
- External server configuration loader and deterministic release-candidate pipeline.
- Local IIS Testing deployment and read-only acceptance.
- POS Agent foundation, diagnostics, database recovery, and maintenance slices (E07–E09).
- POS package trust, signer verification, SCM lifecycle, and rollback architecture.

### Baselined Target Architecture (CR-001, ADR-0029)
- Approved transition from browser-direct loopback to **WPF Standalone + Admin Fleet Supervision**.
- Azure DevOps Epics E16, E17, E18, E19 created and mapped.
- E11 reconciled as superseded roadmap; E12 updated to target architecture.
- Conversion roadmap (Phases 0–9) and first slice (`WPF-01`) defined.

### Planned Implementation (Phases 1–9)
- `WPF-01`: Extract shared Agent application layer + secure Named Pipe IPC foundation.
- `WPF-02`: WPF native desktop application shell and local capability parity.
- `WPF-03`: Agent-initiated SignalR connection, device identity, and fleet supervision in Angular Hub.
- `WPF-04`: Allowlisted typed remote commands, backup streaming, and admin RBAC.
- `WPF-05`: Side-by-side parity validation on representative machines and cutover.

---

## 12. Out of Scope Unless Separately Approved

- Rebuilding Online Orders or QA Prompt Studio in WPF (they remain central web applications).
- Arbitrary remote desktop / screen sharing tools.
- Generic PowerShell, CMD, or SQL interactive consoles.
- Direct live mutations or un-gated fleet deployments to Production.
- Replacing working internal Agent algorithms where reuse is proven and safe.

---

## 13. Business Acceptance Criteria

A capability is acceptable when:
1. Registration and capability metadata are verified.
2. Required server and machine configurations exist and fail closed if missing.
3. Environment safety policies permit the target operation.
4. Pre-flight diagnostics and mutation leases pass.
5. All operations obey business rules, redaction policies, and audit logging.
6. Automated unit, integration, and security regression tests pass.
7. WPF desktop application proves full parity and offline autonomy.
8. Representative-machine Testing passes with live RMS instances.
9. Formal sign-off is recorded by Architecture Authority (GPT-5.6 Sol) and Owner.

---

## 14. Traceability Hierarchy

```text
Business Requirements (BR-001..BR-040)
  └── Azure DevOps Epics (E01..E19)
        └── User Stories (US-*)
              ├── Acceptance Criteria
              ├── GitHub Pull Requests
              ├── Automated Tests (Backend, Frontend, POS, Pester)
              └── Deployment Evidence
```
