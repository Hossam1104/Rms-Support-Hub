# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main`
- **Programme:** No active standalone refactor. The RMS+ Support Hub UI /
  branding / rename programme is complete and closed.
- **Next programme:** POS Maintenance integration planning. See
  `docs/POS_MAINTENANCE_INTEGRATION_READINESS.md`.

This file records durable current facts only. Milestone history lives in
`.ai/HISTORY.md`; implementation evidence lives in Git.

## Identity

| Facet | Value |
|---|---|
| Display name | RMS+ Support Hub |
| .NET root | `RmsSupportHub.*` (`Api`, `Core`, `Data`, `Tests`) |
| npm package | `rms-support-hub` |
| GitHub repository | `Hossam1104/Rms-Support-Hub` |
| Canonical origin | `https://github.com/Hossam1104/Rms-Support-Hub.git` |
| Local folder | `D:\AI Tools\DBS\online_order_tool` — rename to `Rms-Support-Hub` still pending, see below |
| Visibility | Public by explicit owner decision; the owner intends to return it to Private after the POS integration work |

The local folder rename was attempted and refused by Windows: live processes
hold the directory (the open VS Code workspace, the C# Dev Kit and Roslyn
language servers, and the agent shells). Nothing in the repository depends on
the folder name. Close everything rooted there, then from a shell **outside**
it run `Rename-Item -LiteralPath "D:\AI Tools\DBS\online_order_tool" -NewName
"Rms-Support-Hub"`, re-check `git status` and `git remote -v` from the new
path, and update the row above.

## Application

- One Angular 22 SPA and one .NET 10 Web API. Registered tools:
  **QA Prompt Studio** (available), **Online Order Tool** (available), and
  **POS Maintenance Tool** (Coming Soon, informational, non-operational).
- UPC Testing and UPC Production share the existing environment architecture;
  Production uses server-owned UPC Testing connection details with the approved
  `RmsMainProd` catalog override. No browser database/connection detail is
  accepted; Production runtime validation was not executed locally.
- Routes are lazy and typed through `ToolRouteData`; the current topology is in
  `docs/REPOSITORY_STRUCTURE.md`.
- Prompt Studio behavior is frozen: deterministic local builders, canonical
  section counts (Bug 11, Story 7, Test Case 9), advisory quality semantics,
  drafts, ten-entry history, copy, Markdown/text export, and Ctrl/Cmd+Enter.
  No external AI execution.
- Online Order behavior is frozen and server-authoritative: API/DTO/payload
  contracts, validation, totals, filters, paging, statuses, capability guarding,
  send, cancel, and resend.
- No POS operation or generic execution surface exists. Order Requests use a month-to-date default window and tokenized grouped filters; base-only queries cap matching results to ten newest `OrderRequests` by `Id DESC`, while header-derived filters retain paging.

## Compatibility contracts

These persisted storage keys are byte-exact and must never be "cleaned" because
their prefix reflects an earlier product name. No migration exists.

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

Other identifiers that survive renames because they are external or business
contracts, not host identity:

- API paths and the `/api` proxy contract; JSON property names; payload shapes;
  request fixtures; database, table, and SQL column names.
- Module keys `upc_ecommerce`, `ghc_ecommerce`, `ghc_unicommerce`; behavior is
  gated by `IOrderModule.Capabilities`, never a module-key comparison.
- Payment and integration values such as `COD`, `Visa`, `Tamara`, `Tabby`,
  `Mada`, and `CashOnDelivery`.
- Feature wording: `QA Prompt Studio`, `Online Order Tool`,
  `POS Maintenance Tool`.
- Supplied asset filenames and case, including the deliberate `warrning.svg`
  spelling and the root `/assets/Saudi_Riyal.svg` verifier path.

## Design system

Semantic tokens, density and surface geometry, gradients, typography,
animations, shared cards, tables, forms, `ThemeService`, and `MotionService` are
the UI touch points; raw color literals stay in the token and gradient files.
The Hub-only Three.js scene is dynamically imported, decorative, pointer-
transparent, aria-hidden, DPR-capped, visibility-pausing, and disposable, and
degrades to a CSS gradient under reduced motion, absent WebGL, or import
failure. Details are in `docs/design-system.md`.

## Validation baseline

Recorded 2026-08-10 (Online Orders UI redesign, then the items detail grid, then the date range picker fix); see `docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md` for the prior full gate table. Backend rows are from the same day against unchanged backend code.

| Gate | Result |
|---|---|
| Frontend tests | 54 files / 300 tests passed, 0 skipped |
| Backend tests | 174 passed, 0 failed, 0 skipped |
| Release build | 0 warnings, 0 errors; all Angular budgets clear |
| Production initial bundle | 456.40 kB raw / 104.24 kB estimated transfer |
| Lazy `three-module` chunk | 734.66 kB raw / 153.79 kB estimated transfer |
| Production-offline initial bundle | 442.09 kB raw / 103.65 kB estimated transfer |
| Riyal asset verifier | Passed (SHA-1 verified, 924 bytes) |
| Rendered browser pass | Not run; no browser automation tool is available in this environment |

## Boundaries and deferred scope

- Production is out of bounds: no Production access, SQL, deployment, or state-changing action is authorized. Testing is the default environment.
- A running local `RmsSupportHub.Api` locks `backend/src/**/bin`, failing
  `scripts/build.ps1` with MSB3027/MSB3021. Stop it, or pass
  `--artifacts-path <temp>` to `dotnet test`/`dotnet build`. Not a build defect.
- `ConnectionStrings:UpcEcommerceTest` is not configured in the local Testing
  environment, so Testing-only UPC order and order-request calls return HTTP
  500. This is deferred environment setup, not an application defect.
- UPC Production resolver tests use an in-memory connection configuration and fakes only; no Production database or order API call was made.
- UPC Testing fixture acceptance, live COD acceptance/send/resend/cancellation, and Production database index/deployment work all remain deferred and unauthorized.
- POS integration is the next programme and has not started.
