# Current Task

- **Task ID:** UI-U3
- **Status:** Ready
- **Owner:** Unassigned
- **Role:** Implement

## Objective

Complete branch discovery and selection so every branch workflow uses the existing `dbo.Branches` endpoint and a shared accessible searchable selector, with no history-derived or free-text branch source remaining.

## Done When

- `GET /api/modules/{key}/branches` returns camelCase `{ code, name }` options through capability-gated, cached backend code and its tests pass.
- A standalone `shared/ui/searchable-select` filters by label and code, supports keyboard-only selection and focus return, exposes correct combobox ARIA, virtualizes long lists, and renders loading/empty/error states in both themes.
- Order header, Add Product guidance, Order Requests filtering, and resend all consume the shared selector and the new endpoint; only the selected branch code is persisted or submitted.
- The obsolete order-history branch DTO, route, repository method, and frontend calls are absent.
- `dotnet test backend/OnlineOrderTool.slnx --nologo`, the relevant frontend tests, and `.\scripts\build.ps1` pass.
- A UPC Testing call reports a sanitized branch count/sample and a browser check confirms name/code filtering, keyboard selection, and draft persistence; unavailable infrastructure is reported explicitly.

## Scope

### Read First

- `docs/UI_Rework_Plan.md` sections D6, D7, decision 2, and U3.
- `docs/UI_Rework_Prompts.md` section `Session U3`.
- `docs/database-schema.md` verified `dbo.Branches` row.
- `.ai/plans/UI-U3-branches.md`.
- `backend/src/OnlineOrderTool.Api/Controllers/LookupController.cs`.
- `backend/tests/OnlineOrderTool.Tests/LookupControllerTests.cs`, including the current camelCase assertion failure.
- `frontend/src/app/features/order-requests/order-requests.store.ts`, including its obsolete `modules/{key}/order-requests/branches` call.

### May Change

- U3 branch DTO/repository/capability/lookup files and their backend tests.
- `frontend/src/app/shared/ui/searchable-select/**` and `frontend/src/app/shared/ui/index.ts`.
- `frontend/src/app/features/kitchen-sink/kitchen-sink.component.ts`.
- Branch consumers under `features/flat-order/**` and `features/order-requests/**`.
- Branch-related frontend models, services, and tests.
- Task-related shared-memory files after implementation.

### Out of Scope

- UI Rework U4-U8, unrelated visual redesign, payload/validator changes, schema changes, dependency upgrades, deployment, and authentication design.
- Live Production database or RMS API actions.

## Constraints

- Use only verified `dbo.Branches` columns; do not invent schema or payload fields.
- Preserve capability-driven dispatch and the Core -> Data -> API boundary.
- Keep Testing as the default lane and never run agent verification against Production.
- Preserve U2's batched draft-store flow; branch selection persists the code, not the display label.
- Do not commit credentials, connection strings, private endpoints, or live response data.

## Plan

- Execute `.ai/plans/UI-U3-branches.md`.

## Current Checkpoint

- **Baseline commit:** `4bf142bbd0adfd3265d9e009048e46193647c5ac`
- **Starting condition:** Clean application baseline. U3 backend repository/endpoint/capability code is committed, but one backend camelCase assertion fails; the shared selector and four frontend integrations are absent, and Order Requests still calls a removed branch route.
- **Required next action:** Start plan step 1 by reconciling the existing backend U3 implementation and its failing test before building the shared selector.
