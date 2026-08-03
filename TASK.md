# Current Task

- **Task ID:** UI-U4
- **Status:** Ready
- **Role:** Implement

## Objective

Finish U4 so the flat-order builder uses the item lookup result, server-owned
totals, real request lifecycle state, explicit endpoint configuration, and
inline validation without changing the verified payload or validation layer.

## Done When

- Item lookup populates the Add Product form with the available English and
  Arabic names, code, unit price, VAT rate, and computed net price; a missing
  branch blocks lookup with a picker-directed message.
- The summary reads the typed `calculate-totals` response instead of a client
  money-math reimplementation, and draft mutations refresh it through U2's
  batched store.
- Send loading follows the request lifecycle; the active environment URL is
  read-only unless an explicit custom-endpoint toggle is enabled.
- `{ success: false, errors: [...] }` validation maps to inline field errors,
  and the temporary `PUT order-field` adapter and its callers are removed.
- Targeted tests, `.\scripts\build.ps1`, and the required Testing-only
  verification pass; unavailable live infrastructure is reported separately.

## Read First

- `docs/UI_Rework_Plan.md` defects D2, D8, and D13.
- `docs/UI_Rework_Prompts.md` section `Session U4`.
- `.ai/plans/UI-U4-builder.md`.
- The `TotalsCalculator` response shape, current builder files, direct tests,
  and the current task-related diff.

## Scope

- Flat-order item lookup, Add Product population, server totals, send state,
  endpoint controls, inline validation, and removal of the U2 compatibility
  adapter.
- Preserve U0-U3 branch selection and U2's batched draft contract while
  integrating the builder changes.

## Constraints

- Do not modify `FlatOrderPayloadBuilder`, `FlatOrderValidator`,
  `TotalsCalculator`, verified SQL, payload fixtures, or module capability
  routing.
- Keep backend dependencies flowing Core -> Data -> API; do not add module-key
  comparisons or invent payload keys.
- Verification is UPC Testing only; never send, cancel, or resend against
  Production.
- U5-U8, dependency upgrades, and unrelated redesign are out of scope.

## Checkpoint

- **Baseline commit:** `f4c579291dcabcf011cd6c325f3d0aa871e1a3cc`
- U3 implementation and local build gates are complete. The safe UPC Testing
  branch read returned HTTP 500 and browser verification remains pending; do
  not claim that live gate as passed.
