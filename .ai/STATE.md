# Current Project State
- **Updated:** 2026-08-11
- **Branch:** `main`
- **Programme:** RMS+ Support Hub UI/branding/rename complete; INT-00 through
  INT-05 POS integration work complete within their approved boundaries.
- **Next gate:** INT-06 Live Transport Security Evidence requires fresh owner authorization; it is not executed in this session.
This file records durable facts only; milestone history lives in `.ai/HISTORY.md` and implementation evidence lives in Git.
## Identity
| Facet | Value |
|---|---|
| Display name | RMS+ Support Hub |
| .NET root | `RmsSupportHub.*` (`Api`, `Core`, `Data`, `Tests`) |
| npm package | `rms-support-hub` |
| GitHub repository | `Hossam1104/Rms-Support-Hub` |
| Canonical origin | `https://github.com/Hossam1104/Rms-Support-Hub.git` |
| Local folder | `D:\AI Tools\DBS\online_order_tool` (rename to `Rms-Support-Hub` pending; live processes previously held it) |
| Visibility | Public by explicit owner decision; owner intends Private after POS integration |
## Application
- One Angular 22 SPA and one .NET 10 Web API. QA Prompt Studio and Online Order Tool are available; POS Maintenance is Coming Soon and non-operational.
- Routes are lazy/typed through `ToolRouteData`; topology is in `docs/REPOSITORY_STRUCTURE.md`.
- Prompt Studio is frozen: deterministic builders, Bug 11/Story 7/Test Case 9,
  advisory quality, drafts, ten-entry history, copy/exports, shortcuts; no external AI execution.
- Online Order is frozen/server-authoritative: API/DTO/payload contracts, validation, totals, filters, paging, statuses, capability guarding, send/cancel/resend.
- UPC Testing/Production retain existing architecture; no browser connection detail or local Production validation.
- No POS operation/generic execution surface exists. Order Requests use month-to-date, tokenized filters, a ten-newest base-only path, and paging for header-derived filters.
## POS integration architecture checkpoint
INT-00 and INT-00R are documentation/governance complete. The canonical target
is a separate Windows `RmsSupportHub.Pos.Agent` reached directly by Support Hub
Angular over trusted HTTPS on fixed loopback using HTTP/1.1 only; `RmsSupportHub.Api`,
`Core`, and `Data` are not its path.
```text
INT-00 / INT-00R: COMPLETE; CLAUDE ARCHITECTURE CHECKPOINT: PASS
ORIGINAL IMPORT PROVENANCE (INT-01/02/03): 25922b499d33bd73f241ffc26c212dd000e81433
CORRECTED AGENT PROVENANCE BASELINE (INT-04): 010abc52dc110cfde3dc2c53e057890ff6edaf97
CLAUDE OPUS PRIVILEGED-BOUNDARY REVIEW: PROV-1 CLOSED BY INT-03R / LIVE EVIDENCE OPEN
INT-04: AGENT HOST / RUNTIME COMPOSITION COMPLETE
INT-05: BROWSER TRANSPORT / OPENAPI / CLIENT ADAPTER COMPLETE
NEXT: INT-06 LIVE TRANSPORT SECURITY EVIDENCE - OWNER AUTHORIZATION REQUIRED
TRANSPORT: TRUSTED HTTPS / HTTP/1.1; SUPPORT HUB SECURE CONTEXT REQUIRED
LNA: VERSIONED CHROME/EDGE MATRIX / LIVE EVIDENCE OPEN
WINDOWS LOOPBACK AUTH: BACK-CONNECTION / HOSTNAME EVIDENCE OPEN
CORS: ANONYMOUS EXACT-ORIGIN PREFLIGHT; APP: NEGOTIATE + LOCAL ADMIN
MACHINE TRUST: MANDATORY; SSE: READ-ONLY / NO MUTATION TOKEN
REPOSITORY IMPORT / INTEGRATION IMPLEMENTATION: INT-05 Agent contract/client foundation composed; no Support Hub backend relay or POS UI activation
INT-05: BROWSER TRANSPORT / OPENAPI / CLIENT ADAPTER COMPLETE; INT-06 LIVE EVIDENCE OWNER-GATED
```
Agent origin: `https://rms-pos-agent.localhost:5001`; the host is headless, Windows
Service-capable, loopback-only, HTTPS-only, and HTTP/1.1-only. It
registers Negotiate in production, uses exact-origin CORS and local-Administrators/
SID ownership checks, and exposes only health/live, health/ready,
authenticated session, and mutation-token foundation routes. INT-05 adds the destination-owned
versioned OpenAPI document at `/pos/openapi`, build-time
generation, the exact `openapi-typescript@7.13.0` Support Hub type output under
`frontend/src/app/core/pos-agent/generated/`, and a dedicated `HttpBackend` transport
with credentialed session/token requests and conservative error classification.
Production registers no feature mutation descriptors and does
not expose runtime OpenAPI. Trusted machine certificate provisioning is
mandatory. Kerberos is preferred; `.localhost` SPN and NTLM loopback/back-
connection behavior remain open live evidence. REST/SSE/artifact feature
transports and POS UI activation remain future scope.
INT-04 destination: `/pos/RmsSupportHub.Pos.slnx` contains six source and four
test projects. Graph: Application→Domain; Infrastructure→Application+Domain;
Agent→Contracts+Domain+Application+Infrastructure; WinUI→Application+Domain+
Infrastructure; tests→their source projects. Domain/Contracts have no packages;
Application uses `Microsoft.Extensions.Logging.Abstractions` 10.0.10.
Infrastructure owns the Windows SQL/SCM/DPAPI package graph; WinUI retains
Windows App SDK and CommunityToolkit packages. The Agent host has only the
approved foundation routes; feature operations, POS UI activation, standalone
POS Angular source, raw history, and general backend/frontend integration remain excluded. CI builds
and tests portable projects on Ubuntu, builds/tests the POS solution and Agent
on Windows, validates the OpenAPI/generated-client drift lane, and validates a
WinUI publish artifact. Current POS Release tests pass Domain 7/7, Application
75/75, Infrastructure 60/60, and Agent 69/69; the frontend suite passes 341/341
across 56 files.
## Compatibility contracts
These persisted storage keys are byte-exact; no migration exists:
```text
onlineOrderTool.activeEnvironment.<moduleKey>
qa-support-hub:theme
qa-support-hub:motion
qa-support-hub.prompt-studio.history
qa-support-hub.prompt-studio.bug-draft
qa-support-hub.prompt-studio.story-draft
qa-support-hub.prompt-studio.test-case-draft
order-tool.sidebar-collapsed
```
External/business identifiers remain unchanged: API `/api`, JSON/payload contracts,
fixtures, database/table/SQL names, module keys, payment values, statuses, and wording. Behavior is capability-gated; UPC methods are Visa/Tamara/Tabby (ADR-0014).
Asset filenames/folders are owner-supplied, not a contract: `frontend/public/assets/` mirrors the supplied `assets/` drop (`CompanyLogos/`, `ClientsLogo/`, `Payments/`, `CustomMessageBox/`, root `Saudi_Riyal.svg`, `loader.svg`, and `offer_logo.png`); `app-assets.ts` is the only path source, and RMS+/DBS use `themedAsset` colourway pairs.
## Design system
Semantic tokens, density, surfaces, typography, cards, tables, forms,
`ThemeService`, and `MotionService` are UI touch points; raw colors stay in token files. The decorative lazy Hub Three.js scene degrades safely; details: `docs/design-system.md`.
The Hub is a two-band single-viewport layout: a hero carrying the paired RMS+ and DBS lockups in one plate at a shared height, and an elastic tool grid. There is no footer band. Above 1024x720 the page is locked to `100dvh`, while smaller viewports scroll normally.
The Online Order landing mirrors that reading order: hero with a module summary, a directory heading, then a 3/2/1-column grid. `app-module-card` follows the tool-card structure and accent names; its grid uses `grid-auto-rows: auto` so Coming Soon rows stay compact.
Environment reachability is a separate fact from the Live/Test lane label and is never merged into it (ADR-0019): `GET /api/modules/health` TCP-probes each endpoint, caches 30s, and the card shows the result as its own chip. A missing entry is `unknown`, never `unreachable`.
## Validation baseline
Frontend rows re-recorded 2026-08-11; backend row stands from 2026-08-10. See `docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md` for the prior full gate table.
| Gate | Result |
|---|---|
| Frontend tests | 56 files / 341 tests passed, 0 skipped |
| Backend tests | 192 passed, 0 failed, 0 skipped |
| Release build | 0 warnings, 0 errors; Angular budgets clear |
| Production initial bundle | 456.13 kB raw / 104.22 kB estimated transfer |
| Lazy `three-module` chunk | 734.66 kB raw / 153.96 kB estimated transfer |
| Production-offline initial bundle | 442.06 kB raw / 103.59 kB estimated transfer |
| Riyal asset verifier | Passed; SHA-1 verified, 924 bytes |
| Rendered browser pass | Not run; browser automation unavailable in this environment |
## Boundaries and deferred scope
- Production access, SQL, deployment, and state-changing actions are out of bounds; Testing is default. A running local API can lock `backend/src/**/bin`; use a stopped API or temporary artifacts path.
- `ConnectionStrings:UpcEcommerceTest` is absent locally, so Testing-only UPC
  order/request calls return HTTP 500; deferred environment setup.
- UPC fixture/live acceptance, Production index/deployment, and POS Agent live
  service/device validation remain deferred. INT-05's Agent contract and
  transport were build/test validated; no live service, SQL, SCM, SMB, device,
  or browser runtime was executed.
- POS evidence gates remain open: LocalSystem/Session 0 SMB, live transport,
  LNA/managed-browser, Negotiate/SPN, real SQL/SCM/restore/maintenance/
  downloader, remote-trigger reconciliation/idempotency, SQL TLS
  (`TrustServerCertificate = true`), and WinUI cutover by design; architecture
  decisions are not evidence.
