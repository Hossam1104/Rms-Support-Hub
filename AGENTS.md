# Shared AI Operating Contract

This file is the canonical instruction source for Codex, Claude, and Kimi.
Claude loads it through `CLAUDE.md`. Do not duplicate these instructions elsewhere.

## Shared-brain principle

The shared brain is the repository, Git, `TASK.md`, and the concise files under `.ai/`.
Chat transcripts and hidden model reasoning are not project records.
Never ask another model to reconstruct the project from previous conversations.

## Mandatory startup

For any task:

1. Read `TASK.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`.
5. Read only the source, tests, and documentation named in `TASK.md`, plus task-related changed files.
6. Read `.ai/PROJECT.md` only when non-obvious stable context is required.
7. Read `.ai/DECISIONS.md` only when the task may affect an existing decision; open a detailed ADR only when its affected area matches the task.

Do not automatically read:

- `.ai/archive/`
- every requirement or design document
- the full repository
- full Git history
- full diffs unrelated to the task
- old model transcripts or exported sessions

## One active owner

Only one model owns implementation at a time.
The next model continues from the repository state instead of repeating discovery, planning, or completed work.

Treat these sources in this order:

1. Current code and tests
2. Current Git status and task-related diff
3. `TASK.md`
4. `.ai/HANDOFF.md` for incomplete work
5. `.ai/STATE.md`
6. Stable project documentation and decisions

Challenge previous work only when there is concrete evidence of a defect, contradiction, security risk, or unmet acceptance criterion.

## Roles

The role is declared in `TASK.md`:

- `Plan`: inspect only enough context to produce an executable plan; do not implement.
- `Implement`: execute the accepted plan or objective; do not restart planning without a blocker.
- `Debug`: reproduce, isolate, fix, and validate.
- `Review`: inspect the task-related diff and tests only; do not modify unless requested.
- `Test`: validate the changed scope and report evidence.

Small, well-scoped tasks should use one model only. Use multiple models only when the task benefits from a separate planning, implementation, or review checkpoint.

## Execution rules

- Work only within the requested scope.
- Prefer existing patterns and utilities.
- Avoid unrelated refactoring, formatting, and dependency changes.
- Do not paste large files into project-memory documents.
- Do not store raw logs, complete diffs, test output, credentials, URLs with secrets, connection strings, or personal data in `.ai/`.
- Do not commit, push, deploy, or run destructive commands unless explicitly requested.
- For minor uncertainty, make the safest reversible assumption and record it in the handoff only if another model needs it.
- Ask the user only for a material business decision, unavailable access, or unsafe/destructive action.

## Validation

- Run targeted checks for the changed scope first.
- Run a broad build or regression suite only when the impact is broad or `TASK.md` requires it.
- Distinguish new failures from pre-existing failures.
- Never claim a check passed unless it ran successfully.
- Before completion, inspect the final task-related diff and remove temporary/debugging changes.

## Repository-specific guardrails

- Treat `docs/request_examples/**` and the mirrored backend test fixtures as the payload contract.
- Treat current repository SQL plus `docs/database-schema.md` as the database contract; never guess a column or JSON key.
- Keep backend dependencies flowing Core -> Data -> API, with API as the composition root.
- Gate module behavior through `IOrderModule.Capabilities`; do not add module-key string comparisons.
- Keep connection strings and credentials outside tracked files; use user-secrets or environment variables.
- Use the Testing environment for agent-run live verification. Never send, cancel, or resend against Production.
- Do not edit generated or runtime paths: `bin/`, `obj/`, `node_modules/`, `dist/`, `.angular/`, or `var/`.
- Component styles must consume design tokens; raw color literals belong only in the designated token/gradient files.
- For broad changes, run `.\scripts\build.ps1`; record any unavailable live dependency separately.

## Memory update policy

### Plan and prompt lifecycle

- `TASK.md` and at most one linked file under `.ai/plans/` define active work.
- Keep programme documents and execution prompts limited to unfinished
  sessions. Do not leave completed prompts in the active documentation tree.
- Record completed milestones concisely in `.ai/HISTORY.md` with commit or
  validation evidence.
- Archive a completed plan under `.ai/archive/` only when it has lasting audit
  value; otherwise delete it. Executed prompt runners normally belong only in
  Git history.
- After moving or deleting a plan, update live documentation links so current
  contracts never depend on archived prompts.

After completed work:

- Update `.ai/STATE.md` only with durable current facts; replace outdated text rather than appending history.
- Set `.ai/HANDOFF.md` to `Empty`.
- Update `.ai/PROJECT.md` only when stable architecture, commands, integrations, or non-obvious conventions changed.
- Update `.ai/DECISIONS.md` only for a lasting decision. Put detailed rationale in one ADR under `.ai/decisions/`.
- Move a large completed plan to `.ai/archive/` only when it has audit value; otherwise delete it.

When stopping before completion:

- Update `.ai/HANDOFF.md` with only the delta: completed work, exact next action, changed files, validation, blocker, and risks.
- Keep the handoff below 40 lines.
- Do not rewrite the full project state or implementation history.

## Completion response

Return only:

### Result
Completed, Partially Completed, Blocked, Planning Completed, or Review Completed.

### Changes
Concise task-related changes.

### Validation
Commands executed and results.

### Remaining
Only unresolved work, blockers, or risks.
