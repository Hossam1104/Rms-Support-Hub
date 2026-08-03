# Current Task

- **Task ID:** UI-U5
- **Status:** Ready
- **Role:** Implement

## Objective

Build the dark-first design-system foundation for UI-U6 and UI-U7 while
preserving the completed U4 order-builder behavior and all verified backend,
payload, SQL, and draft contracts.

## Done When

- Dark-first tokens define distinct page, panel, raised, overlay, interactive,
  muted, hover, and selected surfaces; the light theme remains complete.
- Radius, shadow, motion, reduced-motion, and still-used compatibility aliases
  remain intact; gradients are limited to status and intentional accent use.
- Standalone signal-based `ui-card`, `ui-section`, `ui-field`, `ui-input`,
  `ui-select`, `ui-button`, `ui-table`, and `ui-toolbar` primitives are
  exported and covered in the development-only kitchen sink.
- Toasts cap visible items at three, queue overflow, collapse repeated messages,
  pause while hovered/focused, support manual close, and remain accessible.
- Sidebar collapse persists and drives the shell's real `--sidebar-width`
  offset without a dead gutter.
- Focused U5 tests, the full backend/frontend suites, raw-color and legacy
  compatibility searches, memory checks, and `scripts/build.ps1` pass.
- U6 layout work, U7 migration, API/payload/database changes, dependency
  changes, and production actions remain out of scope.

## Read First

- `docs/UI_Rework_Plan.md` defects D9, D10, and D12 plus decisions 5-6.
- `docs/UI_Rework_Prompts.md` Session U5.
- `.ai/plans/UI-U5-design-system.md`.
- `docs/design-system.md` and the targeted token, gradient, shared UI, toast,
  sidebar, shell, kitchen-sink, and direct test files.

## Scope

- Frontend design tokens, gradient discipline, shared UI primitives, toast
  lifecycle/queue behavior, sidebar state publication, shell offset, kitchen
  sink coverage, and design-system documentation.
- Preserve the `.glass-*` bridge until U7; new U5 primitives must use tokens.

## Constraints

- Do not modify `FlatOrderPayloadBuilder`, `FlatOrderValidator`,
  `TotalsCalculator`, verified SQL, request fixtures, module capability
  routing, API contracts, or U4 behavior.
- Keep backend dependencies flowing Core -> Data -> API and credentials outside
  tracked files.
- Verification is UPC Testing only when a live call is required. Never send,
  cancel, or resend against Production.
- Do not edit generated/runtime paths or add dependencies.

## Checkpoint

- **U4 implementation reviewed:** `fd5d65d`
- U4 is locally complete. Endpoint/totals/validation checks were reported
  green; database-dependent item lookup remains blocked by HTTP 502, so live
  item population and browser evidence are not claimed.
