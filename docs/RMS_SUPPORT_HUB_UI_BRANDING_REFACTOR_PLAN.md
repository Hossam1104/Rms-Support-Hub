# RMS+ Support Hub — UI, Branding, Asset, Density & Project-Rename Refactor Plan

**Planner:** GPT-5.6 Sol  
**Executor:** Codex GPT-5.6 Luna Max  
**Final reviewer:** Claude Opus 5  
**Target product name:** **RMS+ Support Hub**  
**GitHub-compatible repository slug:** **Rms-Support-Hub**
**Execution model:** Small validated sessions; branch → implement → validate → commit → fast-forward merge → push → verify `0 0`.

---

## Program Status

| Session | Status |
|---|---|
| 00 — Baseline, Asset Inventory & Rename Map | Completed |
| 01 — Product & Technical Rename | Completed |
| 02 — Asset Pipeline & Brand Foundation | Completed |
| 03 — Global Density, Table & Surface System | Completed |
| 04 — Landing, Three.js, Shared Cards, Icons & Motion | Completed |
| 05 — Prompt Studio UI Harmonization | Completed |
| 06 — Online Orders Dense UI + Branding + Tables | Active |
| 07 — Cross-Project UI Closure | Planned |
| 08 — GitHub Rename & Final Checkpoint | Planned |
| R1 — Opus Final Review | Planned |

## 1. Objectives

Refactor the existing support application into a polished **RMS+ Support Hub** while preserving all current business behavior.

The program has four goals:

1. **Reduce wasted space throughout the whole frontend**
   - tighter page and panel spacing
   - denser section headers and forms
   - less vertical scrolling
   - better use of desktop width
   - consistent margins around grids/tables

2. **Standardize tables and grids globally**
   - visible outer borders
   - row separators
   - optional column separators where useful
   - consistent header and totals rows
   - compact cell padding
   - safe margins inside parent cards
   - clear numeric alignment

3. **Rebrand the host product**
   - display name: `RMS+ Support Hub`
   - .NET technical root: `RmsSupportHub`
   - npm/package identifier: `rms-support-hub`
   - GitHub repository: `Rms-Support-Hub`
   - integrate RMS, DBS, UPC, GHC/Whites, Riyal, payment, offer, loader and custom-message assets

4. **Refresh the visual language**
   - stronger landing page
   - refine the existing lazy Hub-only Three.js scene
   - consistent equal-height peer cards
   - more meaningful icons
   - restrained animation across the application
   - branded module identities
   - preserve light/dark/reduced-motion support

This is a UI/product-identity refactor, not a business-function rewrite.

---

## 2. Screenshot-Driven UI Findings

### 2.1 Excessive vertical space

The current Order Builder uses large vertical gaps in:
- page hero/header
- workflow strip
- section headers
- field rows
- card padding
- gaps between Order Header, Customer, Products, Payments and Payload sections

**Target:** reduce unnecessary vertical travel by roughly 15–30% where safe while preserving readability, focus visibility and touch targets.

### 2.2 Tables need stronger boundaries

The supplied screenshots show:
- grids too close to parent card edges
- weak row boundaries
- columns relying heavily on whitespace
- totals lacking separation
- dense product rows that are harder to scan than necessary

**Target global table treatment:**
- 1px semantic outer border
- row separators
- optional vertical separators for dense numeric tables
- consistent header background and bottom border
- 8–12px cell padding based on density
- numeric alignment
- clear totals/footer row
- 12–16px safe inset from parent card
- horizontal scrolling only for genuinely wide tables

This applies to the whole project, not only Online Orders.

### 2.3 Card hierarchy

Peer cards across Hub, Prompt Studio, Online Order module landing and POS should use one visual contract:

```text
card
├── identity/header
│   ├── logo/icon
│   ├── title
│   └── status
├── description
├── capability/content
└── footer action
```

Within the same grid:

```css
grid-auto-rows: 1fr;

.card {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.card-footer {
  margin-top: auto;
}
```

Do not use arbitrary fixed heights.

---

## 3. Naming Strategy

| Context | Target |
|---|---|
| Application display name | `RMS+ Support Hub` |
| Browser title | `RMS+ Support Hub` |
| README heading | `RMS+ Support Hub` |
| .NET technical root | `RmsSupportHub` |
| .NET API project | `RmsSupportHub.Api` |
| .NET tests | `RmsSupportHub.Tests` |
| npm package | `rms-support-hub` |
| GitHub repository | `Rms-Support-Hub` |

Preserve feature names:
- QA Prompt Studio
- Online Order Tool
- POS Maintenance Tool

Preserve external/runtime identifiers unless proven safe to rename:
- API paths
- DB names/tables
- payload fields
- module keys such as `upc_ecommerce`
- payment codes
- integration identifiers
- persisted values

---

## 4. Asset Inventory and Intended Usage

The executor must inspect the real `./assets` directory before integrating anything.

| Asset | Intended usage |
|---|---|
| `RMS_Logo.svg` | Primary product identity: navbar, Hub hero, app mark |
| `DBS_Logo.svg` | Secondary company attribution: “Built by DBS”/landing/footer |
| `UPC_Logo.svg` | UPC E-Commerce module card, sidebar/module header |
| `GHC_Logo.svg` / Whites logo | GHC/Whites module/client identity only where actual context confirms it |
| `Saudi_Riyal.svg` | Canonical Riyal/currency icon throughout monetary UI |
| `Visa.png` | Existing Visa payment presentation |
| `MasterCard.png` | Existing Mastercard payment presentation |
| `MADA.png` | Existing Mada payment presentation |
| `tabby.png` | Existing Tabby payment presentation |
| `tamara.png` | Existing Tamara payment presentation |
| `offer_logo.png` | Existing offer/discount/promotion presentation |
| `loader.svg` | Branded loading state where useful |
| `CustomMessage Box/*` | Inspect first; use only where semantics are confirmed |

### Asset architecture

Do not scatter hardcoded paths throughout components. Create one small typed asset catalog such as:

```text
frontend/src/app/core/config/app-assets.ts
```

Example semantic references:

```ts
APP_ASSETS.brand.rms
APP_ASSETS.brand.dbs
APP_ASSETS.modules.upc
APP_ASSETS.modules.ghc
APP_ASSETS.payments.visa
APP_ASSETS.payments.mastercard
APP_ASSETS.payments.mada
APP_ASSETS.payments.tabby
APP_ASSETS.payments.tamara
APP_ASSETS.commerce.offer
APP_ASSETS.system.loader
```

Use a runtime service only if runtime behavior genuinely requires one.

---

## 5. Brand Hierarchy

### Level 1 — Product
**RMS+ Support Hub**
- RMS logo
- product name
- navbar
- global design language

### Level 2 — Company attribution
**DBS**
- secondary attribution
- landing/footer/about area
- never visually compete with RMS product identity

### Level 3 — Module/client identity
Examples:
- UPC
- GHC / Whites
- Online Order modules

Use in:
- module cards
- sidebar/module identity
- module headers
- context badges

---

## 6. Global Density System

Normalize semantic tokens for:

```text
--page-padding-inline
--page-padding-block
--section-gap
--panel-gap
--panel-padding
--panel-padding-compact
--control-height
--control-height-compact
--table-cell-padding-x
--table-cell-padding-y
--table-header-height
--card-gap
```

Use responsive `clamp()` where helpful.

### Desktop target
At 1440–1920px:
- more useful content above the fold
- smaller hero/page-header footprint
- compact workflow bars
- forms use horizontal space effectively
- tables do not float in excessive whitespace
- summary panels remain readable but compact

---

## 7. Shared Table System

All true data tables should use:

### Container
- semantic surface
- 1px outer border
- safe margin/inset
- overflow-x only when necessary

### Header
- differentiated surface
- visible bottom border
- stronger typography
- compact height

### Rows
- row separators
- readable hover state
- compact padding

### Cells
- text left aligned
- numeric values right aligned
- status appropriate/centered
- long identifiers wrap or truncate safely

### Totals/footer
- top border
- stronger totals
- clear semantic emphasis

Apply to:
- Online Orders
- Order Requests
- Items
- Products
- shared table/grid components
- other genuinely tabular views

Do not force table styling onto cards or definition lists.

---

## 8. Shared Card System

Reuse/extend the existing shared card design rather than creating a second competing system.

Useful semantic variants:
- navigation
- module
- information
- workspace
- metric
- payment

Equal height applies to peer cards in the same selection grid:
- Hub tools
- Prompt Studio generators
- Online Order modules
- payment method cards
- POS capability cards

It does not apply to:
- forms
- tables
- large workspaces
- summaries

---

## 9. Landing Page Refresh

### Hero
Use:
- RMS logo
- `RMS+ Support Hub`
- concise support-workspace subtitle
- optional small DBS attribution
- environment indicator
- restrained motion

Reduce unnecessary hero height.

### Three.js
The project already contains a lazy Hub scene. Refine it; do not add a second 3D system.

Keep:
- Hub-only
- dynamic/lazy import
- decorative canvas
- pointer-events none
- DPR cap
- visibility pause
- teardown
- reduced-motion fallback
- no functional dependency on WebGL

### Primary cards
1. QA Prompt Studio
2. Online Order Tool
3. POS Maintenance Tool — Coming Soon

Requirements:
- equal height
- aligned header/status/action
- capability summary
- strong logo/icon identity
- restrained hover/focus motion

---

## 10. Icons and Motion

Audit the existing icon approach first. Reuse it if adequate.

Add meaningful icons to:
- section headers
- workflow steps
- filters/search
- Customer/Products/Payments/Payload
- Prompt Studio generator types
- module capabilities
- loading/empty/status states
- primary and utility actions

Avoid icon spam.

### Motion
Use existing MotionService/reduced-motion behavior.

Add subtle:
- card entrance
- hover/focus
- icon micro-motion
- section expansion
- status/message appearance
- loading feedback

Avoid heavy animation on tables, pagination and continuous data refresh.

---

## 11. Online Order UI Refactor

### Shell
Reduce:
- hero height
- breadcrumb gaps
- sidebar excess padding
- section gaps

Preserve all routing and business behavior.

### Order Builder sections
Compact:
- Order Header
- Customer
- Products
- Payments
- Payload & Send

Each header should use:
- icon
- title
- concise helper
- collapse affordance
- consistent compact height

### Products table
Add:
- outer border
- safe card inset
- row separators
- compact quantity/discount fields
- aligned numbers
- aligned delete action
- consistent Riyal rendering

### Item summary
Add:
- outer table border
- row separators
- clear totals row
- compact padding
- better margins from card edges

### Payment assets
Use Visa/MasterCard/MADA/Tabby/Tamara only for existing payment methods.
Never change underlying codes or payloads.

### Offer asset
Use `offer_logo.png` only in existing discount/offer contexts.

---

## 12. Prompt Studio UI Refactor

Preserve all generator logic and canonical output contracts.

Improve only:
- landing cards
- page-header density
- form spacing
- preview panel
- Prompt Quality panel
- history layout
- icons
- motion
- equal-height landing cards

Do not reintroduce removed complexity.

---

## 13. POS Coming Soon

Keep POS non-operational.

Improve only:
- RMS+ branding
- card layout
- capability cards
- icons
- spacing
- motion
- consistency

No POS operations.

---

## 14. Technical Project Rename

Isolate this work in its own session.

Target technical names:

```text
OnlineOrderTool.Api   → RmsSupportHub.Api
OnlineOrderTool.Tests → RmsSupportHub.Tests
OnlineOrderTool.*     → RmsSupportHub.*
```

Update:
- solution/project filenames
- project folders
- `.sln`
- `.csproj`
- `ProjectReference`
- namespaces/usings
- root namespace/assembly name
- launch profiles
- scripts
- development docs
- package metadata

Preserve external/business contracts listed in Section 3.

---

## 15. GitHub Rename

Do after code/UI work is stable and pushed.

Target:
- application: `RMS+ Support Hub`
- repository slug: `Rms-Support-Hub`

Verify clean `main` and `0 0` first.

If GitHub CLI is authenticated/admin-authorized:

```bash
gh repo rename Rms-Support-Hub
```

Then update local remote explicitly:

```bash
git remote set-url origin https://github.com/<OWNER>/Rms-Support-Hub.git
git remote -v
git fetch origin
```

Verify `0 0`.

---

## 16. Execution Sessions

### Session 00 — Baseline, Asset Inventory & Rename Map
Confirm asset inventory, naming map, UI touch map, baseline tests/build/bundle.

### Session 01 — Product & Technical Rename
Rename host product and technical project identifiers while preserving business contracts.

### Session 02 — Asset Pipeline & Brand Foundation
Centralize assets and integrate RMS/DBS/UPC/GHC/Riyal/payment/offer/loading visuals.

### Session 03 — Global Density, Table & Surface System
Reduce global spacing, add table borders/margins, normalize cards/forms/panels.

### Session 04 — Landing, Three.js, Shared Cards, Icons & Motion
Refresh landing, refine existing Hub Three.js scene, equalize cards, improve icons/motion.

### Session 05 — Prompt Studio UI Harmonization
Apply compact branded system without changing generator behavior.

### Session 06 — Online Orders Dense UI + Branding + Tables
Apply screenshot-driven compaction, table structure and commerce assets while preserving business logic.

### Session 07 — Cross-Project UI Closure
Finish consistency, responsive matrix, themes, reduced motion, asset audit and visual regression.

### Session 08 — GitHub Rename & Final Checkpoint
Rename GitHub repository to `Rms-Support-Hub`, update remote, stale-name scan, final tests/build/browser smoke.

### Review R1 — Opus 5
Independent final review after Sessions 00–08. Findings only first; remediation prompts only for confirmed issues.

---

## 17. Browser Acceptance Matrix

Final routes:

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
- 1440 × 900
- 1024 × 900
- 900 × 900
- 768 × 900
- 390 × 844

Verify:
- one H1
- one main
- no shell overflow
- compact but readable density
- tables have margins and borders
- cards align/equalize
- logos scale correctly
- icons accessible
- actions reachable
- light/dark
- reduced motion
- no new console errors

---

## 18. Performance Rules

### Three.js
Keep:
- lazy
- Hub-only
- decorative
- teardown-safe
- low complexity

Do not put Three.js into the initial/root bundle.

### Images
- preserve vector SVGs
- `object-fit: contain`
- correct dimensions/aspect ratios
- avoid layout shift
- lazy-load non-critical images where useful

### Animation
No continuous high-frequency animation outside the Hub scene.

---

## 19. Accessibility Rules

- identity logos get meaningful alt text
- decorative marks use empty alt/aria-hidden
- icon-only controls have accessible names
- reduced motion honored
- Three.js canvas decorative
- color is never the only status cue
- hover has equivalent focus
- tables preserve semantic structure
- dense mode must retain visible focus

---

## 20. Git Rules

Every Luna session:

```text
main synchronized
→ session branch
→ implement
→ validate
→ diff review
→ commit
→ switch main
→ pull --ff-only
→ merge --ff-only
→ push
→ verify 0 0
→ delete branch
```

Never:
- `git reset --hard`
- `git push --force`
- `git push --force-with-lease`

Stop on divergence/conflict.

If Windows Git auto-maintenance locks recur, use session-local:

```text
-c gc.auto=0
-c maintenance.auto=false
```

---

## 21. Completion Definition

### Branding
- RMS+ Support Hub shown throughout host UI
- GitHub repo = Rms-Support-Hub
- local remote uses canonical renamed URL
- RMS primary identity
- DBS secondary attribution
- UPC/GHC contextual identity

### Density
- excessive vertical space reduced
- less scrolling
- compact panels/forms/headers

### Tables
- safe margins
- outer borders
- row separation
- clear totals
- numeric alignment

### Cards
- equal height in peer grids
- aligned actions
- consistent logo/icon hierarchy

### Assets
- every useful supplied asset has a semantic role
- CustomMessage assets used only when verified
- no scattered duplicate asset paths

### Motion
- more polished but restrained
- Three.js Hub-only
- reduced-motion path works

### Behavior
- Prompt Studio behavior unchanged
- Online Order business behavior unchanged
- POS remains Coming Soon

### Quality
- frontend tests pass
- backend tests pass
- production build passes
- browser matrix passes
- no new console errors
- no serious bundle regression

---

## 22. Final Opus Review Criteria

Opus 5 reviews:
1. naming consistency
2. brand hierarchy
3. asset correctness
4. spacing/density
5. table usability
6. equal-height cards
7. icon consistency
8. animation restraint
9. Three.js performance/isolation
10. themes
11. reduced motion
12. responsive behavior
13. accessibility
14. Prompt Studio preservation
15. Online Order preservation
16. POS non-operational state
17. technical/project naming
18. GitHub naming/remote
19. maintainability
20. duplicated/new UI debt

Finding levels:

```text
BLOCKER
HIGH
MEDIUM
LOW
OPTIONAL
```

Opus should propose focused Luna remediation prompts only for confirmed issues.
