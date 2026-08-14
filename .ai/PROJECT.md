# Stable Project Context
Read this file only when the task requires stable, non-obvious project knowledge.
Do not copy facts that can be cheaply discovered from the repository.
## Product and Business Boundaries
- RMS+ Support Hub is an internal browser application hosting QA support tools;
  Online Orders compose/validate/send/inspect/cancel/resend pharmacy orders;
  Prompt Studio generates locally; POS Maintenance is a direct, read-only
  operational workspace backed by the local POS Agent.
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
- Angular uses standalone lazy components, typed models, signals, relative `/api`, and a dev proxy; privileged POS is direct trusted HTTPS/HTTP/1.1 browser -> loopback `RmsSupportHub.Pos.Agent`, not API/Core/Data; CORS is exact-origin anonymous preflight; application requests use Windows Negotiate/authorization; tokens are single-use/server-bound; SSE is read-only; artifacts use authenticated fetch.
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
- No background workers, queues, repository migrations, E2E framework, or
  Support Hub application auth scheme exists. INT-03 imported the isolated POS
  solution; INT-03R set Agent provenance to `010abc52dc110cfde3dc2c53e057890ff6edaf97`.
  Historical INT-01/02/03 imports remain attributed to
  `25922b499d33bd73f241ffc26c212dd000e81433`.
 - INT-04 composes the Windows-Service-capable Agent at `https://rms-pos-agent.localhost:5001`; INT-05 adds versioned `/pos/openapi`, generated types, and direct `HttpBackend`; INT-05F isolates `openapi-typescript@7.13.0` with a TypeScript 5 peer-compatible lockfile. INT-07 adds the first production read-only device/connectivity/configuration/service routes and direct workspace, with no mutation route or API relay; runtime OpenAPI remains hidden. INT-CI01 makes Windows maintenance/SMB semantics deterministic and all five POS CI lanes green.
 - The Agent also owns the typed RMS database recovery surface at `/api/v1/rms/databases/{branch|cashier}`. Backup and restore use canonical targets, Agent-owned roots, opaque approved artifacts, bounded native SQL, exact confirmation, target-specific service coordination, one-use mutation tokens, bounded idempotency/concurrency, and principal-scoped REST/SSE operation truth. The browser never supplies or receives a raw SQL/database/path/service capability.
 - The Agent composes the existing typed downloader and maintenance application services at `/api/v1/downloads/**`, `/api/v1/maintenance/**`, and `/api/v1/artifacts/{artifactId}`. Downloader configuration and credentials are projected from Agent-owned stores; branch selection, cleanup/reset policy, bounded operation state, one-use challenges/tokens, and opaque artifact capabilities are server-owned. Browser responses contain only logical branch/target state, stable safe codes, and principal-scoped opaque handles.
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
- Restore/install: `dotnet restore backend/RmsSupportHub.slnx`; `cd frontend; npm ci`.
- POS Agent contract generation: set `PosAgentSecurity__SupportHubOrigin`, build
  `pos/src/RmsSupportHub.Pos.Agent/RmsSupportHub.Pos.Agent.csproj`, run
  `npm ci --prefix tools/pos-agent-client-generator` and `npm ci --prefix frontend`,
  then `npm --prefix frontend run generate:pos-agent-client`. Output is under
  `frontend/src/app/core/pos-agent/generated/` and is not edited manually.
- POS restore/build/tests: `dotnet restore pos/RmsSupportHub.Pos.slnx`; `dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo --warnaserror`; `dotnet test pos/tests/RmsSupportHub.Pos.Domain.Tests/RmsSupportHub.Pos.Domain.Tests.csproj -c Release --no-restore`; `dotnet test pos/tests/RmsSupportHub.Pos.Application.Tests/RmsSupportHub.Pos.Application.Tests.csproj -c Release --no-restore`; `dotnet test pos/tests/RmsSupportHub.Pos.Infrastructure.Tests/RmsSupportHub.Pos.Infrastructure.Tests.csproj -c Release --no-restore`; `dotnet test pos/tests/RmsSupportHub.Pos.Agent.IntegrationTests/RmsSupportHub.Pos.Agent.IntegrationTests.csproj -c Release --no-build --no-restore`; `dotnet publish pos/src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj -c Release -r win-x64 --self-contained false --no-restore --nologo`.
 - POS restore/build/tests: `dotnet restore pos/RmsSupportHub.Pos.slnx`; `dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo --warnaserror`; `dotnet test pos/RmsSupportHub.Pos.slnx --nologo`; the focused project commands remain available for diagnosis; `dotnet publish pos/src/PosAdminTool.WinUI/PosAdminTool.WinUI.csproj -c Release -r win-x64 --self-contained false --no-restore --nologo`.
- INT-13C Testing provisioning: run `scripts/setup-pos-agent-testing.ps1 -IUnderstandTestingOnly -Confirm:$false` and `scripts/remove-pos-agent-testing.ps1 -IUnderstandTestingOnly -WhatIf -Confirm:$false` only on the authorized Testing machine. Exact browser/IWA policy logic is in `scripts/PosAgentWindowsProvisioning.psm1`; the task-scoped normal-user browser evidence launcher is `scripts/invoke-pos-browser-evidence.ps1` and uses `tools/pos-browser-evidence`.
- INT-13D secure Support Hub Testing runtime: use the exact origin
  `https://support-hub.integration.test:4443` and run
  `scripts/start-pos-agent-testing.ps1 -IUnderstandTestingOnly`
  only from an elevated, owner-authorized Testing PowerShell session. The start
  path builds the real Angular production bundle, publishes the existing API to
  external machine-local staging, serves it from API `wwwroot` on loopback
  HTTPS/HTTP/1.1, selects the separately owned LocalMachine certificate, and
  proves `/` plus `/tools/pos-maintenance`. `scripts/PosTestingConfiguration.psm1`
  rejects alternate origins; `scripts/PosSupportHubProvisioning.psm1` owns the
  Support Hub host/certificate lifecycle. Do not claim live protected evidence
  until the exact endpoint and both Limited interactive-user browser channels
  respond.
- Lint/format/E2E: no configured command; current counts and bundle sizes live in `.ai/STATE.md`.
- Manual IIS publish package: `.\scripts\publish-iis.ps1` - Angular production
  build + .NET Release publish combined into `publish/RmsSupportHub-IIS/`
  (API + `wwwroot/` Angular build) and `publish/RmsSupportHub-IIS.zip`
  (contents at ZIP root, no outer folder). See `docs/MANUAL_IIS_DEPLOYMENT.md`.
  Temporary manual workflow pending a CI/CD pipeline; script never touches
  IIS or writes secrets into the package.
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
  - POS machine-local ownership belongs to the separate Agent/deployment:
  LocalSystem, trusted certificate/private-key ACL, browser policy, explicit
  SQL/SMB credentials, allowlisted operations, and privileged audit. Per-device
  scope is required; loopback, LNA, Negotiate/SPN, certificate lifecycle, and
  device-operation evidence remain gates, not current Hub runtime facts.
## Critical Conventions

- Never invent SQL columns or payload keys. Verify against current repositories,
  `docs/database-schema.md`, reference JSON, and contract tests.
- API JSON is camelCase and errors use the exception-middleware envelope. A
  normal lookup miss may be a successful response; upstream failures must
  surface as 5xx.
- Testing is the default environment. Production actions have UI typed
  confirmation, but that is not authorization.
- INT-13C browser provisioning is exact-origin and version-selected: Chrome/Edge
  146+ use `LoopbackNetworkAllowedForUrls`, older supported generations use
  `LocalNetworkAccessAllowedForUrls`, AuthServerAllowlist contains only the
  exact Agent hostname, and `BackConnectionHostNames` remains REG_MULTI_SZ.
  Preserve unrelated values, use ownership/WhatIf, fail closed on conflicts,
  and never write `DisableLoopbackCheck=1`.
- Frontend feature code consumes typed models and live module metadata;
  production API URLs remain relative.
- Component styles consume CSS variables. Raw colors are restricted to
  `frontend/src/styles/_tokens.css` and `_gradients.css`.
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
- The token palette and the 6 kB/8 kB component style budgets are fixed; wide and
  caption-hidden tables plus global accessibility utilities are shared, and narrow
  order-builder screens stay inside the viewport via a compact labelled sidebar rail.
- Order Requests has one canonical route-level detail page, compatibility
  redirects, and a same-number resend contract; the superseded validation
  component tree is gone. State-changing Testing send/cancel/resend evidence
  remains deferred.
