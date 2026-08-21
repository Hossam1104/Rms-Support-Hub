# RMS+ Support Hub â€” Azure DevOps Backlog Blueprint

**Azure DevOps Project:** `Rms_Support_Hub`  
**Implementation Source of Truth:** `Hossam1104/Rms-Support-Hub`  
**Prepared:** 2026-08-21

> Planning aliases below are temporary. Azure DevOps will assign the real work-item IDs.

## Status Rules

- **Done** â€” implemented and merged to `main`.
- **In Review** â€” implemented on an open PR / awaiting acceptance.
- **Planned** â€” agreed or repository-documented future work.
- **Conditional** â€” implement only when an authoritative upstream contract or data source exists.

---

# E01 â€” Platform Foundation & Unified Support Hub
**Epic Status:** Done

### US-E01-01 â€” Unified application shell
**Status:** Done  
As an operator, I want one Support Hub shell so that QA, Online Orders and POS tools are available in one workspace.

**Acceptance Criteria**
- Shared navigation is available.
- Tool routes are isolated.
- Deep links resolve.
- Shared UI controls are reused.

### US-E01-02 â€” Shared responsive design system
**Status:** Done

### US-E01-03 â€” Central API composition
**Status:** Done

### US-E01-04 â€” Health and readiness endpoints
**Status:** Done

---

# E02 â€” QA Prompt Studio
**Epic Status:** Done

### US-E02-01 â€” Bug refinement
**Status:** Done

### US-E02-02 â€” User-story refinement
**Status:** Done

### US-E02-03 â€” Test-case generation
**Status:** Done

### US-E02-04 â€” Multi-format export
**Status:** Done  
**Acceptance Criteria:** Generic Markdown, Jira-oriented and Azure DevOps-oriented formats are supported.

### US-E02-05 â€” Deterministic quality feedback
**Status:** Done

### US-E02-06 â€” Client-side privacy and bounded history
**Status:** Done

---

# E03 â€” Online Order Core Platform
**Epic Status:** Done

### US-E03-01 â€” Module registry and capability model
**Status:** Done

### US-E03-02 â€” Server-owned environment selection
**Status:** Done

### US-E03-03 â€” Draft lifecycle
**Status:** Done

### US-E03-04 â€” Server-side totals and VAT calculations
**Status:** Done

### US-E03-05 â€” Exact compiled JSON preview
**Status:** Done

### US-E03-06 â€” Server-side payload validation
**Status:** Done

### US-E03-07 â€” Generic send workflow
**Status:** Done

### US-E03-08 â€” Bounded endpoint/database diagnostics
**Status:** Done

### US-E03-09 â€” Common item/consumer lookup contracts
**Status:** Done

---

# E04 â€” UPC E-Commerce
**Epic Status:** Done

### US-E04-01 â€” UPC Testing environment
**Status:** Done

### US-E04-02 â€” UPC branch lookup
**Status:** Done

### US-E04-03 â€” UPC item lookup
**Status:** Done

### US-E04-04 â€” UPC consumer lookup
**Status:** Done

### US-E04-05 â€” UPC-specific order payload
**Status:** Done

### US-E04-06 â€” UPC order submission
**Status:** Done

### US-E04-07 â€” UPC OrderRequests list and filters
**Status:** Done

### US-E04-08 â€” UPC OrderRequest detail
**Status:** Done

### US-E04-09 â€” UPC safe cancellation
**Status:** Done

### US-E04-10 â€” UPC same-number resend
**Status:** Done

### US-E04-11 â€” UPC Production policy gate
**Status:** Done as architecture; operational Production acceptance belongs to E12.

---

# E05 â€” GHC E-Commerce
**Epic Status:** In Review â€” Draft PR #26

### US-E05-01 â€” Verify GHC Testing database schema
**Status:** In Review

**Acceptance Criteria**
- Item and consumer queries use verified Testing schema.
- Queries are bounded and parameterized.
- Schema documentation reflects verified behavior.

### US-E05-02 â€” GHC item lookup
**Status:** In Review

### US-E05-03 â€” GHC consumer lookup
**Status:** In Review

### US-E05-04 â€” Preserve GHC-specific order fields
**Status:** In Review

**Acceptance Criteria**
- GHC contact/delivery fields are supported.
- GHC-only fields do not leak into UPC payloads.

### US-E05-05 â€” GHC payment metadata
**Status:** In Review

**Acceptance Criteria**
- Card/bank metadata is supported where required.
- Credit-customer fields are supported.
- UPC behavior is unchanged.

### US-E05-06 â€” GHC request history
**Status:** In Review

### US-E05-07 â€” GHC Testing environment activation
**Status:** In Review

### US-E05-08 â€” GHC synthetic Testing send
**Status:** In Review

### US-E05-09 â€” Diagnose downstream GHC send rejection
**Status:** Planned

**Acceptance Criteria**
- Reproduce with sanitized evidence.
- Classify payload/reference-data/business-rule/downstream cause.
- Fix only if defect is inside Support Hub.
- Add regression coverage for any Support Hub fix.

---

# E06 â€” GHC Uni-Commerce
**Epic Status:** In Review / Planned

### US-E06-01 â€” Specialized invoice payload builder
**Status:** Done

### US-E06-02 â€” Complete Uni-Commerce draft persistence
**Status:** In Review

### US-E06-03 â€” Uni-Commerce Testing environment configuration
**Status:** In Review

### US-E06-04 â€” Uni-Commerce consumer lookup
**Status:** In Review

### US-E06-05 â€” Uni-Commerce request/invoice history adapter
**Status:** In Review where verified data exists

### US-E06-06 â€” Uni-Commerce synthetic Testing send
**Status:** In Review

### US-E06-07 â€” Diagnose downstream Uni-Commerce send rejection
**Status:** Planned

### US-E06-08 â€” Uni-Commerce item lookup
**Status:** Conditional  
**Acceptance Criteria:** Implement only when an authoritative compatible item source is identified.

### US-E06-09 â€” Uni-Commerce cancellation
**Status:** Conditional  
**Acceptance Criteria:** Requires verified upstream cancellation contract.

### US-E06-10 â€” Uni-Commerce resend
**Status:** Conditional  
**Acceptance Criteria:** Requires verified upstream resend contract.

---

# E07 â€” POS Secure Agent Foundation
**Epic Status:** Done

### US-E07-01 â€” Permanent RMS Support Agent service
**Status:** Done

### US-E07-02 â€” HTTPS loopback listener
**Status:** Done

### US-E07-03 â€” Windows Negotiate authentication
**Status:** Done

### US-E07-04 â€” Local Administrators authorization
**Status:** Done

### US-E07-05 â€” Exact-origin CORS
**Status:** Done

### US-E07-06 â€” Direct browser-to-Agent boundary
**Status:** Done

### US-E07-07 â€” Ownership-aware Testing provisioning
**Status:** Done

### US-E07-08 â€” Build identity and runtime ownership validation
**Status:** Done

---

# E08 â€” POS Diagnostics & Recovery
**Epic Status:** Done

### US-E08-01 â€” RMS installation discovery
**Status:** Done

### US-E08-02 â€” RMS service health
**Status:** Done

### US-E08-03 â€” RMS database diagnostics
**Status:** Done

### US-E08-04 â€” Database backup
**Status:** Done

### US-E08-05 â€” Guarded database restore
**Status:** Done

### US-E08-06 â€” DB backup downloader
**Status:** Done

### US-E08-07 â€” Cleanup preview and execution
**Status:** Done

### US-E08-08 â€” Branch reset preview and execution
**Status:** Done

### US-E08-09 â€” Operational health
**Status:** Done

### US-E08-10 â€” Incident timeline
**Status:** Done

### US-E08-11 â€” Safe Support Bundle
**Status:** Done

### US-E08-12 â€” Safety Snapshots
**Status:** Done

### US-E08-13 â€” Constrained diagnostic console
**Status:** Done

### US-E08-14 â€” Bounded Main Server profile/read workflow
**Status:** Done

---

# E09 â€” POS Package Lifecycle & Security Hardening
**Epic Status:** Done as architecture

### US-E09-01 â€” Canonical machine-owned package trust
**Status:** Done

### US-E09-02 â€” Distinct signer-pin validation
**Status:** Done

### US-E09-03 â€” Immutable startup trust snapshot
**Status:** Done

### US-E09-04 â€” Trusted package verification
**Status:** Done

### US-E09-05 â€” Install / Upgrade / Repair lifecycle
**Status:** Done

### US-E09-06 â€” Uninstall lifecycle
**Status:** Done

### US-E09-07 â€” Rollback and recovery
**Status:** Done

### US-E09-08 â€” Durable lifecycle audit
**Status:** Done

### US-E09-09 â€” Deferred security hardening remediation
**Status:** Done

---

# E10 â€” Release, CI & Testing Deployment
**Epic Status:** Done

### US-E10-01 â€” Deterministic release candidate
**Status:** Done

### US-E10-02 â€” Release integrity manifest
**Status:** Done

### US-E10-03 â€” Offline runtime validation
**Status:** Done

### US-E10-04 â€” Sanitized Testing package configuration
**Status:** Done

### US-E10-05 â€” External server-owned configuration
**Status:** Done

### US-E10-06 â€” IIS Testing deployment
**Status:** Done

### US-E10-07 â€” Exact build identity verification
**Status:** Done

### US-E10-08 â€” Backend/frontend/POS CI gates
**Status:** Done

---

# E11 â€” Local Integrated Testing Acceptance
**Epic Status:** In Review â€” Draft PR #26

### US-E11-01 â€” Protected GHC/Uni Testing configuration
**Status:** In Review

### US-E11-02 â€” Local Testing POS signing/trust boundary
**Status:** In Review

### US-E11-03 â€” Secure Support Hub local origin
**Status:** In Review

### US-E11-04 â€” POS Agent local runtime
**Status:** In Review

### US-E11-05 â€” Preserve native RMS services during cleanup
**Status:** In Review

### US-E11-06 â€” End-to-end Online Order + POS local smoke
**Status:** Planned after PR #26 acceptance and downstream fixes

---

# E12 â€” Production Readiness & Controlled Rollout
**Epic Status:** Planned

### US-E12-01 â€” Authoritative Production Support Hub configuration
**Status:** Planned

### US-E12-02 â€” Production Online Order acceptance
**Status:** Planned

### US-E12-03 â€” Real Production package signer PKI
**Status:** Planned

### US-E12-04 â€” Real Testing release signer PKI
**Status:** Planned

### US-E12-05 â€” Production certificate lifecycle
**Status:** Planned

### US-E12-06 â€” Managed browser policy rollout
**Status:** Planned where required

### US-E12-07 â€” Representative-machine Production rehearsal
**Status:** Planned

### US-E12-08 â€” Fleet/customer deployment procedure
**Status:** Planned

### US-E12-09 â€” Production rollback rehearsal
**Status:** Planned

### US-E12-10 â€” Production go-live acceptance
**Status:** Planned

---

# E13 â€” Future Online Order Integrations
**Epic Status:** Planned

### US-E13-01 â€” OMS contract discovery
**Status:** Planned

### US-E13-02 â€” OMS implementation
**Status:** Conditional on authoritative contract

### US-E13-03 â€” Call Center contract discovery
**Status:** Planned

### US-E13-04 â€” Call Center implementation
**Status:** Conditional on authoritative contract

### US-E13-05 â€” Shared module onboarding checklist
**Status:** Planned

---

# E14 â€” Operational Hardening & Observability
**Epic Status:** Planned

### US-E14-01 â€” Improve module-health reason visibility
**Status:** Planned

### US-E14-02 â€” External-config mapped-drive classification
**Status:** Planned

### US-E14-03 â€” Permission-denied external-config regression
**Status:** Planned

### US-E14-04 â€” Resolve platform-specific ACL test reliability
**Status:** Planned / verify against final PR #26 state

### US-E14-05 â€” Operational runbook consolidation
**Status:** Planned

### US-E14-06 â€” Support diagnostics UX refinement
**Status:** Planned

---

# E15 â€” Delivery Governance & Traceability
**Epic Status:** Planned

### US-E15-01 â€” Establish Azure DevOps hierarchy
**Status:** Planned

**Acceptance Criteria**
- Epics represent product capability areas.
- User Stories represent testable business outcomes.
- Historical delivered work is marked Done.
- Current PR work is Active/In Review.
- Future work remains Planned.

### US-E15-02 â€” Link User Stories to GitHub PRs
**Status:** Planned

### US-E15-03 â€” Add acceptance criteria to active work
**Status:** Planned

### US-E15-04 â€” Link validation evidence to work items
**Status:** Planned

### US-E15-05 â€” Maintain BRD-to-backlog traceability
**Status:** Planned

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

1. Create E01â€“E15 Epics.
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
