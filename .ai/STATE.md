# Current Project State

- **Updated:** 2026-08-04
- **Branch:** `main`
- **Release or milestone:** Final project polish merged locally: optional-payment
  Cash on Delivery, local-only phone numbers, UPC-first dashboard, Riyal asset
  standardization, and repository cleanup. UI Rework U0-U8 remains complete.

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
- A payment is optional. An empty payment list is the Cash-on-Delivery state
  and sends successfully (ADR-0006). Phone number fields carry the bare local
  number; the Saudi country code stays in its own key (ADR-0007).
- UPC is pinned first on the module dashboard through
  `orderModulesForDisplay`; everything else keeps the backend's order.
- Every visible Riyal amount renders through `app-riyal`, which masks
  `public/assets/Saudi_Riyal.svg`. No visible `SAR` or `ر.س` text remains.

## Final Local Verification

- Backend tests: 127/127 passed (`dotnet test OnlineOrderTool.slnx -c Release`).
- Frontend tests: 100/100 passed across 20 spec files.
- Release build 0 warnings/errors; `npm run build` succeeded, initial Angular
  bundle 427.35 kB, with one non-blocking `anyComponentStyle` budget warning
  on `order-summary-rail.component.ts` (6.33 kB against a 6 kB warning and
  8 kB error threshold).
- Static cleanup: no legacy glass tokens/classes, no raw color literals in
  components, no focused/skipped tests, no whitespace errors.
- Business rules verified end-to-end against a local API instance built from
  these sources: `GET export-json` on a payment-free draft returned
  `order_payment_method` `"COD"`, `order_payment_status` `"not_payment"` and
  an empty payment list, and phone inputs in `+966`/`00966`/`0` forms were
  reduced to the bare local number on both UPC and GHC variants. Synthetic
  numbers only; no customer data was used or stored.

## Deferred Acceptance

- No in-app browser instance was available; responsive/theme/visual checks at
  1920/1280/900/600 remain unperformed, including visual confirmation of the
  Riyal glyph and the module logos.
- Full safe Testing order population, send, Order Requests UI inspection,
  cancel, and resend remain unperformed because no approved real Testing item
  was available. Prior U4/U7 item-lookup HTTP 502 evidence remains an external
  dependency limitation; the current synthetic probe did not prove item
  population.
- No Production action was attempted.

## Known Risks

- Cash on Delivery sends `order_payment_method` `"COD"`, but the verified
  reference payloads carry `"cash"`. The divergence predates this work and is
  systemic (the builder emits `"Visa"` where fixtures say `"visa"`), but it was
  unreachable until payment-free orders became sendable. One payment-free send
  against UPC **Testing** must confirm the RMS accepts `"COD"` before Cash on
  Delivery is relied on in Production. See ADR-0006.
- `public/assets/Saudi_Riyal.svg`, `upc_logo.svg` and `whites_logo.svg` are
  unverified placeholders: the Riyal asset draws the text `ر.س` rather than a
  true vector glyph, and both logos are generated text-in-gradient marks. No
  logo was fabricated or downloaded. Each is a one-file drop-in replacement
  once an approved asset is supplied.
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
- The final polish task is complete and merged into local `main`. Nothing is
  pushed or deployed.
- `.ai/HANDOFF.md` is Empty.
