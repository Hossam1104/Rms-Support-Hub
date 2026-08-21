# Azure DevOps Rebaseline — WPF / Agent Architecture

This is the intended Azure change set. New Azure IDs are assigned during creation.

## Preserve Historical Epics

Do not rewrite delivered history:

- E07 #12845 — POS Secure Agent Foundation — keep Closed.
- E08 #12846 — POS Diagnostics & Recovery — keep Closed.
- E09 #12847 — POS Package Lifecycle & Security — keep Closed.

## Reconcile Existing Roadmap

### E11 #12849 — Local Integrated Testing Acceptance

Reclassify as **superseded roadmap / migration handoff to CR-001**.

- #12943 `[US-E11-02] Local Testing POS signing/trust boundary`
  - Close/Supersede by new E19 Testing release-trust story.
- #12947 `[US-E11-06] End-to-end Online Order + POS local smoke`
  - Close/Supersede by new E19 side-by-side parity / representative-machine acceptance.

Preserve history/comments; do not delete.

### E12 #12850 — Production Readiness & Controlled Rollout

Keep Planned/New.

Update future POS-specific stories:
- #12950 — `Real Production Agent/WPF package signer PKI`
- #12951 — `Real Testing Agent/WPF release signer PKI`
- #12952 — `Production Agent device / certificate lifecycle`
- #12953 — existing Managed Browser rollout becomes Superseded by WPF/Agent deployment policy
- #12954 — `Representative-machine Production rehearsal — Agent Service + WPF + Angular Admin`
- #12955 — `Fleet/customer deployment procedure — Agent Service + WPF bundle`
- #12956 — `Production rollback rehearsal — Agent/WPF`
- #12957 — `Production go-live acceptance — WPF/Agent supervised architecture`

Online Order Production stories stay unchanged.

## New POS Iterations

- `POS-07 - WPF Agent Architecture`
- `POS-08 - WPF Local Experience`
- `POS-09 - Admin Fleet Supervision`
- `POS-10 - WPF Migration and Rollout`

## E16 — Agent Platform Re-Architecture

Area: POS  
Iteration: POS-07  
Priority: 1

Stories:
- US-E16-01 — Inventory and parity-map existing Agent capabilities
- US-E16-02 — Extract shared Agent command/query application layer
- US-E16-03 — Define local/remote invocation context and authorization source
- US-E16-04 — Secure WPF-to-Agent Windows Named Pipe transport
- US-E16-05 — Agent-initiated SignalR Hub connection
- US-E16-06 — Machine registration and per-device identity
- US-E16-07 — Offline event queue, reconnect and state synchronization
- US-E16-08 — Unified correlation, audit, progress and cancellation contracts
- US-E16-09 — Agent/WPF version compatibility contract
- US-E16-10 — Architecture security/failure-mode test harness

BRs: BR-027, BR-030, BR-032, BR-033, BR-034, BR-035, BR-036, BR-037.

## E17 — WPF Standalone Local Operations

Area: POS  
Iteration: POS-08  
Priority: 2

Stories:
- US-E17-01 — WPF shell and local machine dashboard
- US-E17-02 — Agent/RMS service health and approved service control
- US-E17-03 — Database health and diagnostics
- US-E17-04 — Database backup/download and guarded restore
- US-E17-05 — Logs and safe Support Bundle
- US-E17-06 — Safety Snapshots and incident timeline
- US-E17-07 — Cleanup and branch-reset workflows
- US-E17-08 — Package install/upgrade/repair/uninstall
- US-E17-09 — Rollback and recovery
- US-E17-10 — Local activity/audit view
- US-E17-11 — Local Windows authorization and high-risk confirmation UX
- US-E17-12 — Offline standalone operation
- US-E17-13 — WPF functional parity acceptance matrix

BRs: BR-027, BR-028, BR-030, BR-034, BR-036, BR-038, BR-039.

## E18 — Admin Fleet Supervision & Remote Support

Area: POS  
Iteration: POS-09  
Priority: 2

Stories:
- US-E18-01 — Registered machine inventory
- US-E18-02 — Agent heartbeat and machine health dashboard
- US-E18-03 — WPF heartbeat, crash and version monitoring
- US-E18-04 — Central issues/alerts aggregation
- US-E18-05 — Machine detail and operational timeline
- US-E18-06 — Remote typed diagnostics and log collection
- US-E18-07 — Remote Support Bundle
- US-E18-08 — Remote database backup request and artifact status
- US-E18-09 — Approved remote RMS service control
- US-E18-10 — Controlled remote package lifecycle
- US-E18-11 — Admin RBAC and remote command policy
- US-E18-12 — Remote command progress, cancellation and idempotency
- US-E18-13 — Fleet version/update visibility
- US-E18-14 — Central audit search and correlation

BRs: BR-029, BR-030, BR-031, BR-032, BR-033, BR-035, BR-037, BR-038, BR-040.

## E19 — WPF Migration, Compatibility & Rollout

Area: POS  
Iteration: POS-10  
Priority: 1

Stories:
- US-E19-01 — Preserve/reuse existing E07–E09 capability contracts
- US-E19-02 — Side-by-side browser/WPF compatibility mode
- US-E19-03 — Migrate Testing release signer/trust to Agent+WPF bundle
- US-E19-04 — Service + WPF installer and upgrade path
- US-E19-05 — Existing Agent installation migration
- US-E19-06 — Agent/WPF package trust and version compatibility
- US-E19-07 — WPF crash recovery / Agent continuity proof
- US-E19-08 — Side-by-side functional/security parity validation
- US-E19-09 — Representative-machine integrated Testing acceptance
- US-E19-10 — Deprecate browser-direct privileged POS path
- US-E19-11 — Pilot deployment
- US-E19-12 — Fleet rollout and rollback procedure
- US-E19-13 — Support/admin runbooks and training
- US-E19-14 — Final migration acceptance

BRs: BR-019, BR-021, BR-027, BR-038, BR-039.

## State After Rebaseline

- E07/E08/E09 remain Closed historical evidence.
- E11 is closed/superseded as the old local-browser acceptance roadmap.
- E12 remains Planned and updated to the new end-state.
- E16–E19 are New.
- E15 Governance remains Active/Ongoing.
- No WPF implementation story is Active until Sol starts WPF-01.
- No Production story is closed or implied ready.

## First Implementation Story

Activate first:

`US-E16-02 — Extract shared Agent command/query application layer`

Then:

`US-E16-04 — Secure WPF-to-Agent Windows Named Pipe transport`

Do not start with full WPF UI migration.
