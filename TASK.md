# Current Task

- **Task ID:** UI-U7
- **Status:** Ready
- **Role:** Implement

## Objective

Migrate the remaining application surfaces from the temporary `.glass-*`
feature styling to the U5 shared token/primitives foundation, preserving every
existing route, environment control, capability gate, drawer workflow, status,
lookup, validation, and API contract. Remove the legacy glass bridge only after
all consumers are migrated.

## Done When

- Navbar, sidebar, breadcrumb, landing, Order Requests/drawer/filter bar,
  Uni-Commerce, and Order Validation use U5/U6 tokenized primitives and retain
  their current routes and behavior.
- The navbar documentation affordance is a meaningful route/link or is removed;
  no `alert()` dead action remains.
- `.glass-card`, `.glass-panel`, `.glass-input`, and `.glass-button` have no
  remaining consumers or definitions; only now-unused compatibility aliases are
  removed.
- Both dark and light themes retain accessible focus, disabled, loading, dialog,
  table, status, environment, and reduced-motion behavior.
- Focused affected-route tests, the full backend/frontend suites, production
  build, legacy-class grep, raw-color grep, context, memory, and diff checks
  pass. Browser verification is reported only when an instance is available.
- U6 flat-order behavior and the U5 primitive contract remain intact. No U8
  end-to-end or live/Production action is started.

## Read First

- `docs/UI_Rework_Plan.md` D10/D14 and the U7 row.
- `docs/UI_Rework_Prompts.md` Session U7.
- `.ai/plans/UI-U7-app-migration.md`.
- `.ai/STATE.md`, `.ai/PROJECT.md`, and the current task-related diff.
- `docs/design-system.md`, the shared UI barrel/primitives, and the listed
  navbar/sidebar/breadcrumb/landing/order-request/Uni-Commerce/validation files.

## Scope

- Remaining app-wide primitive migration, the documentation action, removal of
  unused glass compatibility rules, affected tests, and task documentation.
- Reuse U5 tokens and primitives plus the U6 builder patterns; do not create a
  parallel visual system.

## Constraints

- Do not modify payload builders, validators, totals, SQL, request fixtures,
  API contracts, capabilities, draft persistence, or dependencies.
- Preserve six order-request drawer tabs, filters, statuses, branch picker,
  environment switching, theme switching, and the superseded validation route.
- Testing is the only live environment. Never send, cancel, or resend against
  Production. Do not start U8, push, deploy, reset, stash, rebase, or amend.
- Do not edit generated/runtime paths or store secrets/customer data in tracked
  files or `.ai/`.

## Checkpoint

- U6 implementation committed at `dac0cc4`.
- U6 local validation: backend 110/110, frontend 81/81 across fifteen spec
  files, `scripts/build.ps1` passes, and the production initial bundle is
  429.42 kB with zero Release warnings/errors.
- Browser inspection remains unavailable; UPC Testing item lookup remains an
  external HTTP 502 limitation. No Production action was attempted.
