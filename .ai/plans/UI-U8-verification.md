# UI-U8 — End-to-End Verification, Documentation, and Cleanup

## Objective

Perform Testing-only end-to-end verification, final documentation
reconciliation, security and repository cleanup checks, and programme closeout
without adding features.

## Entry criteria

- UI-U7 is closed locally and its implementation/closeout commits are present.
- `TASK.md` identifies UI-U8 with role `Test`.
- The U5/U6 primitive contract and U7 legacy-class removal gates are green.
- Backend/frontend tests and the production build have a current green result.
- Every live action is limited to UPC Testing; no Production action is allowed.

## Verification sequence

1. Read the mandatory startup files and run the context script.
2. Re-run the full backend/frontend/build gates and required static/security
   scans.
3. When an in-app browser is available, verify the affected routes and the
   UPC Testing flow at the required themes and responsive widths.
4. Verify the safe Testing order flow through Order Requests, detail, and
   cancellation without sending any Production request.
5. Reconcile only verified facts in `README.md`, API/schema/design-system docs,
   and the active UI plan.
6. Confirm no credentials, customer payloads, generated files, or runtime
   drafts are tracked; inspect the final diff and leave the handoff empty.

## Exit criteria

- Full local gates and required repository scans pass.
- The Testing-only flow is evidenced or its exact external/browser blocker is
  documented without claiming success.
- Documentation reflects the final architecture and active task.
- No feature, payload, SQL, capability, dependency, or Production behavior was
  changed.
- The worktree is clean and the closeout evidence is recorded in history.

## Constraints

Do not add features, change contracts, use Production, push, deploy, reset,
stash, rebase, amend, or edit generated/runtime paths.
