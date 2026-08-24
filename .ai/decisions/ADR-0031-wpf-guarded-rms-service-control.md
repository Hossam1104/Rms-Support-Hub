# ADR-0031: WPF guarded RMS service control

## Status

Draft for GPT-5.6 Sol review, WPF-07, 2026-08-24

## Context

WPF-03 delivered read-only RMS and Agent service health. WPF-07 adds local
service mutation without widening the WPF trust boundary or duplicating the
existing typed service-manager implementation. The browser-direct service
control path remains intact for compatibility; WPF service control must use
Local IPC and the shared Application seam.

## Decision

- The Agent owns a fixed catalog of opaque identifiers for
  `RMS.BranchService`, `RMS.CashierService`, and `RMSServiceManager`. The
  Agent service itself is rejected by self-protection. Caller-supplied Windows
  service names, SCM handles/targets, commands, executables, and generic
  payloads are not part of the contract.
- The only mutation actions are typed Start, Stop, and Restart. Stop and
  Restart require exact confirmation tokens. WPF collects confirmation but
  the Agent validates it again at the mutation boundary.
- LocalAdministrator may mutate; LocalOperator is read-only; RemoteHub and
  unauthenticated invocation contexts cannot use this local mutation path.
  The existing Local IPC ACL, identity verification, and operation-scoped SQOS
  rules remain authoritative.
- The Agent issues a short-lived opaque mutation authorization through a typed
  Local IPC operation. Its process-local bounded store retains only a token
  fingerprint and binds the grant to the authenticated principal, opaque
  service ID, action, confirmation, and correlation ID. Consumption is atomic
  and one-use; mismatches and replays fail closed, while a completed
  idempotent retry may recover its already-recorded safe result.
- A single bounded reference-counted coordinator binds principal, service,
  action, confirmation, correlation, idempotency key, and mutation lease. A
  same-target action is rejected while active; independent targets may run
  concurrently; completed entries and idle references are pruned.
- The transport-neutral Application service records ordered requested,
  accepted, started, dispatch, and terminal audit events. It fails closed before
  dispatch if required audit cannot be recorded. It verifies the desired final
  state and reports post-dispatch timeout/cancellation or unknown state as
  OutcomeUnknown. Final audit failure cannot produce a successful result.
- WPF renders bounded progress and safe error states, confirms Stop/Restart,
  refreshes the shared health snapshot after an action, and cancels its local
  wait on shutdown without inventing an outcome. Agent controls are never
  shown for the Agent row.

## Consequences

Service control can be used offline by an authorized local administrator while
preserving one implementation seam for future approved control surfaces. The
operation can truthfully distinguish a verified state from an ambiguous state,
at the cost of requiring administrator review after a timeout or incomplete
restart. Database restore, snapshots, cleanup/reset, package lifecycle,
configuration changes, and remote/fleet service control remain out of scope.

## Validation

WPF-07 deterministic evidence: service-control Application tests 10/10, WPF
tests 65/65, Agent Integration 281/281, WPF Release build 0 warnings/0 errors;
no real RMS service mutation was performed.
