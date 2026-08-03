# Current Task

- **Task ID:** UI-U8
- **Status:** Ready
- **Role:** Test

## Objective

Perform end-to-end Testing-only verification, final documentation
reconciliation, security and repository cleanup checks, and programme closeout
without adding new features.

## Done When

- Verify the UPC Testing-only order flow through Order Requests and
  cancellation when the safe Testing dependency is available.
- Reconcile the final documentation and confirm no credentials, generated
  files, or runtime drafts are tracked.
- Keep the session verification and cleanup only; do not add features or use
  Production.

## Read First

- `docs/UI_Rework_Plan.md` U8 row and testing constraints.
- `docs/UI_Rework_Prompts.md` Session U8.
- `.ai/plans/UI-U8-verification.md`.
- `.ai/STATE.md`, `.ai/PROJECT.md`, and the current task-related diff.
- `README.md`, `docs/api-spec.md`, `docs/design-system.md`, and
  `docs/database-schema.md` only when final facts need reconciliation.

## Scope

- Testing-only end-to-end verification, final documentation reconciliation,
  security and repository cleanup checks, and programme closeout.
- Do not add features or change established payload, SQL, capability, or UI
  contracts.

## Constraints

- Do not modify payload builders, validators, totals, SQL, request fixtures,
  API contracts, capabilities, draft persistence, dependencies, or features.
- Testing is the only live environment. Never send, cancel, or resend against
  Production. Do not push, deploy, reset, stash, rebase, or amend.
- Do not edit generated/runtime paths or store secrets/customer data in tracked
  files or `.ai/`.

## Checkpoint

- U7 implementation committed at `d3219dd`; the closeout documentation and U8
  activation commit is the next checkpoint.
- U7 local validation: backend 110/110, frontend 85/85 across seventeen spec
  files, `scripts/build.ps1` passes, and the production initial bundle is
  427.19 kB with zero Release warnings/errors.
- No in-app browser instance is available; UPC Testing item lookup remains an
  external HTTP 502 limitation. No Production action was attempted.
