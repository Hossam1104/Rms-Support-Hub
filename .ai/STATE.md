# Current Project State

- **Updated:** 2026-08-08
- **Branch:** `main` after Session 00 merge
- **Current program:** RMS+ Support Hub UI / Branding Refactor
- **Session status:** Session 00 completed; Session 01 active and ready
- **Current GitHub repository:** `Hossam1104/online_order_tool`
- **Future GitHub repository:** `Hossam1104/Rms-Support-Hub` (Session 08)
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
  disposable; reduced motion/WebGL/import failure falls back to CSS.

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

## Deferred scope

- UPC Testing fixture acceptance remains deferred pending Testing approval; no
  live COD acceptance, send, resend, or cancellation claim is made.
- Production database/index work remains deferred and unauthorized.
- POS integration remains deferred until the independent POS project completes.
- Deployment topology and Production acceptance remain unknown/pending.

## Verification boundary

- No interactive browser matrix was run in Session 00. The 1440/1024/900/768/
  390 light/dark/reduced-motion/WebGL-fallback matrix remains Session 07 work.
- No Production state-changing action or Production SQL was performed.
