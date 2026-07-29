# Migration Guide: From Many Memory Files to One Shared Brain

## Recommended Structure

```text
AGENTS.md                 # Canonical behavior rules for Codex and Kimi
CLAUDE.md                 # Imports AGENTS.md; no duplicated rules
TASK.md                   # The current task only; replace for every task
.ai/
├── STATE.md              # Current project snapshot; replace stale content
├── PROJECT.md            # Stable, non-obvious context; read on demand
├── DECISIONS.md          # Compact decision index; read on demand
├── HANDOFF.md            # Only for unfinished cross-model continuation
├── decisions/            # Detailed ADRs, opened only when relevant
├── plans/                # One optional active large plan
├── archive/              # Never part of normal startup
└── scripts/
    ├── context.py         # Bounded Git status/diff summary
    └── check_memory.py    # Context-size guardrail
```

## Map the Existing Files

| Existing file | New destination | Migration rule |
|---|---|---|
| `.ai/PROJECT_CONTEXT.md` + `.ai/CONTEXT.md` | `.ai/PROJECT.md` | Merge only stable, non-obvious facts. Delete discoverable repository details and duplicates. |
| `.ai/CURRENT_STATE.md` + `.ai/IMPLEMENTATION_PROGRESS.md` | `.ai/STATE.md` | Keep current truth only. Replace old status; do not keep a diary. |
| `.ai/REQUIREMENTS.md` | Normal `docs/requirements/` files | Keep source requirements outside startup memory. `TASK.md` should point to exact sections needed now. |
| `.ai/IMPLEMENTATION_PLAN.md` | `TASK.md` or one `.ai/plans/<task>.md` | Use only for the active complex task. Archive or delete after completion. |
| `.ai/HANDOFF.md` | `.ai/HANDOFF.md` | Keep, but only while work is partial or blocked and below 40 lines. |
| `.ai/CODE_REVIEW.md` | Pull request/review tool or archive | Do not reload completed review history into every model. Keep only unresolved findings in `TASK.md` or `HANDOFF.md`. |
| `.ai/TEST_RESULTS.md` | CI/test report or archive | In shared memory store only command + pass/fail + relevant blocker. |

## Why the Old Layout Consumes Quota

The same facts tend to appear in project context, current state, progress, handoff, review, and test files. Every model then pays to read multiple versions of the same information, often before reading the actual changed code. Splitting a large document into more imported files does not reduce startup context when all files are still loaded.

The optimized design uses:

- Git for exact history and diffs.
- `TASK.md` for the current objective and boundaries.
- `STATE.md` for the latest project truth.
- `HANDOFF.md` for only the unfinished delta.
- On-demand project context and decisions.

## Cross-Model Workflow

### Small task

Use one model from start to validation. The other models do not need to participate.

### Complex task

1. Planner reads the minimum context and writes a plan of no more than 10 steps into `TASK.md`.
2. Executor reads `TASK.md`, `STATE.md`, the compact Git summary, and the scoped files. It does not re-plan unless blocked.
3. Reviewer reads the task-related diff, acceptance criteria, and targeted tests only.
4. The final owner updates `STATE.md` and clears `HANDOFF.md`.

### Switching because of quota

The outgoing model updates `HANDOFF.md` with:

- what is already complete;
- exact next action;
- changed files;
- validation performed;
- current blocker or risk.

The incoming model starts a fresh session, reads the shared files, and continues. Do not paste the outgoing conversation or request a full-project analysis again.

## Model Routing Recommendation

Use the models as a pooled team, not three copies of the same worker:

- **Claude:** difficult architecture, ambiguous requirements, risk analysis, or a focused independent review.
- **Codex:** primary code implementation, debugging, refactoring, and targeted validation.
- **Kimi:** repository exploration, large-document analysis, bulk test/scenario generation, overflow implementation, or targeted review when another quota is constrained.

This is a default routing rule, not a requirement to use all three on every task. For quota efficiency, one task phase has one owner.

## Session Rules

- Start a new session for a new task or unrelated phase.
- Resume a session only for the same unfinished task with the same model.
- Use manual compaction in Claude or Kimi after a major phase when the conversation must continue, prioritizing the objective, decisions, changed files, validation, and remaining work.
- Prefer a fresh session plus `TASK.md`/`HANDOFF.md` over repeatedly compacting a very long conversation.
- Never export full model transcripts into the repository as shared memory.

## Task Prompt

Use this minimal prompt with any of the three tools:

```text
Execute the current task in TASK.md according to AGENTS.md.
Continue from .ai/HANDOFF.md only if its status is In Progress or Blocked.
Do not repeat completed planning or broad project discovery.
Use the repository and Git diff as the source of truth, run targeted validation, and update only the memory files required by AGENTS.md.
```

## Context Budget

Run this periodically:

```bash
python .ai/scripts/check_memory.py
```

Treat the limits as guardrails, not targets. Smaller is better. Detailed requirements, plans, examples, and historical reports should remain available on disk but load only when the current task references them.
