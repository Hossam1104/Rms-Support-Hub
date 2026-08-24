# Current Project State

- **Updated:** 2026-08-24
- **Branch:** `feat/wpf-07-guarded-rms-service-control`
- **WPF-07 status:** Implementation complete in commit `ac1b3ef` on this branch;
  Draft PR and Sol acceptance are still pending. Do not merge WPF-07 or begin
  WPF-08.
- **WPF-07 baseline:** Started from `main` at WPF-06 merge
  `7af1e54042dbe31ef86a34d6ec687da66539be62`, after exact-head acceptance of
  `8267e7de7e698c6d31be0e682c96a1c1ceca98fd` and PR #37.

## Delivered WPF-07 facts

- The service-control path is `WPF -> typed LocalIpcClient -> Agent Local IPC
  -> transport-neutral Application -> typed IServiceManager`.
- The server-owned catalog exposes only opaque IDs for `RMS.BranchService`,
  `RMS.CashierService`, and `RMSServiceManager`. The Agent service is rejected
  by self-protection; arbitrary names, SCM targets, commands, executables, and
  generic payload fields are not accepted.
- Only typed Start/Stop/Restart actions exist. Stop and Restart require exact
  confirmation. LocalAdministrator is mutable; LocalOperator is read-only;
  RemoteHub and unauthenticated contexts are denied.
- A typed Local IPC authorization step issues a short-lived opaque grant bound
  to principal, service, action, confirmation, and correlation. The bounded
  Agent-side store retains only its fingerprint and consumes each grant once;
  mismatch, expiry, and replay fail closed while completed idempotent retries
  recover the prior result.
- The bounded reference-counted `ServiceMutationCoordinator` binds principal,
  service, action, confirmation, correlation, idempotency, and the mutation
  lease. Same-target actions conflict, different targets may proceed, and idle
  references/completed keys are pruned.
- The Application service records requested/accepted/started/dispatch and
  terminal audit outcomes, fails closed before mutation when required audit is
  unavailable, verifies final service state, and reports timeout/cancellation
  ambiguity as OutcomeUnknown with recovery truth. Final audit failure cannot be
  reported as success.
- WPF Services now has typed action adapters, explicit Stop/Restart confirmation,
  administrator-only controls, operator read-only copy, hidden Agent controls,
  bounded progress/error state, refresh-after-action, and shutdown cancellation.
  Existing WPF-05 diagnostics and WPF-06 backup/export paths remain unchanged.

## Validation evidence

- WPF Release build with `--warnaserror`: passed, 0 warnings and 0 errors.
- WPF tests: 65/65 passed, including 3 new service-control workspace tests.
- Service-control Application focused tests: 10/10 passed.
- Agent Integration: 281/281 passed, including the WPF architecture boundary.
- The earlier architecture failure caused by the WPF identifier
  `CanControlServices` was corrected to a neutral capability name; the focused
  and full Agent Integration reruns pass.
- No real RMS service was started, stopped, or restarted. No Production,
  database restore, machine provisioning, operator-group provisioning, or
  remote/fleet operation was performed.

## Azure and roadmap truth

- #13032 remains Active/P1 and must remain open until Sol accepts and WPF-07
  merges. Attach the final Draft PR/CI evidence there.
- #13034 remains Active but is now P2 after WPF-06 backup/export delivery;
  guarded restore remains separately tracked by #13129 New/P2. #13142 remains
  New/P2 and #13143 retains its live priority. #13018 remains Active/P1;
  #13033 and #13035 remain Closed/P1; #13116, #13019, #13020, and Online Order
  #13072-#13076 remain unchanged.
- TASK.md now contains the complete WPF-08 Safety Snapshots & Incident Timeline
  prompt only. WPF-08 is not started.

## Runtime boundary

- Runtime verification must use Testing only. The WPF Release artifact may be
  left running after final authorized verification, but runtime evidence must
  report an actually responding process and endpoint, never configuration alone.
- No Computer Use visual evidence is claimed unless that capability is
  available during the final pass.

`.ai/HANDOFF.md` is `Empty` while this completed branch is handed off for Sol
review.
