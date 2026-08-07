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

# Session 04 — Prompt Studio Angular Migration Foundation

## Objective

Rebuild the standalone QA Prompt Studio structure as native Angular feature routes
without implementing the enhanced prompt-generation logic yet.

## Scope

Preserve the Prompt Studio landing, generator entry points, back navigation,
theme integration, draft persistence, character counters, sample loading,
preview, copy action, and `Ctrl/Cmd + Enter` behavior. Add typed reactive-form
models and placeholder builders for Bug, Story, and Test Case generators under
the existing `/tools/prompt-studio` route.

The detailed execution prompt and validation contract are maintained in
`docs/QA_SUPPORT_HUB_SESSION_PROMPTS.md`. Online Orders behavior and the
POS migration-pending state remain unchanged.