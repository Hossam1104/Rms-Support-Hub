# Current Project State

- **Updated:** 2026-08-03
- **Branch:** `main`
- **Reviewed implementation:** `fd5d65d`
- **Release or milestone:** UI Rework U4 complete locally; U5 next

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring, SQL-backed
  Order Requests, capability-driven routing, and per-session local drafts
  exist; live support varies by module.
- U0-U3 remain committed: verified branch schema/environment corrections,
  Testing-default safety, serialized batched draft persistence, and the
  capability-gated branch picker.
- U4 now routes item lookup results into the Add Product form, reads typed
  server totals, uses request-lifecycle send state, displays the resolved
  endpoint safely, maps validation errors inline, and removes the temporary
  `PUT order-field` adapter. The review also prevents stale database-filled
  lookup values and fabricated pre-load stat zeros.
- U4 local validation is green: backend 110/110, frontend 57/57, and the
  production build passes with a 419.85 kB initial bundle and zero Release
  warnings/errors.
- Kimi's safe Testing endpoint/totals/validation checks were reported green;
  the database-dependent item lookup remains blocked by HTTP 502. No live item
  population or browser verification is claimed.
- U5 is active in `TASK.md`; its plan is `.ai/plans/UI-U5-design-system.md`.
  The `.glass-*` bridge remains intentionally in place until U7.
- `.ai/HANDOFF.md` is Empty. No Production action was attempted.

## Active Blockers

- UPC Testing item lookup depends on external database infrastructure and
  returned HTTP 502; this blocks live item-population evidence but not the
  locally verified U5 work.
- Browser verification is unavailable unless an in-app browser instance can be
  provided.

## Known Risks

- Controllers expose operational and order/customer data without an
  application authentication or authorization scheme.
- Outbound TLS certificate verification defaults to disabled for the shared RMS
  HTTP client.
- Local draft JSON may contain customer/order data and has no confirmed
  cleanup, encryption, or multi-instance strategy.
- Live SQL/API behavior, GHC schemas, production hosting, monitoring, backup,
  and secret-rotation ownership are not established by local tests.
- Documented missing indexes on request-header/invoice order-number joins can
  make history filters time out on large live datasets.

## Recently Completed

- U4 review corrected stale lookup display state and pre-load totals rendering;
  local backend/frontend/build gates are green.
- `fd5d65d` delivered the U4 endpoint-display, lookup, server-totals,
  lifecycle, endpoint-control, validation, and adapter-removal implementation.
- `015a627` repaired the Angular shell test and added searchable-selector
  coverage; U3 closeout and the U4 activation were previously recorded.

## Next Recommended Task

- Execute UI-U5 from `.ai/plans/UI-U5-design-system.md`: establish the
  dark-first token system, shared primitives, capped toasts, sidebar reflow,
  and development-only kitchen-sink coverage.
