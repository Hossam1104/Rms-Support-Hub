# Current Task

- **Task ID:** UI-U3
- **Status:** Ready
- **Role:** Implement

## Objective

Finish U3 so every branch workflow uses the verified `dbo.Branches` endpoint
and one accessible searchable selector.

## Done When

- The capability-gated, five-minute-cached branch endpoint returns camelCase
  `{ code, name }`; refresh bypasses the cache and backend tests pass.
- `shared/ui/searchable-select` supports code/name filtering, keyboard
  selection, focus return, combobox ARIA, virtual scrolling, and
  loading/empty/error states.
- Order header, Add Product, Order Requests filter, and resend use that
  selector and persist/submit only the branch code.
- The history-derived branch route, repository method, DTO, and frontend call
  are removed.
- Backend tests and `.\scripts\build.ps1` pass. Testing-only live/browser
  verification reports a sanitized branch sample and confirms filtering,
  keyboard selection, and draft persistence; unavailable infrastructure is
  reported.

## Read First

- `docs/UI_Rework_Prompts.md` section `Session U3`.
- `docs/database-schema.md` verified `dbo.Branches` row.
- `.ai/plans/UI-U3-branches.md`.
- Current task-related diff and direct backend/frontend tests.

## Constraints

- Use only verified branch columns; preserve capability routing and
  Core -> Data -> API dependencies.
- Preserve U2's batched draft flow. Persist the code, never the label.
- Verification is UPC Testing only; never send, cancel, or resend against
  Production.
- U4-U8, schema/payload changes, dependency upgrades, and unrelated redesign
  are out of scope.

## Checkpoint

- **Baseline commit:** `4bf142bbd0adfd3265d9e009048e46193647c5ac`
- Backend repository/endpoint/capability code exists. Fix its camelCase test,
  then build the selector and migrate the four consumers.
