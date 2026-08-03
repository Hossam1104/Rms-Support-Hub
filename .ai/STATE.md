# Current Project State

- **Updated:** 2026-08-03
- **Branch:** `main`
- **HEAD:** `015a627d43a845116db78e57a3085086c51a93cd`
- **Release or milestone:** UI Rework U3 complete locally; U4 next

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring, SQL-backed Order Requests, capability-driven routing, and per-session local drafts exist; live support varies by module.
- UPC is the only module with Order Requests and branch lookup enabled. GHC E-Commerce history/consumer schema remains unverified; GHC Uni-Commerce lacks live lookup/history/cancel/resend support.
- UI Rework U0-U2 are committed: verified branch schema/environment corrections, Testing-default safety, and atomic batched draft persistence.
- U3 is implemented in `f4c5792` and its documentation/source closeout is recorded in `3453b19`: the capability-gated `dbo.Branches` endpoint, five-minute cache/refresh path, shared searchable selector, and all current branch consumers are present.
- The `order-info.component.ts` import correction required for the production Angular build is committed in `3453b19` and does not change the feature contract.
- Targeted branch-controller tests pass 7/7; the full backend suite passes 106/106; `scripts/build.ps1` passes with a clean Release build and production bundle.
- Frontend unit tests pass 8/8 across two spec files; `015a627` replaces the generated starter assertion and adds focused deterministic coverage for the searchable selector.
- The safe UPC Testing branch read returned HTTP 500, so no live branch sample or browser verification was completed. No Production action was attempted.
- No CI/CD, container, infrastructure-as-code, migration mechanism, or deployment automation was found in the repository.
- Active plan/prompt surfaces now contain only UI Rework U4-U8. Completed
  milestone plans are indexed in `.ai/HISTORY.md`; executed prompt runners are
  removed from the active documentation tree.

## Active Blockers

- The UPC Testing branch endpoint returned HTTP 500 during the safe read, so
  live U3 acceptance and browser verification remain unavailable.

## Known Risks

- Controllers expose operational and order/customer data without an application authentication or authorization scheme.
- Outbound TLS certificate verification defaults to disabled for the shared RMS HTTP client.
- Local draft JSON may contain customer/order data and has no confirmed cleanup, encryption, or multi-instance strategy.
- Frontend and backend local test gates plus the production build are green; live/browser evidence for U3 is still unavailable.
- The branch cache key uses the resolved connection-string value in process
  memory; it is not returned to clients or written to logs.
- Live SQL/API behavior, GHC schemas, production hosting, monitoring, backup, and secret-rotation ownership are not established by local tests.
- Documented missing indexes on request-header/invoice order-number joins can make history filters time out on large live datasets.

## Recently Completed

- The project-plan reconciliation archived completed milestone plans with
  audit value, removed executed prompt runners, and retained U4-U8 as the
  active UI programme.
- `015a627` repaired the Angular shell test and added focused searchable-selector
  unit tests; the frontend suite now passes 8/8.
- `3453b19` reconciled the U3 closeout documentation and activated the U4 plan.
- `f4c5792` completed the U3 branch workflow implementation; local backend and
  production build validation passed, with live/browser verification pending.
- `a6a07ba` established the shared AI workflow skeleton and archived completed
  milestone plans with audit value.
- `5ddc4de` added serialized atomic draft writes, batched patches, and the debounced frontend draft store.
- `6fd7f77` made Testing the default, persisted/surfaced environments, and added Production confirmation gates.
- `682fd55` and `ea66830` corrected UPC environment evidence and documented the verified `dbo.Branches` schema.

## Next Recommended Task

- Start UI-U4 from `.ai/plans/UI-U4-builder.md`: connect item lookup results,
  server totals, real send lifecycle, endpoint controls, and inline validation.
