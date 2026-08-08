# RMS+ Support Hub - Active Task

Program:
RMS+ Support Hub UI / Branding Refactor

Active Session:
03 - Global Density, Tables, Borders & Surface System

Previous:
02 - Completed

Role:
Implement

Expected branch:
refactor/rms-hub-03-density-tables

Expected commit:
refactor(ui): compact layouts and standardize data tables

## Global Contract

Repository root:

```text
D:\AI Tools\DBS\online_order_tool
```

Before each session:

1. Read `AGENTS.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`.
5. Read only the source, tests, and documentation named in this task, plus
   task-related changed files.
6. Inspect Git state before changing files.
7. Execute the task completely.
8. Run targeted validation first, then the required full validation gates.
9. Review the final task-scoped diff and remove temporary changes.
10. Update durable state only when materially useful.

Safety:

```text
Never git reset --hard.
Never force push.
Never discard unrelated changes.
Never auto-resolve semantic conflicts.
Never modify Production.
Never run Production SQL.
Never invoke state-changing Online Order actions during UI validation.
Never implement POS operations.
```

The following business behavior is frozen unless this session explicitly says
otherwise:

- Prompt Studio canonical outputs
- Prompt Quality semantics
- Prompt history max = 10
- draft persistence
- Copy/Markdown/Text export
- Ctrl/Cmd+Enter
- Online Order API contracts
- Online Order DTOs/payloads
- filters/paging/status behavior
- capability guard
- payment codes
- integration/module keys
- POS remains Coming Soon

Repository guardrails:

- Treat `docs/request_examples/**` and mirrored backend test fixtures as the
  payload contract.
- Treat current repository SQL plus `docs/database-schema.md` as the database
  contract; never guess a column or JSON key.
- Keep backend dependencies flowing Core -> Data -> API, with API as the
  composition root.
- Gate module behavior through `IOrderModule.Capabilities`; do not add module-key
  string comparisons.
- Keep connection strings and credentials outside tracked files.
- Use the Testing environment for agent-run live verification. Never send,
  cancel, or resend against Production.
- Do not edit generated or runtime paths: `bin/`, `obj/`, `node_modules/`,
  `dist/`, `.angular/`, or `var/`.
- Component styles must consume design tokens; raw color literals belong only
  in the designated token/gradient files.

## Session 03 - Global Density, Tables, Borders & Surface System

### Goal

Reduce project-wide wasted space and establish consistent tables, margins,
cards, panels and form density.

### Critical Requirement

This is global. Do not make one-off Online Order-only CSS fixes.

### Required Work

#### 1. Normalize density tokens

Inspect existing tokens and create/reuse semantic values for:

- page padding
- section gap
- panel gap
- panel padding
- compact panel padding
- control height
- compact control height
- table cell X/Y padding
- table header height
- card gap

Prefer tokens and `clamp()` to scattered magic numbers.

#### 2. Reduce vertical waste

Target approximately 15-30% reduction where safe in:

- page headers
- breadcrumb gaps
- workflow bars
- section headers
- accordion headers
- forms
- panel padding
- action bars
- table spacing

Do not make the UI cramped or reduce focus/tap accessibility.

#### 3. Shared table contract

Every true data table should gain:

- 1px outer border
- visible header border
- row separators
- safe card inset/margins
- compact cell padding
- numeric alignment
- clear totals/footer
- responsive horizontal scrolling only when necessary

Optional vertical separators are allowed where they materially improve dense
numeric scanning.

#### 4. Apply representative global surfaces

Apply to:

- shared table/grid components
- Online Order table
- Order Requests
- Products
- Items
- at least one non-Online-Order table/list if truly tabular

#### 5. Form density

Normalize:

- labels
- helper text
- control heights
- field gaps

Do not change validation/data.

#### 6. Panels/cards

Normalize:

- border
- radius
- padding
- internal gap
- header height

Do not perform the major landing redesign yet.

### Validation

Required:

```text
npm --prefix frontend test -- --watch=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
npm --prefix frontend run build -- --configuration production
git diff --check
```

If browser exists, check representative pages at 1440/1024/900/768/390.

If not, do not claim rendered visual validation; Session 07 will close it.

### Commit

```text
refactor(ui): compact layouts and standardize data tables
```
