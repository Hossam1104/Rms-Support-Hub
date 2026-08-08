# SESSION 08 — GitHub Rename & Final Branded Checkpoint

## Goal

Rename the GitHub repository and finish the new project identity after all code/UI work is stable.

## Branch

```text
chore/rms-hub-08-repository-rename
```

## Preconditions

Require:
- Sessions 00–07 complete
- browser matrix passed
- `main` clean
- `main == origin/main`
- `0 0`
- GitHub reachable
- GitHub CLI/auth with repository admin permission

If any precondition fails, stop.

## Naming

Application:

```text
RMS+ Support Hub
```

GitHub repository:

```text
Rms-Support-Hub
```

Do not attempt spaces or `+` in the GitHub repository name.

### 1. Final stale-name scan

Search tracked repo for:

```text
QA Support Hub
OnlineOrderTool
online_order_tool
Online_Order_Tool
```

Classify all remaining matches.

Preserve the feature name:

```text
Online Order Tool
```

### PERSISTED STORAGE CONTRACTS — DO NOT RENAME

The following runtime values are external/persisted compatibility contracts.
During Session 08 stale-name cleanup, every value below MUST remain
byte-identical and MUST be classified as:

```text
EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE
```

| Contract | Source | Literal | Classification |
|---|---|---|---|
| Environment selection prefix | `frontend/src/app/core/services/module.service.ts` — `ENV_STORAGE_PREFIX` | `onlineOrderTool.activeEnvironment.` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |
| Motion preference | `frontend/src/app/core/services/motion.service.ts` — `MOTION_STORAGE_KEY` | `qa-support-hub:motion` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |
| Theme preference | `frontend/src/app/core/services/theme.service.ts` — `THEME_STORAGE_KEY` | `qa-support-hub:theme` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |
| Prompt Studio history | `frontend/src/app/features/prompt-studio/services/prompt-history.service.ts` — `PROMPT_STUDIO_HISTORY_KEY` | `qa-support-hub.prompt-studio.history` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |
| Prompt Studio bug draft | `frontend/src/app/features/prompt-studio/services/prompt-storage.service.ts` — `PROMPT_STUDIO_DRAFT_KEYS.bug` | `qa-support-hub.prompt-studio.bug-draft` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |
| Prompt Studio story draft | `frontend/src/app/features/prompt-studio/services/prompt-storage.service.ts` — `PROMPT_STUDIO_DRAFT_KEYS.story` | `qa-support-hub.prompt-studio.story-draft` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |
| Prompt Studio test-case draft | `frontend/src/app/features/prompt-studio/services/prompt-storage.service.ts` — `PROMPT_STUDIO_DRAFT_KEYS.testCase` | `qa-support-hub.prompt-studio.test-case-draft` | `EXTERNAL / PERSISTED CONTRACT — DO NOT CHANGE` |

Renaming these values during repository, project, or product stale-name
cleanup would silently discard or orphan existing users' theme preference,
motion preference, Prompt Studio bug drafts, Prompt Studio story drafts,
Prompt Studio test-case drafts, Prompt Studio history, and per-module selected
environment. The application test suite may still pass because these exact
storage-key literals are not necessarily pinned by regression tests.

Therefore, repository/project/product rename does **not** authorize storage-key
rename.

### 2. Verify technical identity

Confirm:
- browser title = RMS+ Support Hub
- navbar = RMS+ Support Hub
- README = RMS+ Support Hub
- npm = rms-support-hub
- .NET = RmsSupportHub.*
- features retain correct names

### 3. Rename GitHub

Inspect:

```bash
git remote -v
gh repo view
```

Rename:

```bash
gh repo rename Rms-Support-Hub
```

Use an explicit repository flag if the CLI requires it.

Then explicitly update local canonical URL:

```bash
git remote set-url origin https://github.com/Hossam1104/Rms-Support-Hub.git
git remote -v
git -c gc.auto=0 -c maintenance.auto=false fetch --prune origin
```

### 4. Final regression

Run:
- frontend full tests
- backend tests/build
- production frontend build
- representative browser smoke

No state-changing Online Order actions.

### 5. Current docs only

Update durable docs containing the old repository/project identity.

Do not recreate removed historical session plans.

## Commit

If tracked files changed:

```text
chore(brand): finalize RMS+ Support Hub repository identity
```

Remember the remote URL itself is local `.git/config` metadata and is not committed.

## Final Git

Require:
- `main`
- clean
- canonical new remote
- `main == origin/main`
- `0 0`
- session branch deleted

## Final Response

```text
## Result
Completed / Partially Completed / Blocked

## Identity
Product: RMS+ Support Hub
GitHub: <owner>/Rms-Support-Hub
.NET: RmsSupportHub.*
npm: rms-support-hub

## Branding
RMS:
DBS:
UPC/GHC:
Payment assets:

## UI
Density:
Tables:
Cards:
Icons:
Motion:
Responsive:

## Validation
Frontend:
Backend:
Production build:
Browser:

## Git
Remote:
Main/origin: 0 0
Branch cleanup:
```

---
