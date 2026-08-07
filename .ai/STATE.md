# Current Project State

- **Updated:** 2026-08-08
- **Branch:** `main`
- **Release or milestone:** QA Support Hub — Session 14 Cross-Tool UI and Responsive Hardening

## Working State

- The repository contains the .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests. Existing order authoring,
  payment, detail, cancel, resend, payload, and Production-safety contracts
  remain unchanged.
- Session 01 added the QA Support Hub route skeleton: `/` serves the hub
  dashboard; `/tools/prompt-studio`, `/tools/online-orders`, and
  `/tools/pos-maintenance` are lazy-loaded with typed route metadata
  (`ToolRouteData` in `frontend/src/app/core/models/tool.model.ts`). The
  Online Order workspace moved canonically under `/tools/online-orders` and
  is re-mounted at the legacy `/modules/:key` path so existing URLs still
  resolve. Top-level branding is now QA Support Hub; the POS placeholder
  shows `Coming Soon` with no actions. Prompt Studio and POS internals are not
  migrated yet.
- Session 02 built the shared design-token, theme, and motion foundation:
  `_tokens.css` now owns typography, spacing (`--space-1..8`), and z-index
  (`--z-*`) scales alongside the existing color/radius/shadow/motion tokens.
  `ThemeService` remains the single global light/dark theme, now with a
  `prefers-color-scheme` fallback and persistence under the namespaced
  `qa-support-hub:theme` key. A new `MotionService` (`core/services/motion.service.ts`)
  resolves `system`/`reduce`/`full`, persists explicit choices under
  `qa-support-hub:motion`, and stamps `data-motion` on `<html>`; the
  reduced-motion CSS rules key off that attribute with the media query as
  the pre-bootstrap fallback, so a user choice overrides the system in
  either direction. New shared primitives: `ui-icon-button` and
  `app-tool-card` (composed on `ui-card`); the navbar exposes a motion
  cycle toggle. Existing Online Order feature pages were not restyled.
- Session 04 replaced the Prompt Studio route placeholder with a native lazy
  Angular feature: `/tools/prompt-studio` renders three generator cards, and
  `/bugs`, `/stories`, and `/test-cases` render typed reactive-form workspaces.
  Feature-local builders, namespaced debounced draft persistence, plain-text
  preview, clipboard/download actions, counters, samples, clear actions, and
  Ctrl/Cmd+Enter generation are implemented; the shared `ui-select` primitive
  now honors restored reactive-form values. Prompt Studio is available in the
  hub registry; Online Orders remains available and POS remains Coming Soon.
- Session 05 expanded Bug Refinement into a grouped typed form with safe legacy
  draft normalization, deterministic Concise/Standard/Deep and
  Generic/Jira/Azure DevOps prompt modes, configurable report sections, safe
  sample data, Markdown/plain-text export, and focused coverage. The former
  standalone Prompt Generator was reviewed for parity and retired.
- Session 06 expanded Story Refinement into a production-ready grouped typed
  reactive form with safe legacy draft normalization, deterministic
  Checklist/Given-When-Then/Both acceptance criteria modes, Concise/Standard/
  Deep detail levels, Generic Markdown/Jira/Azure DevOps formats, optional Open
  Questions/QA Impact/Definition of Ready/Suggested Test Coverage sections,
  sample data, counters, copy/download behavior, keyboard generation, and
  pending-write-safe draft persistence. The enhanced Bug Refiner remains
  available and the Test Case foundation remains unchanged.
- Session 07 expanded Test Case Generation into a grouped typed reactive form
  with requirement/story reference, scenario categories, environment, test
  data, postconditions, cleanup, automation candidacy, regression tags,
  expected-result modes, and Single Test Case/Scenario Matrix/Jira-friendly/
  Spreadsheet-friendly output types. The deterministic builder preserves
  screenshot/evidence inference, labels suggested values, and emits warnings
  for missing information, vague steps/results, duplicate steps, missing
  observable outcomes, and per-step result mismatches. Legacy drafts and the
  existing copy/download workflow remain compatible.
- Session 08 simplified the Bug, Story, and Test Case source models and
  deterministic builders to their canonical headings while preserving legacy
  semantic draft normalization and existing namespaced draft keys. Prompt Studio
  now has a deterministic, advisory-only `PromptQualityService` and compact
  quality panel for missing facts, vague or duplicate steps, observability,
  equality/contradiction, evidence, triage, actor, assumption, and sensitive-data
  guidance. `PromptHistoryService` stores at most ten newest generated prompts
  under `qa-support-hub.prompt-studio.history`, retaining only type, title,
  timestamp, and prompt text; storage failures and malformed records are
  non-blocking and attachment contents are never stored.
- Session 09 integrated Online Orders with the shared QA Support Hub shell.
  `/tools/online-orders` remains canonical, `/modules/:key` remains compatible,
  breadcrumbs and Hub/module navigation are shared, and the Online Order
  landing now uses the shared page-header and tokenized shell spacing. Existing
  Online Order business behavior, capability guarding, API calls, and official
  assets remain unchanged.
- Session 10 completed the POS informational workspace at
  `/tools/pos-maintenance`. The page shows `Coming Soon`, planned capability
  areas, and a return path to the Hub. A typed `PosCapability` model and
  `docs/POS_MAINTENANCE_MIGRATION_INTAKE.md` preserve the future integration
  security boundary; no POS operation, backend contract, or generic execution
  surface was added.
- The QA Support Hub programme started with Session 00 (repository baseline
  and architecture decision), which is complete. Implementation plan:
  `docs/QA_SUPPORT_HUB_IMPLEMENTATION_PLAN.md`; session prompts:
  `docs/QA_SUPPORT_HUB_SESSION_PROMPTS.md`; the former standalone Prompt
  Studio source was migration reference only and is now retired.
- Baseline architecture decisions are recorded in
  `.ai/decisions/ADR-0011-qa-support-hub-baseline.md`; the repository map is
  section 23 of the implementation plan.
- QA Support Hub **Session 14 — Cross-Tool UI and Responsive Hardening** is
  complete (see `TASK.md` and the session tracker). The POS Maintenance Tool is
  being developed independently outside this repository, so POS integration is
  intentionally deferred. Sessions 11-13 are deferred by design and are not
  blockers. POS remains Coming Soon and non-operational; Prompt Studio and
  Online Orders remain available.

## Deferred Acceptance and Database Scope

- UPC Testing fixture acceptance: **Deferred - Testing approval required**.
  It does not block application deployment, but it blocks any live COD
  acceptance claim, send, resend, or cancellation.
- Production index work: **Deferred by user**. It is a separate future,
  database-owner-approved task, does not block application deployment, and no
  Production database change is authorized in this session.
- No false fixture approval, COD acceptance, or Production index deployment is
  recorded.

## Local Verification

- Focused frontend Order Requests/searchable-select tests: 43 passed across
  four spec files; full frontend suite: 141 passed across 24 files.
- Focused backend Order Requests tests: 35 passed; full backend suite: 161
  passed with no skipped tests.
- `scripts/build.ps1` passed the backend test, Release build, and Angular
  production-build gates. The production bundle is 438.35 kB with no
  style-budget warning.
- `npm run test:riyal-asset` passed with the provenance-verified asset.
- Session 00 required documentation validation only: documentation links and
  referenced paths verified, `git diff --check` clean, and the working tree
  limited to documentation and project-memory changes.
- Session 01: targeted route/branding specs passed (12 tests), full frontend
  suite 147 passed across 24 files, Angular production build passed with
  per-tool lazy chunks, and direct navigation to `/`, all three tool routes,
  a canonical Online Order deep link, and a legacy `/modules/:key` deep link
  returned HTTP 200 from the dev server. Backend untouched; no backend
  contract changed.
- Session 02: targeted theme/motion/primitive specs passed (26 tests), full
  frontend suite 173 passed across 27 files, Angular production build passed
  (440.63 kB initial, no style-budget warning), light/dark token-contract
  and duplicate-token checks passed, and hub/tool/legacy routes returned
  HTTP 200 from the dev server. Backend Release build and 161 tests passed
  unchanged; the `build.ps1` Debug-copy step was blocked only by the user's
  locally running `OnlineOrderTool.Api` process locking Debug DLLs.
- Session 03: focused Hub and shared-card specs passed (5 and 7 tests), full
  frontend suite passed with 178 tests across 28 files, Angular production
  build passed at 438.47 kB initial with no budget warnings, and responsive
  browser checks passed at 1440px, 900px, and 390px with no horizontal
  overflow. Dark theme, reduced motion, direct navigation to all three tool
  routes, and the empty-registry fallback were verified; backend and Online
  Order business code were unchanged.
- Session 04: focused Prompt Studio specs passed with 21 tests across 8 files;
  route and hub status checks passed; full frontend suite passed with 199
  tests across 36 files; Angular production build passed at 438.51 kB initial
  with no budget warnings; and browser checks verified landing, all three
  generator routes, sample/generate/preview/copy affordances, Ctrl+Enter,
  draft restore after reload, dark/light theme, reduced motion, and responsive
  layouts at 1440px, 900px, and 390px without horizontal overflow. Backend and
  Online Order business code were unchanged.
- Session 05: focused Prompt Studio specs passed with 33 tests across 9 files;
  the full frontend suite passed with 211 tests across 37 files; the Angular
  production build passed at 440.44 kB initial with no budget warnings; the
  former standalone generator was deleted after parity audit; persisted theme
  and motion preferences were verified on direct Bug-route loads; and the
  post-deletion legacy-reference scan found no frontend dependency. Backend,
  Online Orders, Story, and Test Case behavior were unchanged.
- Session 06: Story-focused specs passed with 26 tests; the full Prompt Studio
  suite passed with 55 tests across 9 files; the full frontend suite passed
  with 233 tests across 37 files; the Angular production build passed at
  440.44 kB initial with no budget warnings; browser checks passed for the
  Story route at 1440px, 900px, and 390px without horizontal overflow; Sample,
  Clear, reload persistence, Generate, all acceptance/detail/format modes,
  Copy, Markdown/plain-text export, Ctrl/Cmd+Enter, light/dark theme, and
  reduced motion were verified; no backend or Online Order files changed.
- Session 07: Test Case-focused specs passed with 17 tests across 2 files; the
  full frontend suite passed with 244 tests across 37 files; editor diagnostics
  were clean; the Angular production build passed at 440.44 kB initial with no
  budget warnings; and the direct `/tools/prompt-studio/test-cases` route
  returned HTTP 200 from the local dev server. Backend, Online Orders, Bug,
  and Story behavior remained unchanged.
- Session 08: focused Prompt Studio specs passed with 46 tests across 11 files;
  the full frontend suite passed with 224 tests across 39 files; editor
  diagnostics were clean; the Angular production build passed at 440.44 kB
  initial with no budget warnings; quality findings remained advisory; bounded
  local history, malformed/blocked storage handling, Open/Copy/Delete/Clear,
  existing draft persistence, Ctrl/Cmd+Enter, and Markdown/plain-text exports
  were verified. Browser checks passed at 1440px, 900px, and 390px with no
  horizontal overflow, and light/dark plus reduced-motion persistence were
  verified. Backend, Online Orders, and POS behavior remained unchanged.
- Session 09: focused shell/routing specs passed with 14 tests across four
  files; the full frontend suite passed with 230 tests across 42 files; editor
  diagnostics were clean; the Angular production build passed at 440.55 kB
  initial with no budget warnings. Browser checks verified Hub to Online Orders
  and return navigation, canonical and legacy route refreshes, breadcrumb
  hierarchy, capability guards, light/dark theme, reduced motion, and shell
  layouts at 1440px, 900px, and 390px without horizontal overflow. Backend,
  Online Order business logic, and generated artifacts were unchanged.
- Session 10: focused POS tests passed with 5 tests; the full frontend suite
  passed with 235 tests across 43 files; editor diagnostics and `git diff
  --check` were clean; the Angular production build passed at 442.00 kB
  initial with no budget warnings. Browser checks verified Hub/POS navigation,
  the non-operational placeholder, planned capabilities, light/dark theme,
  reduced motion, keyboard-reachable return navigation, and responsive 1440px,
  900px, and 390px layouts without horizontal overflow. Console error checks
  were clean; backend, Online Orders, Prompt Studio, and generated artifacts
  were unchanged.
- Session 14: the full frontend suite passed with 236 tests across 43 files;
  the optimized production configuration passed at 427.35 kB initial with no
  budget warnings while external font inlining was disabled for offline
  verification. The standard production build was also attempted but could
  not retrieve Google Fonts because `fonts.googleapis.com` was unavailable;
  touched-file diagnostics and `git diff --check` were clean. Browser checks
  passed for 45 route/viewport combinations at 1440px, 1024px, 900px, 768px,
  and 390px with no shell overflow, one H1 per route, consistent light/dark
  themes, system/reduced/full motion, and keyboard-visible focus rings. Prompt
  Studio narrow-screen interaction and the informational, non-operational POS
  route were verified; backend and business logic were unchanged.

## Deployment Discovery Blocker

- README and `.ai/PROJECT.md` state that hosting/deployment topology is not
  documented.
- The repository contains no authoritative IIS, Docker, systemd, CI/CD,
  transfer, service, deployment-folder, target-server, or health-endpoint
  configuration.
- The exact target environment, server identity, deployment mechanism, secure
  access method, rollback location, and health checks are therefore unknown.
- Application deployment stays deferred until the final QA Support Hub release
  session; the plan scopes deployment outside feature sessions.

## Programme Status

- U0-U8, final project polish, Order Requests unification, and acceptance
  hardening are closed.
- QA Support Hub: Sessions 00 through 10 and 14 completed; Sessions 11-13
  deferred while the independent POS project is developed; Sessions 15-16 not
  started.
