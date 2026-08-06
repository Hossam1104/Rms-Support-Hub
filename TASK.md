
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
```

---

# Session 01 — Shared Route Skeleton and Product Rename

## Objective

Introduce the QA Support Hub route structure and product identity while preserving existing routes.

## Prompt

```text
# Session 01 — Shared Route Skeleton and Product Rename

Execute the shared execution contract.

Goal:
Create the route skeleton for QA Support Hub without migrating feature internals yet.

Tasks:

1. Inspect the existing Angular route configuration and root layout.
2. Introduce or confirm these routes:
   - `/` → QA Support Hub dashboard placeholder
   - `/tools/prompt-studio`
   - `/tools/online-orders`
   - `/tools/pos-maintenance`
3. Lazy-load each tool route.
4. Preserve existing Online Order URLs using redirects or compatibility routes.
5. Rename visible top-level product branding from Online Order Tool to QA Support Hub where it represents the whole application.
6. Do not rename Online Order feature-specific headings that should remain Online Orders.
7. Add typed route metadata for:
   - tool title
   - breadcrumb
   - status
   - accent
8. Add placeholder pages for Prompt Studio and POS Maintenance.
9. The POS page must clearly show `Migration Pending`; it must not contain fake actions.
10. Ensure direct route refresh continues to work with the existing backend SPA fallback.
11. Do not restyle the entire application in this session.

Validation:
- Targeted Angular route tests.
- Angular type check/build.
- Direct navigation to all three routes.
- Existing Online Order route compatibility.
- No backend contract changes.

Commit message:
`feat(shell): add QA Support Hub route structure`
```

## Completion Gate

- All tool routes resolve.
- Existing Online Order navigation still works.
- POS is clearly pending.

---