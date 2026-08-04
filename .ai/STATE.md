# Current Project State

- **Updated:** 2026-08-04
- **Branch:** `main` after the final-acceptance no-fast-forward merge
- **Release or milestone:** Final acceptance hardening for Riyal provenance,
  responsive token UI, branch-selector dismissal, and safe UPC Testing gates.

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
  geometry, does not alter the active keyboard row while the pointer moves
  across options, and closes its overlay on outside clicks without reopening
  through focus restoration.
- Every visible currency amount renders through `app-riyal`, which points to
  `/assets/Saudi_Riyal.svg` and inherits `currentColor`. The checked-in asset is
  the approved two-path SAMA vector; the canonical content SHA-1 is
  `02b0fe79a4c8f39f6344682e7ef4dcb5f21cf938`, verified by
  `npm run test:riyal-asset`.
- Resend is allowed for known statuses 2, 3, 5, 6, 7, 8, and 9, and blocked
  for New (1), With_Delegate (4), and unknown values. The server reads the
  selected row's stored RequestJson, verifies `order_code`, changes only
  `branch_code`, preserves unknown fields and the original number, and does
  not mutate the stored history row.
- Testing remains the default environment. Production resend/cancel flows
  require typed confirmation, and no Production action is part of local work.
- The Order Requests list now uses a sea-glass/Atlantic token palette with
  responsive modern filter inputs, stable horizontal grid geometry, compact
  short-result viewports, and explicit loading/focus states.
- Shared `ui-table` now supports wide/caption-hidden tables; global `.sr-only`
  and `.mono` utilities remove repeated component CSS. The order-builder shell
  uses a compact labelled sidebar rail and narrow page-header rules so forms,
  grids, and controls stay within the viewport.
- Order Requests searches normalize exact order numbers and phone input,
  cancel superseded list/branch/detail requests, and fail visibly after a
  15-second request timeout. The backend projects ConsumerMobile correctly,
  pages base rows before header/invoice lookups when possible, and runs list,
  count, and stats reads concurrently.

## Final Local Verification

- Backend tests: 148/148 passed.
- Frontend tests: 107/107 passed across 22 spec files.
- `npm run test:riyal-asset`: passed; official vector structure and canonical
  hash verified.
- `dotnet build backend/OnlineOrderTool.slnx -c Release --nologo`: 0 warnings,
  0 errors.
- `npm run build`: passed; Angular initial bundle 426.93 kB with no style-budget
  warnings. The 6 kB warning / 8 kB error budgets remain unchanged.
- `scripts/build.ps1`: all checks passed after stopping the repo's local Debug
  API process that had locked its own DLLs.
- Local Edge headless desktop/mobile route screenshots loaded successfully.
- `git diff --check` passed. No Production send, cancel, or resend was
  attempted.

## Deferred Acceptance

- No connected in-app browser instance was available
  (`agent.browsers.list()` returned no browsers), so interactive theme and
  outside-click evidence remains external; local Edge screenshots cover the
  desktop/mobile layout.
- UPC Testing metadata/branch reads were safe and read-only; the item lookup
  returned HTTP 502. No explicitly approved synthetic QA branch/item was
  available for a state-changing send/cancel/resend, and no Production action
  was attempted. The COD `"COD"` acceptance send remains pending.

## Known Risks

- `upc_logo.svg` and `whites_logo.svg` remain separate existing asset review
  items; the Riyal asset is now provenance-verified and no longer a placeholder.
- Controllers do not provide application authentication/authorization, the
  shared RMS client defaults TLS verification off, and live SQL/API behavior
  remains environment-dependent.
- The canonical resend contract deliberately reuses the original number;
  the upstream Testing RMS must accept duplicate-number resend semantics when
  a real safe verification fixture is available.

## Programme Status

- U0-U8 and Final Acceptance Hardening remain closed. The final Order Requests
  unification plus acceptance hardening are implemented and merged into local
  `main` with the requested no-fast-forward merge.
- `.ai/HANDOFF.md` is Empty and there is no active implementation plan.
