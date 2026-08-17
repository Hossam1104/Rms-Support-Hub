# GPT-5.6 SOL — Architecture & Programme Planning — Support Hub Post-Slice C Integration & Staging Readiness

MODEL: GPT-5.6 Sol
EFFORT: HIGH
ROLE: Plan
MODE: Planning only — do not modify product code, create branches, or mutate external environments

## Background

With the completion and merge of PR #21 (POS Slice C Agent trust/lifecycle) and
PR #22 (deferred hardening L-1 through L-4), the POS Agent code-hardening cycle
is complete in repository scope. External Production/fleet/PKI release gates
remain clearly defined and gated.

The core RMS+ Support Hub application contains four primary tool surfaces:
1. **Online Orders Tool**: Multi-module order creation, validation, pricing, and
   draft lifecycle (UPC live-capable with Testing database gating).
2. **Order Requests**: SQL-backed order history, detail, filtering, and resend.
3. **Prompt Studio**: Local-only, deterministic QA prompt generation and reactive forms.
4. **POS Maintenance Console**: Operations console backed by the permanent
   `RmsSupportAgent` service.

In addition, the module registry retains registered unavailable stubs for OMS
and Call Center, and manual IIS release packaging (`scripts/publish-iis.ps1`)
exists for staging distribution.

## Objective

Produce a comprehensive, structured implementation plan for the next phase of
RMS+ Support Hub product delivery and staging readiness:
1. **Registered Stub Modules Strategy**: Define architecture and capability
   contracts for OMS and Call Center stubs (or determine deprecation/readiness milestones).
2. **Staging / Testing Delivery Verification**: Establish end-to-end testing and
   validation runbooks for Support Hub release candidate packaging, offline asset
   verification, and automated build artifact generation.
3. **External Gateway & Environment Integration**: Structure prerequisites and
   mock/live boundaries for Testing environment configuration (e.g.,
   `UpcEcommerceTest` connection handling, database performance index rollout plan,
   and error envelopes).
4. **Non-blocking Backlog Debt Management**: Plan resolution for the three
   post-merge Low audit/test items (LOW-1, LOW-2, LOW-3) without destabilizing
   accepted Slice C trust boundaries.

## Scope & Constraints

- Inspect architecture docs (`docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md`,
  `docs/POS_MAINTENANCE_INTEGRATION_PLAN.md`, `docs/design-system.md`,
  `docs/api-spec.md`), backend capabilities
  (`backend/src/RmsSupportHub.Core/Capabilities/`), and frontend module definitions.
- Respect all established ADRs (ADR-0001 through ADR-0027).
- Do not plan direct Production mutation or bypass external release gates.
- Keep plan actionable, modular, and phased with clear ownership and risk classification.

## Deliverable

An actionable architecture plan artifact or report detailing:
1. Executive summary & scope boundary.
2. Capability matrix & module roadmap.
3. Staging validation and release candidate packaging specifications.
4. Phased execution schedule with model/effort assignments for subsequent implementation sessions.
