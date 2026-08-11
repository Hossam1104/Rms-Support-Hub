# Current Project State

- **Updated:** 2026-08-11
- **Branch:** `main`
- **Programme:** RMS+ Support Hub UI/branding/rename complete; INT-00, INT-00R,
  INT-01, and INT-02 portable POS source import complete.
- **Next gate:** INT-03 Windows Infrastructure + retained WinUI import - owner authorization required / not yet executed.

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

- One Angular 22 SPA and one .NET 10 Web API. QA Prompt Studio and Online Order
  Tool are available; POS Maintenance is Coming Soon and non-operational.
- Routes are lazy/typed through `ToolRouteData`; topology is in `docs/REPOSITORY_STRUCTURE.md`.
- Prompt Studio is frozen: deterministic builders, Bug 11/Story 7/Test Case 9,
  advisory quality, drafts, ten-entry history, copy/exports, shortcuts; no external AI execution.
- Online Order is frozen/server-authoritative: API/DTO/payload contracts,
  validation, totals, filters, paging, statuses, capability guarding, send/cancel/resend.
- UPC Testing/Production retain existing architecture; no browser connection detail or local Production validation.
- No POS operation/generic execution surface exists. Order Requests use month-to-date,
  tokenized filters, a ten-newest base-only path, and paging for header-derived filters.
## POS integration architecture checkpoint
INT-00 and INT-00R are documentation/governance complete. The canonical target
is a separate Windows `RmsSupportHub.Pos.Agent` reached directly by Support Hub
Angular over trusted HTTPS on fixed loopback using HTTP/1.1 only;
`RmsSupportHub.Api`, `Core`, and `Data` are not its path.

```text
INT-00 / INT-00R: COMPLETE; CLAUDE ARCHITECTURE CHECKPOINT: PASS
POS SOURCE: MERGE-READY CANDIDATE AT 25922b499d33bd73f241ffc26c212dd000e81433
PRIVILEGED POS BOUNDARY: SEPARATE WINDOWS POS AGENT
TRANSPORT: TRUSTED HTTPS / HTTP/1.1; SUPPORT HUB SECURE CONTEXT REQUIRED
LNA: VERSIONED CHROME/EDGE MATRIX / LIVE EVIDENCE OPEN
WINDOWS LOOPBACK AUTH: BACK-CONNECTION / HOSTNAME EVIDENCE OPEN
CORS: ANONYMOUS EXACT-ORIGIN PREFLIGHT; APP: NEGOTIATE + LOCAL ADMIN
MACHINE TRUST: MANDATORY; SSE: READ-ONLY / NO MUTATION TOKEN
ARTIFACTS: AUTHENTICATED FETCH / OPAQUE HANDLE; TOKEN: SINGLE-USE / OPERATION-BOUND
DIRECT BROWSER TO LOOPBACK AGENT: PER-DEVICE LOCAL MAINTENANCE ARCHITECTURE
PREFERRED TRANSPORT: SUPPORT HUB BROWSER → LOCAL LOOPBACK POS AGENT
STANDALONE POS ANGULAR: FROZEN / REFERENCE ONLY; WINUI: RETAINED
REPOSITORY IMPORT / INTEGRATION IMPLEMENTATION: INT-02 PORTABLE BOUNDARY IMPORTED; WINDOWS RUNTIME NOT EXECUTED
INT-01: DESTINATION SKELETON COMPLETE; INT-02: PORTABLE SOURCE + TESTS IMPORTED / PASSING
INT-03: WINDOWS INFRASTRUCTURE + RETAINED WINUI OWNER-GATED
```
Agent origin: `https://rms-pos-agent.localhost:<fixed-port>`; trusted machine
certificate provisioning is mandatory. Kerberos is preferred but `.localhost`
SPN and NTLM loopback/back-connection behavior remain open; REST is state
truth, SSE is read-only/no-token progress, and artifacts use authenticated
fetch with opaque handles.

INT-02 destination: `/pos/RmsSupportHub.Pos.slnx` contains five source and two
test projects. Graph: Application→Domain; Infrastructure→Application+Domain;
Agent→Contracts+Domain+Application+Infrastructure; tests→their portable
source projects. Domain/Contracts have no packages; Application uses
`Microsoft.Extensions.Logging.Abstractions` 10.0.10; tests use the approved
baseline. No Infrastructure, Agent runtime, WinUI, POS Angular, raw history,
  or privileged implementation was imported. CI builds/tests portable on Ubuntu
  and builds the POS solution on Windows. INT-03 remains owner-gated.
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
fixtures, database/table/SQL names, module keys, payment values, statuses, wording, and asset filenames. Behavior is capability-gated; UPC methods are Visa/Tamara/Tabby (ADR-0014).

## Design system

Semantic tokens, density, surfaces, typography, cards, tables, forms,
`ThemeService`, and `MotionService` are UI touch points; raw colors stay in token files. The decorative lazy Hub Three.js scene degrades safely; details: `docs/design-system.md`.

## Validation baseline

Recorded 2026-08-10; see `docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md` for the prior full gate table.

| Gate | Result |
|---|---|
| Frontend tests | 55 files / 325 tests passed, 0 skipped |
| Backend tests | 188 passed, 0 failed, 0 skipped |
| Release build | 0 warnings, 0 errors; Angular budgets clear |
| Production initial bundle | 456.42 kB raw / 104.28 kB estimated transfer |
| Lazy `three-module` chunk | 734.66 kB raw / 153.90 kB estimated transfer |
| Production-offline initial bundle | 442.11 kB raw / 103.63 kB estimated transfer |
| Riyal asset verifier | Passed; SHA-1 verified, 924 bytes |
| Rendered browser pass | Not run; browser automation unavailable in this environment |

## Boundaries and deferred scope

- Production access, SQL, deployment, and state-changing actions are out of bounds; Testing is default. A running local API can lock `backend/src/**/bin`; use a stopped API or temporary artifacts path.
- `ConnectionStrings:UpcEcommerceTest` is absent locally, so Testing-only UPC
  order/request calls return HTTP 500; deferred environment setup.
- UPC fixture/live acceptance, Production index/deployment, and all POS
  Windows/runtime implementation remain deferred and unauthorized.
- POS evidence gates remain open: LocalSystem/Session 0 SMB, live transport,
  LNA/managed-browser, Negotiate/SPN, real SQL/SCM/restore/maintenance/
  downloader, remote-trigger reconciliation/idempotency, SQL TLS
  (`TrustServerCertificate = true`), and WinUI cutover by design. Architecture
  decisions are not evidence.
