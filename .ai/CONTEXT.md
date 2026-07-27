# Project Context

## Project Summary

- Project name: Online Order Tool
- Purpose: Internal tool for composing, validating, sending, inspecting, cancelling, and resending pharmacy/e-commerce order and invoice requests.
- Business domain: Pharmacy/e-commerce order integration with client RMS systems.
- Primary users: Internal order/integration operators; exact roles are not documented.
- Current maturity: Active remediation; core UPC/GHC workflows exist, but modules and live integrations have differing levels of completeness.
- Evidence confidence: High for local architecture/configuration/tests; low to unknown for live databases, outbound services, and deployment.

The repository contains a typed .NET API and Angular SPA. It replaced a legacy Flask implementation. Five modules are registered: UPC E-Commerce, GHC E-Commerce, and GHC Uni-Commerce are exposed as available; OMS and Call Center are unavailable stubs.

## Technology Stack

- Languages: C# and TypeScript; HTML/CSS templates and styles.
- Frontend: Angular 22 standalone components, Angular Router/CDK, signals, RxJS.
- Backend: ASP.NET Core Web API targeting .NET 10.
- Desktop or mobile: Browser SPA; no native client confirmed.
- Database: External Microsoft SQL Server databases.
- ORM or data access: Dapper and `Microsoft.Data.SqlClient` with explicit SQL.
- Testing: xUnit, Moq, ASP.NET Core `WebApplicationFactory`; Angular unit-test builder with Vitest.
- Build tools: .NET SDK, Angular CLI, npm, PowerShell scripts.
- Deployment: API is configured to serve static files; production SPA uses same-origin `/api`. Host/process packaging is not documented.
- Infrastructure: External SQL Server and RMS HTTP APIs; ownership and production topology are unknown.

## Repository Structure

| Path | Purpose |
|---|---|
| `backend/src/OnlineOrderTool.Core` | Domain models, module registry, payload builders, validation, totals, and draft services. |
| `backend/src/OnlineOrderTool.Data` | Dapper SQL Server repositories and connection factory. |
| `backend/src/OnlineOrderTool.Api` | Controllers, middleware, configuration, and dependency composition. |
| `backend/tests/OnlineOrderTool.Tests` | Backend unit, contract, repository-shape, middleware, and API integration tests. |
| `frontend/src/app` | Angular application, layouts, feature areas, services, models, guards, and shared UI. |
| `frontend/src/styles` | Design tokens, gradients, typography, and animation foundations. |
| `docs` | API, database-schema, design-system, plans, prompts, and reference payload documentation. |
| `scripts` | Local development and aggregate build/validation scripts. |

## Main Modules

- Module registry and capabilities
  - Responsibility: Declares module availability, environments, capabilities, defaults, payload construction, validation, and lookup dispatch.
  - Main location: `backend/src/OnlineOrderTool.Core/Modules`
  - Important dependencies: Core payload builders/validators and repository interfaces.
- Flat-order workflow
  - Responsibility: GHC/UPC draft entry, item/consumer lookup, products, payments, totals, payload preview, send, and cancel.
  - Main locations: `frontend/src/app/features/flat-order`, API order/product/payment/lookup controllers.
  - Important dependencies: Draft manager, module registry, SQL repositories, and outbound API client.
- GHC Uni-Commerce workflow
  - Responsibility: Invoice-style draft and row-item payload construction.
  - Main locations: `frontend/src/app/features/unicommerce`, `GhcUnicommerceModule.cs`.
  - Important dependencies: Uni-Commerce builder/validator; configured lookups and live endpoints are not available.
- Order Requests
  - Responsibility: Paginated request history, details, statistics, branches, cancel, and resend.
  - Main locations: `frontend/src/app/features/order-requests`, `OrderRequestsController.cs`, `OrderRequestRepository.cs`.
  - Important dependencies: SQL `OrderRequests` and related tables; capability is enabled for UPC only.
- Shared UI and application shell
  - Responsibility: Navigation, environment selection, themes, route guards, reusable controls, and API error handling.
  - Main locations: `frontend/src/app/layout`, `frontend/src/app/core`, `frontend/src/app/shared`.
  - Important dependencies: Live module metadata from `GET /api/modules`.

## Architecture

- The backend is layered: Core has domain behavior, Data implements Core repository interfaces, and API composes both and exposes controllers.
- `ModuleRegistry` owns five module definitions. Capabilities drive controller and route access instead of frontend-only module-key lists.
- Angular loads module metadata before rendering, then lazy-loads feature routes. Global module/environment state and route-scoped signal stores manage UI state.
- A browser receives an HttpOnly GUID session cookie. Draft JSON is stored by `(sessionId, moduleKey)` below the API content root.
- Payloads are built and validated on the backend. SQL repositories supply item/consumer lookups and request history. `IApiClient` sends or cancels requests through configured module environments.
- No background workers, message queues, or application cache are present.

## Application Entry Points

- Application/API startup: `backend/src/OnlineOrderTool.Api/Program.cs`
- UI startup: `frontend/src/main.ts`, bootstrapping `App` with `appConfig`
- UI routes: `frontend/src/app/app.routes.ts`
- Worker or service startup: None detected.
- Test execution: `backend/OnlineOrderTool.slnx` and Angular's `test` target in `frontend/angular.json`

## Data and Storage

- External SQL Server is accessed with Dapper. The repository owns queries, not database migrations.
- No migration mechanism was detected; database schemas are externally managed and documented in `docs/database-schema.md`.
- Main data areas: module environments/capabilities, `OrderDraft`, products, payments, consumers, row items, outbound payloads, and order-request history/details.
- Drafts are local JSON under `var/drafts`; `var/` is Git-ignored.
- Browser `localStorage` persists theme and the selected environment per module.
- No cache or repository-owned object/file storage was detected beyond local drafts.

## Authentication and Authorization

- No authentication scheme, login flow, identity provider, `[Authorize]` usage, or role/permission model was found.
- `oot_sid` is a 30-day HttpOnly, SameSite=Lax GUID cookie for draft isolation; it is not authentication.
- Controllers are not protected by an application authorization policy. External network controls, if any, are unknown.

## External Integrations

| Name | Purpose | Direction | Protocol | Main source location | Current known status |
|---|---|---|---|---|---|
| UPC RMS APIs | Send and cancel UPC orders | Outbound | HTTPS/HTTP via `HttpClient` | `UpcEcommerceModule.cs`, `ApiClient.cs` | Configured for Testing/Production; live access not verified in this refresh. |
| GHC RMS APIs | Send and cancel GHC orders | Outbound | HTTPS via `HttpClient` | `GhcEcommerceModule.cs`, `ApiClient.cs` | Configured; live access not verified. |
| SQL Server | Lookups and request history | Outbound | TDS via `Microsoft.Data.SqlClient` | `OnlineOrderTool.Data`, module connection-string names | Tracked connection strings are empty; UPC schema is documented as live-verified, but no live call ran in this refresh. |
| GHC Uni-Commerce | Invoice dispatch and data access | Outbound | Intended HTTP/SQL | `GhcUnicommerceModule.cs` | Module UI/payload logic exists; endpoint, lookups, and request-history capabilities are unavailable. |

## Important Commands

```text
Install:        cd frontend; npm ci                 (lockfile-backed; not run in this refresh)
Build all:      .\scripts\build.ps1                 (script inspected; aggregate command not run)
Run:            .\scripts\dev.ps1                   (script inspected; not run)
Backend test:   cd backend; dotnet test OnlineOrderTool.slnx --nologo
Frontend test:  cd frontend; npm test
Targeted test:  cd backend; dotnet test OnlineOrderTool.slnx --filter FullyQualifiedName~DraftManagerTests
Type check:     cd frontend; npm run build -- --configuration production
Lint:           Not configured
```

## Testing Approach

- Backend xUnit tests cover payload/validation fixtures, normalizers, module behavior, repositories and SQL shape, controllers, middleware, draft persistence, and in-process API integration.
- Reference JSON under `backend/tests/OnlineOrderTool.Tests/fixtures` is copied into the test output and used as executable payload contracts.
- Most data tests do not connect to a live SQL Server; repository query shape is tested separately.
- Frontend has one `app.spec.ts` file with two test cases. No E2E framework is configured.
- Baseline on 2026-07-27: 101 backend tests passed and the Angular production build passed. Frontend unit tests were not run.

## Stable Constraints

- Do not commit connection strings or other secrets; tracked placeholders remain empty.
- Keep API and frontend contracts synchronized and use camelCase JSON.
- Preserve module capability-driven routing and behavior.
- Treat reference payload fixtures and the verified database-schema document as contracts; do not invent fields or columns.
- Testing is the default environment. Production actions require explicit UI confirmation where implemented.
- Production frontend API calls remain same-origin through relative `/api`.
- Preserve the design-token rule: component styles consume tokens rather than introducing raw color literals.

## Known Limitations

- OMS and Call Center are unavailable stubs.
- GHC E-Commerce request-history capability and its consumer schema remain unverified.
- GHC Uni-Commerce lacks item/consumer lookup, request-history, cancel, and resend capabilities.
- There is no application authentication/authorization boundary.
- Local drafts have no confirmed retention cleanup, encryption, or multi-instance coordination.
- Frontend automated coverage is minimal and live integration behavior is not established by the local suite.

## Unknowns

- Production hosting, reverse proxy, deployment automation, monitoring, backup, and secret-rotation ownership.
- Exact internal user roles, authorization requirements, and audit requirements.
- Current reachability and correctness of all configured live API/database environments.
- Database migration/index ownership and whether documented performance indexes were added.
