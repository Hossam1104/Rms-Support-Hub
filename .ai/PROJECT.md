# Stable Project Context

Read this file only when the task requires stable, non-obvious project knowledge.
Do not copy facts that can be cheaply discovered from the repository.

## Product and Business Boundaries

- Online Order Tool is an internal browser application for composing, validating, sending, inspecting, cancelling, and resending pharmacy/e-commerce orders and invoices to client RMS systems.
- Implemented user-facing domains are UPC E-Commerce, GHC E-Commerce, GHC Uni-Commerce, and SQL-backed Order Requests. OMS and Call Center are registered unavailable stubs.
- UPC item pricing is branch-specific. A valid branch code is required before item lookup, and 6-digit UPC material numbers normalize to the stored 18-digit form.
- Flat orders require identity/address fields, at least one product, and at least one supported payment. Payment-status and full-settlement rules are enforced in `FlatOrderValidator`.
- Uni-Commerce returns require `ParentReferenceNumber`; online, points, and customer-credit settlement must equal invoice net amount within 0.01.
- Order status controls cancel/resend eligibility through `OrderRequestStatus`; controllers re-check these rules server-side.

## Architecture Invariants

- Backend layering is Core (domain, module capabilities, builders, validators) -> Data (Dapper repositories) -> API (controllers, middleware, DI). Core has no external package dependency.
- `ModuleRegistry` and `IOrderModule.Capabilities` own availability, environment, lookup, history, cancel, and resend behavior. Frontend routing consumes these flags; avoid module-key branching.
- Payload construction, validation, totals, and draft persistence are server-owned. Reference JSON fixtures are executable payload contracts.
- External SQL Server schemas are not migrated by this repository. Dapper repositories use explicit SQL; current source is authoritative when it differs from documentation.
- `OrderRequests` is the history base table. Request/response blobs are detail-only; related header/invoice rows use the most recent matching record to avoid duplicate attempts.
- Drafts are JSON under API `var/drafts`, isolated by an HttpOnly session GUID plus module key, serialized per key, and replaced atomically.
- Angular uses standalone lazy-loaded components, typed API models, signals, relative `/api`, and a dev proxy. Production API calls are same-origin; the hosting/deployment topology is not documented.
- No background workers, queues, repository-owned migrations, E2E framework, or application authentication/authorization scheme are present.

## Build and Validation Entry Points

- Full gate: `.\scripts\build.ps1` — passed on 2026-08-03 (106 backend tests, Release build, and production Angular build).
- Backend tests: `cd backend; dotnet test OnlineOrderTool.slnx --nologo` — passed 106/106 on 2026-08-03.
- Targeted backend tests: `cd backend; dotnet test OnlineOrderTool.slnx --filter FullyQualifiedName~<TestClass>` — branch controller slice passed 7/7 on 2026-08-03.
- Frontend production build/type check: `cd frontend; npm run build -- --configuration production` — passed through the full gate on 2026-08-03.
- Frontend tests: `cd frontend; npm test -- --watch=false` — one stale starter test fails because it expects the removed generated `<h1>`; current result is recorded in `.ai/STATE.md`.
- Local run: `.\scripts\dev.ps1` — discovered but not executed; starts API on port 5200 and Angular on port 4200.
- Restore/install: `dotnet restore backend/OnlineOrderTool.slnx`; `cd frontend; npm ci` — discovered but not executed.
- Lint/format/E2E: no configured command.

## Integrations

- SQL Server supports lookups and request history. Ownership: `OnlineOrderTool.Data`; schema contract: `docs/database-schema.md`; named configuration: `backend/src/OnlineOrderTool.Api/appsettings.json`. Tracked connection-string values stay empty.
- Branch options use the capability-gated `/api/modules/{key}/branches` endpoint, the verified `dbo.Branches` contract, and a short in-memory cache; the frontend persists and submits only the branch code.
- Client RMS HTTP APIs support send/cancel by module environment. Ownership: `IOrderModule` definitions plus `ApiClient`; configuration contract: module definitions and `appsettings.json`. Never record endpoint values in AI memory.
- Development secrets use the API project's .NET user-secrets; deployed values use environment variables. See `README.md` without copying values.
- Local draft persistence is owned by `SessionIdMiddleware` and `DraftManager`; `var/` is ignored and must not be treated as durable multi-instance storage.

## Critical Conventions

- Never invent SQL columns or payload keys. Verify against current repositories, `docs/database-schema.md`, reference JSON, and contract tests.
- API JSON is camelCase and errors use the exception-middleware envelope. A normal lookup miss may be a successful response; upstream failures must surface as 5xx.
- Testing is the default environment. Production actions have UI typed confirmation, but that is not authorization.
- Frontend feature code consumes typed models and live module metadata; production API URLs remain relative.
- Component styles consume CSS variables. Raw colors are restricted to `frontend/src/styles/_tokens.css` and `_gradients.css`.
