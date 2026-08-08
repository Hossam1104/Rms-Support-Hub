# Current Project State

- **Updated:** 2026-08-08
- **Branch:** `main`
- **Release or milestone:** QA Support Hub — Release Candidate Ready / Awaiting
  Deployment Decision
- **Mode:** Post-release iterative maintenance and enhancement. No implementation
  session is active.

This file records durable current facts only. Milestone history lives in
`.ai/HISTORY.md`; detailed evidence lives in Git.

## Application

- One Angular 22 SPA and one .NET 10 Web API host the QA Support Hub. Three
  tools are registered: **QA Prompt Studio** and **Online Order Tool** are
  available; **POS Maintenance Tool** is Coming Soon and non-operational while
  its independent project is developed outside this repository.
- Routes are lazy and typed through `ToolRouteData`
  (`frontend/src/app/core/models/tool.model.ts`). `/` is the hub,
  `/tools/prompt-studio`, `/tools/online-orders`, and `/tools/pos-maintenance`
  are the tool roots, and the legacy `/modules/:key` mount still resolves.
- Prompt Studio is fully client-side and deterministic: typed reactive forms,
  feature-local builders, namespaced debounced drafts, advisory
  `PromptQualityService`, and `PromptHistoryService` capped at ten records.
  Nothing is sent to an external provider and attachment contents are never
  stored.
- Online Order behavior — payload construction, validation, totals, filters,
  paging, statuses, capability guarding, cancel, and resend — is unchanged from
  the release and stays server-authoritative.
- The POS route shows status, planned capability areas, and a return path only.
  No POS operation, backend contract, or generic execution surface exists.

## Design system

- `frontend/src/styles/_tokens.css` and `_gradients.css` are the only places
  raw colors may appear. Components consume semantic tokens.
- One shared card contract drives every card surface: `--card-radius`,
  `--card-padding`, `--card-gap`, `--card-min-height`, `--card-surface`,
  `--card-surface-quiet`, `--card-border`, `--card-border-hover`,
  `--card-sheen`, `--card-shadow`, `--card-shadow-hover`, `--card-lift`. Peer
  cards in one grid use `grid-auto-rows: 1fr` and pin their action with
  `margin-top: auto`, so equal height comes from the grid, never a fixed pixel
  height.
- Tool identity uses `--tool-<brand|info|amber|teal>-from/to`: violet-blue for
  Prompt Studio, cyan-blue for Online Orders, muted amber for POS.
- `app-tool-card` is the shared navigation card used by the Hub and the Prompt
  Studio landing. `app-module-card` and the POS capability cards reuse the same
  tokens without being forced into the navigation-card layout.
- `ThemeService` (light/dark) and `MotionService` (system/reduce/full) are the
  single global preference services and stamp `data-theme` / `data-motion` on
  `<html>`.

## Hub scene (Three.js)

- `features/hub/hub-scene` renders a decorative particle constellation behind
  the Hub hero. Three.js is imported dynamically, so it forms its own lazy
  chunk (~735 kB raw / ~154 kB transfer) that no other feature loads and that
  never enters the initial bundle.
- The scene is aria-hidden, pointer-transparent, and never required. Reduced
  motion, missing WebGL, or a failed import all fall back to the static
  `--scene-backdrop` gradient. Rendering pauses while the document is hidden;
  device pixel ratio is capped at 1.5; frames, listeners, geometries,
  materials, and the renderer are released on destroy.
- No other route runs a WebGL scene. Internal pages reuse the static
  `--scene-backdrop` gradient instead.

## Deferred scope

- UPC Testing fixture acceptance: **Deferred — Testing approval required.** It
  does not block deployment, but it blocks any live COD acceptance claim, send,
  resend, or cancellation.
- Production index work: **Deferred by user.** Separate, database-owner-approved
  task; no Production database change is authorized.
- POS integration: deferred until the independent POS project completes and a
  migration task is authorized.
- Deployment topology: the repository contains no authoritative IIS, Docker,
  systemd, CI/CD, transfer, service, target-server, or health-endpoint
  configuration, so deployment mechanism, rollback location, and health checks
  are unknown. Application deployment and Production acceptance remain pending.

## Current verification

Post-release cleanup and visual refresh (2026-08-08):

- Frontend: 250 tests passed across 46 files, no skipped tests.
- Backend: 161 tests passed, 0 failed, 0 skipped; Release build passed.
- `scripts/build.ps1` passed the backend test, Release build, and Angular
  production-build gates.
- Angular production build: 439.28 kB initial (from 436.68 kB), no budget
  warnings; the Three.js `three-module` chunk (734.66 kB raw / 153.98 kB
  transfer) is lazy and Hub-only.
- Riyal asset provenance verified; `python .ai/scripts/check_memory.py` clean.
- Route smoke checks returned HTTP 200 from the dev server for the Hub, the
  three Prompt Studio generators, Online Orders, a canonical module deep link,
  a legacy `/modules/:key` deep link, and POS.

**Not verified in this task:** no interactive browser was available, so the
visual/responsive matrix (1440/1024/900/768/390, light and dark, reduced
motion, WebGL fallback rendering) was reviewed statically against the
breakpoints and reduced-motion rules in source, not rendered. Re-run the
browser matrix when a browser tool is available.
