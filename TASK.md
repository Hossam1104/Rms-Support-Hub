
# Execution Contract

You are implementing one scoped session in the existing QA Support Hub repository.

Before changing code:

1. Read `AGENTS.md` completely.
2. Read `QA_SUPPORT_HUB_IMPLEMENTATION_PLAN.md`.
3. Read `QA_SUPPORT_HUB_SESSION_PROMPTS.md` only for the current session.
4. Read `TASK.md`, `.ai/STATE.md`, and `.ai/HANDOFF.md` when they exist.
5. Inspect the current Git branch, status, diff, and recent commits.
6. Inspect the actual code related to this session.
7. Preserve unrelated user work.
8. Do not reset, discard, or overwrite changes you do not own.

Execution rules:

- Execute the session through completion; do not stop after planning.
- Keep the change limited to the current session.
- Follow the existing Angular and .NET architecture.
- Preserve existing Online Order behavior unless this session explicitly changes it.
- Do not deploy to IIS or any server.
- Do not run Production SQL or state-changing Production operations.
- Do not add credentials, connection strings, secrets, private addresses, or customer data.
- Do not commit generated artifacts, `dist`, `node_modules`, backups, logs, or screenshots.
- Do not introduce an iframe as the final integration mechanism.
- Do not add arbitrary command, PowerShell, or SQL execution fields.
- Use typed models and deterministic behavior.
- Maintain accessibility and reduced-motion support.
- Use targeted validation for the current change.
- Run the full repository suite only when the session explicitly requests it or when a change affects application-wide routing/build behavior.
- Review the final diff.
- Update project state documentation only when materially required.
- Commit with the requested commit message when all validation passes.
- Push only when repository policy and the user’s current workflow allow it.
- Return a concise execution report with: Result, Changes, Validation, Commit, Remaining.


# Session 02 — Design System Foundation

## Objective

Build a shared design-token, theme, and motion foundation without broadly rewriting feature pages.

## Prompt


# Session 02 — Design System Foundation

Execute the shared execution contract.

Goal:
Build a shared design-token, theme, and motion foundation without broadly rewriting feature pages.

Tasks:

1. Inspect current global styles and theme services.
2. Create a maintainable design-system structure for:
   - colors
   - typography
   - spacing
   - radius
   - shadows
   - z-index
   - motion durations/easing
3. Use the Prompt Studio visual direction:
   - deep neutral surfaces
   - subtle glass panels
   - controlled gradients
   - clear status colors
   - modern cards
4. Preserve the existing official assets and established app branding rules.
5. Implement one global light/dark theme service.
6. Persist the theme under a namespaced storage key.
7. Add reduced-motion preference:
   - honor `prefers-reduced-motion`
   - optional user override
8. Create shared primitives only where needed:
   - button
   - icon button
   - status badge
   - tool card base
   - page header
   - empty state
   - toast
9. Do not migrate the Prompt Studio form yet.
10. Do not bulk-replace all existing Online Order styles.
11. Remove duplicated new styles created in this session.
12. Ensure text remains sharp during hover animations.

Validation:
- Targeted component tests.
- Theme persistence check.
- Reduced-motion check.
- Light/dark visual check.
- Angular production build.

Commit message:
`feat(design-system): add shared themes and motion tokens`


## Completion Gate

- One global theme exists.
- Shared cards and controls are available.
- Reduced motion works.
