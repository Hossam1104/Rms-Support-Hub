# Current Task

- **Task ID:** UI-U6
- **Status:** Ready
- **Role:** Implement

## Objective

Rebuild the order-builder workspace around the completed U5 design system,
preserving U2 draft batching, U3 branch selection, U4 item lookup, server
totals, validation, send lifecycle, and every backend, payload, SQL, and
capability contract.

## Done When

- The order builder uses a responsive two-column workspace with collapsible
  `ui-section` content, sticky section navigation, and a sticky summary rail.
- The summary rail shows server-owned item count, subtotal, VAT, discount,
  delivery, total, paid, balance, validation issues, environment, Validate,
  and Send state; invalid Send is disabled with a useful reason.
- Products and payments use dense/sticky `ui-table` layouts with correct row
  totals, explicit delete controls, draft-store quantity/discount edits, and
  real empty/loading states.
- The layout has deliberate field spans, draft-load skeletons, a responsive
  bottom total/Send bar below 1200px, a one-column mobile mode below 768px,
  and no page-level horizontal scroll.
- Focused U6 tests, the full backend/frontend suites, and the production build
  pass; browser checks cover 1920, 1280, 900, and 600px when available.
- U5 shared primitives and U4 behavior remain intact. No API, payload, SQL,
  dependency, capability, or Production-action changes are introduced.

## Read First

- `docs/UI_Rework_Plan.md` defect D11, U6 row, and decisions 1, 3, 4, 6-8.
- `docs/UI_Rework_Prompts.md` Session U6.
- `.ai/plans/UI-U6-builder-layout.md`.
- `docs/design-system.md` and the U5 shared primitives.
- The direct `features/flat-order/**`, draft store, server-totals, validation,
  send, and existing flat-order test files named by the active plan.

## Scope

- Flat-order layout, collapsible sections, sticky navigation, summary rail,
  product/payment tables, responsive action bars, loading/empty states, and
  focused tests/documentation needed for the builder workspace.
- Reuse U3 picker, U4 data flow, U2 store behavior, U5 tokens, and U5 shared
  primitives rather than creating parallel contracts.

## Constraints

- Do not modify `FlatOrderPayloadBuilder`, `FlatOrderValidator`,
  `TotalsCalculator`, verified SQL, request fixtures, module capability
  routing, API contracts, draft persistence semantics, or U4 behavior.
- Keep backend dependencies flowing Core -> Data -> API and credentials outside
  tracked files.
- Verification is UPC Testing only when a live call is required. Never send,
  cancel, or resend against Production.
- Do not edit generated/runtime paths, add dependencies, start U7 migration, or
  change unrelated modules.

## Checkpoint

- **U5 design-system foundation complete:** `3f6646d`
- U5 local validation is green: backend 110/110, frontend 68/68 across twelve
  spec files, `scripts/build.ps1` passes, and the production initial bundle is
  429.42 kB with zero Release warnings/errors.
- U4 remains locally complete. Endpoint/totals/validation checks were reported
  green; database-dependent item lookup remains blocked by HTTP 502, so live
  item population and browser evidence are not claimed.
