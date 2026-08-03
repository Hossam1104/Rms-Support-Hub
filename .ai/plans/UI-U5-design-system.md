# UI-U5 Design System Plan

Status: Ready

## Objective

Create the shared dark-first design-system foundation for U6 and U7 without
changing U4 behavior, backend contracts, payloads, SQL, or dependencies.

## Work

1. Refactor `_tokens.css` around a distinct dark surface hierarchy, readable
   text ramp, semantic colors, borders, focus rings, complete light overrides,
   and preserved radius/shadow/motion/reduced-motion scales.
2. Keep the nine status gradients, mesh hero, and intentional accents while
   ensuring default surfaces use tokens. Preserve `.glass-*` only as the U7
   compatibility bridge; new primitives must not consume it.
3. Create and export signal-based `ui-card`, collapsible `ui-section`,
   `ui-field`, `ui-input`, `ui-select`, `ui-button`, `ui-table`, and
   `ui-toolbar` primitives with accessible focus, disabled, loading, and
   responsive states.
4. Rebuild toast behavior around a three-item visible cap, queued promotion,
   repeated-message collapse, timed dismissal, hover/focus pause, manual close,
   responsive bottom-right placement, live-region semantics, and reduced motion.
5. Publish and persist sidebar collapse; drive the shell offset from the real
   expanded/collapsed width through `--sidebar-width`.
6. Expand the development-only kitchen sink to demonstrate all primitives,
   states, themes, status pills, searchable branch selection, and toast queue
   behavior. Update `docs/design-system.md`.

## Validation

- Focused primitive, toast, sidebar, and kitchen-sink tests.
- `dotnet test backend/OnlineOrderTool.slnx --nologo`.
- `npm test -- --watch=false` from `frontend`.
- `.\scripts\build.ps1` with zero Release warnings/errors and a bundle under
  the configured budget.
- Raw-color and legacy-class searches, `python .ai/scripts/context.py`,
  `python .ai/scripts/check_memory.py`, and `git diff --check`.
- Browser verification when an in-app browser is available; otherwise report
  it as unavailable without fabricating visual evidence.

## Constraints

- U6 order-builder layout and U7 feature migration are out of scope.
- Do not modify payload builders, validators, totals, verified SQL, request
  fixtures, API behavior, capability routing, or Production safety.
- Do not add dependencies or edit generated/runtime paths.
