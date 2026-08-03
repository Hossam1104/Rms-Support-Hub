# Current Project State

- **Updated:** 2026-08-03
- **Branch:** `main`
- **Reviewed implementation:** `3f6646d`
- **Release or milestone:** UI Rework U5 complete locally; U6 next

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring,
  SQL-backed Order Requests, capability-driven routing, and per-session local
  drafts exist; live support varies by module.
- U0-U4 remain committed: verified branch schema/environment corrections,
  Testing-default safety, serialized batched draft persistence, the
  capability-gated branch picker, U4 item lookup, server totals, send
  lifecycle, endpoint display, inline validation, and adapter removal.
- U5 established dark-first/light-complete tokens, preserved the token and
  gradient compatibility layers, added eight standalone shared primitives,
  capped and queued accessible toasts, persisted sidebar collapse, and made
  the shell offset follow the actual sidebar width. The development-only
  kitchen sink demonstrates the primitive and state surface.
- U5 local validation is green: backend 110/110, frontend 68/68 across twelve
  spec files, `scripts/build.ps1` passes, and the production initial bundle is
  429.42 kB with zero Release warnings/errors.
- The `.glass-*` bridge remains intentionally in place for U7 migration; no
  new U5 primitive consumes it. Raw application color literals are absent
  outside the designated token/gradient layers, apart from the token-based
  skeleton shimmer declaration.
- U6 is active in `TASK.md`; its plan is
  `.ai/plans/UI-U6-builder-layout.md`.
- `.ai/HANDOFF.md` is Empty. No Production action was attempted.

## Active Blockers

- UPC Testing item lookup depends on external database infrastructure and
  returned HTTP 502; this blocks live item-population evidence but not the
  locally verified U5 work.
- Browser verification is unavailable because no in-app browser instance is
  available in this session.

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

- `3f6646d` delivered the U5 dark-first design system, shared primitives,
  toast queue, sidebar persistence/reflow, kitchen sink, tests, and docs.
- U4 review corrected stale lookup display state and pre-load totals rendering;
  local backend/frontend/build gates are green.
- `fd5d65d` delivered the U4 endpoint-display, lookup, server-totals,
  lifecycle, endpoint-control, validation, and adapter-removal implementation.
- `015a627` repaired the Angular shell test and added searchable-selector
  coverage; U3 closeout and U4 activation were previously recorded.

## Next Recommended Task

- Execute UI-U6 from `.ai/plans/UI-U6-builder-layout.md`: rebuild the
  order-builder workspace with collapsible sections, sticky navigation, dense
  product/payment tables, and a server-driven summary rail.
