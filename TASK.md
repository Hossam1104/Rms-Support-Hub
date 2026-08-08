
Repository root:

```text
D:\AI Tools\DBS\online_order_tool
```

Before each session:

1. Read `AGENTS.md`.
2. Read `.ai/STATE.md`.
3. Read `.ai/HANDOFF.md`.
4. Read `TASK.md` if present.
5. Inspect Git state.
6. Inspect only session-relevant source.
7. Execute the task completely.
8. Run targeted validation.
9. Run the required full validation gates.
10. Review the final task-scoped diff.
11. Update durable state only when materially useful.
12. Commit automatically.
13. Fast-forward merge to `main`.
14. Push `main`.
15. Verify `origin/main...main == 0 0`.
16. Delete the session branch.

Safety:

```text
Never git reset --hard.
Never force push.
Never discard unrelated work.
Never auto-resolve semantic conflicts.
Never modify Production.
Never run Production SQL.
Never invoke state-changing Online Order actions during UI validation.
Never implement POS operations.
```

Use session-local Git maintenance suppression when required:

```text
git -c gc.auto=0 -c maintenance.auto=false ...
```

The following business behavior is frozen unless a session explicitly says otherwise:

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

---

# SESSION 00 — Baseline, Asset Inventory & Rename Map

## Goal

Build a precise implementation map before changing branding, project identifiers, global UI primitives, or assets.

This is primarily inspection/documentation.

## Branch

```text
refactor/rms-hub-00-baseline
```

## Required Work

### 1. Synchronize Git

Require:

```text
branch = main
worktree = clean
main == origin/main
ahead/behind = 0 0
```

If not, stop and report the exact blocker.

### 2. Inspect current naming

Search the tracked repository for:

```text
QA Support Hub
Online Order Tool
OnlineOrderTool
online_order_tool
Online_Order_Tool
QA_SUPPORT_HUB
```

Classify every meaningful occurrence as:

```text
DISPLAY NAME
TECHNICAL PROJECT NAME
NAMESPACE
DOCUMENTATION
GIT/REMOTE REFERENCE
BUSINESS FEATURE NAME
EXTERNAL CONTRACT
DO NOT RENAME
```

Do not assume every `Online Order` reference should disappear. **Online Order Tool remains a feature inside RMS+ Support Hub.**

### 3. Inspect actual `./assets`

Inventory the real filesystem:

```text
RMS_Logo.svg
DBS_Logo.svg
UPC_Logo.svg
GHC_Logo.svg
Saudi_Riyal.svg
MADA.png
MasterCard.png
Visa.png
tabby.png
tamara.png
offer_logo.png
loader.svg
CustomMessage Box/*
```

For each:
- exact filename/case
- format
- dimensions/viewBox
- transparency
- likely semantic purpose
- duplicate/obsolete status
- current frontend copy/reference if any

Do not modify the assets yet.

### 4. Inspect UI architecture

Map the source controlling:
- navbar
- global shell
- page headers
- breadcrumbs
- sidebar
- cards
- tables
- form controls
- spacing/tokens
- typography
- icons
- ThemeService
- MotionService
- Hub Three.js scene
- Prompt Studio landing/workspaces
- Online Order shell/order/order-requests
- POS Coming Soon

### 5. Establish measured baseline

Record:
- frontend test count
- backend test count
- production initial bundle
- Three.js lazy chunk size
- current routes
- current project names
- current Git remote

Use repository-supported commands only.

### 6. Create one concise map

Create:

```text
docs/RMS_SUPPORT_HUB_REFACTOR_MAP.md
```

Include only:
- rename map
- asset map
- UI touch map
- risky identifiers to preserve
- session dependencies

Do not create multiple new planning documents.

## Validation

```text
git diff --check
```

If documentation-only, full application regression is not required.

## Commit

```text
docs(refactor): map RMS+ Support Hub branding and UI refactor
```

## Final Response

```text
## Result
Completed / Blocked

## Naming Map
Display-name references:
Technical rename references:
Preserved Online Order feature references:
External/do-not-rename references:

## Assets
Confirmed:
Unknown/requires review:

## UI Touch Map
<summary>

## Baseline
Frontend:
Backend:
Production bundle:
Three.js lazy chunk:

## Git
Commit:
Ahead/behind: 0 0
```