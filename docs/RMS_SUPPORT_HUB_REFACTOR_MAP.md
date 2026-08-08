# RMS+ Support Hub Refactor Map

Session 00 baseline for the branding, asset, density, UI, and technical-rename
programme. This map records current evidence; it does not rename code, routes,
projects, assets, or external identifiers.

## Rename map

| Observed reference | Classification | Current evidence | Planned handling |
|---|---|---|---|
| `QA Support Hub` | DISPLAY NAME / DOCUMENTATION | 39 matching lines in 21 tracked files, including the Hub heading, navbar, sidebar, POS copy, tests, README, release documents, state, and historical ADRs. | Session 01 changes host-level product copy to `RMS+ Support Hub`; historical/audit references are reviewed individually. |
| `Online Order Tool` | BUSINESS FEATURE NAME / DOCUMENTATION / EXTERNAL CONTRACT | 23 matching lines in 14 tracked files, including tool metadata, Online Orders landing, Hub tests, README, API/schema documents, and release state. | Preserve as the feature name inside the RMS+ Support Hub. Do not rename the `/tools/online-orders` feature route or business wording. |
| `OnlineOrderTool` | TECHNICAL PROJECT NAME / NAMESPACE | Session 00 baseline: 261 matching lines in 89 tracked files, including the solution, project folders/files, .NET namespaces/usings, scripts, tests, and technical documentation. | Completed in Session 01: physical solution/project identities and host namespaces now use `RmsSupportHub.*`; payload, API, database, and persisted contracts remain unchanged. |
| `online_order_tool` | GIT/REMOTE REFERENCE / DOCUMENTATION | 7 matching lines in 3 tracked files, primarily repository-structure and task/path documentation. | Keep the current repository identity through Session 07; GitHub rename is deferred to Session 08. |
| `Online_Order_Tool` | GIT/REMOTE REFERENCE / DOCUMENTATION | Present in the active execution prompt as a search target; the observed remote URL is `https://github.com/Hossam1104/Online_Order_Tool.git`. | Do not alter the current remote in Session 01; the GitHub rename remains Session 08. |
| `QA_SUPPORT_HUB` | DOCUMENTATION / FILE NAME | Appears in active search instructions and the tracked release-readiness filename `docs/QA_SUPPORT_HUB_RELEASE_READINESS.md` plus its links. | Review current-document links during the technical/product rename; preserve historical evidence unless the next session proves a safe rename. |
| `RMS-Support-Hub` | FUTURE GIT/REMOTE REFERENCE | The two programme documents used the older future-slug spelling. | Normalized in both programme documents to the authoritative future target `Rms-Support-Hub`; actual `origin` remains the current Online Order Tool repository until Session 08. |

Current product targets are deliberately distinct:

```text
Display: RMS+ Support Hub
.NET technical root: RmsSupportHub
npm package: rms-support-hub
Future GitHub repository: Hossam1104/Rms-Support-Hub (Session 08)
```

## Asset map

The real tracked asset directory contains 17 files. All supplied assets remain
under the exact root path `assets/`; the Angular public copies now use semantic
folders under `frontend/public/assets/`. The Riyal SVG remains at the public
root as a deliberate compatibility path for the existing verifier and currency
component. The table below records the Session 00 inventory; current integration
is summarized after it.

| Exact file | Format and measured geometry | Transparency / semantic reading | Current copy/reference status |
|---|---|---|---|
| `assets/RMS_Logo.svg` | SVG; `viewBox="0 0 120 50"`; no explicit width/height | Vector artwork; no root background observed. Primary RMS product mark. | No frontend copy or direct component reference. |
| `assets/DBS_Logo.svg` | SVG; `468 × 355`; `viewBox="0 0 468 355"`; full-canvas embedded PNG pattern | Root `fill="none"`, but a full-canvas embedded raster is used, so final alpha needs visual review. DBS attribution only. | No frontend copy or direct component reference. |
| `assets/UPC_Logo.svg` | SVG; `1000px × 1000px`; `viewBox="0 0 1000 1000"` | Vector/group artwork; no root background observed. UPC E-Commerce identity. | Existing code asks for missing `assets/upc_logo.svg` / `static/assets/upc_logo.svg`; case/name/path do not match. |
| `assets/GHC_Logo.svg` | SVG; `1320px × 831px`; `viewBox="0 0 1320 831"` | Vector/group artwork; no root background observed; render review required. GHC/Whites identity is not yet semantically settled. | Existing code asks for missing `assets/whites_logo.svg` / `static/assets/whites_logo.svg`; do not substitute blindly. |
| `assets/Saudi_Riyal.svg` | SVG; `viewBox="0 0 1124.14 1256.39"`; 2 non-empty paths | Vector-only currency mark with no text, script, image, or external reference. Canonical Riyal asset. | `app-riyal` and the verifier expect `/assets/Saudi_Riyal.svg` and `frontend/public/assets/Saudi_Riyal.svg`; the public copy is missing and `scripts/verify-riyal-asset.ps1` currently fails on that missing file. |
| `assets/MADA.png` | PNG; `96 × 96` | Transparent pixels measured. Mada payment presentation. | No current frontend copy/reference. Payment code remains external contract. |
| `assets/MasterCard.png` | PNG; `80 × 50` | Transparent pixels measured. Mastercard payment presentation. | No current frontend copy/reference. |
| `assets/Visa.png` | PNG; `80 × 50` | Transparent pixels measured. Visa payment presentation. | No current frontend copy/reference. |
| `assets/tabby.png` | PNG; `250 × 250` | Transparent pixels measured. Tabby payment presentation. | No current frontend copy/reference. |
| `assets/tamara.png` | PNG; `256 × 256` | Transparent pixels measured. Tamara payment presentation. | No current frontend copy/reference. |
| `assets/offer_logo.png` | PNG; `512 × 512` | Transparent pixels measured. Existing offer/discount context only. | No current frontend copy/reference. |
| `assets/loader.svg` | SVG; `62px × 62px`; `viewBox="0 0 128 128"` | Vector spinner with no root background observed. Loading state only. | No current frontend copy/reference. |
| `assets/CustomMessageBox/error.svg` | SVG; `287px × 240px`; `viewBox="0 0 287 240"` | Grouped illustration with gradients; no root page background, internal artwork requires render review. Error message art. | No current frontend copy/reference. |
| `assets/CustomMessageBox/information.svg` | SVG; `287px × 240px`; `viewBox="0 0 287 240"` | Same grouped illustration family; internal artwork requires render review. Information message art. | No current frontend copy/reference. |
| `assets/CustomMessageBox/question.svg` | SVG; `287px × 240px`; `viewBox="0 0 287 240"` | Same grouped illustration family; internal artwork requires render review. Question/confirmation art. | No current frontend copy/reference. |
| `assets/CustomMessageBox/success.svg` | SVG; `287px × 240px`; `viewBox="0 0 287 240"` | Same grouped illustration family; internal artwork requires render review. Success message art. | No current frontend copy/reference. |
| `assets/CustomMessageBox/warrning.svg` | SVG; `287px × 240px`; `viewBox="0 0 287 240"` | Same grouped illustration family; filename preserves the supplied `warrning` spelling. Warning art. | No current frontend copy/reference; spelling and semantics require an explicit Session 02 decision. |

No exact duplicate SHA-256 hashes were found among the 17 root assets. Session
02 created the typed catalog at `frontend/src/app/core/config/app-assets.ts`
and the reusable `app-brand-mark` primitive. New UI references use catalog
entries; the Riyal root path is the only deliberate compatibility location.

### Session 02 asset integration

- `brand/`: RMS and DBS copies. RMS is used by the navbar and Hub identity;
  DBS is a small Hub attribution on a neutral plate.
- `modules/`: UPC and GHC/Whites copies. UPC is used by module cards and the
  module sidebar; GHC/Whites is used only for the confirmed GHC module keys.
- `payments/`: Visa, MasterCard, MADA, Tabby, and Tamara copies, catalogued for
  the later payment presentation session without changing payment values.
- `commerce/`: the offer asset, catalogued for existing offer contexts.
- `system/`: the loader and confirmed CustomMessageBox family, catalogued with
  no new message functionality. The supplied `warrning.svg` spelling remains
  the public filename and is exposed as semantic `warning`.
- `Saudi_Riyal.svg` remains at `frontend/public/assets/Saudi_Riyal.svg`; the
  existing verifier passes and `app-riyal` consumes the catalog reference.

### Session 04 landing, scene, card and motion integration

- The Hub landing now uses a compact two-column hero with RMS identity, DBS
  attribution, workspace/default-lane status, and a tool-availability rail;
  the three registered tools and POS Coming Soon state remain authoritative.
- `app-tool-card` keeps the shared equal-height/pinned-footer contract while
  adding semantic capability, status, and action icons. Motion distances are
  tokenized so hover/focus transforms collapse with the existing
  `MotionService` reduced-motion contract.
- The existing lazy Hub-only Three.js scene remains decorative, pointer-
  transparent, DPR-capped, visibility-paused, and disposable. A small themed
  RMS+ core/halo was added without textures or post-processing; WebGL and
  reduced-motion fallbacks remain CSS-only.
- Session 04 validation passed: 49 frontend test files / 258 tests, production
  build with no warnings, 440.68 kB initial raw / 101.68 kB estimated transfer,
  and 734.66 kB raw / 153.90 kB estimated transfer for the lazy Three.js
  chunk. Interactive browser evidence was unavailable in this environment.

## UI touch map

| Surface | Current controlling source | Baseline responsibility / next-session dependency |
|---|---|---|
| Root shell and notifications | `frontend/src/app/app.html`, `app.ts`, `app.config.ts`, `shared/components/toast` | Router outlet and global toast composition. Product metadata and shell labels are Session 01 scope. |
| Navbar and global preferences | `layout/navbar/navbar.component.ts`, `core/services/theme.service.ts`, `core/services/motion.service.ts` | Fixed global navbar, environment selector, theme toggle, and motion toggle. RMS product identity is Session 01; preference behavior is frozen. |
| Online Order shell | `features/module-shell/module-shell.component.ts`, `layout/sidebar/sidebar.component.ts`, `layout/breadcrumb/breadcrumb.component.ts` | Fixed sidebar/navbar, collapse state, module identity, breadcrumbs, canonical and legacy mounts. Preserve route and module-key contracts. |
| Page headers and shared surfaces | `shared/ui/page-header`, `ui-card`, `ui-section`, `ui-toolbar`, `ui-field`, `ui-button`, `ui-input`, `ui-select`, `searchable-select`, `ui-icon-button` | Shared geometry and form primitives. Density and surface changes belong to Session 03. |
| Cards and navigation cards | `shared/ui/tool-card`, `features/hub/hub.component.ts`, `features/landing/module-card.component.ts`, `features/pos-maintenance/pos-maintenance-placeholder.component.ts` | Hub, Prompt Studio, Online Order module, and POS peer-card layouts already consume the shared card contract; landing refinement is Session 04. |
| Tables and tabular summaries | `shared/ui/ui-table`, `features/flat-order/components/products-table.component.ts`, `payments-table.component.ts`, `features/unicommerce/components/row-items-table.component.ts`, `features/order-requests/components/requests-table.component.ts` | Existing table wrapper, product/payment/item/request tables, and numeric summaries are the global density/table touch points for Sessions 03 and 06. |
| Tokens, typography, gradients, motion | `frontend/src/styles/_tokens.css`, `_typography.css`, `_gradients.css`, `_animations.css`, `order-requests-filter.css`, `styles.css` | Semantic colors, card tokens, spacing/radius/type scales, gradients, reduced-motion rules, and Bootstrap Icons are centralized here. Raw colors stay in token/gradient files. |
| Theme and motion services | `core/services/theme.service.ts`, `core/services/motion.service.ts` | `data-theme` and `data-motion` are stamped on `<html>`; system, explicit, and reduced-motion behavior is already centralized. |
| Hub landing and scene | `features/hub/hub.component.ts`, `hub/tool-registry.ts`, `features/hub/hub-scene/hub-scene.component.ts` | Three.js is dynamically imported only by the decorative Hub scene; CSS fallback, WebGL probe, DPR cap, visibility pause, and teardown are already present. Refine only in Session 04. |
| Prompt Studio landing/workspaces | `features/prompt-studio/prompt-studio.component.ts`, `prompt-studio.routes.ts`, generator components, `components/generator-workspace`, `prompt-preview`, `prompt-quality-panel`, history/storage services | Client-side generator UI and canonical output behavior are frozen. Visual harmonization belongs to Session 05. |
| Online Order landing/modules | `features/landing/landing.component.ts`, `module-card.component.ts`, `core/services/module.service.ts` | Module cards, environment selection, dynamic module data, and actual keys (`upc_ecommerce`, `ghc_ecommerce`, `ghc_unicommerce`) are feature behavior; branding/asset presentation is Session 02/06. |
| Flat Order workspace | `features/flat-order/flat-order.component.ts`, `components/order-info`, `client-info`, `delivery-info`, `products-table`, `payments-table`, `api-config`, `order-section-navigation`, `order-summary-rail` | Order Header, Customer, Delivery, Products, Payments, Payload & Send sections and summary rail; UI-only compaction is Session 06. |
| Uni-Commerce workspace | `features/unicommerce/unicommerce.component.ts`, `components/order-fields`, `consumer-section`, `delivery-section`, `row-items-table`, `invoice-summary` | Invoice fields, customer/delivery, row items, summary, API config; preserve payload and validation contracts. |
| Order Requests | `features/order-requests/order-requests.component.ts`, `components/filter-bar`, `requests-table`, `order-request-details`, cancel/resend dialogs | Filters, stats, table, pagination, details, cancel/resend presentation. Business behavior and capability gating are frozen. |
| POS Coming Soon | `features/pos-maintenance/pos-maintenance-placeholder.component.ts`, `core/models/pos-capability.model.ts` | Status, planned capability cards, and return link only. No POS operations or generic execution surface may be added. |

Current route topology is lazy and typed through `ToolRouteData`:

```text
/                                      Hub
/tools/prompt-studio                   Prompt Studio landing
/tools/prompt-studio/bugs              Bug Refinement
/tools/prompt-studio/stories           Story Refinement
/tools/prompt-studio/test-cases        Test Case Generation
/tools/online-orders                   Online Order landing
/tools/online-orders/modules/:key/order
/tools/online-orders/modules/:key/unicommerce
/tools/online-orders/modules/:key/order-requests
/tools/online-orders/modules/:key/order-requests/:orderId
/modules/:key/...                      legacy Online Order compatibility mount
/tools/pos-maintenance                 POS Coming Soon
/_kitchen-sink                         development-only route; absent in production
```

Current technical project names are `frontend` / npm package `rms-support-hub`,
`backend/RmsSupportHub.slnx`, and .NET projects
`RmsSupportHub.Api`, `RmsSupportHub.Core`, `RmsSupportHub.Data`, and
`RmsSupportHub.Tests`. Session 01 verified the renamed solution, project
references, namespaces, assembly/root namespaces, scripts, current docs, and
test assembly. The observed Git remote is
`https://github.com/Hossam1104/Online_Order_Tool.git`.

## Risky identifiers to preserve

- API paths and the `/api` proxy contract.
- JSON properties, payload shapes, request fixtures, database names/tables,
  SQL column names, and repository query contracts.
- Module keys including `upc_ecommerce`, `ghc_ecommerce`, and
  `ghc_unicommerce`; `IOrderModule.Capabilities` remains the behavior gate.
- Payment and integration values/codes such as `COD`, `Visa`, `Tamara`,
  `Tabby`, `Mada`, `CashOnDelivery`, and other persisted payment values.
- Environment/connection-string identifiers and upstream integration URLs.
- Persisted storage namespaces and draft keys, including the existing
  `onlineOrderTool.activeEnvironment.*`, `qa-support-hub:theme`, and
  `qa-support-hub:motion` values, unless a later migration explicitly proves
  a safe compatibility path.
- Feature wording: `QA Prompt Studio`, `Online Order Tool`, and
  `POS Maintenance Tool`; POS remains Coming Soon/non-operational.
- Supplied asset filenames and case until the centralized asset pipeline has a
  deliberate compatibility mapping; do not silently turn `GHC_Logo.svg` into a
  `whites_logo.svg` contract.

## Session dependencies

| Session | Dependency from this map |
|---|---|
| 00 — Baseline | Completed: naming inventory, 17-file asset inventory, UI touch map, preserved identifiers, routes, project names, remote, and measured baseline. |
| 01 — Product & Technical Rename | Use this map to rename host display/technical identifiers while preserving feature names, routes, payloads, module keys, and persisted values. Future GitHub rename remains deferred. |
| 02 — Asset Pipeline & Brand Foundation | Move/centralize the root assets through the existing public/static convention, resolve missing `upc_logo`/`whites_logo` and Riyal paths deliberately, and review GHC/Whites plus CustomMessageBox semantics. |
| 03 — Global Density, Tables & Surface System | Apply tokenized spacing, panel, form, card, and true-table contracts globally using the touch points above. |
| 04 — Landing, Three.js, Shared Cards, Icons & Motion | Completed: compact Hub hero/status rail, semantic card icons and pinned actions, tokenized reduced-motion distances, and a restrained themed scene anchor; preserve fallback, isolation, and shared card/motion behavior. |
| 05 — Prompt Studio UI Harmonization | Active: apply branded compact presentation without changing builder sections, quality semantics, drafts, history, exports, or keyboard shortcuts. |
| 06 — Online Orders Dense UI & Commerce Assets | Apply screenshot-driven compaction and table/payment/Riyal/offer presentation without changing APIs, DTOs, payloads, status/filter/paging, capability, or order actions. |
| 07 — Cross-Project UI Closure | Perform the required responsive/theme/reduced-motion/browser matrix and final asset/table/card audit. |
| 08 — GitHub Rename & Final Checkpoint | Rename the actual GitHub repository only after Sessions 00–07 are complete and synchronized; update the remote to `Hossam1104/Rms-Support-Hub`. |
| R1 — Opus review | Independent final review after Session 08; no remediation is implied by this baseline map. |

### Baseline measurements

Commands executed on the Session 00 branch:

- `npm --prefix frontend test -- --watch=false --no-progress`: 46 test files,
  250 tests passed.
- Session 00 baseline command (before the technical rename): `dotnet test backend/OnlineOrderTool.slnx -c Release --nologo`: 161 passed,
  0 failed, 0 skipped.
- `npm --prefix frontend run build -- --configuration production`: initial
  439.28 kB raw / 101.43 kB estimated transfer; no budget warning.
- The same production build measured the lazy `three-module` chunk at
  734.66 kB raw / 153.98 kB estimated transfer.
- `scripts/verify-riyal-asset.ps1`: expected Session 02 integration gap — the
  required `frontend/public/assets/Saudi_Riyal.svg` copy is not present.
