# RMS+ Support Hub — Azure DevOps Backlog Blueprint

**Azure DevOps Project:** `Rms_Support_Hub`
**Implementation Source of Truth:** `Hossam1104/Rms-Support-Hub`
**Prepared:** 2026-08-21

> Planning aliases below are temporary. Azure DevOps will assign the real work-item IDs.

## Status Rules

- **Done** — implemented and merged to `main`.
- **In Review** — implemented on an open PR / awaiting acceptance.
- **Planned** — agreed or repository-documented future work.
- **Conditional** — implement only when an authoritative upstream contract or data source exists.

## Area & Iteration Classification Strategy

To ensure effortless navigation across functional domains and delivery phases, Azure DevOps items are classified by:

1. **Area Path (Product / Functional Domain Ownership):**
   - `Rms_Support_Hub\Platform` — Unified application shell, shared infrastructure, CI/CD, deployment, and cross-domain governance.
   - `Rms_Support_Hub\QA` — Prompt Studio authoring tools, test case generation, and export utilities.
   - `Rms_Support_Hub\Online Orders` — Integration adapters, environment selection, payload authoring, and request lifecycle (UPC, GHC, Uni-Commerce, future OMS/Call Center).
   - `Rms_Support_Hub\POS` — Local POS Support Agent service, Windows loopback boundary, diagnostics, guarded restore, and machine-owned package trust lifecycle.

2. **Iteration Path (Delivery Phase / Milestone):**
   - Delivery-phase iterations (e.g. `QA-01 - Prompt Studio`, `OO-01 - Core Platform`, `POS-01 - Secure Agent Foundation`, `PLAT-01 - Platform Foundation`) reflect engineering milestones rather than calendar sprints.
   - Historical completed work remains truthfully marked as **Closed** and is readily discoverable via Area Path filters, Iteration Path milestones, team backlogs (configured with `includeChildren=true`), and Shared Queries.

---

# E01 — Platform Foundation & Unified Support Hub
**Epic Status:** Done

### US-E01-01 — Unified application shell
**Status:** Done
As an operator, I want one Support Hub shell so that QA, Online Orders and POS tools are available in one workspace.

**Acceptance Criteria**
- Shared navigation is available.
- Tool routes are isolated.
- Deep links resolve.
- Shared UI controls are reused.

### US-E01-02 — Shared responsive design system
**Status:** Done

### US-E01-03 — Central API composition
**Status:** Done

### US-E01-04 — Health and readiness endpoints
**Status:** Done

---

# E02 — QA Prompt Studio
**Epic Status:** Done

### US-E02-01 — Bug refinement
**Status:** Done

### US-E02-02 — User-story refinement
**Status:** Done

### US-E02-03 — Test-case generation
**Status:** Done

### US-E02-04 — Multi-format export
**Status:** Done
**Acceptance Criteria:** Generic Markdown, Jira-oriented and Azure DevOps-oriented formats are supported.

### US-E02-05 — Deterministic quality feedback
**Status:** Done

### US-E02-06 — Client-side privacy and bounded history
**Status:** Done

---

# E03 — Online Order Core Platform
**Epic Status:** Done

### US-E03-01 — Module registry and capability model
**Status:** Done

### US-E03-02 — Server-owned environment selection
**Status:** Done

### US-E03-03 — Draft lifecycle
**Status:** Done

### US-E03-04 — Server-side totals and VAT calculations
**Status:** Done

### US-E03-05 — Exact compiled JSON preview
**Status:** Done

### US-E03-06 — Server-side payload validation
**Status:** Done

### US-E03-07 — Generic send workflow
**Status:** Done

### US-E03-08 — Bounded endpoint/database diagnostics
**Status:** Done

### US-E03-09 — Common item/consumer lookup contracts
**Status:** Done

---

# E04 — UPC E-Commerce
**Epic Status:** Done

### US-E04-01 — UPC Testing environment
**Status:** Done

### US-E04-02 — UPC branch lookup
**Status:** Done

### US-E04-03 — UPC item lookup
**Status:** Done

### US-E04-04 — UPC consumer lookup
**Status:** Done

### US-E04-05 — UPC-specific order payload
**Status:** Done

### US-E04-06 — UPC order submission
**Status:** Done

### US-E04-07 — UPC OrderRequests list and filters
**Status:** Done

### US-E04-08 — UPC OrderRequest detail
**Status:** Done

### US-E04-09 — UPC safe cancellation
**Status:** Done

### US-E04-10 — UPC same-number resend
**Status:** Done

### US-E04-11 — UPC Production policy gate
**Status:** Done as architecture; operational Production acceptance belongs to E12.

---

# E05 — GHC E-Commerce
**Epic Status:** Active (Downstream diagnosis remains Planned)

### US-E05-01 — Verify GHC Testing database schema
**Status:** Done

**Acceptance Criteria**
- Item and consumer queries use verified Testing schema.
- Queries are bounded and parameterized.
- Schema documentation reflects verified behavior.

### US-E05-02 — GHC item lookup
**Status:** Done

### US-E05-03 — GHC consumer lookup
**Status:** Done

### US-E05-04 — Preserve GHC-specific order fields
**Status:** Done

**Acceptance Criteria**
- GHC contact/delivery fields are supported.
- GHC-only fields do not leak into UPC payloads.

### US-E05-05 — GHC payment metadata
**Status:** Done

**Acceptance Criteria**
- Card/bank metadata is supported where required.
- Credit-customer fields are supported.
- UPC behavior is unchanged.

### US-E05-06 — GHC request history
**Status:** Done

### US-E05-07 — GHC Testing environment activation
**Status:** Done

### US-E05-08 — GHC synthetic Testing send
**Status:** Done (Testing transport verified; downstream diagnosis tracked separately)

### US-E05-09 — Diagnose downstream GHC send rejection
**Status:** Planned

**Acceptance Criteria**
- Reproduce with sanitized evidence.
- Classify payload/reference-data/business-rule/downstream cause.
- Fix only if defect is inside Support Hub.
- Add regression coverage for any Support Hub fix.

---

# E06 — GHC Uni-Commerce
**Epic Status:** Active (Downstream diagnosis and conditional capabilities remain open)

### US-E06-01 — Specialized invoice payload builder
**Status:** Done

### US-E06-02 — Complete Uni-Commerce draft persistence
**Status:** Done

### US-E06-03 — Uni-Commerce Testing environment configuration
**Status:** Done

### US-E06-04 — Uni-Commerce consumer lookup
**Status:** Done

### US-E06-05 — Uni-Commerce request/invoice history adapter
**Status:** Done

### US-E06-06 — Uni-Commerce synthetic Testing send
**Status:** Done (Testing transport verified; downstream diagnosis tracked separately)

### US-E06-07 — Diagnose downstream Uni-Commerce send rejection
**Status:** Planned

### US-E06-08 — Uni-Commerce item lookup
**Status:** Conditional
**Acceptance Criteria:** Implement only when an authoritative compatible item source is identified.

### US-E06-09 — Uni-Commerce cancellation
**Status:** Conditional
**Acceptance Criteria:** Requires verified upstream cancellation contract.

### US-E06-10 — Uni-Commerce resend
**Status:** Conditional
**Acceptance Criteria:** Requires verified upstream resend contract.

---

# E07 — POS Maintenance — Secure Agent Foundation
**Epic Status:** Done

### US-E07-01 — Permanent RMS Support Agent service
**Status:** Done

### US-E07-02 — HTTPS loopback listener
**Status:** Done

### US-E07-03 — Windows Negotiate authentication
**Status:** Done

### US-E07-04 — Local Administrators authorization
**Status:** Done

### US-E07-05 — Exact-origin CORS
**Status:** Done

### US-E07-06 — Direct browser-to-Agent boundary
**Status:** Done

### US-E07-07 — Ownership-aware Testing provisioning
**Status:** Done

### US-E07-08 — Build identity and runtime ownership validation
**Status:** Done

---

# E08 — POS Maintenance — Diagnostics & Recovery
**Epic Status:** Done

### US-E08-01 — RMS installation discovery
**Status:** Done

### US-E08-02 — RMS service health
**Status:** Done

### US-E08-03 — RMS database diagnostics
**Status:** Done

### US-E08-04 — Database backup
**Status:** Done

### US-E08-05 — Guarded database restore
**Status:** Done

### US-E08-06 — DB backup downloader
**Status:** Done

### US-E08-07 — Cleanup preview and execution
**Status:** Done

### US-E08-08 — Branch reset preview and execution
**Status:** Done

### US-E08-09 — Operational health
**Status:** Done

### US-E08-10 — Incident timeline
**Status:** Done

### US-E08-11 — Safe Support Bundle
**Status:** Done

### US-E08-12 — Safety Snapshots
**Status:** Done

### US-E08-13 — Constrained diagnostic console
**Status:** Done

### US-E08-14 — Bounded Main Server profile/read workflow
**Status:** Done

---

# E09 — POS Maintenance — Package Lifecycle & Security
**Epic Status:** Done as architecture

### US-E09-01 — Canonical machine-owned package trust
**Status:** Done

### US-E09-02 — Distinct signer-pin validation
**Status:** Done

### US-E09-03 — Immutable startup trust snapshot
**Status:** Done

### US-E09-04 — Trusted package verification
**Status:** Done

### US-E09-05 — Install / Upgrade / Repair lifecycle
**Status:** Done

### US-E09-06 — Uninstall lifecycle
**Status:** Done

### US-E09-07 — Rollback and recovery
**Status:** Done

### US-E09-08 — Durable lifecycle audit
**Status:** Done

### US-E09-09 — Deferred security hardening remediation
**Status:** Done

---

# E10 — Release, CI & Testing Deployment
**Epic Status:** Done

### US-E10-01 — Deterministic release candidate
**Status:** Done

### US-E10-02 — Release integrity manifest
**Status:** Done

### US-E10-03 — Offline runtime validation
**Status:** Done

### US-E10-04 — Sanitized Testing package configuration
**Status:** Done

### US-E10-05 — External server-owned configuration
**Status:** Done

### US-E10-06 — IIS Testing deployment
**Status:** Done

### US-E10-07 — Exact build identity verification
**Status:** Done

### US-E10-08 — Backend/frontend/POS CI gates
**Status:** Done

---

# E11 — Local Integrated Testing Acceptance
**Epic Status:** Active (Release PKI and final integrated smoke acceptance remain open)

### US-E11-01 — Protected GHC/Uni Testing configuration
**Status:** Done

### US-E11-02 — Local Testing POS signing/trust boundary
**Status:** Active / Ongoing (Release-PKI/trust-material blocker remains open)

### US-E11-03 — Secure Support Hub local origin
**Status:** Done

### US-E11-04 — POS Agent local runtime
**Status:** Done

### US-E11-05 — Preserve native RMS services during cleanup
**Status:** Done

### US-E11-06 — End-to-end Online Order + POS local smoke
**Status:** Planned after PR #26 acceptance and downstream fixes

---

# E12 — Production Readiness & Controlled Rollout
**Epic Status:** Planned

### US-E12-01 — Authoritative Production Support Hub configuration
**Status:** Planned

### US-E12-02 — Production Online Order acceptance
**Status:** Planned

### US-E12-03 — Real Production package signer PKI
**Status:** Planned

### US-E12-04 — Real Testing release signer PKI
**Status:** Planned

### US-E12-05 — Production certificate lifecycle
**Status:** Planned

### US-E12-06 — Managed browser policy rollout
**Status:** Planned where required

### US-E12-07 — Representative-machine Production rehearsal
**Status:** Planned

### US-E12-08 — Fleet/customer deployment procedure
**Status:** Planned

### US-E12-09 — Production rollback rehearsal
**Status:** Planned

### US-E12-10 — Production go-live acceptance
**Status:** Planned

---

# E13 — Future Online Order Integrations
**Epic Status:** Planned

### US-E13-01 — OMS contract discovery
**Status:** Planned

### US-E13-02 — OMS implementation
**Status:** Conditional on authoritative contract

### US-E13-03 — Call Center contract discovery
**Status:** Planned

### US-E13-04 — Call Center implementation
**Status:** Conditional on authoritative contract

### US-E13-05 — Shared module onboarding checklist
**Status:** Planned

---

# E14 — Operational Hardening & Observability
**Epic Status:** Planned

### US-E14-01 — Improve module-health reason visibility
**Status:** Planned

### US-E14-02 — External-config mapped-drive classification
**Status:** Planned

### US-E14-03 — Permission-denied external-config regression
**Status:** Planned

### US-E14-04 — Resolve platform-specific ACL test reliability
**Status:** Planned / verify against final PR #26 state

### US-E14-05 — Operational runbook consolidation
**Status:** Planned

### US-E14-06 — Support diagnostics UX refinement
**Status:** Planned

### US-E14-07 — Uni-Commerce read-query timeout and consumer lookup performance hardening
**Status:** Planned

### US-E14-08 — Capability-driven GHC frontend field gating
**Status:** Planned

### US-E14-09 — Uni draft persistence and export preview resilience
**Status:** Planned

### US-E14-10 — Order-history ascending sort URL query contract
**Status:** Planned

---

# E15 — Delivery Governance & Traceability
**Epic Status:** Active

### US-E15-01 — Establish Azure DevOps hierarchy
**Status:** Done

**Acceptance Criteria**
- Epics represent product capability areas.
- User Stories represent testable business outcomes.
- Historical delivered work is marked Done.
- Current PR work is Active/In Review.
- Future work remains Planned.

### US-E15-02 — Link User Stories to GitHub PRs
**Status:** Done

### US-E15-03 — Add acceptance criteria to active work
**Status:** Done

### US-E15-04 — Link validation evidence to work items
**Status:** Done

### US-E15-05 — Maintain BRD-to-backlog traceability
**Status:** Active / Ongoing

---

## Recommended Azure DevOps Fields

For every User Story:
- Title
- Description
- Acceptance Criteria
- State
- Priority
- Tags
- Parent Epic
- GitHub PR
- Validation Evidence
- Environment
- Business Requirement IDs

Recommended tags:

`SupportHub`, `QA`, `OnlineOrders`, `UPC`, `GHC`, `UniCommerce`, `POS`, `Security`, `Testing`, `ProductionReadiness`, `Deployment`, `TechDebt`

## Priority Model

| Priority | Meaning |
|---|---|
| 1 | Security, data integrity, Production safety, blocking release |
| 2 | Core business workflow or major support capability |
| 3 | Operational improvement / non-blocking enhancement |
| 4 | Cleanup, UX refinement or low-risk technical debt |

## Recommended Creation Order

1. Create E01–E15 Epics.
2. Create historical Done stories to establish traceability.
3. Create PR #26 stories under E05, E06 and E11 as Active/In Review.
4. Make E12 Production Readiness the next major roadmap Epic.
5. Create E13 and E14 as New/Planned.
6. Link GitHub PRs and validation evidence.
7. Keep Azure work-item state synchronized with independently verified repository state.

## Important Status Notes

- Draft PR #26 must not be marked Done until independently accepted and merged.
- Conditional Uni-Commerce item/cancel/resend stories remain conditional until authoritative upstream contracts/data exist.
- Production readiness is separate from Testing readiness.
