# UI-U7 - App-Wide Primitive Migration and Legacy Glass Removal

- **Status:** Ready
- **Task:** UI-U7
- **Owner:** Implement

## Objective

Move the remaining application surfaces from the temporary `.glass-*`
compatibility layer to the U5 tokenized primitives and the U6 builder patterns.
Preserve all existing routes, environment controls, capability gates, drawer
workflows, statuses, lookup behavior, validation behavior, and API contracts.

## Scope

- Navbar, sidebar, breadcrumb, landing page, and module-shell-adjacent layout.
- Order Requests list, filter bar, detail drawer, and its six tabs.
- GHC Uni-Commerce authoring and Order Validation, including the superseded
  route.
- Replace or remove the navbar documentation affordance; no dead `alert()`
  action may remain.
- Remove `.glass-card`, `.glass-panel`, `.glass-input`, and `.glass-button`
  definitions only after repository-wide consumer migration is complete.
- Add focused regression coverage for affected routes and preserve both theme
  variants, focus/disabled/loading/dialog/table/status/environment behavior,
  and reduced-motion behavior.

## Execution order

1. Inventory the listed consumers and their existing route, signal, capability,
   drawer, and environment behavior; identify only task-related files.
2. Migrate the shared shell, navbar, sidebar, breadcrumb, and landing surfaces
   to tokens and existing primitives.
3. Migrate Order Requests, its filters, and the six-tab detail drawer while
   retaining statuses, actions, and branch/environment controls.
4. Migrate Uni-Commerce and Order Validation without changing payloads,
   validators, totals, SQL, capabilities, or API calls.
5. Remove the dead documentation action and delete only now-unused legacy
   `.glass-*` rules/aliases.
6. Run focused tests, full backend/frontend tests, production build, legacy and
   raw-color searches, context/memory/diff checks, and browser verification in
   both themes when an instance is available.
7. Update project memory and leave `.ai/HANDOFF.md` Empty.

## Constraints and safety

- Do not modify payload builders, validators, totals, SQL, fixtures, API
  contracts, capabilities, draft persistence, dependencies, or U6 flat-order
  behavior.
- Testing is the only live lane. Never send, cancel, or resend against
  Production; do not begin U8 end-to-end verification.
- Do not push, deploy, reset, stash, rebase, amend, or edit generated/runtime
  paths.

## Validation evidence required

- Focused affected-route tests plus `npm test -- --watch=false`.
- `dotnet test backend/OnlineOrderTool.slnx --nologo`.
- `./scripts/build.ps1` with warning-free Release build.
- `git grep` empty for all four legacy classes and raw application color
  literals outside token/gradient files.
- `python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`, and
  `git diff --check`.
- Browser checks at 1920/1280/900/600px and in both themes only when the
  in-app browser is available; report unavailable evidence explicitly.
