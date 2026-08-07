
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


# Session 03 — QA Support Hub Dashboard

## Objective

Build the main external dashboard with three selectable tool cards.

## Prompt


# Session 03 — QA Support Hub Dashboard

Execute the shared execution contract.

Goal:
Implement the root QA Support Hub dashboard with three modern selectable cards.

Cards:

1. QA Prompt Studio
   - route: `/tools/prompt-studio`
   - status: Available
   - action: Open Prompt Studio

2. Online Order Tool
   - route: `/tools/online-orders`
   - status: Available
   - action: Open Online Orders

3. POS Maintenance Tool
   - route: `/tools/pos-maintenance`
   - status: Migration Pending
   - action: View Migration Status or disabled based on the architecture plan

Tasks:

1. Create a typed tool registry as the single source of card metadata.
2. Build the responsive dashboard using the shared Tool Card component.
3. Add:
   - title
   - description
   - icon
   - status badge
   - capability summary
   - action
4. Make cards keyboard accessible.
5. Add subtle entrance, hover, focus, and selection animations.
6. Honor reduced-motion mode.
7. Lazy-load Three.js only if the project can support it without affecting the main bundle.
8. When Three.js is used:
   - hub only
   - pause when page hidden
   - cap pixel ratio
   - provide non-WebGL fallback
9. Do not add continuous heavy animation to tool pages.
10. Add empty/error fallback when tool metadata cannot load.
11. Add targeted tests for navigation and card status.

Validation:
- Desktop, tablet, and mobile layouts.
- Keyboard navigation.
- Reduced-motion behavior.
- Card routing.
- POS card cannot invoke non-existent operations.
- Angular production build.

Commit message:
`feat(hub): add animated QA tool dashboard`


## Completion Gate

- The root dashboard is complete.
- All cards represent the correct availability state.
- Available cards navigate correctly.
