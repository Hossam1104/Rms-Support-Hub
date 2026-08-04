# Current Project State

- **Updated:** 2026-08-04
- **Branch:** `main`
- **Release or milestone:** Final Order Requests UX, branch-selector stability,
  Riyal renderer reconciliation, and same-number resend implementation.

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring,
  SQL-backed Order Requests, capability-driven routing, and per-session local
  drafts remain in place.
- `/modules/:key/order-requests` is the canonical guarded history route. Its
  `:orderId` child is a full-page detail view with Items, Order Info,
  Transactions, Request JSON, and Response JSON sections; the JSON sections
  start collapsed. `/requests` and `/validation` remain compatibility
  redirects.
- The superseded `frontend/src/app/features/order-validation` component tree
  is removed. The sidebar exposes one Order Requests entry.
- `app-searchable-select` submits branch codes only, uses fixed 40px option
  geometry, and does not alter the active keyboard row while the pointer moves
  across options.
- Every visible currency amount renders through `app-riyal`, which points to
  `/assets/Saudi_Riyal.svg` and inherits `currentColor`. The checked-in asset is
  still a placeholder containing legacy text, so the approved vector asset is
  an external completion dependency; no glyph was fabricated or downloaded.
- Resend is allowed for known statuses 2, 3, 5, 6, 7, 8, and 9, and blocked
  for New (1), With_Delegate (4), and unknown values. The server reads the
  selected row's stored RequestJson, verifies `order_code`, changes only
  `branch_code`, preserves unknown fields and the original number, and does
  not mutate the stored history row.
- Testing remains the default environment. Production resend/cancel flows
  require typed confirmation, and no Production action is part of local work.

## Final Local Verification

- Backend tests: 145/145 passed.
- Frontend tests: 105/105 passed across 21 spec files.
- `dotnet build backend/OnlineOrderTool.slnx -c Release --nologo`: 0 warnings,
  0 errors.
- `scripts/build.ps1`: all checks passed; Angular initial bundle 425.91 kB.
  Angular emitted two existing/non-blocking style-budget warnings: the flat
  order summary rail and the new Order Requests detail page exceed the 6 kB
  warning budget but remain below the 8 kB error budget.
- `git diff --check` passed. No Production send, cancel, or resend was
  attempted.

## Deferred Acceptance

- No in-app browser instance was available (`agent.browsers.list()` returned
  no browsers), so responsive/theme/visual checks and visual confirmation of
  the Riyal glyph remain unperformed.
- No approved live Testing order was available for safe list/detail/send,
  cancel, or resend verification. Local tests cover the status, payload, and
  duplicate-submit guards.

## Known Risks

- `public/assets/Saudi_Riyal.svg`, `upc_logo.svg`, and `whites_logo.svg` need
  approved asset replacements. The Riyal component path and color behavior are
  ready, but the current Riyal file is not the approved glyph.
- Controllers do not provide application authentication/authorization, the
  shared RMS client defaults TLS verification off, and live SQL/API behavior
  remains environment-dependent.
- The canonical resend contract deliberately reuses the original number;
  the upstream Testing RMS must accept duplicate-number resend semantics when
  a real safe verification fixture is available.

## Programme Status

- U0-U8 remain closed. The final Order Requests unification is implemented and
  merged into local `main` with the requested no-fast-forward merge.
- `.ai/HANDOFF.md` is Empty and there is no active implementation plan.
