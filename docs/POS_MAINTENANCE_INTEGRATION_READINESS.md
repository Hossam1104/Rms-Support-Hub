# POS Maintenance - Integration Readiness

## Purpose and status

This document describes **RMS+ Support Hub's side** of the future POS
Maintenance integration: the seams a POS feature plugs into, the primitives it
must reuse, the places where a merge will collide, and what must be
re-validated after the architecture checkpoint.

The cross-project architecture is closed in
[POS_MAINTENANCE_INTEGRATION_PLAN.md](POS_MAINTENANCE_INTEGRATION_PLAN.md).
This document is its Support Hub readiness companion. The independent POS
project remains a read-only provenance source for the authorized SHA; INT-00
did not import or inspect POS source and did not implement an Agent or Angular
feature.

**Status: ARCHITECTURE CLOSED / EVIDENCE OPEN.** The process boundary, direct
browser transport, LNA, managed-browser policy, Negotiate, hostname/port/
certificate, CORS, antiforgery, identity, ownership, source-import, contract,
and CI decisions are recorded in the canonical plan and ADRs. Live Agent,
browser-policy, representative-device, and real-operation evidence remains
open. INT-01 is blocked until the narrow architecture checkpoint passes and
the owner explicitly authorizes execution.

## Current RMS+ architecture

One Angular 22 SPA and one .NET 10 Web API.

- **Frontend** - standalone components with signals, lazy routes carrying typed
  `ToolRouteData`, a hand-rolled token design system with dark/light themes,
  and Angular CDK for overlay/virtual-scroll/a11y.
- **Backend** - `RmsSupportHub.Core` (domain, module capabilities, builders,
  validators; no external package dependency) -> `RmsSupportHub.Data` (Dapper
  repositories) -> `RmsSupportHub.Api` (controllers, middleware, DI
  composition root). Dependencies flow in that direction only.
- **Layout and contracts** - see [REPOSITORY_STRUCTURE.md](REPOSITORY_STRUCTURE.md)
  for placement and route topology, [design-system.md](design-system.md) for
  tokens and primitives, and [api-spec.md](api-spec.md) for the current REST
  surface.

## Current POS placeholder

| Aspect | Current state |
| --- | --- |
| Route | `/tools/pos-maintenance`, lazy, typed `TOOL_ROUTE_DATA.posMaintenance` |
| Component | `frontend/src/app/features/pos-maintenance/pos-maintenance-placeholder.component.ts` |
| Model | `frontend/src/app/core/models/pos-capability.model.ts` |
| Hub entry | `pos-maintenance` in `frontend/src/app/features/hub/tool-registry.ts`, accent `amber`, status not-available |
| Backend | **None.** No POS controller, service, repository, configuration key, or DI registration exists. |

The page is informational only: status text and planned capability areas. It
performs no operation and calls no API. Negative tests assert that boundary and
must stay green until a future, authorized integration branch deliberately
replaces the placeholder.

## Canonical architecture seam

The future path is:

```text
Support Hub Angular
    |
    | HTTPS + Local Network Access + Windows Negotiate
    | exact Origin/CORS + approved antiforgery contract
    v
Separate Windows RmsSupportHub.Pos.Agent
    |
    +--> POS domain/application/contracts
    +--> SQL, SCM, SMB/filesystem, backup trigger HTTP
```

The general `RmsSupportHub.Api` is not in the privileged POS request path. The
portable Support Hub `Core` and `Data` projects do not reference Windows POS
Infrastructure. The Agent remains loopback-only, uses typed allow-listed
operations, and never becomes a generic command or script execution surface.
See [POS_MAINTENANCE_INTEGRATION_PLAN.md](POS_MAINTENANCE_INTEGRATION_PLAN.md)
and ADR-0015/0016 for the full security and ownership decisions.

## Integration entry points

These are the Support Hub seams a future authorized POS feature may attach to.
They are not implementation authorization and are not changed by INT-00.

| Seam | File or destination | Future integration responsibility |
| --- | --- | --- |
| Route | `frontend/src/app/app.routes.ts` | Replace the placeholder `loadComponent` with a final POS feature route while keeping the published path `/tools/pos-maintenance` and typed `ToolRouteData`. |
| Route metadata | `frontend/src/app/core/models/tool.model.ts` (`TOOL_ROUTE_DATA.posMaintenance`) | Flip availability only after the Agent contract and feature are proven. |
| Hub tile | `frontend/src/app/features/hub/tool-registry.ts` | Update capability and action text only with the real feature. |
| Feature folder | `frontend/src/app/features/pos-maintenance/` | The final Support Hub feature replaces the placeholder here and owns the UI. |
| Agent contract | Future `/pos/src/RmsSupportHub.Pos.Contracts` and `RmsSupportHub.Pos.Agent` | Own typed POS operations and authoritative OpenAPI; do not place privileged endpoints in `RmsSupportHub.Api`. |
| DI and configuration | Future Agent composition root and deployment configuration | Keep Agent identity, certificate, port, browser policy, and operation allowlists outside tracked secrets. Do not add POS DI to the general API during INT-00. |
| Tests | Existing Support Hub tests plus future `/pos/tests/` | Preserve the current suites; add Agent security, OpenAPI, cross-process, WinUI, and representative-device lanes in the destination. |

## Shared primitives to reuse

The Hub already owns these. A POS feature that builds its own equivalents is a
defect, not a design choice.

- **Shell** - navbar, breadcrumb, and page container. POS is a tool inside the
  Hub, not a second application shell.
- **Theme** - `ThemeService` and semantic tokens. Component styles consume
  `--surface-*`, `--text-*`, and `--state-*`; raw hex belongs only in the token
  and gradient files.
- **Motion** - `MotionService`. Every animation collapses under
  `data-motion="reduce"`.
- **Density and cards** - the existing page, panel, control, table, and
  `--card-*` contracts, `app-tool-card`, and `ui-card`.
- **Tables and forms** - `ui-table`, `ui-field`, `ui-input`, `ui-select`, and
  `app-searchable-select`.
- **Dialogs and states** - `ConfirmDialogComponent`, `DrawerComponent`,
  `app-status-pill`, `app-empty-state`, `app-skeleton`, `app-stat-tile`, and
  `ToastService`.
- **Icons and errors** - Bootstrap Icons, `APP_ASSETS`, the existing exception
  envelope, and `error-envelope.interceptor.ts`.

## Frontend collision areas

| Area | Risk | Note |
| --- | --- | --- |
| `app.routes.ts` | **High** | Both sides eventually edit the POS route. Keep the Hub lazy route and `ToolRouteData` shape. |
| `package.json` / `package-lock.json` | **High** | Support Hub owns the final package and lockfile. Do not add a second UI kit, icon set, HTTP client, or state library without evidence. |
| `tool-registry.ts`, `tool.model.ts` | **Medium** | Small, deliberate edits on one tool entry. |
| `styles/_tokens.css`, `_gradients.css` | **Medium** | POS consumes existing tokens; additions extend the scale rather than creating a parallel palette. |
| `angular.json` | **Medium** | Budgets, assets, and `production-offline` remain load-bearing. Do not widen a budget silently. |
| `shared/ui/index.ts`, assets | **Low-Medium** | Add only genuinely shared primitives and semantic assets through the existing catalog. |

## Backend and deployment collision areas

| Area | Risk | Note |
| --- | --- | --- |
| `RmsSupportHub.Api/Program.cs` | **High** | Existing middleware order and Support Hub CORS/TLS policy remain unchanged. The general API does not register or proxy the privileged Agent. |
| `*.csproj` / NuGet | **High** | Existing portable backend projects remain portable. Windows POS packages belong in the isolated `/pos` destination. |
| `appsettings.json` | **Medium** | Existing Support Hub keys stay separate from Agent deployment configuration; no POS credential, token, or connection string is tracked. |
| API route namespaces | **Medium** | The Agent owns POS routes; they must not shadow the existing Online Order routes in the general API. |
| `RmsSupportHub.slnx` | **Low-Medium** | Do not add POS projects to the current solution during INT-00. |
| Logging and error handling | **Low** | Support Hub keeps its current envelope. The Agent owns privileged audit, correlation, timeout, and operation outcomes. |

Support Hub's `Outbound:VerifyTls` setting remains its existing internal RMS
outbound policy. The Agent's HTTPS certificate and SQL TLS decisions are
separate deployment controls. `TrustServerCertificate = true` remains an
unresolved SQL TLS deployment decision and is not changed by INT-00.

## Safety and business boundaries

These hold before, during, and after future integration:

1. Production is out of bounds for agent-run verification. Testing is the
   default environment; Production actions remain visibly gated.
2. No generic execution surface: no arbitrary PowerShell, command, SQL,
   script-upload, executable-path, or generic process-launch input.
3. Online Order and Prompt Studio behavior is frozen, including API/DTO/payload
   contracts, module capabilities, payment values, statuses, filters, paging,
   calculations, send/resend/cancel semantics, and Prompt Studio 11/7/9
   section contracts.
4. Persisted storage keys remain byte-exact. A future POS key uses a new
   namespace and does not disturb the eight keys listed in `.ai/STATE.md`.
5. The placeholder stays safe until it is deliberately replaced on an
   authorized integration branch.

## INT-01 entry sequence (blocked)

1. Pass the narrowly scoped `CLAUDE OPUS 5 POS INTEGRATION ARCHITECTURE
   CHECKPOINT` covering process isolation, browser-to-loopback transport, LNA,
   managed browser policy, Negotiate, hostname/port/certificate, CORS,
   antiforgery, identity, audit/resource ownership, destination isolation, and
   clean source import.
2. Supply and review the POS source at the authorized provenance SHA, complete
   the `[REQUIRES SOURCE REVIEW]` rows in
   `POS_MAINTENANCE_MIGRATION_INTAKE.md`, and produce a clean tracked snapshot.
   Do not merge raw POS Git history.
3. Inventory real operations and privileges, then define the Agent-owned typed
   OpenAPI contract and Support Hub generated consumer.
4. Implement the isolated `/pos` projects and Agent. Do not add privileged POS
   execution to `RmsSupportHub.Api`, `Core`, or `Data`.
5. Build the final Support Hub feature against the Agent contract using the
   existing primitives; replace the placeholder only when operationally proven.
6. Run destination CI lanes, the browser-policy matrix, cross-process tests,
   representative-device evidence, and the existing Support Hub regression
   gate before any merge.

INT-01 is not staged for execution by this document.

## Required post-integration validation

| Gate | Requirement |
| --- | --- |
| Frontend suite | `npm --prefix frontend test -- --watch=false`; no regression against the recorded 325-test baseline, plus new POS tests |
| Backend suite | 188 existing tests still pass, plus new Agent/POS tests |
| Full gate | `./scripts/build.ps1` or the repository's Windows equivalent: backend tests, Release build with 0 warnings, and Angular production build |
| Bundle budgets | POS stays out of the initial bundle beyond its route chunk; investigate meaningful growth |
| Offline build | `production-offline` still succeeds |
| Riyal verifier | `scripts/verify-riyal-asset.ps1` passes |
| Persisted keys | All eight keys remain byte-exact in source |
| Business contracts | Prompt Studio 11/7/9 and the Online Order API/DTO/payload surface remain unchanged |
| Rendered pass | POS plus one Online Order and one Prompt Studio route, desktop/mobile, light/dark, reduced motion; one H1/main landmark, no shell overflow, no unlabeled control |
| Browser transport | Managed/unmanaged Chrome and Edge LNA, exact-origin CORS, Negotiate, certificate, antiforgery, denial/revocation, and Agent-unreachable evidence |
| Device evidence | LocalSystem/Session 0 SMB, SQL, SCM, restore/maintenance, downloader, and cross-process behavior on representative hardware |
| Secrets | No credential, token, or connection-string value in the diff |

## Open evidence gates

The architecture decisions are closed; the following are intentionally not
converted into architecture claims:

- `ADR-012 LOCAL SYSTEM / SESSION 0 SMB`: open; representative device evidence
  required.
- Live Agent/browser transport: open.
- LNA and managed-browser policy: open; architecture defined, live evidence
  required.
- Negotiate browser policy and Kerberos/NTLM behavior: open; live evidence
  required. SPN behavior is not guessed.
- Real SQL, SCM, restore, maintenance, downloader, and remote-trigger
  reconciliation/idempotency: open or unverified.
- SQL TLS: `TrustServerCertificate = true` remains an open deployment decision;
  it must be resolved before deployment or a Production claim.
- WinUI cutover: open by design.
- Integration implementation: not authorized.

The pre-existing local Testing gap (`ConnectionStrings:UpcEcommerceTest` is not
configured) remains unrelated to INT-00 and does not authorize Production
verification.
