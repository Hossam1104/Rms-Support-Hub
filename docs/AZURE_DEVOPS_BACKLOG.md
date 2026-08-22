# RMS+ Support Hub — Azure DevOps Backlog Blueprint

**Azure DevOps Project:** `Rms_Support_Hub`
**Implementation Source of Truth:** `Hossam1104/Rms-Support-Hub`
**Prepared:** 2026-08-22
**Architecture Rebaseline:** Post PR #30 / CR-001 / ADR-0029

## Status Rules

- **Done** — implemented and merged to `main` (Closed).
- **Active** — actively in progress / governance.
- **Planned / New** — agreed architecture target for future implementation.
- **Conditional** — implement only when an authoritative upstream contract or data source exists.
- **Superseded** — historical roadmap item replaced by an updated architecture contract.

## Area & Iteration Classification Strategy

Azure DevOps items are classified by:

1. **Area Path (Functional Domain Ownership):**
   - `Rms_Support_Hub\Platform` — Unified application shell, shared infrastructure, CI/CD, deployment, and cross-domain governance.
   - `Rms_Support_Hub\QA` — Prompt Studio authoring tools, test case generation, and export utilities.
   - `Rms_Support_Hub\Online Orders` — Integration adapters, environment selection, payload authoring, and request lifecycle (UPC, GHC, Uni-Commerce, future OMS/Call Center).
   - `Rms_Support_Hub\POS` — Local POS Support Agent service, Windows Named Pipes IPC, WPF standalone desktop, SignalR outbound Hub connectivity, and machine-owned package trust lifecycle.

2. **Iteration Path (Delivery Phase / Milestone):**
   - **QA Iterations:** `QA-01 - Prompt Studio`, `QA-02 - Future Enhancements`
   - **Online Orders Iterations:** `OO-01 - Core Platform`, `OO-02 - UPC E-Commerce`, `OO-03 - GHC E-Commerce`, `OO-04 - GHC Uni-Commerce`, `OO-05 - Integrated Testing`, `OO-06 - Production Readiness`, `OO-07 - Future Integrations`
   - **POS Iterations:**
     - `POS-01 - Secure Agent Foundation` (Historical / Closed)
     - `POS-02 - Diagnostics and Recovery` (Historical / Closed)
     - `POS-03 - Package Lifecycle and Security` (Historical / Closed)
     - `POS-04 - Local Integration and Acceptance` (Historical / Closed)
     - `POS-05 - Production Readiness` (Planned)
     - `POS-06 - Operational Hardening` (Planned)
     - `POS-07 - WPF Agent Architecture` (New Target — E16)
     - `POS-08 - WPF Local Experience` (New Target — E17)
     - `POS-09 - Admin Fleet Supervision` (New Target — E18)
     - `POS-10 - WPF Migration and Rollout` (New Target — E19)
   - **Platform Iterations:** `PLAT-01 - Platform Foundation`, `PLAT-02 - Release and Testing Deployment`, `PLAT-03 - Integration Acceptance`, `PLAT-04 - Production Readiness`, `PLAT-05 - Operational Hardening`, `PLAT-06 - Governance and Traceability`

---

# E01 — Platform Foundation & Unified Support Hub (#12839)
**Epic Status:** Done (Closed)

### US-E01-01 — Unified application shell (#12854)
**Status:** Done (Closed)

### US-E01-02 — Shared responsive design system (#12855)
**Status:** Done (Closed)

### US-E01-03 — Central API composition (#12856)
**Status:** Done (Closed)

### US-E01-04 — Health and readiness endpoints (#12857)
**Status:** Done (Closed)

---

# E02 — QA Prompt Studio (#12840)
**Epic Status:** Done (Closed)

### US-E02-01 — Bug refinement (#12858)
**Status:** Done (Closed)

### US-E02-02 — User-story refinement (#12859)
**Status:** Done (Closed)

### US-E02-03 — Test-case generation (#12860)
**Status:** Done (Closed)

### US-E02-04 — Multi-format export (#12861)
**Status:** Done (Closed)

### US-E02-05 — Deterministic quality feedback (#12862)
**Status:** Done (Closed)

### US-E02-06 — Client-side privacy and bounded history (#12863)
**Status:** Done (Closed)

---

# E03 — Online Order Core Platform (#12841)
**Epic Status:** Done (Closed)

### US-E03-01 — Module registry and capability model (#12864)
**Status:** Done (Closed)

### US-E03-02 — Server-owned environment selection (#12865)
**Status:** Done (Closed)

### US-E03-03 — Draft lifecycle (#12866)
**Status:** Done (Closed)

### US-E03-04 — Server-side totals and VAT calculations (#12867)
**Status:** Done (Closed)

### US-E03-05 — Exact compiled JSON preview (#12868)
**Status:** Done (Closed)

### US-E03-06 — Server-side payload validation (#12869)
**Status:** Done (Closed)

### US-E03-07 — Generic send workflow (#12870)
**Status:** Done (Closed)

### US-E03-08 — Bounded endpoint/database diagnostics (#12871)
**Status:** Done (Closed)

### US-E03-09 — Common item/consumer lookup contracts (#12872)
**Status:** Done (Closed)

---

# E04 — UPC E-Commerce (#12842)
**Epic Status:** Done (Closed)

### US-E04-01 — UPC Testing environment (#12873)
**Status:** Done (Closed)

### US-E04-02 — UPC branch lookup (#12874)
**Status:** Done (Closed)

### US-E04-03 — UPC item lookup (#12875)
**Status:** Done (Closed)

### US-E04-04 — UPC consumer lookup (#12876)
**Status:** Done (Closed)

### US-E04-05 — UPC-specific order payload (#12877)
**Status:** Done (Closed)

### US-E04-06 — UPC order submission (#12878)
**Status:** Done (Closed)

### US-E04-07 — UPC OrderRequests list and filters (#12879)
**Status:** Done (Closed)

### US-E04-08 — UPC OrderRequest detail (#12880)
**Status:** Done (Closed)

### US-E04-09 — UPC safe cancellation (#12881)
**Status:** Done (Closed)

### US-E04-10 — UPC same-number resend (#12882)
**Status:** Done (Closed)

### US-E04-11 — UPC Production policy gate (#12883)
**Status:** Done (Closed)

---

# E05 — GHC E-Commerce (#12843)
**Epic Status:** Done (Closed)

### US-E05-01 — Verify GHC Testing database schema (#12884)
**Status:** Done (Closed)

### US-E05-02 — GHC item lookup (#12885)
**Status:** Done (Closed)

### US-E05-03 — GHC consumer lookup (#12886)
**Status:** Done (Closed)

### US-E05-04 — Preserve GHC-specific order fields (#12887)
**Status:** Done (Closed)

### US-E05-05 — GHC payment metadata (#12888)
**Status:** Done (Closed)

### US-E05-06 — GHC request history (#12889)
**Status:** Done (Closed)

### US-E05-07 — GHC Testing environment activation (#12890)
**Status:** Done (Closed)

### US-E05-08 — GHC synthetic Testing send (#12891)
**Status:** Done (Closed)

### US-E05-09 — Diagnose downstream GHC send rejection (#12892)
**Status:** Done (Closed / Diagnosed)

---

# E06 — GHC Uni-Commerce (#12844)
**Epic Status:** Active (3 child stories Conditional/New)

### US-E06-01 — Specialized invoice payload builder (#12893)
**Status:** Done (Closed)

### US-E06-02 — Complete Uni-Commerce draft persistence (#12894)
**Status:** Done (Closed)

### US-E06-03 — Uni-Commerce Testing environment configuration (#12895)
**Status:** Done (Closed)

### US-E06-04 — Uni-Commerce consumer lookup (#12896)
**Status:** Done (Closed)

### US-E06-05 — Uni-Commerce request/invoice history adapter (#12897)
**Status:** Done (Closed)

### US-E06-06 — Uni-Commerce synthetic Testing send (#12898)
**Status:** Done (Closed)

### US-E06-07 — Diagnose downstream Uni-Commerce send rejection (#12899)
**Status:** Done (Closed / Diagnosed)

### US-E06-08 — Uni-Commerce item lookup (#12900)
**Status:** Conditional (New)

### US-E06-09 — Uni-Commerce cancellation (#12901)
**Status:** Conditional (New)

### US-E06-10 — Uni-Commerce resend (#12902)
**Status:** Conditional (New)

---

# E07 — POS Maintenance — Secure Agent Foundation (#12845)
**Epic Status:** Done (Closed Historical Baseline)

### US-E07-01 — Permanent RMS Support Agent service (#12903)
**Status:** Done (Closed)

### US-E07-02 — HTTPS loopback listener (#12904)
**Status:** Done (Closed)

### US-E07-03 — Windows Negotiate authentication (#12905)
**Status:** Done (Closed)

### US-E07-04 — Local Administrators authorization (#12906)
**Status:** Done (Closed)

### US-E07-05 — Exact-origin CORS (#12907)
**Status:** Done (Closed)

### US-E07-06 — Direct browser-to-Agent boundary (#12908)
**Status:** Done (Closed)

### US-E07-07 — Ownership-aware Testing provisioning (#12909)
**Status:** Done (Closed)

### US-E07-08 — Build identity and runtime ownership validation (#12910)
**Status:** Done (Closed)

---

# E08 — POS Maintenance — Diagnostics & Recovery (#12846)
**Epic Status:** Done (Closed Historical Baseline)

### US-E08-01 — RMS installation discovery (#12911)
**Status:** Done (Closed)

### US-E08-02 — RMS service health (#12912)
**Status:** Done (Closed)

### US-E08-03 — RMS database diagnostics (#12913)
**Status:** Done (Closed)

### US-E08-04 — Database backup (#12914)
**Status:** Done (Closed)

### US-E08-05 — Guarded database restore (#12915)
**Status:** Done (Closed)

### US-E08-06 — DB backup downloader (#12916)
**Status:** Done (Closed)

### US-E08-07 — Cleanup preview and execution (#12917)
**Status:** Done (Closed)

### US-E08-08 — Branch reset preview and execution (#12918)
**Status:** Done (Closed)

### US-E08-09 — Operational health (#12919)
**Status:** Done (Closed)

### US-E08-10 — Incident timeline (#12920)
**Status:** Done (Closed)

### US-E08-11 — Safe Support Bundle (#12921)
**Status:** Done (Closed)

### US-E08-12 — Safety Snapshots (#12922)
**Status:** Done (Closed)

### US-E08-13 — Constrained diagnostic console (#12923)
**Status:** Done (Closed)

### US-E08-14 — Bounded Main Server profile/read workflow (#12924)
**Status:** Done (Closed)

---

# E09 — POS Maintenance — Package Lifecycle & Security (#12847)
**Epic Status:** Done (Closed Historical Baseline)

### US-E09-01 — Canonical machine-owned package trust (#12925)
**Status:** Done (Closed)

### US-E09-02 — Distinct signer-pin validation (#12926)
**Status:** Done (Closed)

### US-E09-03 — Immutable startup trust snapshot (#12927)
**Status:** Done (Closed)

### US-E09-04 — Trusted package verification (#12928)
**Status:** Done (Closed)

### US-E09-05 — Install / Upgrade / Repair lifecycle (#12929)
**Status:** Done (Closed)

### US-E09-06 — Uninstall lifecycle (#12930)
**Status:** Done (Closed)

### US-E09-07 — Rollback and recovery (#12931)
**Status:** Done (Closed)

### US-E09-08 — Durable lifecycle audit (#12932)
**Status:** Done (Closed)

### US-E09-09 — Deferred security hardening remediation (#12933)
**Status:** Done (Closed)

---

# E10 — Release, CI & Testing Deployment (#12848)
**Epic Status:** Done (Closed)

### US-E10-01 — Deterministic release candidate (#12934)
**Status:** Done (Closed)

### US-E10-02 — Release integrity manifest (#12935)
**Status:** Done (Closed)

### US-E10-03 — Offline runtime validation (#12936)
**Status:** Done (Closed)

### US-E10-04 — Sanitized Testing package configuration (#12937)
**Status:** Done (Closed)

### US-E10-05 — External server-owned configuration (#12938)
**Status:** Done (Closed)

### US-E10-06 — IIS Testing deployment (#12939)
**Status:** Done (Closed)

### US-E10-07 — Exact build identity verification (#12940)
**Status:** Done (Closed)

### US-E10-08 — Backend/frontend/POS CI gates (#12941)
**Status:** Done (Closed)

---

# E11 — Local Integrated Testing Acceptance (#12849)
**Epic Status:** Closed (Superseded by CR-001 / Epics E16–E19)

### US-E11-01 — Protected GHC/Uni Testing configuration (#12942)
**Status:** Done (Closed)

### US-E11-02 — Local Testing POS signing/trust boundary (#12943)
**Status:** Closed (Superseded by US-E19-03 / #13060)

### US-E11-03 — Secure Support Hub local origin (#12944)
**Status:** Done (Closed)

### US-E11-04 — POS Agent local runtime (#12945)
**Status:** Done (Closed)

### US-E11-05 — Preserve native RMS services during cleanup (#12946)
**Status:** Done (Closed)

### US-E11-06 — End-to-end Online Order + POS local smoke (#12947)
**Status:** Closed (Superseded by US-E19-09 / #13066)

---

# E12 — Production Readiness & Controlled Rollout (#12850)
**Epic Status:** Planned (New)

### US-E12-01 — Authoritative Production Support Hub configuration (#12948)
**Status:** Planned (New)

### US-E12-02 — Production Online Order acceptance (#12949)
**Status:** Planned (New)
**Traceability:** BR-003, BR-006, BR-026.

### US-E12-03 — Real Production Agent/WPF package signer PKI (#12950)
**Status:** Planned (New)
**Traceability:** BR-015, BR-019.

### US-E12-04 — Real Testing release signer PKI (#12951)
**Status:** Planned (New)
**Traceability:** BR-019, BR-021.

### US-E12-05 — Production Agent device / certificate lifecycle (#12952)
**Status:** Planned (New)
**Traceability:** BR-015, BR-019, BR-033.

### US-E12-06 — Managed browser policy rollout (#12953)
**Status:** Closed (Superseded by CR-001 / ADR-0029)

### US-E12-07 — Representative-machine Production rehearsal — Agent Service + WPF + Angular Admin (#12954)
**Status:** Planned (New)
**Traceability:** BR-021, BR-025, BR-027.

### US-E12-08 — Fleet/customer deployment procedure — Agent Service + WPF bundle (#12955)
**Status:** Planned (New)
**Traceability:** BR-021, BR-038.

### US-E12-09 — Production rollback rehearsal — Agent/WPF (#12956)
**Status:** Planned (New)
**Traceability:** BR-019, BR-021, BR-038.

### US-E12-10 — Production go-live acceptance — WPF/Agent supervised architecture (#12957)
**Status:** Planned (New)
**Traceability:** BR-003, BR-025, BR-027, BR-039.

---

# E13 — Future Online Order Integrations (#12851)
**Epic Status:** Planned (New)

### US-E13-01 — OMS contract discovery (#12958)
**Status:** Planned (New)

### US-E13-02 — OMS implementation (#12959)
**Status:** Conditional (New)

### US-E13-03 — Call Center contract discovery (#12960)
**Status:** Planned (New)

### US-E13-04 — Call Center implementation (#12961)
**Status:** Conditional (New)

### US-E13-05 — Shared module onboarding checklist (#12962)
**Status:** Planned (New)

---

# E14 — Operational Hardening & Observability (#12852)
**Epic Status:** Planned (New)

### US-E14-01 — Improve module-health reason visibility (#12963)
**Status:** Planned (New)

### US-E14-02 — External-config mapped-drive classification (#12964)
**Status:** Planned (New)

### US-E14-03 — Permission-denied external-config regression (#12965)
**Status:** Planned (New)

### US-E14-04 — Resolve platform-specific ACL test reliability (#12966)
**Status:** Planned (New)

### US-E14-05 — Operational runbook consolidation (#12967)
**Status:** Planned (New)

### US-E14-06 — Support diagnostics UX refinement (#12968)
**Status:** Planned (New)

### US-E14-07 — Uni-Commerce read-query timeout and consumer lookup performance hardening (#12974)
**Status:** Planned (New)

### US-E14-08 — Capability-driven GHC frontend field gating (#12975)
**Status:** Planned (New)

### US-E14-09 — Uni draft persistence and export preview resilience (#12976)
**Status:** Planned (New)

### US-E14-10 — Order-history ascending sort URL query contract (#12977)
**Status:** Planned (New)

---

# E15 — Delivery Governance & Traceability (#12853)
**Epic Status:** Active (Ongoing Governance)

### US-E15-01 — Establish Azure DevOps hierarchy (#12969)
**Status:** Done (Closed)

### US-E15-02 — Link User Stories to GitHub PRs (#12970)
**Status:** Done (Closed)

### US-E15-03 — Add acceptance criteria to active work (#12971)
**Status:** Done (Closed)

### US-E15-04 — Link validation evidence to work items (#12972)
**Status:** Done (Closed)

### US-E15-05 — Maintain BRD-to-backlog traceability (#12973)
**Status:** Active (Ongoing)

---

# E16 — Agent Platform Re-Architecture (#13017)
**Epic Status:** New (Approved under CR-001 / ADR-0029)
**Area:** `Rms_Support_Hub\POS`
**Iteration:** `Rms_Support_Hub\POS-07 - WPF Agent Architecture`
**Priority:** 1

### US-E16-01 — Inventory and parity-map existing Agent capabilities (#13021)
**Status:** New | **Priority:** 2 | **Traceability:** BR-027, BR-030
**Acceptance Criteria:**
- All E07-E09 endpoints, diagnostics, service controls, backup/restore, cleanup, bundle, snapshots, and package lifecycle handlers are inventoried with contract schemas.
- Every capability is mapped to a target shared command/query handler.
- No capability is missed or dropped.

### US-E16-02 — Extract shared Agent command/query application layer (#13022)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-030
**Acceptance Criteria:**
- Shared command/query handlers exist in a reusable application layer.
- Handlers are transport-agnostic (usable by Named Pipes, SignalR, or HTTP).
- Existing HTTP endpoints delegate to shared handlers without behavioral drift.
- Mutation leases, idempotency guards, and bounded redaction are preserved.

### US-E16-03 — Define local/remote invocation context and authorization source (#13023)
**Status:** New | **Priority:** 1 | **Traceability:** BR-030, BR-034, BR-035, BR-040
**Acceptance Criteria:**
- InvocationContext captures caller source (LocalWpf vs RemoteHub), identity, and correlation ID.
- InvocationContext distinguishes authenticated local operator/admin authority and remote admin/device authority.
- Per-command authorization evaluates required operation privilege against the authenticated caller context.
- Authorization fails closed when context is missing, invalid, or insufficient.

### US-E16-04 — Secure WPF-to-Agent Windows Named Pipe transport (#13024)
**Status:** New | **Priority:** 1 | **Traceability:** BR-028, BR-034
**Acceptance Criteria:**
- Agent exposes secure Windows Named Pipe listener with restricted ACLs permitting only SYSTEM, Local Administrators, and the dedicated RMS Support Operators group.
- Unauthorized local identities (Everyone, Guests, anonymous, unrestricted Authenticated Users) are rejected fail-closed at IPC connection.
- Caller Windows identity is authenticated; anonymous IPC is prohibited.
- Per-command Agent application layer authorization remains mandatory for all typed operations.

### US-E16-05 — Agent-initiated SignalR Hub connection (#13025)
**Status:** New | **Priority:** 1 | **Traceability:** BR-032, BR-033
**Acceptance Criteria:**
- Agent initiates outbound HTTPS SignalR connection to Hub.
- No inbound listening ports required on POS machine.
- Connection handles network drop, reconnection, and backoff gracefully.
- Hub tracks real-time connected/disconnected state per machine.

### US-E16-06 — Machine registration and per-device identity (#13026)
**Status:** New | **Priority:** 1 | **Traceability:** BR-032, BR-033
**Acceptance Criteria:**
- Each POS machine registers with unique device ID and cryptographic credentials.
- Hub authenticates Agent device identity during SignalR handshake.
- Unregistered or revoked agents are rejected fail-closed.
- Device credentials stored securely outside tracked repository files.

### US-E16-07 — Offline event queue, reconnect and state synchronization (#13027)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-036
**Acceptance Criteria:**
- Agent buffers events during Hub outage up to configured bounded limit.
- FIFO delivery with oldest-drop or fail-closed policy on overflow.
- Pending events synchronize automatically upon SignalR reconnection.
- Duplicate delivery is prevented by message correlation/idempotency.

### US-E16-08 — Unified correlation, audit, progress and cancellation contracts (#13028)
**Status:** New | **Priority:** 1 | **Traceability:** BR-030, BR-035, BR-037
**Acceptance Criteria:**
- Operations emit correlated audit events with matching schema for local and remote paths.
- Progress notifications stream to initiating client in real-time.
- Cancellation tokens propagate cleanly to running handlers.
- Audit records include caller identity, machine ID, operation, and outcome.

### US-E16-09 — Agent/WPF version compatibility contract (#13029)
**Status:** New | **Priority:** 2 | **Traceability:** BR-031, BR-038
**Acceptance Criteria:**
- Named Pipe handshake validates protocol version and assembly compatibility.
- Major version mismatch blocks execution with clear upgrade instruction.
- WPF reports version mismatch telemetry to Agent for fleet visibility.
- Backward-compatible minor versions operate without disruption.

### US-E16-10 — Architecture security and failure-mode test harness (#13030)
**Status:** New | **Priority:** 1 | **Traceability:** BR-030, BR-034, BR-035, BR-036
**Acceptance Criteria:**
- Automated tests prove unauthorized IPC connections fail closed.
- SignalR disconnect/reconnect and offline queue recovery are verified.
- Concurrent local/remote lease collision handling is validated.
- Failure mode harness runs cleanly in CI.

---

# E17 — WPF Standalone Local Operations (#13018)
**Epic Status:** New (Approved under CR-001 / ADR-0029)
**Area:** `Rms_Support_Hub\POS`
**Iteration:** `Rms_Support_Hub\POS-08 - WPF Local Experience`
**Priority:** 2

### US-E17-01 — WPF shell and local machine dashboard (#13031)
**Status:** New | **Priority:** 2 | **Traceability:** BR-027, BR-028

### US-E17-02 — Agent/RMS service health and approved service control (#13032)
**Status:** New | **Priority:** 2 | **Traceability:** BR-027, BR-028, BR-030

### US-E17-03 — Database health and diagnostics (#13033)
**Status:** New | **Priority:** 2 | **Traceability:** BR-027, BR-028

### US-E17-04 — Database backup/download and guarded restore (#13034)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-030

### US-E17-05 — Logs and safe Support Bundle (#13035)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-030

### US-E17-06 — Safety Snapshots and incident timeline (#13036)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-030

### US-E17-07 — Cleanup and branch-reset workflows (#13037)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-030

### US-E17-08 — Package install/upgrade/repair/uninstall (#13038)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-038

### US-E17-09 — Rollback and recovery (#13039)
**Status:** New | **Priority:** 1 | **Traceability:** BR-028, BR-038

### US-E17-10 — Local activity/audit view (#13040)
**Status:** New | **Priority:** 2 | **Traceability:** BR-028, BR-037

### US-E17-11 — Local Windows authorization and high-risk confirmation UX (#13041)
**Status:** New | **Priority:** 1 | **Traceability:** BR-028, BR-034
**Acceptance Criteria:**
- High-risk mutating operations (database restore, branch reset, cleanup execution, package install/upgrade/repair/uninstall, rollback/recovery, privileged service control) require local Administrator/elevated authorization and explicit confirmation.
- Authorized Local Operators can execute non-destructive diagnostic, health, log, backup, and support bundle operations without elevation.
- Non-admin users attempting elevated operations receive clear permission-denied feedback and elevation guidance.
- All authorization decisions and high-risk operation confirmations are durably audited.

### US-E17-12 — Offline standalone operation (#13042)
**Status:** New | **Priority:** 1 | **Traceability:** BR-028, BR-036

### US-E17-13 — WPF functional parity acceptance matrix (#13043)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-028, BR-039

---

# E18 — Admin Fleet Supervision & Remote Support (#13019)
**Epic Status:** New (Approved under CR-001 / ADR-0029)
**Area:** `Rms_Support_Hub\POS`
**Iteration:** `Rms_Support_Hub\POS-09 - Admin Fleet Supervision`
**Priority:** 2

### US-E18-01 — Registered machine inventory (#13044)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-033

### US-E18-02 — Agent heartbeat and machine health dashboard (#13045)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-031

### US-E18-03 — WPF heartbeat, crash and version monitoring (#13046)
**Status:** New | **Priority:** 1 | **Traceability:** BR-029, BR-031, BR-038

### US-E18-04 — Central issues/alerts aggregation (#13047)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-031

### US-E18-05 — Machine detail and operational timeline (#13048)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-037

### US-E18-06 — Remote typed diagnostics and log collection (#13049)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-035

### US-E18-07 — Remote Support Bundle (#13050)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-035

### US-E18-08 — Remote database backup request and artifact status (#13051)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-035

### US-E18-09 — Approved remote RMS service control (#13052)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-035, BR-040

### US-E18-10 — Controlled remote package lifecycle (#13053)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-035, BR-038, BR-040

### US-E18-11 — Admin RBAC and remote command policy (#13054)
**Status:** New | **Priority:** 1 | **Traceability:** BR-029, BR-040

### US-E18-12 — Remote command progress, cancellation and idempotency (#13055)
**Status:** New | **Priority:** 1 | **Traceability:** BR-029, BR-035

### US-E18-13 — Fleet version/update visibility (#13056)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-038

### US-E18-14 — Central audit search and correlation (#13057)
**Status:** New | **Priority:** 2 | **Traceability:** BR-029, BR-037

---

# E19 — WPF Migration, Compatibility & Rollout (#13020)
**Epic Status:** New (Approved under CR-001 / ADR-0029)
**Area:** `Rms_Support_Hub\POS`
**Iteration:** `Rms_Support_Hub\POS-10 - WPF Migration and Rollout`
**Priority:** 1

### US-E19-01 — Preserve/reuse existing E07-E09 capability contracts (#13058)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-030

### US-E19-02 — Side-by-side browser/WPF compatibility mode (#13059)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-039

### US-E19-03 — Migrate Testing release signer/trust to Agent+WPF bundle (#13060)
**Status:** New | **Priority:** 1 | **Traceability:** BR-019, BR-038
*Note: Supersedes old roadmap story #12943.*

### US-E19-04 — Service + WPF installer and upgrade path (#13061)
**Status:** New | **Priority:** 2 | **Traceability:** BR-038

### US-E19-05 — Existing Agent installation migration (#13062)
**Status:** New | **Priority:** 2 | **Traceability:** BR-038, BR-039

### US-E19-06 — Agent/WPF package trust and version compatibility (#13063)
**Status:** New | **Priority:** 1 | **Traceability:** BR-019, BR-038

### US-E19-07 — WPF crash recovery / Agent continuity proof (#13064)
**Status:** New | **Priority:** 1 | **Traceability:** BR-031

### US-E19-08 — Side-by-side functional/security parity validation (#13065)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-039

### US-E19-09 — Representative-machine integrated Testing acceptance (#13066)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-039
*Note: Supersedes old roadmap story #12947.*

### US-E19-10 — Deprecate browser-direct privileged POS path (#13067)
**Status:** New | **Priority:** 2 | **Traceability:** BR-039

### US-E19-11 — Pilot deployment (#13068)
**Status:** New | **Priority:** 2 | **Traceability:** BR-039

### US-E19-12 — Fleet rollout and rollback procedure (#13069)
**Status:** New | **Priority:** 1 | **Traceability:** BR-038, BR-039

### US-E19-13 — Support/admin runbooks and training (#13070)
**Status:** New | **Priority:** 3 | **Traceability:** BR-027, BR-029

### US-E19-14 — Final migration acceptance (#13071)
**Status:** New | **Priority:** 1 | **Traceability:** BR-027, BR-039
