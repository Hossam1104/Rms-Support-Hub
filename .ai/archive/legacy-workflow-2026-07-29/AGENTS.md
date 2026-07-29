# AI Agent Instructions

This repository uses:

* `EXECUTE_TASK.md` for the current task
* `.ai/CURRENT_STATE.md` for the latest project status
* `.ai/CONTEXT.md` for stable project context
* `.ai/DECISIONS.md` for important technical decisions

These instructions apply to any AI model working in this repository.

## Mandatory startup

When asked to execute `EXECUTE_TASK.md`:

1. Read `EXECUTE_TASK.md`.
2. Read `.ai/CURRENT_STATE.md`.
3. Inspect the current Git status and task-related diff.
4. Read only the source and test files relevant to the task.
5. Read `.ai/CONTEXT.md` only when project architecture, commands, modules, or integrations are unclear.
6. Read `.ai/DECISIONS.md` only when the task may affect an existing technical decision.

Do not read the complete repository unless the task requires broad analysis.

Do not read Git history unless it is needed to understand the task.

Do not stop after summarizing or planning unless `EXECUTE_TASK.md` explicitly requests planning only.

## Execution

Determine the required role automatically from the task:

* Plan when planning is requested.
* Implement when code changes are requested.
* Debug when an error investigation is requested.
* Review when review is requested.
* Test when validation is requested.

Follow these rules:

* Work only within the requested scope.
* Inspect existing patterns before changing code.
* Avoid unrelated refactoring and formatting.
* Reuse existing utilities and conventions.
* Avoid unnecessary dependencies.
* Preserve backward compatibility when required.
* Do not expose credentials, secrets, tokens, or personal data.
* Do not run destructive database or infrastructure commands.
* Do not commit or push unless explicitly requested.
* Do not ask the user to repeat information already available in the repository.

For minor uncertainties, make the safest reasonable assumption and document it in `.ai/CURRENT_STATE.md`.

Ask the user only when execution requires a material business decision, unavailable access, a destructive action, or clarification that cannot safely be inferred.

## Validation

After implementation:

1. Run only the validation relevant to the changed scope.
2. Prefer targeted tests over the complete test suite.
3. Run a full build or regression suite only when required by the task or when the change has broad impact.
4. Fix failures introduced by the current changes.
5. Clearly distinguish task-related failures from pre-existing failures.
6. Never claim that a test passed unless it was executed successfully.

Examples:

* Small bug fix: affected unit tests plus compilation or type checking.
* API change: affected API tests and contract validation.
* UI change: affected component tests and build.
* Shared-library change: broader dependent tests.
* Configuration-only change: configuration validation and startup check where safe.

## Final review

Before completion:

1. Inspect the final task-related Git diff.
2. Confirm only intended files changed.
3. Remove debugging code and temporary files.
4. Check that no secrets were introduced.
5. Confirm the result matches `EXECUTE_TASK.md`.

## Project-memory update

After every completed task, update only `.ai/CURRENT_STATE.md`.

Keep it concise and replace outdated current-state information instead of continuously appending full historical logs.

Use this structure:

```markdown
# Current State

## Last Task

- Task:
- Result: Completed / Partial / Blocked
- Date:

## Changes

- Concise list of meaningful changes
- Maximum of approximately 10 entries

## Files Changed

- Relevant files only

## Validation

- Command: result
- Command: result
- Not run or blocked checks, when relevant

## Current Blockers

- None, or concise blocker details

## Known Risks

- Current relevant risks only

## Next Recommended Task

- One specific next action
```

Keep `.ai/CURRENT_STATE.md` preferably below 100 lines.

Do not add raw command output, long code samples, chat transcripts, or full Git diffs.

Update `.ai/CONTEXT.md` only when stable project information changes, such as:

* Technology stack
* Main architecture
* Module structure
* Build or run commands
* Database approach
* Authentication model
* External integrations

Update `.ai/DECISIONS.md` only when a lasting technical decision is made or changed.

Do not update project-memory files for trivial formatting-only changes unless they materially affect the current state.

## Completion response

Return:

### Result

Completed, Partially Completed, Blocked, Planning Completed, or Review Completed.

### Changes

A concise summary of the work.

### Validation

Commands executed and their results.

### Remaining

Only unresolved work, risks, or blockers.

Do not provide a long narrative unless requested.

## Final instruction

When asked to execute `EXECUTE_TASK.md`:

1. Read the task.
2. Load only the minimum relevant context.
3. Perform the work.
4. Run targeted validation.
5. Review the diff.
6. Update `.ai/CURRENT_STATE.md`.
7. Update context or decisions only when materially required.
8. Report the result.
