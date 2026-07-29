# Current State

## Snapshot

- Last updated: 2026-07-28
- Updated by: Codex
- Current branch: `main`
- Repository status: Dirty; U3 implementation is in progress and `UI_Rework_Prompts.md` has uncommitted prompt-only edits.
- Overall project status: In Progress
- Active workstream: U3 branch repository, API, and shared searchable selector.
- Confidence level: High for local source/build/tests; live integrations remain unverified.

## Current Status

The .NET 10 API and Angular 22 SPA build successfully. UPC and GHC flat-order workflows, GHC Uni-Commerce payload authoring, capability-based module routing, local session drafts, and UPC Order Requests are implemented to varying degrees. OMS and Call Center remain stubs.

U2 is committed as `5ddc4de`: draft writes are serialized and atomic, order-data fields can be patched in one batch, and the flat-order UI uses a debounced route-scoped draft store. Backend concurrency coverage and the production frontend build pass; direct frontend store tests and live browser verification were not run.

## Module Status

| Module | Status | Test Status | Notes |
|---|---|---|---|
| Backend API/Core | Implemented | Tested | 101 local xUnit tests passed. |
| SQL data access | Implemented | Tested | Query/contract tests pass; live SQL was not exercised. |
| UPC E-Commerce | Implemented | Tested | Automated coverage exists; live Testing/Production calls were not run. |
| GHC E-Commerce | Partially Implemented | Tested | Flat-order code is covered; Order Requests is disabled and consumer schema is unverified. |
| GHC Uni-Commerce | Partially Implemented | Tested | Payload fixtures exist; lookups/history/cancel/resend are unavailable. |
| Order Requests | Implemented | Tested | Enabled for UPC only; local controller/repository tests pass. |
| Draft persistence U2 | Implemented | Tested | Backend concurrency tests pass; frontend store lacks direct tests. |
| Angular SPA | Implemented | Partially Implemented | Production build passes; only two frontend unit cases were discovered. |
| OMS | Not Started | Not Started | Registered unavailable stub. |
| Call Center | Not Started | Not Started | Registered unavailable stub. |

## Recent Relevant Changes

- `UI_Rework_Prompts.md`: U1–U8 were condensed for Kimi K3-256K, with targeted reads and concise validation reporting; U0 is unchanged.
- `5ddc4de`: implements U2 atomic/serialized draft writes, batched patching, the debounced `DraftStore`, and concurrency coverage.
- `6fd7f77`: defaults to Testing, persists/surfaces environments, and gates Production UI actions.
- `ea66830` / `682fd55`: correct UPC endpoint/host information and add environment guards/schema evidence.
- `b011ffb`: rebinds the builder to corrected schemas and removes the legacy Flask application.
- `b703153`: rebuilds SQL-backed Order Requests detail, cancel, and resend.
- `6cf084f`: introduces the shared bold-gradient design system and UI kit.

## Current Uncommitted Changes

- U3 branch repository/API work is in progress across its task-related backend files.
- `UI_Rework_Prompts.md` has prompt-only Kimi context/quota reductions for U1–U8.

## Validation Status

| Validation | Result | Notes |
|---|---|---|
| Build | Passed | Angular production build passed; initial bundle 419.50 kB, below configured 500 kB warning budget. |
| Compilation | Passed | Backend projects compiled during `dotnet test`. |
| Unit tests | Passed | 101/101 backend tests passed. |
| Integration tests | Passed | In-process backend integration tests are part of the passing suite; no live external services. |
| End-to-end tests | Not Run | No E2E framework is configured; live Testing workflow was not exercised. |
| Type checking | Passed | Angular production/AOT build passed. |
| Lint | Not Run | No lint script or Angular lint target is configured. |
| Prompt document | Passed | `git diff --check -- UI_Rework_Prompts.md`; U0 exact-match check passed; U1–U8 word count reduced 47.2%. |

## Current Blockers

- GHC database credentials/schema confirmation blocks enabling GHC Order Requests and verifying its consumer lookup.
- GHC Uni-Commerce lacks confirmed live endpoint/database configuration and corresponding capabilities.

## Known Risks

- Controllers have no implemented authentication or authorization boundary despite exposing operational and customer/order data.
- Outbound TLS verification defaults to disabled in code, and no tracked `Outbound` setting overrides that default.
- Diagnostic/custom-target endpoints accept caller-supplied destinations; access control and allowlisting are absent.
- Local draft JSON can contain customer/order data and has no confirmed cleanup, encryption, or multi-instance strategy.
- The U2 frontend store lacks direct tests, and Uni-Commerce still uses per-field server echoes.
- Frontend automated coverage is too small to protect the current UI remediation.
- Local tests do not prove live SQL schemas, credentials, endpoints, cancellation, or resend behavior.

## Known Defects or Gaps

- GHC consumer lookup SQL is explicitly marked as an unverified best guess.
- GHC Order Requests is capability-disabled; GHC Uni-Commerce has multiple disabled capabilities.
- OMS and Call Center contain no production behavior.
- Flat-order branch entry remains free text, item lookup results are not fully wired into product entry, and client/server totals are duplicated.
- The superseded Order Validation route remains in the UI.

## Current Priorities

1. Implement U3 using the verified `dbo.Branches` schema and a shared searchable branch selector.
2. Implement U4 so lookup data, server totals, loading state, and validation errors drive the order-builder workflow.
3. Begin U5's dark-first UI primitives, toast controls, and responsive shell behavior.

## Next Recommended Task

- Objective: Implement U3 branch discovery and selection from the verified `dbo.Branches` schema.
- Relevant module: UPC branch lookup and shared frontend controls.
- Likely files: new Core/Data branch repository contracts, `LookupController.cs`, a new `frontend/src/app/shared/ui/searchable-select` control, and branch consumers in flat-order and Order Requests.
- Expected validation: repository/API tests, searchable-select component tests, Angular production build, and a Testing-lane branch call when live database access is available.
- Definition of done: the builder, product lookup, Order Requests filter, and resend flow use the shared keyboard-accessible selector; filtering works by code and name; the history-derived branch source is retired; no unverified SQL columns are introduced.
