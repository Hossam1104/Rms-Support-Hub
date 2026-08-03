# UI-U4 Builder Correctness Plan

Status: Ready

## Objective

Finish the flat-order workflow defects D2, D8, and D13 while preserving the
verified payload, validation, capability, SQL, and U2 draft contracts.

## Work

1. Trace the existing item lookup response and pass it into Add Product. Fill
   the available English/Arabic names, code, unit price, VAT rate, and computed
   net price while keeping database-provided fields editable. Block lookup when
   no branch is selected and direct the operator to the U3 picker.
2. Replace client-side totals arithmetic with the typed `calculate-totals`
   response. Refresh it after draft mutations through the existing batched
   store and show the complete server breakdown.
3. Bind the active environment URL, make it read-only by default, and add an
   explicit custom-endpoint toggle. Tie send loading to the request lifecycle.
4. Map server validation errors to inline field errors, focus or scroll to the
   first issue, and retain a concise issue summary.
5. Remove the temporary `PUT /order-field` adapter and all callers after the
   replacement flow is verified.

## Read First

- `docs/UI_Rework_Plan.md` D2, D8, and D13
- `docs/UI_Rework_Prompts.md` Session U4
- `TASK.md`, `.ai/STATE.md`, and current task-related diff
- `TotalsCalculator`, flat-order components, direct tests, and request fixtures

## Validation

- Targeted backend tests for any changed API behavior
- `dotnet test backend/OnlineOrderTool.slnx --nologo`
- `.\scripts\build.ps1`
- Testing-only live item/totals checks when infrastructure is available; never
  send, cancel, or resend against Production

## Constraints

- Do not change `FlatOrderPayloadBuilder`, `FlatOrderValidator`,
  `TotalsCalculator`, verified SQL, reference payloads, or module-key routing.
- Keep credentials outside tracked files and keep `.ai/HANDOFF.md` under 40
  lines if the session stops before completion.
