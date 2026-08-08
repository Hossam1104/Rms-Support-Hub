# RMS+ Support Hub — Active Task

Program:
RMS+ Support Hub UI / Branding Refactor

Active Session:
01 — Product & Technical Rename

Previous Session:
00 — Completed

# Global Contract for Every Luna Session

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


# SESSION 01 — Product & Technical Rename

## Goal

Rename the host application to **RMS+ Support Hub** and technical project root to **RmsSupportHub** while preserving all external/business contracts.

## Branch

```text
refactor/rms-hub-01-project-rename
```

## Naming Contract

```text
Display: RMS+ Support Hub
.NET root: RmsSupportHub
npm package: rms-support-hub
```

Do NOT rename GitHub yet. GitHub rename is Session 08.

## Required Work

### 1. Read Session 00 map

```text
docs/RMS_SUPPORT_HUB_REFACTOR_MAP.md
```

### 2. Rename user-facing host identity

Replace host-level `QA Support Hub` with `RMS+ Support Hub` where it means the overall product.

Preserve:
- QA Prompt Studio
- Online Order Tool
- POS Maintenance Tool

### 3. Rename Angular metadata

Update as applicable:
- browser title
- application metadata
- npm package name
- host shell labels
- README/current docs
- tests expecting the old host name

Use npm-safe:

```text
rms-support-hub
```

Do not rename feature routes.

### 4. Rename .NET host project identifiers

After mapping every reference, rename:

```text
OnlineOrderTool.Api
→ RmsSupportHub.Api

OnlineOrderTool.Tests
→ RmsSupportHub.Tests
```

Rename equivalent project/solution folders/files and host namespaces:

```text
OnlineOrderTool.*
→ RmsSupportHub.*
```

Update:
- `.sln`
- `.csproj`
- project references
- namespaces/usings
- assembly/root namespace
- test namespaces
- launch profiles
- build scripts
- local-development docs
- command paths

### 5. Preserve contracts

Do NOT rename:
- API routes
- JSON properties
- database schemas/tables
- module keys such as `upc_ecommerce`
- payment values/codes
- integration/customer identifiers
- persisted values
- capability names
- Online Order feature wording

unless proven to be purely the obsolete technical host name.

### 6. Stale-name scan

At end search tracked source for:

```text
OnlineOrderTool
QA Support Hub
online_order_tool
```

Classify remaining matches; do not blindly force zero.

## Validation

Required:

```text
npm --prefix frontend test -- --watch=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
npm --prefix frontend run build -- --configuration production
git diff --check
```

No merge on any failing gate.

## Commit

```text
refactor(brand): rename host to RMS+ Support Hub
```

## Final Response

Report:
- renamed projects/files
- renamed namespaces
- renamed frontend metadata
- preserved external identifiers
- justified old-name references
- frontend/backend/build results
- Git `0 0`

---
