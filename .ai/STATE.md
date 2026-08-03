# Current Project State

- **Updated:** 2026-08-04
- **Branch:** `main`
- **Reviewed implementation:** `d3219dd`
- **Closeout commit:** U8 documentation and programme closeout commit
- **Release or milestone:** UI Rework U0-U8 complete locally; external acceptance evidence deferred

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring,
  SQL-backed Order Requests, capability-driven routing, and per-session local
  drafts remain in place; live support varies by module.
- U2 serialized/batched draft writes, U3 capability-gated branch lookup, U4
  item lookup/server totals/send lifecycle, U5 shared tokens/primitives, U6
  flat-order workspace, and U7 app-wide primitive migration are preserved.
- U7 removed all `.glass-*` definitions, consumers, and dead compatibility
  aliases. The Order Requests route-driven drawer, six tabs, capability gates,
  and Production safety paths remain preserved.
- The API has no standalone validation endpoint. U6 Validate remains a
  non-sending draft/preview/totals refresh; `send-request` remains the
  server-authoritative validation/send path.

## Final Local Verification

- Backend tests: 110/110 passed.
- Frontend tests: 85/85 passed across 17 spec files.
- `scripts/build.ps1`: passed; Release build 0 warnings/errors; initial
  Angular bundle 427.19 kB.
- Static cleanup: no legacy glass tokens/classes, `alert()`, in-app `rgba()`
  or raw feature colors; no tracked drafts/generated paths/debug markers or
  conflict markers; Markdown relative-link check passed.
- Local host checks: Angular routes served on `localhost:4200`; API catalog,
  endpoint metadata, totals, branch lookup, and Order Requests list/detail
  read endpoints responded successfully on the Testing lane.
- Safe probes did not populate a product: one synthetic item lookup returned a
  normal no-match response and a repeat returned the documented upstream HTTP
  502. No payload, customer fields, or live order data was written to project
  files or memory.

## Deferred Acceptance

- No in-app browser instance was available; responsive/theme/visual checks at
  1920/1280/900/600 remain unperformed.
- Full safe Testing order population, send, Order Requests UI inspection,
  cancel, and resend remain unperformed because no approved real Testing item
  was available. Prior U4/U7 item-lookup HTTP 502 evidence remains an external
  dependency limitation; the current synthetic probe did not prove item
  population.
- No Production action was attempted.

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

## Programme Status

- U0-U8 are complete locally. No next UI implementation session or active UI
  rework plan exists.
- `.ai/HANDOFF.md` is Empty.
