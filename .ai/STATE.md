# Current Project State

- **Updated:** 2026-07-29
- **Branch:** `main`
- **Baseline commit:** `4bf142bbd0adfd3265d9e009048e46193647c5ac`
- **Release or milestone:** UI Rework U3 (active session 1 of 6)

## Working State

- The repository contains a .NET 10 Web API, Angular 22 SPA, Dapper SQL Server data layer, and xUnit/Vitest tests.
- UPC/GHC flat-order authoring, GHC Uni-Commerce invoice authoring, SQL-backed Order Requests, capability-driven routing, and per-session local drafts exist; live support varies by module.
- UPC is the only module with Order Requests and branch lookup enabled. GHC E-Commerce history/consumer schema remains unverified; GHC Uni-Commerce lacks live lookup/history/cancel/resend support.
- UI Rework U0-U2 are committed: verified branch schema/environment corrections, Testing-default safety, and atomic batched draft persistence.
- U3 is partial at the baseline: backend branch DTO/repository/capability/endpoint code exists, while the shared selector and frontend integrations do not.
- Order Requests still calls the removed `modules/{key}/order-requests/branches` route, so its branch options cannot load from the current backend.
- Backend Release tests on 2026-07-29: 105 passed, 1 failed (`LookupControllerTests.ListBranches_WithCapability_ReturnsRepositoryData`, camelCase assertion mismatch).
- Frontend unit tests on 2026-07-29: 1 passed, 1 failed (`app.spec.ts` still expects a generated `<h1>`); production build passed at 419.50 kB with no budget warning.
- No CI/CD, container, infrastructure-as-code, migration mechanism, or deployment automation was found in the repository.
- Active plan/prompt surfaces now contain only UI Rework U3-U8. Completed
  milestone plans are indexed in `.ai/HISTORY.md`; executed prompt runners are
  removed from the active documentation tree.

## Active Blockers

- None confirmed for local U3 implementation.
- Live U3 acceptance requires configured UPC Testing SQL access and network reachability; availability was not tested during discovery.

## Known Risks

- Controllers expose operational and order/customer data without an application authentication or authorization scheme.
- Outbound TLS certificate verification defaults to disabled for the shared RMS HTTP client.
- Local draft JSON may contain customer/order data and has no confirmed cleanup, encryption, or multi-instance strategy.
- Current local regression baselines are red in one backend U3 test and one stale frontend shell test.
- Live SQL/API behavior, GHC schemas, production hosting, monitoring, backup, and secret-rotation ownership are not established by local tests.
- Documented missing indexes on request-header/invoice order-number joins can make history filters time out on large live datasets.

## Recently Completed

- The project-plan reconciliation archived completed milestone plans with
  audit value, removed executed prompt runners, and retained U3-U8 as the
  active UI programme.
- `4bf142b` added the U3 backend branch foundation and the shared AI workflow skeleton, but did not complete U3 frontend work.
- `5ddc4de` added serialized atomic draft writes, batched patches, and the debounced frontend draft store.
- `6fd7f77` made Testing the default, persisted/surfaced environments, and added Production confirmation gates.
- `682fd55` and `ea66830` corrected UPC environment evidence and documented the verified `dbo.Branches` schema.

## Next Recommended Task

- Complete UI-U3 from `.ai/plans/UI-U3-branches.md`, beginning with the existing backend test failure and ending with all four frontend branch consumers using the shared selector.
