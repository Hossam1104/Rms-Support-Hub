# Current Project State

- **Updated:** 2026-08-10
- **Branch:** `main`
- **Programme:** RMS+ Support Hub UI/branding/rename complete; INT-00 POS
  cross-project architecture decision closure complete.
- **Next gate:** CLAUDE OPUS 5 POS INTEGRATION ARCHITECTURE CHECKPOINT - review required / no execution authorized; INT-01 blocked.

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

INT-00 is documentation/governance complete. The canonical target is a separate
Windows `RmsSupportHub.Pos.Agent` reached directly by Support Hub Angular over
HTTPS on fixed loopback; `RmsSupportHub.Api`, `Core`, and `Data` are not its path.

```text
INT-00: COMPLETE
CROSS-PROJECT INTEGRATION PLAN: COMPLETE
POS SOURCE: MERGE-READY CANDIDATE AT 25922b499d33bd73f241ffc26c212dd000e81433
PRIVILEGED POS BOUNDARY: SEPARATE WINDOWS POS AGENT
PREFERRED TRANSPORT: SUPPORT HUB BROWSER -> LOCAL LOOPBACK POS AGENT
LOCAL NETWORK ACCESS: ARCHITECTURE DECIDED / LIVE EVIDENCE OPEN
STANDALONE POS ANGULAR: FROZEN / REFERENCE ONLY
WINUI: RETAINED
REPOSITORY IMPORT: NOT AUTHORIZED
INTEGRATION IMPLEMENTATION: NOT AUTHORIZED
INT-01: BLOCKED
CLAUDE OPUS 5 ARCHITECTURE CHECKPOINT: REQUIRED
```

Agent origin: `https://rms-pos-agent.localhost:<fixed-port>`. Future transport
uses exact-origin CORS/LNA, Windows Negotiate, and the selected one-use request-token contract; Kerberos preferred, NTLM fallback acceptable pending evidence, no delegation.

Future destination: `/pos/src` contains portable Domain/Application/Contracts,
Windows Infrastructure/Agent, retained WinUI, and `/pos/tests`; it is not created by INT-00. Raw history merge, import, OpenAPI/client, CI, and runtime work remain blocked.

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
  implementation remain deferred and unauthorized.
- POS evidence gates remain open: LocalSystem/Session 0 SMB, live transport,
  LNA/managed-browser, Negotiate/SPN, real SQL/SCM/restore/maintenance/
  downloader, remote-trigger reconciliation/idempotency, SQL TLS
  (`TrustServerCertificate = true`), and WinUI cutover by design. Architecture
  decisions are not evidence.
