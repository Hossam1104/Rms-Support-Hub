# Stable Project Context

Read this file only when the task requires stable, non-obvious project knowledge.
Do not copy facts that can be cheaply discovered from the repository.

## Product and Business Boundaries

- Online Order Tool is an internal browser application for composing,
  validating, sending, inspecting, cancelling, and resending pharmacy/e-commerce
  orders and invoices to client RMS systems.
- Implemented user-facing domains are UPC E-Commerce, GHC E-Commerce,
  GHC Uni-Commerce, and SQL-backed Order Requests. OMS and Call Center are
  registered unavailable stubs.
- UPC item pricing is branch-specific. A valid branch code is required before
  item lookup, and 6-digit UPC material numbers normalize to the stored
  18-digit form.
- Flat orders require identity/address fields and at least one product. A
  payment is optional: an empty payment list is the verified Cash on Delivery
  state, not an error. Payment-status and full-settlement rules are enforced in
  `FlatOrderValidator` and apply only to payments that are actually present.
- The Saudi country code lives in its own payload key
  (`client_country_code`/`order_country_code`); the number fields carry the
  bare local subscriber number. `Normalizers.NormalizeLocalPhone` is the
  authoritative split and runs inside `FlatOrderPayloadBuilder`.
- Order status controls cancel/resend eligibility through
  `OrderRequestStatus`; controllers re-check these rules server-side.

## Architecture Invariants

- Backend layering is Core (domain, module capabilities, builders, validators)
  -> Data (Dapper repositories) -> API (controllers, middleware, DI). Core has
  no external package dependency.
- `ModuleRegistry` and `IOrderModule.Capabilities` own availability,
  environment, lookup, history, cancel, and resend behavior. Frontend routing
  consumes these flags; avoid module-key branching.
- Payload construction, validation, totals, and draft persistence are
  server-owned. Reference JSON fixtures are executable payload contracts.
- External SQL Server schemas are not migrated by this repository. Dapper
  repositories use explicit SQL; current source is authoritative when it
  differs from documentation.
- `OrderRequests` is the history base table. Request/response blobs are
  detail-only; related header/invoice rows use the most recent matching record
  to avoid duplicate attempts.
- Drafts are JSON under API `var/drafts`, isolated by an HttpOnly session GUID
  plus module key, serialized per key, and replaced atomically.
- Angular uses standalone lazy-loaded components, typed API models, signals,
  relative `/api`, and a dev proxy. Production API calls are same-origin; the
  hosting/deployment topology is not documented.
- U4 exposes only the resolved send environment's key, label, and API URL via
  `GET /api/modules/{key}/endpoint`; module catalog responses do not disclose
  URLs or connection metadata.
- U4's flat-order summary consumes `TotalsSummary` from the server. The
  frontend may show display-only derived values, but authoritative totals stay
  in `TotalsCalculator` and the draft mutation responses.
- U5 shared UI primitives are standalone, signal-based, token-only components
  exported through `frontend/src/app/shared/ui/index.ts`. Toast state is
  capped/queued/deduplicated in `ToastService`, and sidebar collapse is
  persisted by `SidebarStateService` and published to the module shell.
- U6 flat-order authoring is organized into collapsible `ui-section` blocks
  with capability-aware section navigation, a server-value-only summary rail,
  dense product/payment tables, real loading/empty/error states, and a
  responsive bottom action bar. Product/payment edits use their existing PUT
  endpoints; order-data edits retain U2 serialized batching. The current API
  has no standalone validation endpoint, so the U6 Validate action is a
  non-sending draft/preview/totals refresh and `send-request` remains the
  server-authoritative validation/send path.
- No background workers, queues, repository-owned migrations, E2E framework, or
  application authentication/authorization scheme are present.

## Build and Validation Entry Points

- Full gate: `.\scripts\build.ps1` - passed for the final Order Requests
  hardening on 2026-08-04 (160 backend tests, Release build, and production
  Angular build; initial bundle 438.35 kB with no style-budget warning).
- Backend tests: `dotnet test backend/OnlineOrderTool.slnx -c Release
  --nologo` - passed 160/160 on 2026-08-04.
- Frontend production build/type check: `cd frontend; npm run build --
  --configuration production` - covered by the full gate on 2026-08-04.
- Frontend tests: `cd frontend; npm test -- --watch=false` - passed 114/114
  across 23 spec files on 2026-08-04.
- Riyal asset provenance check: `cd frontend; npm run test:riyal-asset` -
  verifies the approved SAMA vector's canonical SHA-1, SVG structure, and
  absence of textual or external references.
- Local run: `.\scripts\dev.ps1` - starts API on port 5200 and Angular on port
  4200; the Order Requests workbench and API filter matrix were verified
  locally against the approved UPC Testing connection without a
  state-changing workflow.
- Restore/install: `dotnet restore backend/OnlineOrderTool.slnx`; `cd frontend;
  npm ci` - discovered but not executed.
- Lint/format/E2E: no configured command.

## Integrations

- SQL Server supports lookups and request history. Ownership:
  `OnlineOrderTool.Data`; schema contract: `docs/database-schema.md`; named
  configuration: `backend/src/OnlineOrderTool.Api/appsettings.json`. Tracked
  connection-string values stay empty.
- Branch options use the capability-gated `/api/modules/{key}/branches`
  endpoint, the verified `dbo.Branches` contract, and a short in-memory cache;
  the frontend persists and submits only the branch code.
- Client RMS HTTP APIs support send/cancel by module environment. Ownership:
  `IOrderModule` definitions plus `ApiClient`; configuration contract: module
  definitions and `appsettings.json`. Never record endpoint values in AI memory.
- Development secrets use the API project's .NET user-secrets; deployed values
  use environment variables. See `README.md` without copying values.
- Local draft persistence is owned by `SessionIdMiddleware` and `DraftManager`;
  `var/` is ignored and must not be treated as durable multi-instance storage.

## Critical Conventions

- Never invent SQL columns or payload keys. Verify against current repositories,
  `docs/database-schema.md`, reference JSON, and contract tests.
- API JSON is camelCase and errors use the exception-middleware envelope. A
  normal lookup miss may be a successful response; upstream failures must
  surface as 5xx.
- Testing is the default environment. Production actions have UI typed
  confirmation, but that is not authorization.
- Frontend feature code consumes typed models and live module metadata;
  production API URLs remain relative.
- Component styles consume CSS variables. Raw colors are restricted to
  `frontend/src/styles/_tokens.css` and `_gradients.css`.
- Order Requests uses one normalized filter contract for list/count/stats:
  exact order matching by default, escaped partial matching when requested,
  last-nine-digit phone matching, latest-header branch/status semantics,
  end-exclusive date bounds, and explicit Apply-driven UI state. The tracked
  `docs/sql/order-requests-performance-indexes.sql` is an externally applied,
  guarded support script, not an application migration.
- U5 primitives are standalone, token-based, and exported through the shared UI
  barrel. The development-only kitchen sink is their compatibility showcase;
  all active feature surfaces now consume this shared system.
- U6 consumes the U5 primitives and server-owned totals without creating
  client-side financial calculations.
- U7 completed the app-wide primitive migration and removed all `.glass-*`
  definitions, consumers, and unused compatibility aliases.
- Final acceptance hardening keeps the token palette and 6 kB/8 kB style
  budgets unchanged, uses shared wide/caption-hidden tables and global
  accessibility utilities, and keeps narrow order-builder screens inside the
  viewport with a compact labelled sidebar rail.
- The final Order Requests unification owns the canonical route-level detail
  page, compatibility redirects, same-number resend contract, and removal of
  the superseded validation component tree. Final local Edge checks cover the
  Order Requests route at 1920, 1440, 1280, 900, 768, 600, and 390px widths
  in dark and light themes, including Clear All, outside-click dismissal, and
  reload/history behavior. The connected in-app browser was unavailable;
  state-changing Testing order population/send/cancel/resend evidence remains
  deferred; there is no active UI rework plan.
