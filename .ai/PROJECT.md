# Stable Project Context

Read this file only when the task requires stable, non-obvious project knowledge.
Do not copy facts that can be cheaply discovered from the repository.

## Product and Business Boundaries

- RMS+ Support Hub is an internal browser application hosting QA support tools;
  Online Orders compose/validate/send/inspect/cancel/resend pharmacy orders;
  Prompt Studio generates locally; POS Maintenance is informational only.
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

- Backend layering is Core (domain, capabilities, builders, validators) -> Data
  (Dapper repositories) -> API (controllers, middleware, DI); Core has no
  external package dependency.
- `ModuleRegistry` and `IOrderModule.Capabilities` own availability, environment,
  lookup, history, cancel, and resend; frontend routing consumes these flags.
- Payload construction, validation, totals, and draft persistence are
  server-owned; reference JSON fixtures are executable payload contracts.
- External SQL Server schemas are not migrated here. Dapper uses explicit SQL;
  current source is authoritative when it differs from documentation.
- `OrderRequests` is the history base table. Request/response blobs are
  detail-only; related rows use the most recent matching record.
- Drafts are JSON under API `var/drafts`, isolated by HttpOnly session GUID plus
  module key, serialized per key, and replaced atomically.
- Angular uses standalone lazy components, typed models, signals, relative
  `/api`, and a dev proxy. Future privileged POS is direct trusted HTTPS/
  HTTP/1.1 browser -> loopback `RmsSupportHub.Pos.Agent`, not a path through
  `RmsSupportHub.Api`, `Core`, or `Data`; CORS preflight is anonymous exact
  origin; application requests use Windows Negotiate/authorization; token is
  single-use/server-operation-bound; SSE is read-only; artifacts use authenticated fetch.
- Supplied assets use the typed `app-assets.ts` catalog and semantic public
  folders; `frontend/public/assets/Saudi_Riyal.svg` remains verifier-required.
  Shared identity marks use `app-brand-mark` with contain-fit sizing and
  explicit decorative accessibility state.
- Prompt Studio uses typed reactive forms, namespaced drafts, deterministic
  builders, advisory quality analysis, and local history capped at ten records;
  it never stores attachments or sends data to an external provider.
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
 - No background workers, queues, repository migrations, E2E framework, or application auth scheme exists; POS INT-00/INT-00R/INT-01/INT-02 are closed.
  INT-02 populated `pos/RmsSupportHub.Pos.slnx` with approved portable source and
  Domain/Application tests. Infrastructure is Windows-targeted/empty,
  Agent inert; POS source isolated from the general backend/frontend. Loopback
  security, retained WinUI, and raw-history exclusion remain future constraints
  (see plan and ADR-0015..0018).
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
- POS restore/build/tests: `dotnet restore pos/RmsSupportHub.Pos.slnx`; `dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo --warnaserror`; `dotnet test pos/tests/RmsSupportHub.Pos.Domain.Tests/RmsSupportHub.Pos.Domain.Tests.csproj -c Release --no-restore`; `dotnet test pos/tests/RmsSupportHub.Pos.Application.Tests/RmsSupportHub.Pos.Application.Tests.csproj -c Release --no-restore`.
- Lint/format/E2E: no configured command; current counts and bundle sizes live in `.ai/STATE.md`.

## Integrations

- SQL Server supports lookups and request history. Ownership `RmsSupportHub.Data`;
  schema `docs/database-schema.md`; named config `appsettings.json`; tracked
  connection-string values stay empty.
- Branch options use the capability-gated `/api/modules/{key}/branches`
  endpoint, the verified `dbo.Branches` contract, and a short in-memory cache;
  the frontend persists and submits only the branch code.
- Client RMS HTTP APIs support send/cancel by module environment; ownership is
  `IOrderModule` plus `ApiClient`. Never record endpoint values in AI memory.
- Development secrets use the API project's .NET user-secrets; deployed values
  use environment variables. See `README.md` without copying values.
- Local draft persistence is owned by `SessionIdMiddleware` and `DraftManager`;
  `var/` is ignored and must not be treated as durable multi-instance storage.
- Future POS machine-local ownership belongs to the separate Agent/deployment:
  LocalSystem, mandatory trusted machine certificate/private-key ACL, browser
  policy matrix, explicit SQL/SMB credentials, allowlisted operations, and
  privileged audit. The architecture is per-device local maintenance; remote
  fleet/LAN scope is a new security programme. Windows loopback/back-connection
  behavior, managed-browser/LNA, Negotiate/SPN, certificate lifecycle, and
  device-operation evidence are gates, not current Hub runtime facts.

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
