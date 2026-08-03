# UI-U6 Order Builder Layout Plan

Status: Ready

## Objective

Rebuild the flat-order authoring workspace around the U5 tokens and shared
primitives while preserving U2 draft batching, U3 branch selection, U4 item
lookup, server totals, validation, send lifecycle, and all backend contracts.

## Work

1. Map the existing flat-order component, draft store, totals, validation, send,
   and product/payment flows before changing layout. Keep the server as the
   source of truth for totals and completion state.
2. Introduce a responsive two-column workspace with collapsible `ui-section`
   content, sticky section navigation, intentional field spans, and skeletons
   while the draft is loading.
3. Add a sticky approximately 340px `order-summary-rail` showing item count,
   subtotal, VAT, discount, delivery, total, paid, balance, validation issues,
   environment, Validate, and Send. Disable Send when invalid and explain why.
4. Rebuild product and payment rows with dense/sticky `ui-table` surfaces,
   correct row net totals, draft-store quantity/discount edits, explicit delete,
   and real empty states.
5. Add a sticky bottom total/Send bar below 1200px, switch to one column below
   768px, preserve narrow-screen behavior, and prevent page horizontal scroll.
6. Add focused tests and update only the order-builder documentation needed to
   describe the new layout and its server-owned data boundaries.

## Validation

- Run focused flat-order/layout tests first.
- `dotnet test backend/OnlineOrderTool.slnx --nologo`.
- `cd frontend; npm test -- --watch=false`.
- `.\scripts\build.ps1` with a warning-free Release build and production bundle
  within budget.
- Run `python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`,
  and `git diff --check`.
- When an in-app browser is available, verify 1920, 1280, 900, and 600px with
  no overlap or page horizontal scroll. Do not claim visual evidence otherwise.

## Constraints

- U5 primitives and tokens are the only shared UI foundation; do not create a
  parallel design system.
- Do not modify `FlatOrderPayloadBuilder`, `FlatOrderValidator`,
  `TotalsCalculator`, verified SQL, request fixtures, capability routing, API
  contracts, draft persistence semantics, Production safety, or dependencies.
- Do not start U7 feature migration or remove the `.glass-*` bridge.
- Do not edit generated/runtime paths or use Production for live verification.
