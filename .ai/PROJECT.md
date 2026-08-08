# Stable Project Context

Read this file only when the task requires stable, non-obvious project knowledge.
Do not copy facts that can be cheaply discovered from the repository.

## Product and Business Boundaries

- RMS+ Support Hub is an internal browser application hosting the QA support
  tools. Its Online Order Tool composes, validates, sends, inspects, cancels,
  and resends pharmacy/e-commerce orders and invoices to client RMS systems;
  QA Prompt Studio generates refinement prompts locally; POS Maintenance is an
  informational placeholder with no operations.
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
- Supplied visual assets are exposed through the typed
  `frontend/src/app/core/config/app-assets.ts` catalog. Public copies use
  semantic `brand`, `modules`, `payments`, `commerce`, and `system` folders;
  `frontend/public/assets/Saudi_Riyal.svg` remains the verifier-required
  compatibility path. Shared identity marks use the standalone
  `app-brand-mark` primitive with contain-fit sizing and explicit decorative
  accessibility state.
- Prompt Studio generators use typed reactive forms with feature-local
  namespaced drafts, deterministic builders, advisory `PromptQualityService`
  analysis, and `PromptHistoryService` local history capped at ten records.
  History stores generated prompt text and labels only; it never stores
  attachment contents or sends data to an external provider.
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

- Full gate: `.\scripts\build.ps1` - backend tests, Release build, and the
  Angular production build in sequence.
- Backend tests: `dotnet test backend/RmsSupportHub.slnx -c Release --nologo`.
- Frontend tests: `cd frontend; npm test -- --watch=false`.
- Frontend production build/type check: `cd frontend; npm run build --
  --configuration production`. A `production-offline` configuration disables
  external font inlining for reproducible offline builds.
- Riyal asset provenance check: `cd frontend; npm run test:riyal-asset` -
  verifies the approved SAMA vector's canonical SHA-1, SVG structure, and
  absence of textual or external references.
- AI context budget check: `python .ai/scripts/check_memory.py`.
- Local run: `.\scripts\dev.ps1` - starts API on port 5200 and Angular on port
  4200. Agent-run live verification uses Testing only, never Production.
- Restore/install: `dotnet restore backend/RmsSupportHub.slnx`; `cd frontend;
  npm ci`.
- Lint/format/E2E: no configured command.
- Current counts and bundle sizes live in `.ai/STATE.md`, not here.

## Integrations

- SQL Server supports lookups and request history. Ownership:
  `RmsSupportHub.Data`; schema contract: `docs/database-schema.md`; named
  configuration: `backend/src/RmsSupportHub.Api/appsettings.json`. Tracked
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
- Every card surface consumes the shared `--card-*` contract, and peer cards in
  one grid get equal height from `grid-auto-rows: 1fr` plus `margin-top: auto`
  on the action block - never a fixed pixel height. Tool identity is the named
  accent key, not a color. See ADR-0012 and `docs/design-system.md`.
- The Hub hero scene is the only WebGL surface. Three.js is dynamically
  imported so it stays in its own lazy chunk, the canvas is decorative and
  aria-hidden, and reduced motion or absent WebGL falls back to the static
  `--scene-backdrop` gradient. Nothing may depend on it functionally.
- Order Requests uses one normalized filter contract for list/count/stats:
  exact order matching by default, escaped partial matching when requested,
  last-nine-digit phone matching, latest-header branch/status semantics,
  end-exclusive date bounds, and explicit Apply-driven UI state. The tracked
  `docs/sql/order-requests-performance-indexes.sql` is an externally applied,
  guarded support script, not an application migration.
- U5 primitives are standalone, token-based, and exported through the shared UI
  barrel; the development-only kitchen sink is their showcase. Every active
  feature surface consumes them, U6 adds no client-side financial calculation,
  and U7 removed all `.glass-*` definitions, consumers, and aliases.
- The token palette and the 6 kB/8 kB component style budgets are fixed. Wide
  and caption-hidden tables plus the global accessibility utilities are shared,
  and narrow order-builder screens stay inside the viewport via a compact
  labelled sidebar rail.
- Order Requests has one canonical route-level detail page, compatibility
  redirects, and a same-number resend contract; the superseded validation
  component tree is gone. State-changing Testing send/cancel/resend evidence
  remains deferred.
