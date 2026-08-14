# POS Production Readiness — M-1 and M-2 Remediation Plan

## Role and scope

**Role:** `Plan`
**Executor:** GPT-5.6 Luna Max
**Repository:** `Hossam1104/Rms-Support-Hub`
**Scope:** ONLY the two open Medium findings (M-1, M-2) from the independent
POS first-release security & readiness review. Nothing else.

Produce an executable implementation plan for the two remaining Production
blockers identified in
`docs/reviews/POS_FIRST_RELEASE_SECURITY_REVIEW_2026-08-14.md`. Do not
implement code in this task — planning only. Do not touch, re-open, or
re-scope any of the Low/Informational findings already remediated on `main`
(PR #8, commit `2c8d664`) — those are closed. Do not start any other POS
feature work. Do not begin implementation of M-1 or M-2 without an accepted
plan and explicit owner sign-off, since both require production managed-fleet
architecture decisions with real security and operational consequences.

**Testing-environment first release (INT-06I + INT-07 + INT-08 + INT-13)
remains APPROVED and is not affected by this task.** This task exists solely
to close the gap to Production/customer-deployment approval.

## Findings to plan for

### M-1 — Managed-endpoint browser policy

Current state: `scripts/PosAgentWindowsProvisioning.psm1` provisions
Chrome/Edge exact-origin IWA and block policies via local Administrator-driven
registry writes on a single representative Testing device
(`docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`). This model does not
scale to or secure a Production fleet.

Plan must cover:
- A managed-fleet delivery mechanism (e.g., GPO, Intune, or equivalent MDM)
  that applies the same exact-origin IWA and URL block/allow policy
  guarantees already validated in Testing — no wildcard or regex origin
  matches, no weakening of the policies validated in INT-13.
- How policy drift/removal is detected or prevented fleet-wide.
- Migration path from the current single-device script-based model to the
  fleet mechanism, without weakening Testing-environment provisioning
  (`scripts/PosAgentWindowsProvisioning.psm1` and its Pester coverage in
  `scripts/tests/PosAgentWindowsProvisioning.Tests.ps1` must remain valid for
  Testing use).
- Rollback/removal story for the managed policy at fleet scale.

### M-2 — Production certificate lifecycle

Current state: `scripts/PosSupportHubProvisioning.psm1` and
`scripts/setup-pos-agent-testing.ps1` create a self-managed, non-exportable,
machine-local certificate suitable for exactly one Testing device. There is
no issuance, renewal, distribution, or revocation lifecycle defined for
Production.

Plan must cover:
- Certificate issuance authority and trust chain for Production (internal CA
  vs. other mechanism) — must not silently rely on the Testing self-signed
  model.
- Renewal cadence and automation before expiry, with no manual-only fallback
  as the sole safety net at fleet scale.
- Distribution mechanism to Production endpoints consistent with whatever
  fleet delivery mechanism M-1 selects (do not design these two independently
  of each other — they will likely share the same managed-endpoint delivery
  path).
- Revocation procedure for a compromised or decommissioned endpoint.
- Impact on the existing loopback-only, exact-origin CORS/Negotiate model
  (`pos/src/RmsSupportHub.Pos.Agent`) — the plan must not alter that model's
  security guarantees, only how the certificate backing it is issued and
  rotated.

## Mandatory startup

1. Read `TASK.md` (this file).
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `docs/reviews/POS_FIRST_RELEASE_SECURITY_REVIEW_2026-08-14.md` in
   full.
5. Read `docs/POS_MAINTENANCE_INTEGRATION_READINESS.md` (release approval
   scope section) and `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`.
6. Read `scripts/PosAgentWindowsProvisioning.psm1`,
   `scripts/PosSupportHubProvisioning.psm1`, and
   `scripts/setup-pos-agent-testing.ps1` to understand the current
   single-device model being replaced for Production.
7. Do not read unrelated POS history (INT-02 through INT-08 planning
   documents) unless a specific architectural question requires it.

## Non-goals

- No production managed-fleet architecture is to be *built* in this task —
  plan only.
- No change to Testing-environment provisioning, evidence, or approval
  status.
- No re-litigation of the six remediated Low findings or I-1/I-2.
- No new POS feature surface (endpoints, contracts, UI).

## Completion response

Return only:

### Result
Planning Completed (or Blocked).

### Plan
Concrete, sequenced implementation plan for M-1 and M-2, including open
decisions that require owner sign-off before implementation (e.g., choice of
MDM/GPO tooling, choice of certificate authority) — do not make these
decisions unilaterally where they carry organizational/procurement
consequences beyond this repository.

### Risks
Security or operational risks the plan must guard against, and how.

### Remaining
Any information or access needed before implementation can begin.
