# Current Project State
- **Updated:** 2026-08-12
- **Branch:** `int-06i-admin-auth-scalar`
- **Programme:** RMS+ Support Hub UI/branding/rename complete; INT-00 through INT-05F and INT-CI01 POS integration work complete within their approved boundaries; INT-06I implementation is complete at its bounded source/documentation scope.
- **Next gate:** Independent security review of INT-06I. The UAC-safe local-Administrator resolver, shared session/mutation authorization seam, and non-production Scalar/OpenAPI documentation are implemented and locally validated; the focused PR is not merged, live Chrome/Edge post-remediation evidence is not claimed, and INT-07 remains unauthorized.
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
- Prompt Studio is frozen: deterministic builders, Bug 11/Story 7/Test Case 9, advisory quality, drafts, ten-entry history, copy/exports, shortcuts; no external AI execution.
- Online Order is frozen/server-authoritative: API/DTO/payload contracts, validation, totals, filters, paging, statuses, capability guarding, send/cancel/resend.
- UPC Testing/Production retain existing architecture; no browser connection detail or local Production validation.
- No POS operation/generic execution surface exists. Order Requests use month-to-date, tokenized filters, a ten-newest base-only path, and paging for header-derived filters.
## POS integration architecture checkpoint
INT-00 and INT-00R are documentation/governance complete. The canonical target is a separate Windows `RmsSupportHub.Pos.Agent` reached directly by Support Hub Angular over trusted HTTPS on fixed loopback using HTTP/1.1 only; `RmsSupportHub.Api`, `Core`, and `Data` are not its path.
```text
INT-00 / INT-00R: COMPLETE; CLAUDE ARCHITECTURE CHECKPOINT: PASS
ORIGINAL IMPORT PROVENANCE (INT-01/02/03): 25922b499d33bd73f241ffc26c212dd000e81433
CORRECTED AGENT PROVENANCE BASELINE (INT-04): 010abc52dc110cfde3dc2c53e057890ff6edaf97
CLAUDE OPUS PRIVILEGED-BOUNDARY REVIEW: PROV-1 CLOSED BY INT-03R / LIVE EVIDENCE OPEN
INT-04: AGENT HOST / RUNTIME COMPOSITION COMPLETE
INT-05: BROWSER TRANSPORT / OPENAPI / CLIENT ADAPTER COMPLETE / ACCEPTED AFTER INT-05F
INT-05F: OPENAPI GENERATOR PEER-DEPENDENCY ISOLATION COMPLETE; ISOLATED TOOLING DEPENDENCY GRAPH
ANGULAR TYPESCRIPT: 6.x; GENERATOR TYPESCRIPT: 5.x PEER-COMPATIBLE; GLOBAL LEGACY PEER BYPASS: NONE
INT-CI01: PORTABLE APPLICATION LINUX CI BASELINE REMEDIATION COMPLETE; WINDOWS-DETERMINISTIC MAINTENANCE/SMB PATH SEMANTICS
PORTABLE UBUNTU POS CI: ALL FIVE POS CI LANES GREEN
INT-06: LIVE TRANSPORT SECURITY EVIDENCE - BLOCKED / FAILED
INT-06F: ELEVATED LIVE TRANSPORT SECURITY EVIDENCE - BLOCKED / FAILED
INT-06G: PRE-ELEVATED LIVE TRANSPORT SECURITY EVIDENCE - BLOCKED / FAILED
INT-06H: REAL BROWSER RUNTIME EVIDENCE - BLOCKED / FAILED
BLOCKER: NORMAL CHROME/EDGE BROWSER NEGOTIATE SESSION AUTHENTICATED BUT LOCAL-ADMINISTRATOR AUTHORIZATION WAS FALSE; ELEVATED CONTROL WAS AUTHORIZED AND REACHED OPERATION_NOT_SUPPORTED, INDICATING A POTENTIAL UAC-FILTERED-TOKEN DEFECT
INT-06I: UAC-SAFE ADMINISTRATOR AUTHORIZATION + SCALAR/OPENAPI DOCUMENTATION - IMPLEMENTED; INDEPENDENT SECURITY REVIEW OPEN
AUTHORIZATION: WINDOWS ACCOUNT LOCAL-GROUP MEMBERSHIP VIA NETUSERGETLOCALGROUPS / LG_INCLUDE_INDIRECT; WELL-KNOWN BUILTIN-ADMINISTRATORS SID; FAIL CLOSED
DOCUMENTATION: SCALAR.ASPNETCORE 2.16.18; DEVELOPMENT/INTEGRATIONTEST ONLY; AI AGENT AND DEFAULT FONTS DISABLED; OPENAPI/CLIENT DRIFT CHECKED
LIVE POST-REMEDIATION BROWSER EVIDENCE: NOT CLAIMED / CONNECTED CHROME-EDGE SESSION NOT AVAILABLE IN THIS EXECUTION CONTEXT
NEXT: INDEPENDENT SECURITY REVIEW; PR NOT MERGED
TRANSPORT: TRUSTED HTTPS / HTTP/1.1; SUPPORT HUB SECURE CONTEXT REQUIRED
LNA: VERSIONED CHROME/EDGE MATRIX / INT-06H PROVEN ON TESTED DEVICE
WINDOWS LOOPBACK AUTH: EXACT BACK-CONNECTION / HOSTNAME PROVEN; REPRESENTATIVE-DEVICE EVIDENCE OPEN
CORS: ANONYMOUS EXACT-ORIGIN PREFLIGHT; APP: NEGOTIATE + LOCAL ADMIN
MACHINE TRUST: MANDATORY; SSE: READ-ONLY / NO MUTATION TOKEN
REPOSITORY IMPORT / INTEGRATION IMPLEMENTATION: INT-05 Agent contract/client foundation composed; no Support Hub backend relay or POS UI activation
```
Agent origin: `https://rms-pos-agent.localhost:5001`; the host is headless,
Windows Service-capable, loopback-only, HTTPS-only, HTTP/1.1-only, and uses
production Negotiate, exact-origin CORS, SID/local-Administrators checks, and
only foundation routes. INT-05 owns `/pos/openapi`, the generated client, and
direct `HttpBackend` transport; INT-06I owns shared UAC-safe authorization and
non-production Scalar/OpenAPI reachability; Production still has no feature
registry or runtime OpenAPI. Trusted machine certificate provisioning and
`.localhost` SPN/NTLM
loopback behavior remain live evidence; feature transports and POS UI activation
remain future scope.
INT-04 destination: `/pos/RmsSupportHub.Pos.slnx` contains six source and four
test projects. Graph: Application→Domain; Infrastructure→Application+Domain;
Agent→Contracts+Domain+Application+Infrastructure; WinUI→Application+Domain+
Infrastructure; tests→their source projects. Domain/Contracts have no packages;
Application uses `Microsoft.Extensions.Logging.Abstractions` 10.0.10;
Infrastructure owns the Windows SQL/SCM/DPAPI graph and WinUI retains its
Windows App SDK packages. The Agent host has only foundation routes; feature
operations, POS UI activation, standalone POS Angular source, raw history, and
general backend/frontend integration remain excluded. CI validates portable
POS projects, Windows Agent/Infrastructure, OpenAPI/client drift, and WinUI
publish. POS Release tests pass Domain 7/7, Application 76/76,
Infrastructure 60/60, and Agent 85/85; frontend passes 341/341.
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
| POS portable CI | GitHub Actions run `31540243375`; all five POS CI lanes passed |
| POS Release tests | Domain 7/7, Application 76/76, Infrastructure 60/60, Agent 85/85; 0 skipped |
| POS Release build | 0 warnings, 0 errors |
| Retained WinUI publish | `PosAdminTool.WinUI.exe`, 23 `.pri`, and 55 `.xbf` resources present |
| Production initial bundle | 456.13 kB raw / 104.22 kB estimated transfer |
| Lazy `three-module` chunk | 734.66 kB raw / 153.96 kB estimated transfer |
| Production-offline initial bundle | 442.06 kB raw / 103.59 kB estimated transfer |
| Riyal asset verifier | Passed; SHA-1 verified, 924 bytes |
| Rendered browser pass | Not run; browser automation unavailable in this environment |
## Boundaries and deferred scope
- Production access, SQL, deployment, and state-changing actions are out of bounds; Testing is default. A running local API can lock `backend/src/**/bin`; use a stopped API or temporary artifacts path.
- `ConnectionStrings:UpcEcommerceTest` is absent locally, so Testing-only UPC
  order/request calls return HTTP 500; deferred environment setup.
- UPC fixture/live acceptance, Production index/deployment, and POS Agent
  business-operation/device validation remain deferred. INT-05 transport was
  build/test validated; INT-06G proved temporary machine transport/Negotiate,
  and INT-06H proved actual-browser LNA/direct health/session transport, but
  browser mutation authorization and POS operations remain open.
- POS evidence gates remain open: LocalSystem/Session 0 SMB, representative-
  device/live transport, managed-browser deployment, Negotiate/SPN, real SQL/SCM/restore/maintenance/
  downloader, remote-trigger reconciliation/idempotency, SQL TLS
  (`TrustServerCertificate = true`), and WinUI cutover; architecture decisions
  are not evidence. INT-06/INT-06F remain historical blocks; INT-06G and
  INT-06H restored all temporary machine/browser state, with the browser admin
  authorization finding remediated in source but post-remediation browser
  evidence and independent security review still open.
