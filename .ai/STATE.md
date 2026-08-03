# Current Project State

- **Updated:** 2026-08-04
- **Branch:** `main`
- **Reviewed implementation:** `dac0cc4`
- **Release or milestone:** UI Rework U6 complete locally; U7 next

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring,
  SQL-backed Order Requests, capability-driven routing, and per-session local
  drafts exist; live support varies by module.
- U0-U5 remain committed: verified branch schema/environment corrections,
  Testing-default safety, serialized batched draft persistence, the
  capability-gated branch picker, U4 item lookup, server totals, validation,
  send lifecycle, endpoint display, and adapter removal, plus the U5 shared
  token/primitives foundation.
- U6 rebuilt the flat-order workspace around collapsible `ui-section` blocks,
  capability-aware sticky section navigation, a server-value-only summary rail,
  dense editable product/payment tables, draft-load/error/empty states, and a
  responsive sticky bottom action bar. Product/payment row edits use the
  existing dedicated PUT endpoints; order-data edits still use U2 batching.
- The current API has no standalone validation endpoint. U6 Validate is a
  non-sending draft/preview/totals refresh and review action; `send-request`
  remains the server-authoritative validation and send path.
- U6 local validation is green: backend 110/110, frontend 81/81 across
  fifteen spec files, `scripts/build.ps1` passes, and the production initial
  bundle is 429.42 kB with zero Release warnings/errors.
- The `.glass-*` bridge remains intentionally in place for U7 migration. U6
  added no new glass-based layout components; raw application color literals
  remain confined to the designated token/gradient layers.
- U7 is active in `TASK.md`; its plan is
  `.ai/plans/UI-U7-app-migration.md`.
- `.ai/HANDOFF.md` is Empty. No Production action was attempted.

## Active Blockers

- UPC Testing item lookup depends on external database infrastructure and
  returned HTTP 502; this blocks live item-population evidence but not local
  U6/U7 work.
- Browser verification is unavailable because no in-app browser instance is
  available in this session.

## Known Risks

- Controllers expose operational and order/customer data without an application
  authentication or authorization scheme.
- Outbound TLS certificate verification defaults to disabled for the shared RMS
  HTTP client.
- Local draft JSON may contain customer/order data and has no confirmed
  cleanup, encryption, or multi-instance strategy.
- Live SQL/API behavior, GHC schemas, production hosting, monitoring, backup,
  and secret-rotation ownership are not established by local tests.
- Documented missing indexes on request-header/invoice order-number joins can
  make history filters time out on large live datasets.

## Recently Completed

- `dac0cc4` delivered the U6 flat-order workspace rebuild, responsive summary
  actions, dense tables, validation navigation, and focused frontend tests.
- `3f6646d` delivered the U5 dark-first design system, shared primitives, toast
  queue, sidebar persistence/reflow, kitchen sink, tests, and docs.
- `fd5d65d` delivered the U4 endpoint-display, lookup, server-totals,
  lifecycle, endpoint-control, validation, and adapter-removal implementation.

## Next Recommended Task

- Execute UI-U7 from `.ai/plans/UI-U7-app-migration.md`: migrate the remaining
  app surfaces to U5/U6 primitives, replace the dead documentation action, and
  remove the legacy glass bridge after all consumers are gone.
