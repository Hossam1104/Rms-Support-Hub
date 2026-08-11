# POS Maintenance - Integration Readiness

## Purpose and status

This document describes **RMS+ Support Hub's side** of the future POS
Maintenance integration: the seams a POS feature plugs into, the primitives it
must reuse, the places where a merge will collide, and what must be
re-validated after the architecture checkpoint.

The cross-project architecture is closed in
[POS_MAINTENANCE_INTEGRATION_PLAN.md](POS_MAINTENANCE_INTEGRATION_PLAN.md).
This document is its Support Hub readiness companion. The independent POS
project remains a read-only provenance source. INT-00R
performed only the required read-only provenance spot checks (`BackupApiClient`,
current Agent hosting/security boundary, and POS ADR-012). INT-01 then
established the isolated destination solution and project boundaries; INT-02
imported only the approved portable Domain/Application/Contracts source and the
two portable test suites. INT-03 imported the approved Windows Infrastructure,
Infrastructure test, and retained WinUI boundaries. INT-03R corrected the
source provenance snapshot. Owner-authorized INT-04 composed the destination
Agent host foundation; INT-05 added the Agent-owned OpenAPI contract and
Support Hub transport/type foundation. INT-05F subsequently isolated the
OpenAPI generator toolchain from the Angular dependency graph. No POS Angular
feature or Support Hub API relay/UI integration was implemented. INT-CI01
restored the portable Ubuntu Application lane and all five destination POS CI
lanes are green.

**Status: INT-00R / INT-01 / INT-02 / INT-03 / INT-03R / INT-04 COMPLETE / INT-05 ACCEPTED AFTER INT-05F / INT-05F COMPLETE / INT-CI01 COMPLETE / PROV-1 CLOSED FOR COMPOSITION / INT-06 OWNER-GATED / ARCHITECTURE CLOSED / EVIDENCE OPEN.** The process
boundary, direct browser transport, LNA version/policy matrix, Negotiate and
loopback back-connection behavior, hostname/port/certificate, CORS preflight,
antiforgery, identity, ownership, source-import, contract, and CI decisions are
recorded in the canonical plan and ADRs. Live Agent, browser-policy,
representative-device, and real-operation evidence remains open. INT-02
  portable source import is complete; Windows Infrastructure and Agent tests and
  retained WinUI publish validation are complete. Agent host composition is
  complete, while Support Hub feature work, live browser transport evidence,
  and live operation evidence remain owner-gated/open.

## INT-03R provenance integrity correction

The original INT-01/INT-02/INT-03 import provenance remains:

```text
25922b499d33bd73f241ffc26c212dd000e81433
```

The corrected Agent provenance candidate for INT-04+ is:

```text
010abc52dc110cfde3dc2c53e057890ff6edaf97
```

The initial Claude Opus privileged-boundary review was blocked on `PROV-1`
because the original snapshot omitted the tracked Agent
`ArtifactCatalog.cs`. INT-03R narrowed only the root generated-artifact ignore
rule and tracked the existing source; `PROV-1` is closed for the destination
composition gate. Live privileged-boundary evidence and later feature gates
remain separately required.

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
    | Trusted HTTPS secure context + HTTP/1.1 only
    | LNA version/policy matrix + Windows Negotiate
    | anonymous exact-origin CORS preflight
    | authenticated/authorized application request
    | server-operation-bound single-use mutation token
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

The initial seam is explicitly per-device local maintenance:

```text
DIRECT BROWSER -> LOOPBACK AGENT:
PER-DEVICE LOCAL MAINTENANCE ARCHITECTURE
```

It is not a remote branch-fleet architecture. A future LAN, central, or
remote-device requirement needs a new architecture/security programme and
must not be solved by widening the Agent listener or adding an ad-hoc API
proxy.

### Transport contract reminders

- Windows loopback/back-connection behavior for the custom Agent hostname is a
  separate evidence item. `BackConnectionHostNames` may contain only the exact
  approved hostname when required; `DisableLoopbackCheck = 1` is prohibited.
- Trusted machine certificate provisioning is mandatory. `.localhost` public-CA
  issuance is not a production assumption; per-device certificate designs
  require device keys, minimum LocalSystem access, lifecycle handling, and
  trust cleanup.
- CORS preflight is an anonymous exact-origin transport check. The application
  request still requires Windows Negotiate, local Administrators authorization,
  exact Origin, and the mutation token.
- REST is state truth and SSE is read-only progress transport with no mutation
  token. Artifacts use authenticated typed fetch plus opaque handles and Blob
  object URLs, not top-level Agent navigation or persistent download URLs.
- The mutation token is not authentication. It is issued and consumed once per
  mutation, per tab, and binds the authenticated principal, exact Origin,
  method, server-owned operation ID, expiry, and anti-replay ID.

### LNA deployment matrix

The matrix below was checked against current first-party Chrome and Edge policy
documentation on 2026-08-10. Chrome and Edge are recorded independently; an
allowlist is not effective until existing block policies and precedence are
inspected.

| Browser generation | Local-network policy | Loopback-specific policy | Readiness requirement |
| --- | --- | --- | --- |
| Chrome 139-145 | `LocalNetworkAccessAllowedForUrls` / `LocalNetworkAccessBlockedForUrls` | Current policy precedence also names `LoopbackNetworkAccessAllowedForUrls` / `LoopbackNetworkAccessBlockedForUrls`; split `LoopbackNetworkAllowedForUrls` starts at 146 | Inspect the actual browser policy surface and use any exposed loopback-specific control. |
| Chrome 146+ | `LocalNetworkAllowedForUrls` / `LocalNetworkBlockedForUrls` and `LocalNetworkAccess*` | `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` | Use the applicable loopback-specific policy for the loopback Agent. |
| Edge 140-145 | `LocalNetworkAccessAllowedForUrls` / `LocalNetworkAccessBlockedForUrls` | Current policy precedence also names `LoopbackNetworkAccessAllowedForUrls` / `LoopbackNetworkAccessBlockedForUrls`; split `LoopbackNetworkAllowedForUrls` starts at 146 | Inspect the actual browser policy surface and use any exposed loopback-specific control. |
| Edge 146+ | `LocalNetworkAllowedForUrls` / `LocalNetworkBlockedForUrls` and `LocalNetworkAccess*` | `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` | Use the applicable loopback-specific policy for the loopback Agent. |

Use no wildcard allowlists, global permanent LNA disablement, or permanent
temporary opt-out policy. The Support Hub origin must be HTTPS/a trusted secure
context. The exact browser build, permission generation, target address space,
allow policy, block policy, policy scope, and precedence belong in the live
evidence record.

## Integration entry points

These are the Support Hub seams a future authorized POS feature may attach to.
They are not implementation authorization and are not changed by INT-00R.

| Seam | File or destination | Future integration responsibility |
| --- | --- | --- |
| Route | `frontend/src/app/app.routes.ts` | Replace the placeholder `loadComponent` with a final POS feature route while keeping the published path `/tools/pos-maintenance` and typed `ToolRouteData`. |
| Route metadata | `frontend/src/app/core/models/tool.model.ts` (`TOOL_ROUTE_DATA.posMaintenance`) | Flip availability only after the Agent contract and feature are proven. |
| Hub tile | `frontend/src/app/features/hub/tool-registry.ts` | Update capability and action text only with the real feature. |
| Feature folder | `frontend/src/app/features/pos-maintenance/` | The final Support Hub feature replaces the placeholder here and owns the UI. |
| Agent contract | `/pos/src/RmsSupportHub.Pos.Contracts`, `/pos/openapi`, and composed `RmsSupportHub.Pos.Agent` | INT-04 supplies the headless host/security foundation; INT-05 owns the versioned authoritative OpenAPI, server-owned token registry seam, generated Support Hub types, and isolated direct transport. Do not place privileged endpoints in `RmsSupportHub.Api`. |
| DI and configuration | `/pos/src/RmsSupportHub.Pos.Agent/Program.cs` plus deployment configuration | INT-04 composes Windows Service hosting, fixed origin `https://rms-pos-agent.localhost:5001`, Negotiate, authorization, CORS/Origin, mutation-token, and service-owned storage ports. Keep deployment identity, certificate, browser policy, and operation allowlists outside tracked secrets. Do not add POS DI to the general API. |
| Tests | Existing Support Hub tests plus `/pos/tests/RmsSupportHub.Pos.Domain.Tests`, `/pos/tests/RmsSupportHub.Pos.Application.Tests`, `/pos/tests/RmsSupportHub.Pos.Infrastructure.Tests`, and `/pos/tests/RmsSupportHub.Pos.Agent.IntegrationTests` | Domain 7/7, Application 76/76, Infrastructure 60/60, and Agent 69/69 passed; frontend 341/341 across 56 files; OpenAPI/client drift and WinUI publish lanes are validated. Live browser/device evidence remains open. |

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
6. Hub session identifiers, including `oot_sid`, never become Agent Windows
   identities, authorization principals, operation/resource owners,
   idempotency identities, audit principals, or mutation-token principals.
7. The direct browser-to-loopback Agent remains per-device local maintenance;
   remote-fleet requirements are out of scope for INT-01.

## INT-01 / INT-02 / INT-03 / INT-CI01 result and next gate

INT-01 verified the accepted architecture checkpoint and established the
isolated `/pos` solution, five buildable project boundaries, and destination-
owned portable and Windows build lanes. INT-02 then imported 44 Domain, 15
Application, and 63 Contracts `.cs` files plus 4 Domain and 9 Application test
`.cs` files from the approved tracked snapshot, reconciled the destination
project graph, and extended the portable CI lane with real tests. INT-03
imported 23 Infrastructure `.cs` files, 7 Infrastructure test `.cs` files, and
34 retained WinUI source/resource files, with destination-owned project
metadata and dependencies. No privileged POS execution was added to
`RmsSupportHub.Api`, `Core`, or `Data`.

INT-CI01 replaced host-dependent path handling in the portable Application
maintenance and downloader seams with deterministic Windows path semantics.
The nine pre-existing Ubuntu Application failures are resolved without
relaxing path-policy rejection or activating any Agent feature route or POS UI.

The imported suites pass with Domain 7/0/0, Application 76/0/0,
Infrastructure 60/0/0, and Agent 69/0/0 (passed/failed/skipped). The POS
solution Release build passes with zero warnings/errors. Retained WinUI
publishes for `win-x64` and contains `PosAdminTool.WinUI.exe` and the expected
packaged resource set. INT-05's
OpenAPI and TypeScript generation is deterministic and the frontend suite passes
341/341 across 56 files. The Agent host was not launched as a live service; no
feature UI, raw POS history, or live Windows/device runtime was executed.

The following remain future owner-gated work:

1. Validate the live browser transport: certificate trust/lifecycle, LNA,
   Negotiate/SPN, browser policy, loopback back-connection, and representative
   device behavior in the authorized evidence gate.
2. Implement and validate the final Support Hub feature and later privileged
operations in their separately authorized gates. INT-05's contract/client
foundation is accepted after INT-05F; live SQL/SCM/SMB/device behavior remains
evidence work.

INT-01, INT-02, INT-03, INT-03R, INT-04, and INT-CI01 are complete; INT-05 is
accepted after INT-05F, which is complete. `PROV-1` is closed for the
composition gate.
INT-06 Live Transport Security Evidence remains owner-authorization required
and is not executed by this integration.

## Required post-integration validation

| Gate | Requirement |
| --- | --- |
| Frontend suite | `npm --prefix tools/pos-agent-client-generator ci`; `npm --prefix frontend ci`; `npm --prefix frontend run generate:pos-agent-client`; `npm --prefix frontend test -- --watch=false`; 341 tests across 56 files passed |
| Backend suite | POS Domain 7, Application 76, Infrastructure 60, and Agent 69 tests pass |
| Full gate | `./scripts/build.ps1` or the repository's Windows equivalent: backend tests, Release build with 0 warnings, and Angular production build |
| Bundle budgets | POS stays out of the initial bundle beyond its route chunk; investigate meaningful growth |
| Offline build | `production-offline` still succeeds |
| Riyal verifier | `scripts/verify-riyal-asset.ps1` passes |
| Persisted keys | All eight keys remain byte-exact in source |
| Business contracts | Prompt Studio 11/7/9 and the Online Order API/DTO/payload surface remain unchanged |
| Rendered pass | POS plus one Online Order and one Prompt Studio route, desktop/mobile, light/dark, reduced motion; one H1/main landmark, no shell overflow, no unlabeled control |
| Browser transport | Managed/unmanaged Chrome and Edge independently: versioned LNA policy matrix and precedence, pre-existing block policy, HTTPS secure context, HTTP/1.1-only Agent, exact-origin anonymous preflight, Negotiate/loopback back-connection, certificate trust/lifecycle, application authorization, mutation-token binding/lifecycle, read-only SSE, authenticated artifact fetch, denial/revocation, and Agent-unreachable evidence |
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
- Negotiate browser policy, Kerberos/NTLM, custom-hostname loopback/back-
  connection, and any SPN registration: open; live evidence required. SPN
  behavior is not guessed, and `DisableLoopbackCheck = 1` is prohibited.
- Support Hub HTTPS secure context and trusted Agent machine certificate
  provisioning/lifecycle: open; mandatory deployment evidence required.
- Anonymous exact-origin CORS preflight, HTTP/1.1-only transport, read-only
  SSE, authenticated artifact fetch/opaque handles, and single-use
  server-operation-bound mutation tokens: open; implementation evidence
  required.
- Per-device scope and explicit rejection of remote/LAN widening: open;
  deployment evidence required.
- Real SQL, SCM, restore, maintenance, downloader, and remote-trigger
  reconciliation/idempotency: open or unverified.
- SQL TLS: `TrustServerCertificate = true` remains an open deployment decision;
  it must be resolved before deployment or a Production claim.
- WinUI cutover: open by design.
- Future operational integration implementation: not authorized.

The INT-00R source finding is closed by the verified POS source:

```text
CLAUDE MEDIUM-8:
NOT APPLICABLE / ALREADY CLOSED BY SOURCE
```

`BackupApiClient` at the approved provenance SHA already maps post-dispatch
non-success responses to `OutcomeUnknown` without an authoritative
side-effect-free remote contract; no automatic retry follows unknown.

The pre-existing local Testing gap (`ConnectionStrings:UpcEcommerceTest` is not
configured) remains unrelated to INT-00 and does not authorize Production
verification.
