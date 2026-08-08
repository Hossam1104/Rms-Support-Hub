# RMS+ Support Hub — Luna Max Execution Sessions

**Planner:** GPT-5.6 Sol  
**Executor:** Codex GPT-5.6 Luna Max  
**Final reviewer:** Claude Opus 5

Execute these sessions **in order**. Do not execute multiple sessions in one chat unless the preceding session is fully merged, pushed, synchronized `0 0`, and its branch deleted.

---

## Execution Status

Session 00: Completed
Session 01: Completed
Session 02: Completed
Session 03: Active
Sessions 04–08: Planned
Opus R1: Planned

---

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

# SESSION 02 — Asset Pipeline & Brand Foundation

## Goal

Make the supplied assets reusable, semantic, centralized and safe.

## Branch

```text
refactor/rms-hub-02-assets
```

## Required Work

### 1. Reinspect actual assets

Use current:

```text
./assets
```

Never assume files are unchanged.

### 2. Normalize frontend asset placement

Use the repository’s existing public/static convention.

Prefer semantic folders such as:

```text
frontend/public/assets/
├── brand/
├── modules/
├── payments/
├── commerce/
└── system/
```

Do not duplicate an already-approved asset unless migration is required.

Preserve the verified Riyal behavior.

### 3. Central asset catalog

Create one typed mapping, preferably:

```text
frontend/src/app/core/config/app-assets.ts
```

Expose semantic references for:
- RMS
- DBS
- UPC
- GHC/Whites
- Riyal
- Visa
- Mastercard
- Mada
- Tabby
- Tamara
- offer
- loader
- confirmed CustomMessage assets

Do not create a service unless runtime logic needs it.

### 4. Reusable logo/brand primitive

Only if useful across multiple screens, add a lightweight logo/brand component or shared pattern supporting:
- size
- alt
- decorative state
- `object-fit: contain`
- consistent aspect handling

Do not over-engineer.

### 5. Integrate hierarchy

RMS:
- global navbar
- Hub hero/product identity

DBS:
- secondary attribution only

UPC:
- UPC E-Commerce module card/shell/sidebar

GHC/Whites:
- only where actual module/context supports it

### 6. CustomMessage assets

Inspect contents. Map only confirmed semantics. Unknown assets stay unused and documented.

No new message functionality.

## Validation

Targeted:
- asset catalog
- logo primitive
- Hub/module card

Then:

```text
npm --prefix frontend test -- --watch=false
npm --prefix frontend run build -- --configuration production
git diff --check
```

## Commit

```text
feat(brand): integrate RMS+ Support Hub asset system
```

---

# SESSION 03 — Global Density, Tables, Borders & Surface System

## Goal

Reduce project-wide wasted space and establish consistent tables, margins, cards, panels and form density.

## Branch

```text
refactor/rms-hub-03-density-tables
```

## Critical Requirement

This is global. Do not make one-off Online Order-only CSS fixes.

## Required Work

### 1. Normalize density tokens

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

### 2. Reduce vertical waste

Target approximately 15–30% reduction where safe in:
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

### 3. Shared table contract

Every true data table should gain:
- 1px outer border
- visible header border
- row separators
- safe card inset/margins
- compact cell padding
- numeric alignment
- clear totals/footer
- responsive horizontal scrolling only when necessary

Optional vertical separators are allowed where they materially improve dense numeric scanning.

### 4. Apply representative global surfaces

Apply to:
- shared table/grid components
- Online Order table
- Order Requests
- Products
- Items
- at least one non-Online-Order table/list if truly tabular

### 5. Form density

Normalize:
- labels
- helper text
- control heights
- field gaps

Do not change validation/data.

### 6. Panels/cards

Normalize:
- border
- radius
- padding
- internal gap
- header height

Do not perform the major landing redesign yet.

## Validation

Required:

```text
npm --prefix frontend test -- --watch=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
npm --prefix frontend run build -- --configuration production
git diff --check
```

If browser exists, check representative pages at 1440/1024/900/768/390.

If not, do not claim rendered visual validation; Session 07 will close it.

## Commit

```text
refactor(ui): compact layouts and standardize data tables
```

---

# SESSION 04 — Landing, Three.js, Shared Cards, Icons & Motion

## Goal

Make the **RMS+ Support Hub** landing visually compelling and finalize the shared card/icon/motion language.

## Branch

```text
refactor/rms-hub-04-landing-motion
```

## Three.js Boundary

Three.js already exists.

Do NOT:
- install another 3D framework
- create additional WebGL scenes in modules
- move Three.js into the root initial bundle

### 1. Landing hero

Use:
- RMS logo
- RMS+ Support Hub title
- concise subtitle
- environment/status
- subtle DBS attribution

Reduce dead vertical hero space.

### 2. Refine current Hub Three.js scene

Preserve:
- dynamic/lazy import
- Hub-only
- aria-hidden/decorative
- pointer-events none
- DPR cap
- visibility pause
- teardown
- reduced-motion fallback
- functional independence from WebGL

Enhance only with restrained technical/RMS visual identity.

No giant models/textures/post-processing stack.

### 3. Main tool cards

Cards:
1. QA Prompt Studio
2. Online Order Tool
3. POS Maintenance Tool

Requirements:
- equal-height peers
- aligned logo/icon/title/status
- aligned footer action
- capability list
- same card contract
- strong focus
- restrained hover
- POS = Coming Soon

### 4. Icon language

Reuse existing icon system.

Add meaningful icons to:
- capability summaries
- card actions
- status/meta
- common section headings

Do not add an icon dependency if current project already has adequate icons.

### 5. Motion language

Use MotionService.

Add subtle:
- card entrances
- hover/focus
- icon movement
- status reveal
- section expansion

Reduced motion removes transforms/entrances.

No heavy table animation.

## Validation

```text
npm --prefix frontend test -- --watch=false
npm --prefix frontend run build -- --configuration production
```

Record:
- initial bundle
- Three.js lazy chunk
- warnings

Browser if available:
- WebGL scene
- fallback
- reduced motion
- light/dark
- card keyboard navigation
- no layout shift
- console clean

## Commit

```text
feat(ui): refresh RMS+ Hub landing and motion system
```

---

# SESSION 05 — Prompt Studio UI Harmonization

## Goal

Apply the new compact branded system to Prompt Studio while preserving its simplified behavior.

## Branch

```text
refactor/rms-hub-05-prompt-studio-ui
```

## Hard Output Contracts

Do not change canonical sections.

Bug = exactly 11 sections.  
Story = exactly 7 sections.  
Test Case = exactly 9 sections.

Do not change:
- quality semantics
- history max 10
- drafts
- copy
- Markdown
- text export
- Ctrl/Cmd+Enter

### 1. Landing cards

Equal-height:
- Bug Refinement
- Story Refinement
- Test Case Generation

Each:
- meaningful icon
- title
- concise explanation
- capability summary
- aligned footer action

### 2. Generator workspaces

Compact:
- page header
- form section spacing
- label/helper gaps
- preview header/action area
- Prompt Quality
- history

Use shared surface/card/density tokens.

### 3. Icons

Add useful icons for:
- Load Sample
- Generate
- Clear
- Copy
- Download
- Quality
- History

Preserve accessible names.

### 4. Motion

Use restrained feedback/entrance only.

Reduced motion must disable nonessential transforms.

## Validation

Run:
- focused builder/generator/history/storage/export tests
- full frontend suite
- production build
- `git diff --check`

Explicitly confirm prompt-builder contracts unchanged.

## Commit

```text
refactor(prompt-studio): harmonize RMS+ compact visual system
```

---

# SESSION 06 — Online Orders Dense UI, Branding, Tables & Commerce Assets

## Goal

Implement the screenshot-driven Online Order UI improvements while preserving every business contract.

## Branch

```text
refactor/rms-hub-06-online-orders-ui
```

## Hard Boundary

Do NOT change:
- APIs
- DTOs
- payload mapping
- module keys
- capability guard
- filters/paging business rules
- payment codes
- status logic
- Send/Resend/Cancel behavior

UI presentation only.

### 1. Module identity

Use:
- RMS+ global brand
- UPC logo for UPC E-Commerce
- GHC/Whites only where actual context supports it

Improve module card and sidebar/header identity.

### 2. Compact Order Builder

Reduce:
- page header height
- workflow height
- inter-section gap
- accordion/header height
- field row gap
- panel padding

Keep it readable.

### 3. Order Header

Compact the existing grid without changing fields:
- Branch
- Order code
- Parent order code
- Delivery cost
- Order status
- Notes
- coordinates

### 4. Products table

Add:
- outer border
- safe inset from parent
- header separation
- row separators
- compact controls
- aligned price/qty/discount/VAT/total
- aligned delete action
- no page-level horizontal overflow

### 5. Items summary

Add:
- outer border
- row separators
- clear totals/footer row
- compact cell padding
- numeric alignment
- safe card margins

### 6. Order Requests

Apply the same shared table contract while preserving all existing functionality.

### 7. Riyal

Use the canonical Saudi Riyal asset/presentation everywhere monetary values use the Riyal.

Do not regress the existing verified Riyal check.

### 8. Payment assets

Map visual logos only to existing methods:
- Visa
- Mastercard
- Mada
- Tabby
- Tamara

Underlying values/codes/payloads remain unchanged.

Unknown method = generic fallback; never guessed logo.

### 9. Offer and loading

Use `offer_logo.png` only for existing discount/offer concepts.

Use loader only if it improves a real current loading state.

### 10. Browser safety

If browser available, use the configured local backend/proxy.

Read-only validation only.

Never click:
- Send
- Resend
- Cancel
- Submit
- destructive order actions

Widths:
- 1440
- 1024
- 900
- 768
- 390

Validate:
- reduced scrolling
- table borders
- table/card margins
- responsive behavior
- summary readability
- logos/payment assets
- no distortion
- console clean

## Full Validation

```text
npm --prefix frontend test -- --watch=false
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
npm --prefix frontend run build -- --configuration production
git diff --check
```

## Commit

```text
refactor(online-orders): compact workflows and brand commerce UI
```

---

# SESSION 07 — Cross-Project UI Closure & Browser Matrix

## Goal

Finish global UI consistency and prove the result visually.

## Branch

```text
refactor/rms-hub-07-ui-closure
```

## Required Work

### 1. Asset audit

Verify each integrated asset:
- semantic use
- no duplicate copies
- correct sizing
- no distortion
- correct alt/decorative state

List intentionally unused supplied assets and why.

### 2. Global consistency

Audit:
- navbar
- page headers
- breadcrumbs
- cards
- tables
- forms
- statuses
- icons
- loaders
- messages
- panels
- empty states

Fix only real inconsistencies.

### 3. Equal-height peers

Verify:
- Hub cards
- Prompt Studio landing cards
- Online Order module cards
- POS capability cards
- payment method cards if card-based

### 4. Global table audit

No true table should:
- touch card edges
- lack readable row separation
- have inconsistent totals
- cause shell overflow

### 5. REQUIRED browser matrix

Routes:

```text
/
 /tools/prompt-studio
 /tools/prompt-studio/bugs
 /tools/prompt-studio/stories
 /tools/prompt-studio/test-cases
 /tools/online-orders
 /tools/online-orders/modules/<actual-key>/order
 /tools/online-orders/modules/<actual-key>/order-requests
 /tools/pos-maintenance
```

Widths:

```text
1440 × 900
1024 × 900
900 × 900
768 × 900
390 × 844
```

Validate:
- one H1
- one primary main
- no shell overflow
- compact density
- table borders
- table margins
- equal cards
- logo scaling
- icons/accessibility
- light theme
- dark theme
- reduced motion
- Three.js only on Hub
- keyboard focus
- no new console/page errors

If browser tooling is unavailable:
- do not claim completion
- return Partially Completed
- leave a precise browser-validation handoff

### 6. Performance

Record:
- initial bundle
- Three.js lazy chunk
- largest relevant feature chunks
- warnings

Investigate major unexplained growth.

## Full Validation

Required:
- frontend full suite
- backend/repository wrapper
- production build
- `git diff --check`

## Commit

```text
refactor(ui): close RMS+ cross-project visual consistency
```

---

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
git remote set-url origin https://github.com/<OWNER>/Rms-Support-Hub.git
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

# FINAL OPUS 5 REVIEW — Run Only After Session 08

Give Opus 5 this review prompt after Luna has completed and pushed all sessions:

```text
# RMS+ Support Hub — Final Independent Review
# Reviewer: Claude Opus 5
# Mode: REVIEW FIRST, DO NOT BEGIN WITH A REFACTOR

Review the completed RMS+ Support Hub after Luna Sessions 00–08.

Repository must be clean on main and synchronized 0 0.

Assess:

1. Product naming consistency
2. GitHub/project naming consistency
3. RMS/DBS/module brand hierarchy
4. Asset usage correctness
5. Logo sizing/distortion
6. Global spacing/density
7. Scroll reduction
8. Table margins/borders/readability
9. Equal-height card implementation
10. Card visual hierarchy
11. Prompt Studio visual consistency
12. Online Order visual consistency
13. POS Coming Soon consistency
14. Icon consistency
15. Motion quality/restraint
16. Reduced-motion behavior
17. Three.js isolation/performance
18. Responsive behavior
19. Light/dark themes
20. Accessibility
21. Bundle/performance regressions
22. Dead/duplicated UI code introduced by the refactor
23. Prompt Studio behavior preservation
24. Online Order business behavior preservation
25. Maintainability

Hard behavior checks:

- Prompt Studio canonical output sections unchanged.
- Online Order API/DTO/payload/business behavior unchanged.
- POS has no operations.

Run relevant tests/builds.

Inspect browser at:
1440 / 1024 / 900 / 768 / 390.

Do not execute state-changing Online Order actions.

Return findings grouped as:

BLOCKER
HIGH
MEDIUM
LOW
OPTIONAL

Every finding must include:
- evidence
- affected files/components
- why it matters
- recommended fix
- whether it needs a Luna remediation prompt

Final recommendation:

APPROVED
APPROVED WITH MINOR FIXES
REQUIRES REMEDIATION

Do not generate a giant remediation program if only small findings exist.
```
