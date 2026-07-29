# Technical Decisions

## Decision Index

| ID | Decision | Status | Evidence |
|---|---|---|---|
| DEC-001 | Layered, module-driven .NET API and Angular SPA | Accepted | Source, config, tests, README, Git |
| DEC-002 | Per-session file-backed drafts | Accepted | Source, tests, documentation |
| DEC-003 | Atomic batched order-data mutation | Accepted | U2 plan, source, tests, commit `5ddc4de` |
| DEC-004 | Verified external contracts are authoritative | Accepted | Repositories, fixtures, tests, docs, Git |
| DEC-005 | Testing-default environment safety | Accepted | Source, tests, UI, Git |

## DEC-001 - Layered, Module-Driven .NET API and Angular SPA

- Status: Accepted
- Evidence: Confirmed from source, configuration, tests, README, and Git.
- Affected areas: Entire application.

### Context

The legacy Flask app and duplicated module-key branching were replaced with typed layers and centralized module behavior.
### Decision

Use Core for domain behavior and `IOrderModule` capabilities, Data for SQL implementations, API for HTTP/composition, and an Angular standalone-component SPA whose routes consume live module metadata.
### Rationale

Documentation records typed contracts, clearer boundaries, and reduced cross-layer drift as goals.
### Consequences

Dependencies and support rules are centralized; API/SPA contracts and separate toolchains must remain synchronized, and capabilities do not replace user authorization.
### Files or Modules

`backend/src`, `backend/OnlineOrderTool.slnx`, `frontend/src`, `backend/src/OnlineOrderTool.Core/Modules`
### Follow-up

Remove remaining identity-specific paths and increase frontend feature coverage.

## DEC-002 - Per-Session File-Backed Drafts

- Status: Accepted
- Evidence: Confirmed from source, tests, and documentation.
- Affected areas: Session middleware, draft service, and order-editing endpoints.

### Context

A process-global draft allowed browser sessions to overwrite each other.
### Decision

Issue an HttpOnly GUID cookie and persist draft JSON by `(sessionId, moduleKey)` below the API content root.
### Rationale

Provide simple session isolation without adding a draft database.
### Consequences

Browser drafts are isolated, but local-disk cleanup, encryption, and multi-instance behavior remain unresolved.
### Files or Modules

`backend/src/OnlineOrderTool.Api/Middleware/SessionIdMiddleware.cs`, `backend/src/OnlineOrderTool.Core/Services/DraftManager.cs`
### Follow-up

Define retention and multi-instance requirements; complete DEC-003.

## DEC-003 - Atomic Batched Order-Data Mutation

- Status: Accepted
- Evidence: Confirmed from `UI_Rework_Plan.md` U2, source, passing backend tests, and commit `5ddc4de`.
- Affected areas: Draft manager, order controller, and flat-order state.

### Context

Per-field asynchronous writes can lose fields, collide on files, and apply stale server echoes.
### Decision

Serialize writes per session/module, atomically replace files, batch fields in one PATCH, debounce frontend edits, and keep local edits authoritative until explicit reload.
### Rationale

Eliminate the confirmed consumer-prefill lost-update race.
### Consequences

Concurrent patches preserve fields; compatibility-route removal, direct frontend tests, and non-flat mutation still need resolution.
### Files or Modules

`backend/src/OnlineOrderTool.Core/Services/DraftManager.cs`, `backend/src/OnlineOrderTool.Api/Controllers/OrderController.cs`, `frontend/src/app/features/flat-order/draft.store.ts`
### Follow-up

Add store tests, document the PATCH route, verify the browser flow, and plan removal of the old single-field route.

## DEC-004 - Verified External Contracts Are Authoritative

- Status: Accepted
- Evidence: Confirmed from repositories, builders, fixtures, tests, docs, and Git.
- Affected areas: SQL access, payload construction, and request history.

### Context

Earlier guessed database columns, payload keys, and local request history caused functional drift.
### Decision

Use explicit parameterized Dapper SQL against documented schemas, build/validate payloads in Core from reference fixtures, and treat SQL `OrderRequests` plus related tables as request-history truth.
### Rationale

Prevent undocumented assumptions and parallel local history from becoming production contracts.
### Consequences

Contracts are reviewable and upstream attempts are authoritative; local tests cannot prove live schema drift, availability, index performance, or fixture provenance.
### Files or Modules

`backend/src/OnlineOrderTool.Data/Repositories`, `backend/src/OnlineOrderTool.Core/Services`, `backend/tests/OnlineOrderTool.Tests/fixtures`, `docs/database-schema.md`
### Follow-up

Reverify GHC queries, enable history per module only after verification, and replace duplicated client totals.

## DEC-005 - Testing-Default Environment Safety

- Status: Accepted
- Evidence: Confirmed from source, tests, UI, and commit `6fd7f77`.
- Affected areas: Environment resolution, persistence, send, and cancel.

### Context

Dictionary order previously selected Production and the active lane was not consistently visible.
### Decision

Declare Testing as default, prefer non-Production fallback, persist selection per module, display the lane, and require typed UI confirmation for Production actions.
### Rationale

Reduce accidental production operations.
### Consequences

Refreshes retain a safer lane, but UI confirmation is not server-side user authorization.
### Files or Modules

`backend/src/OnlineOrderTool.Core/Modules/ModuleEnvironmentResolver.cs`, module definitions, `frontend/src/app/core/services/module.service.ts`, environment UI
### Follow-up

Define server-side production authorization with the eventual identity model.
