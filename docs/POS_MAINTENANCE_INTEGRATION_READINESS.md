# POS Maintenance — Integration Readiness

## Purpose

This document describes **RMS+ Support Hub's side** of the future POS
Maintenance integration: the seams a POS feature plugs into, the primitives it
must reuse, the places where a merge will collide, and what must be re-validated
afterwards.

It deliberately says nothing about the external POS project's implementation.
No POS source has been supplied to this repository, so no POS framework,
dependency, operation, or privilege is asserted here. The inputs to collect from
that project, and the security boundary it must satisfy, are in
[POS_MAINTENANCE_MIGRATION_INTAKE.md](POS_MAINTENANCE_MIGRATION_INTAKE.md).
This document is the counterpart to read alongside it.

**Status: READY WITH FINDINGS.** The seams are stable and documented. The
findings are listed under [Open questions](#open-questions); none of them block
starting integration planning.

## Current RMS+ architecture

One Angular 22 SPA and one .NET 10 Web API.

- **Frontend** — standalone components with signals, lazy routes carrying typed
  `ToolRouteData`, a hand-rolled token design system with dark/light themes, and
  Angular CDK for overlay/virtual-scroll/a11y.
- **Backend** — `RmsSupportHub.Core` (domain, module capabilities, builders,
  validators; no external package dependency) → `RmsSupportHub.Data` (Dapper
  repositories) → `RmsSupportHub.Api` (controllers, middleware, DI composition
  root). Dependencies flow in that direction only.
- **Layout and contracts** — see [REPOSITORY_STRUCTURE.md](REPOSITORY_STRUCTURE.md)
  for placement and the route topology, [design-system.md](design-system.md) for
  tokens and primitives, and [api-spec.md](api-spec.md) for the REST surface.

## Current POS placeholder

| Aspect | Current state |
|---|---|
| Route | `/tools/pos-maintenance`, lazy, typed `TOOL_ROUTE_DATA.posMaintenance` |
| Component | `frontend/src/app/features/pos-maintenance/pos-maintenance-placeholder.component.ts` |
| Model | `frontend/src/app/core/models/pos-capability.model.ts` |
| Hub entry | `pos-maintenance` in `frontend/src/app/features/hub/tool-registry.ts`, accent `amber`, status not-available |
| Backend | **None.** No POS controller, service, repository, configuration key, or DI registration exists. |

The page is informational only: status text and planned capability areas. It
performs no operation and calls no API. Negative tests assert that boundary and
must stay green until the integration branch deliberately replaces the
placeholder.

## Integration entry points

These are the exact files a POS feature attaches to. Nothing else in the shell
should need to change to add the feature.

| Seam | File | What integration does here |
|---|---|---|
| Route | `frontend/src/app/app.routes.ts` | Replace the placeholder `loadComponent` with the POS feature's `loadChildren`. Keep the path `tools/pos-maintenance` — it is a published URL. |
| Route metadata | `frontend/src/app/core/models/tool.model.ts` (`TOOL_ROUTE_DATA.posMaintenance`) | Flip `status` from the not-available value to `available`. |
| Hub tile | `frontend/src/app/features/hub/tool-registry.ts` | Update `capabilities`, `actionLabel`, and `availabilityMessage` for the real feature. |
| Feature folder | `frontend/src/app/features/pos-maintenance/` | The POS feature tree replaces the placeholder component here. |
| Route guarding | `frontend/src/app/core/guards/capability.guard.ts` | Reuse if POS operations are capability-gated; do not invent a parallel guard. |
| API controllers | `backend/src/RmsSupportHub.Api/Controllers/` | A new `PosController` (or similar) lives here; domain logic goes in `Core`, SQL in `Data`. |
| DI | `backend/src/RmsSupportHub.Api/Program.cs` | Register POS services in the existing composition root, alongside the current singletons. |
| Configuration | `backend/src/RmsSupportHub.Api/appsettings.json` | Add named keys with **empty** tracked values only. |
| Tests | `backend/tests/RmsSupportHub.Tests/`, feature `*.spec.ts` files | Extend the existing suites; there is no second test project. |

## Shared primitives to reuse

The Hub already owns these. A POS feature that builds its own equivalents is a
defect, not a design choice.

- **Shell** — navbar, breadcrumb, and the page container. POS is a tool inside
  the Hub, not a second application shell.
- **Theme** — `ThemeService` and the semantic tokens. Component styles consume
  `--surface-*`, `--text-*`, `--state-*`; raw hex belongs only in
  `styles/_tokens.css` and `styles/_gradients.css`.
- **Motion** — `MotionService`. Every animation collapses under
  `data-motion="reduce"`; no POS animation may bypass it.
- **Density** — the `--page-*`, `--section-gap`, `--panel-*`, `--control-height*`
  and `--table-*` geometry tokens.
- **Cards** — the `--card-*` contract and `app-tool-card` / `ui-card`, including
  grid-driven equal height. POS keeps the `amber` accent.
- **Tables** — `ui-table` for any tabular POS data: shared borders, sticky
  header, zebra, wide-table overflow, and caption handling.
- **Dialogs and overlays** — `ConfirmDialogComponent` and `DrawerComponent`.
  Any destructive POS action uses the existing confirm contract; icon-only
  controls need accessible names.
- **Status, empty, and loading states** — `app-status-pill`, `app-empty-state`,
  `app-skeleton`, `app-stat-tile`, `ToastService` (capped, queued, deduplicated).
- **Forms** — `ui-field` + `ui-input` / `ui-select` / `app-searchable-select`.
- **Icons and assets** — Bootstrap Icons, and `APP_ASSETS` for any image. No raw
  asset path strings in templates.
- **Errors** — the `ExceptionMiddleware` envelope on the server and
  `error-envelope.interceptor.ts` on the client.

## Frontend collision areas

Ranked by likelihood of a real merge conflict.

| Area | Risk | Note |
|---|---|---|
| `app.routes.ts` | **High** | Both sides edit the POS route entry. Expect a manual resolution; keep the RMS+ lazy + `ToolRouteData` shape. |
| `package.json` / `package-lock.json` | **High** | Any POS dependency must be justified against what the Hub already provides. Do not add a second UI kit, icon set, HTTP client, or state library. |
| `tool-registry.ts`, `tool.model.ts` | **Medium** | Small, deliberate edits on one tool entry. |
| `styles/_tokens.css`, `_gradients.css` | **Medium** | POS must consume tokens; new tokens are additions to the existing scales, never a parallel palette. |
| `angular.json` | **Medium** | Budgets, asset globs, and the `production-offline` configuration are load-bearing. Do not silently widen a budget to fit POS. |
| `shared/ui/index.ts` | **Low–Medium** | Only if POS contributes a genuinely new shared primitive. |
| `frontend/public/assets/` | **Low–Medium** | New POS assets go in a semantic folder and are registered in `APP_ASSETS`. Watch for name collisions. |
| `app.config.ts`, `main.ts` | **Low** | A new provider is possible; the bootstrap shape should not change. |

## Backend collision areas

| Area | Risk | Note |
|---|---|---|
| `Program.cs` | **High** | DI registrations, middleware order, and the CORS/TLS policy live in one file. `ExceptionMiddleware` must stay first and `SessionIdMiddleware` second. |
| `*.csproj` / NuGet | **High** | `RmsSupportHub.Core` has **no** external package dependency. Keep it that way: POS packages belong in `Data` or `Api`. |
| `appsettings.json` | **Medium** | Named keys only, values empty in the tree. |
| API route namespaces | **Medium** | POS routes must not shadow `modules`, `orders`, `order-requests`, `lookup`, `products`, or `payments`. |
| DTO / model names | **Medium** | `Core.DTOs` and `Core.Models` are flat namespaces; a POS `StatusDto` or `Result` would collide. Prefix POS types. |
| `RmsSupportHub.slnx` | **Low–Medium** | Only if POS adds a project. Prefer folders inside the existing three. |
| Logging / error handling | **Low** | Reuse the existing envelope and `ILogger`; do not add a second logging stack. |

## Configuration and secret boundaries

- **No connection string, token, or credential is ever committed.**
  `appsettings.json` tracks named keys with empty values; real values come from
  .NET user-secrets in development and environment variables when deployed.
- `ConnectionStringResolver` raises a `ConfigurationException` naming the exact
  missing key, instead of failing opaquely inside Dapper. POS configuration
  should fail the same way.
- `Outbound:VerifyTls` is `false` by default and logged once at startup because
  the internal RMS hosts present self-signed certificates. A POS integration
  must not widen that bypass silently or add a second unconditional one.
- Draft/runtime state lives under the API content root's `var/`, which is
  gitignored and is **not** durable multi-instance storage.

## Safety and business boundaries

These hold before, during, and after integration.

1. **Production is out of bounds** for agent-run verification. Testing is the
   default environment; Production actions carry typed confirmation, which is a
   safety gate, not authorization.
2. **No generic execution surface, ever.** No arbitrary PowerShell, command,
   SQL, script-upload, or executable-path input. POS operations must be
   explicit, typed, and allow-listed — `BackupDatabase(request)`, never
   `ExecuteCommand(...)`. This is the intake document's core constraint and it
   survives the merge.
3. **Online Order and Prompt Studio behavior is frozen.** The integration must
   not touch API endpoints, DTOs, payload mappings, module keys, capabilities,
   payment codes, statuses, filters, sorting, paging, send/resend/cancel
   behavior, calculations, or the Prompt Studio 11/7/9 section contracts.
4. **Persisted storage keys are byte-exact.** The eight keys listed in
   `.ai/STATE.md` must not be renamed. A POS key uses a new namespace and does
   not disturb them.
5. **The placeholder stays safe until it is deliberately replaced** on the
   integration branch. Until then the negative POS tests are the guard.

## Recommended integration sequence

1. **Intake** — supply the POS source and complete
   `POS_MAINTENANCE_MIGRATION_INTAKE.md`. Nothing below starts until the
   `[REQUIRES SOURCE REVIEW]` rows are filled from actual source.
2. **Operation and privilege classification** — inventory every real POS
   operation and its least privilege. This decides the backend shape.
3. **Contract design** — define typed, allow-listed API contracts and the
   capability flags that gate them. One ADR records the decision.
4. **Backend first** — `Core` domain and capabilities, then `Data`, then `Api`
   controllers and DI. Tests alongside, in the existing test project.
5. **Frontend feature** — build against the real contracts using the shared
   primitives; replace the placeholder component last.
6. **Flip the switches** — route `loadChildren`, `TOOL_ROUTE_DATA` status, and
   the Hub tile, in one commit, once the feature actually works.
7. **Replace the negative tests** — the POS non-operational assertions are
   retired deliberately and replaced with contract tests, not deleted quietly.
8. **Full validation and a rendered browser pass** before merge.

## Required post-merge validation

| Gate | Requirement |
|---|---|
| Frontend suite | `npm --prefix frontend test -- --watch=false`; no regression against the baseline in `.ai/STATE.md` |
| Backend suite | 161 existing tests still pass, plus new POS tests |
| Full gate | `.\scripts\build.ps1` — backend tests, Release build with 0 warnings, Angular production build |
| Bundle budgets | POS must not enter the initial bundle beyond its route chunk; compare the initial bundle against the recorded baseline and investigate meaningful growth |
| Offline build | `--configuration production-offline` still succeeds |
| Riyal verifier | `scripts/verify-riyal-asset.ps1` passes |
| Persisted keys | All eight keys still byte-exact in source |
| Business contracts | Prompt Studio 11/7/9 and the Online Order API/DTO/payload surface unchanged |
| Rendered pass | `/tools/pos-maintenance` plus one Online Order and one Prompt Studio route, desktop and mobile, light and dark, reduced motion: one H1, one main landmark, no shell overflow, no unlabeled control |
| Secrets | No credential, token, or connection-string value in the diff |

## Open questions

Resolve these during intake; they are the findings behind READY WITH FINDINGS.

- **Machine-local execution model.** The intake document's target direction is
  SPA → API → secured machine-local Windows agent. No agent, machine identity,
  or authorization boundary exists in this repository today, and the current API
  has no application authentication or authorization scheme at all. If POS needs
  per-operator authorization, that is new architecture, not a merge.
- **Capability model fit.** `IOrderModule.Capabilities` gates *order module*
  behavior. Whether POS reuses that abstraction or needs a sibling one is a
  design decision for the contract-design step.
- **Deployment topology.** The repository does not define an authoritative
  hosting, IIS, or health-endpoint topology. A POS agent boundary makes that gap
  more consequential.
- **Testing environment gap.** `ConnectionStrings:UpcEcommerceTest` is not
  configured locally, so Testing-only UPC calls return HTTP 500. This is
  pre-existing deferred environment setup, unrelated to POS, but it will affect
  anyone validating the merged application end to end.
