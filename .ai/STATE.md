# Current Project State

- **Updated:** 2026-08-04
- **Branch:** `main`, pushed to `origin/main` at `6255ea4`; the merged
  temporary branch `luna/order-requests-filter-fix` has been deleted.
- **Release or milestone:** Order Requests database-filter correction and
  modern search-workbench closeout.

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests. Existing order authoring,
  payment, detail, cancel, resend, payload, and Production-safety contracts
  remain unchanged.
- `/modules/:key/order-requests` remains the canonical guarded history route;
  its detail route and compatibility redirects are unchanged.
- Order Requests now has one normalized filter model shared by list, count,
  and stats. Exact order-number matching is the default; escaped contains
  matching is opt-in. Phone values use the last nine digits, branch/status
  values use the latest matching header, and date-to is end-exclusive.
- The repository pages base `OrderRequests` rows before related lookups when
  possible, uses a ranked latest-header CTE for header-derived filters, keeps
  raw JSON out of list projections, counts distinct request IDs, and passes
  cancellation tokens through bounded 15-second list/count/stats commands.
- The Angular filter workbench uses explicit Apply, order-number Enter,
  token-based modern controls, a dedicated nine-status row, active chips,
  refresh/auto-refresh controls, responsive narrow-screen layout, and
  separate loading/error/retry states. Superseded list requests are cancelled
  and stale responses cannot overwrite newer results; existing rows remain
  visible during a failed refresh.
- The CDK branch selector has a backdrop and closes without focus restoration
  when the operator clicks outside it. The app shell and table keep wide grid
  scrolling inside the table surface instead of creating page-level overflow.
- `docs/sql/order-requests-performance-indexes.sql` is the guarded,
  idempotent external support script for the two related-table join indexes.
  It was applied only to the approved UPC Testing database on 2026-08-04;
  Production was not accessed or changed.

## Local Verification

- Focused backend Order Requests tests: 47 passed.
- Focused frontend Order Requests/searchable-select tests: 20 passed.
- Full backend suite: 160/160 passed with no skipped tests.
- Full frontend suite: 114/114 passed across 23 spec files.
- `dotnet build backend/OnlineOrderTool.slnx -c Release --nologo`: 0
  warnings, 0 errors.
- `npm run build`: passed with a 438.35 kB initial bundle and no
  style-budget warning.
- `scripts/build.ps1`: passed all backend test, Release build, and Angular
  production-build gates.
- Riyal asset verification, link validation, generated-path hygiene, raw
  color scan, secret scan, and debug-test hygiene checks passed.
- UPC Testing filter matrix passed after the index script was applied:
  unfiltered, exact/partial order, phone variants, branch, status, outcome,
  date bounds, paging, sorting, combined filters, validation errors, and
  retry/error behavior. No send, cancel, resend, or Production operation was
  attempted.
- Installed Edge headless renders covered the Order Requests route at
  desktop, tablet, and 390px mobile widths. The connected in-app browser was
  unavailable, so interactive pointer/keyboard/theme evidence remains an
  external acceptance item.

## Known Risks and Deferred Acceptance

- The broad status-3 Testing scan was approximately 14.8 seconds against the
  bounded 15-second command limit; the current verified matrix is successful,
  but further query/index tuning should be separately approved if that broad
  status remains a common operational query.
- The join-index script still needs separate database-owner approval before
  any Production application. The application does not own external-schema
  migrations.
- No safe synthetic fixture was authorized for a state-changing Testing
  send/cancel/resend workflow, and no Production action was attempted.
- Controllers still have no application authentication/authorization scheme;
  this existing project boundary is outside the current filter task.

## Programme Status

- U0-U8, final project polish, Order Requests unification, and acceptance
  hardening remain closed. This task is merged and synchronized on `main`;
  `.ai/HANDOFF.md` remains Empty and there is no active implementation plan.
