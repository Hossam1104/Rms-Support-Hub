# Current Project State

- **Updated:** 2026-08-05
- **Branch:** `main`, synchronized with `origin/main` after the final
  stabilization closeout
- **Release or milestone:** Final Order Requests stabilization

## Working State

- The repository contains the .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests. Existing order authoring,
  payment, detail, cancel, resend, payload, and Production-safety contracts
  remain unchanged.
- Order Requests uses one normalized filter contract for list/count/stats:
  exact order matching by default, escaped partial matching when requested,
  last-nine-digit phone matching, latest-header branch/status semantics,
  end-exclusive date bounds, explicit Apply state, bounded cancellation-aware
  loading, and retryable errors.
- Clear All now uses a fresh default-filter factory and one reset transaction.
  It clears draft/applied filters and the visible result snapshot, returns to
  page 1, preserves page size and refresh preferences, removes canonical and
  legacy route aliases, invalidates older request generations, and makes one
  unfiltered request. Refresh, reload, and browser history remain cleared.
- The branch selector has both the CDK outside-click path and a document-level
  pointer guard for real outside clicks, without focus restoration. The grid
  keeps wide content inside its table surface rather than creating page-level
  horizontal overflow.
- `docs/sql/order-requests-performance-indexes.sql` is a guarded, idempotent
  external support script. It was applied only to the approved UPC Testing
  database; Production was not accessed or changed.

## Local Verification

- Focused frontend Order Requests/searchable-select tests: 43 passed across
  four spec files; full frontend suite: 141 passed across 24 files.
- Focused backend Order Requests tests: 35 passed; full backend suite: 161
  passed with no skipped tests.
- `scripts/build.ps1` passed the backend test, Release build, and Angular
  production-build gates. The production bundle is 438.35 kB with no
  style-budget warning.
- `npm run test:riyal-asset` passed with the provenance-verified asset.
  `git diff --check` passed and no generated/runtime paths are in the task
  diff.
- Read-only UPC Testing API timings for the combined list/count/stats path
  were: unfiltered page 25 1.45-1.80 seconds, page 200 1.45-2.57 seconds,
  branch 0.66-1.06 seconds, status 3 3.14-3.70 seconds, and exact fixture
  0.30-0.53 seconds over two runs.
- Installed Edge fallback verification covered Clear All, outside-click
  dismissal, reload/back/forward, dark/light themes, and 1920, 1440, 1280,
  900, 768, 600, and 390px viewports. The connected in-app browser was
  unavailable in this environment.

## Known Risks and Deferred Acceptance

- The join-index script still needs separate database-owner approval before
  any Production application. The application does not own external-schema
  migrations.
- No safe synthetic fixture was authorized for a state-changing Testing
  send/cancel/resend workflow, and no Production action was attempted.
- Controllers still have no application authentication/authorization scheme;
  this existing project boundary is outside the current filter task.

## Programme Status

- U0-U8, final project polish, Order Requests unification, and acceptance
  hardening are closed. After synchronization, `.ai/HANDOFF.md` remains Empty
  and there is no active implementation plan.
