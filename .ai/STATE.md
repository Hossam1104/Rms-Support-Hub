# Current Project State

- **Updated:** 2026-08-09
- **Branch:** `main` after Session 07.1 merge
- **Current program:** RMS+ Support Hub UI / Branding Refactor
- **Session status:** Session 07 completed; Session 07.1 remediation completed; Opus R2 remediation acceptance required; Session 08 blocked
- **Opus R2:** Remediation Required; HIGH-1 Session 08 persisted-storage-key guardrail resolved by Session 07.1
- **Current GitHub repository:** `Hossam1104/online_order_tool`
- **Future GitHub repository:** `Hossam1104/Rms-Support-Hub` (Session 08)
- **Repository visibility:** Current Public; owner decision pending; planner recommends Private due to committed internal network topology
- **Session 08 gate:** Blocked pending planner acceptance of Session 07.1 and the repository visibility decision
- **Product target:** `RMS+ Support Hub`
- **Technical target:** `RmsSupportHub`
- **npm target:** `rms-support-hub`

This file records durable current facts only. Milestone history lives in
`.ai/HISTORY.md`; detailed implementation evidence belongs in Git and the
Session 00 map.

## Application

- One Angular 22 SPA and one .NET 10 Web API host the RMS+ Support Hub. The
  registered tools are **QA Prompt Studio** and **Online Order Tool**; **POS
  Maintenance Tool** is Coming Soon and non-operational.
- Routes are lazy and typed through `ToolRouteData`; the Hub, three Prompt
  Studio generators, Online Orders, canonical/legacy Online Order mounts, and
  POS routes are mapped in `docs/RMS_SUPPORT_HUB_REFACTOR_MAP.md`.
- Prompt Studio behavior remains frozen: deterministic builders, canonical
  sections, quality semantics, draft persistence, history capped at ten, copy/
  Markdown/text export, and Ctrl/Cmd+Enter.
- Online Order behavior remains frozen and server-authoritative: API/DTO/
  payload contracts, validation, totals, filters, paging, statuses,
  capability guarding, cancel, and resend.
- No POS operation or generic execution surface exists.

## Design system and scene

- Semantic tokens, gradients, typography, animations, shared cards, tables,
  forms, ThemeService, and MotionService remain the current UI touch points;
  raw component colors stay restricted to token/gradient files.
- The Hub-only Three.js scene is dynamically imported, decorative, lazy,
  pointer-transparent, aria-hidden, DPR-capped, visibility-pausing, and
  disposable; the restrained themed core/halo is optional, and reduced
  motion/WebGL/import failure falls back to CSS.

## Session 00 baseline

- Frontend: 46 test files, 250 tests passed.
- Backend: 161 tests passed, 0 failed, 0 skipped in Release configuration.
- Production Angular build: 439.28 kB initial raw / 101.43 kB estimated
  transfer, with no budget warnings.
- Lazy `three-module`: 734.66 kB raw / 153.98 kB estimated transfer.
- Naming inventory, asset inventory, UI touch map, risky identifiers, routes,
  project names, and session dependencies are complete in
  `docs/RMS_SUPPORT_HUB_REFACTOR_MAP.md`.
- The 17 supplied assets are tracked under root `assets/`; no exact duplicate
  hashes were found, but `frontend/public/` has no asset copy and the Riyal
  verifier currently fails on its missing public path. Asset integration is
  Session 02 scope.

## Session 01 rename and validation

- Host-level product identity is now **RMS+ Support Hub**; the preserved tools
  remain **QA Prompt Studio**, **Online Order Tool**, and **POS Maintenance
  Tool**.
- The physical backend solution/projects, namespaces, project references,
  assembly/root namespaces, scripts, current technical docs, and test assembly
  now use `RmsSupportHub.*`; the Angular npm package is `rms-support-hub`.
- External behavior was preserved: routes, API/DTO/payload contracts, SQL and
  database identifiers, module keys, payment values, persisted values,
  capability names, and POS Coming Soon boundaries were not changed. The
  current GitHub remote remains unchanged; the GitHub rename is Session 08.
- Required validation passed: 46 frontend test files / 250 tests, 161 backend
  tests, Release build with 0 warnings and 0 errors, production bundle
  439.28 kB raw / 101.42 kB estimated transfer, and lazy Three.js
  734.66 kB raw / 153.95 kB estimated transfer.
- The stale-name audit found no old technical/host names in current source,
  scripts, README, or current technical docs. Remaining matches are justified
  historical ADRs, baseline/map records, and the active execution prompt.
- Session 02 resolved the asset integration gap described above; the canonical
  Riyal SVG is now present at the verifier-required public path.

## Session 02 asset pipeline

- Session 02 completed with the typed catalog at
  `frontend/src/app/core/config/app-assets.ts` and semantic public folders under
  `frontend/public/assets/brand`, `modules`, `payments`, `commerce`, and
  `system`; `frontend/public/assets/Saudi_Riyal.svg` remains as a deliberate
  compatibility path.
- RMS is integrated into the navbar and Hub identity; DBS is a small Hub
  attribution; UPC and confirmed GHC/Whites context are integrated into module
  cards and the module sidebar. The reusable primitive is
  `frontend/src/app/shared/ui/brand-mark/brand-mark.component.ts`.
- Payment, offer, loader, and confirmed CustomMessageBox assets are catalogued.
  The supplied `warrning.svg` filename is preserved under the semantic
  `warning` key; no new message behavior was added.
- Session 02 validation passed: 48 frontend test files / 255 tests, 161 backend
  tests, Release build with 0 warnings and 0 errors, production initial bundle
  439.28 kB raw / 101.39 kB estimated transfer, lazy Three.js 734.66 kB raw /
  153.89 kB estimated transfer, Riyal verifier, and `git diff --check`.

## Session 03 density, tables and surface system

- Session 03 completed the global density contract in `_tokens.css` for page,
  section, panel, form, control, card, and table geometry, then applied it to
  shared shells, cards, headers, fields, inputs, selects, buttons, toolbars,
  pagination, and representative feature surfaces.
- `ui-table` now provides a shared 1px outer border, visible header boundary,
  row separators, compact tokenized insets, numeric alignment, totals/footer
  treatment, and responsive overflow. Products, Payments, Unicommerce Items,
  and the Order Requests virtualized list use the standardized treatment.
- Session 03 was presentation-only: Prompt Studio behavior, Online Order API /
  DTO / payload contracts, totals, filters, paging, statuses, payment values,
  capabilities, and POS Coming Soon behavior remain unchanged.
- Session 03 validation: 48 frontend test files / 255 tests passed; 161 backend
  tests passed; Release build completed with 0 warnings and 0 errors; standalone
  production build completed without warnings at 440.43 kB initial raw /
  101.69 kB estimated transfer, with lazy Three.js at 734.66 kB raw /
  153.85 kB estimated transfer; the offline build and Riyal verifier passed;
  the repository wrapper passed all checks; and `git diff --check` passed.

## Session 04 landing, cards and motion

- The Hub landing now uses a compact two-column hero with RMS identity, DBS
  attribution, workspace/default-lane status, and a tool-availability rail;
  tool routing and POS Coming Soon remain unchanged.
- `app-tool-card` retains equal-height peers and a pinned footer group while
  adding semantic capability/status/action icons. Shared motion distances are
  tokenized so hover/focus movement collapses under reduced motion.
- The existing lazy Hub-only scene gained a small themed RMS+ core/halo with
  explicit geometry/material disposal; dynamic import, fallback, isolation,
  visibility pause, and teardown boundaries remain intact.
- Validation: 49 frontend test files / 258 tests; production build passed with
  no warnings at 440.68 kB initial raw / 101.68 kB estimated transfer, with a
  734.66 kB raw / 153.90 kB estimated-transfer Three.js lazy chunk. The
  interactive browser connector was unavailable; no browser claim is made.

## Session 05 Prompt Studio harmonization

- The Prompt Studio landing keeps three equal-height native-route cards with
  meaningful generator icons, capability summaries, and aligned footer actions.
- Generator workspaces now use compact branded identity headers and tokenized
  panel geometry, with explicit action icons for Load Sample, Generate, Clear,
  Copy, Download, Prompt Quality, and Recent Prompts history.
- The shared button hover lift is tokenized and collapses to zero under reduced
  motion; no nonessential Prompt Studio transform bypasses the motion contract.
- Prompt Studio business behavior remains frozen: Bug has 11 builder sections,
  Story has 7, Test Case has 9; quality semantics, drafts, history max 10,
  copy, Markdown/text export, and Ctrl/Cmd+Enter are unchanged.
- Validation passed: focused Prompt Studio 11 files / 48 tests, full frontend
  49 files / 259 tests, production build with no warnings at 440.74 kB initial
  raw / 101.73 kB estimated transfer, and `git diff --check`. Browser was not
  run; no interactive browser connector was available.

## Session 06 Online Orders dense UI and commerce visuals

- The Online Order Builder now uses compact shared page-header and section
  geometry, tighter local workflow spacing, and tokenized form/panel gaps.
  Products, Payments, and Uni-Commerce Items use the shared wide table shell
  with readable borders, separators, Riyal presentation, and display-only
  totals where applicable. Order Request details reuse the same commerce
  visuals without changing the virtualized list or request behavior.
- The asset catalog now exposes an exact known-payment resolver for Visa,
  Mastercard, Mada, Tabby, and Tamara; unknown methods keep the neutral icon.
  Existing offer contexts use the supplied offer asset, and all changed
  monetary displays use the canonical Riyal component/asset.
- Reviewer R1 medium findings are resolved: the virtual row height now has one
  TypeScript source stamped into CSS and both dialog icon-only close controls
  have accessible names. Low findings are carried into Session 07: reset
  reduced-motion animation delays to zero, standardize Hub/Prompt Studio
  stagger handling, and keep any `capabilityIcon()` redesign optional.
- Session 06 validation passed: 52 frontend test files / 265 tests, 161
  backend tests, Release build with 0 warnings and 0 errors, production
  initial bundle 441.37 kB raw / 101.90 kB estimated transfer, and the
  `three-module` lazy chunk 734.66 kB raw / 154.04 kB estimated transfer.
  The Riyal verifier and `git diff --check` passed. Browser validation was not
  run because the prescribed browser control was unavailable.

## Session 07 Cross-Project UI Closure and browser matrix

- Resolved the Session 06 carry-forward findings: reduced-motion animation
  delays are reset, Prompt Studio uses the shared motion gate, responsive
  workflow navigation wraps below desktop width, the JSON tree toolbar stays
  inside narrow panels, and Online Order module card surfaces stretch to the
  shared grid row height.
- Rendered validation passed for all 9 required routes at 1440/1024/900/768/390
  in light and dark themes, plus reduced-motion coverage. Every case had one
  H1, one main landmark, no page shell overflow, no unlabeled visible form
  control, no unnamed visible link/button, and no broken image. Three.js was
  present only on Hub in full motion, absent elsewhere and under reduced motion,
  and was removed after client-side navigation away from Hub.
- Testing-only UPC order and order-request calls returned HTTP 500 because
  `ConnectionStrings:UpcEcommerceTest` is not configured in the local Testing
  environment. This was recorded as an environment/backend limitation; no
  Production action or state-changing order operation was performed.
- Session 07 validation passed: 52 frontend test files / 266 tests, 161 backend
  tests, Release build with 0 warnings and 0 errors, production initial bundle
  441.43 kB raw, production-offline initial bundle 427.11 kB raw, and lazy
  Three.js 734.66 kB raw. The Riyal verifier and `git diff --check` passed.

## Deferred scope

- UPC Testing fixture acceptance remains deferred pending Testing approval; no
  live COD acceptance, send, resend, or cancellation claim is made.
- Production database/index work remains deferred and unauthorized.
- POS integration remains deferred until the independent POS project completes.
- Deployment topology and Production acceptance remain unknown/pending.

## Verification boundary

- The Session 07 1440/1024/900/768/390 light/dark/reduced-motion/WebGL matrix
  was rendered with the existing local Chromium CDP mechanism because the
  in-app browser connector exposed no browser. The structural and visual gate
  passed; the Testing-only UPC 500s remain an environment limitation.
- No Production state-changing action or Production SQL was performed.
