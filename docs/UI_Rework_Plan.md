# UI Rework & Workflow Remediation Plan

> Companion to [`UI_Rework_Prompts.md`](UI_Rework_Prompts.md) — nine execution
> sessions, `U0` → `U8`, one conversation each.
>
> Successor to [`docs/Prompts/remediation_plan.md`](docs/Prompts/remediation_plan.md)
> (defects `B1`–`B36`, sessions `R0`–`R10`), which fixed the payload contract,
> the SQL contract and the Order Requests feature. That work landed. This
> document covers what it did **not** reach: the layer the operator actually
> touches.

---

## 1. Verdict

The remediation left a **correct engine inside an unusable cockpit**.

What is sound and must not be re-derived: the payload builders and their
key-for-key contract tests, the verified SQL in
[`docs/database-schema.md`](docs/database-schema.md), `OrderRequestRepository`
and the Order Requests page, the `Capabilities` abstraction, the
`Api`/`Core`/`Data`/`Tests` layout, per-session drafts, and the uniform error
envelope.

What is broken is everything between the operator and that engine:

1. **The order builder loses data while you type.** A successful consumer
   lookup leaves Last Name, Phone and Address empty and stacks six
   `The process cannot access the file … because it is being used by another
   process` errors over the page. This is a real lost-update race (D1), not a
   cosmetic glitch.
2. **Item lookup discards its own result** (D2) — the operator retypes the
   price, VAT and name by hand.
3. **Everything defaults to Production.** There is no environment switcher
   inside the module shell at all, the fallback is `environments[0]` =
   `UPC Production`, and cancel ignores the selection entirely (D3, D4).
4. **The Testing host is wrong** — `10.10.9.181:8080` instead of
   `10.10.10.181:8080` (D5).
5. **Branch is a free-text box** (D6) backed by a branch list derived from
   order history rather than the `Branches` table (D7).
6. **Two design systems coexist.** R8 built `shared/ui/**` on new tokens and
   migrated Order Requests; the entire order builder still renders through the
   `.glass-*` classes that `_gradients.css` itself labels *"legacy … superseded"*
   (D10), stacked in one undifferentiated column (D11).

Three facts frame the whole plan:

1. **The user's screenshot is the acceptance test.** Every visible failure in it
   — the error stack, the empty client fields, the flat card column, the
   free-text branch input — maps to a numbered defect below and to a session
   gate.
2. **`dbo.Branches` has not been introspected.** Only `Id` and `BranchCode` are
   verified, via the item CTE in `docs/database-schema.md` §3.2. The branch
   **name** column is unknown. It gets introspected in U0 and written down
   before any branch SQL is authored. The repository's standing rule holds:
   **never invent a column name.**
3. **Testing and Production share one host.** `10.10.10.181` — Testing on
   `:8080` against `RmsMainTest2`, Production on the default port against
   `RmsMainProd`. Every stale `10.10.9.181` and `10.10.8.181` literal is a
   defect.

**Recommendation: repair in place.** The backend contract work is correct and
tested. This programme changes state management, environment resolution, one
new repository, and the whole frontend presentation layer. It does not reopen
`FlatOrderPayloadBuilder`, `FlatOrderValidator`, or any existing verified query.

---

## 2. Defect register

Severity: **S1** = the tool loses data or the feature does not function ·
**S2** = the operator cannot reasonably work · **S3** = quality, drift, hygiene.

### 2.1 Workflow and data loss — S1

| # | Defect | Evidence |
|---|---|---|
| **D1** | **Per-field draft writes race and silently lose fields.** Every keystroke fires `PUT modules/{key}/order-field`. Each request does an unsynchronised load-modify-write against one JSON file, and the client then overwrites its own state with each response (`this.draft.set(res.state)`). A consumer lookup fires **eight** of these back to back: responses built from stale reads arrive out of order and clobber fields written by earlier ones, while the concurrent `File.WriteAllTextAsync` calls throw the file-lock errors. This is the exact failure in the reported screenshot — the toast reads `Found consumer: Mohamed Elbanna` while the Last Name, Phone and Address inputs are empty. | `flat-order.component.ts:194-204`, `:269-292`; `OrderController.cs:41-52`; `DraftManager.cs:49-55` |
| **D2** | **The item lookup result is thrown away.** `onLookupItem` toasts `Found item: {name}` and drops `res.data` on the floor. `AddProductDialogComponent` emits the lookup but has no input to receive a result, so `product.itemCode/itemName/unitPrice/vatPercentage` are never populated. Branch-specific pricing — the entire reason UPC lookups exist — never reaches the payload unless retyped by hand. | `flat-order.component.ts:248-263` vs `add-product-dialog.component.ts:76-97` |
| **D3** | **Cancel always targets Production.** `OrderController.CancelOrder` resolves its environment with `module.GetEnvironment(null)`, which returns the first *available* environment — `UPC Production` — no matter which environment the operator selected or which one the rest of the request used. | `OrderController.cs:148`; `UpcEcommerceModule.cs:81-86` |
| **D4** | **No environment switcher inside the module shell, and the default is Production.** The environment is selectable only from the landing card (`landing.component.ts:49`). `ModuleService.activeEnvironment` is set to `environments[0]` — `UPC Production` for UPC — and is never persisted, so a page refresh or a direct link to `/modules/upc_ecommerce/order` silently puts the operator on the live lane with no on-screen indication of which lane they are on. | `module.service.ts:44-56`; `navbar.component.ts`, `sidebar.component.ts` (neither renders the environment) |
| **D5** | **The Testing API host is wrong, and the documented SQL host is stale.** `ApiUrl`/`CancelUrl` for `UPC Testing` point at `10.10.9.181:8080`; the correct host is `10.10.10.181:8080`. `docs/database-schema.md` and `README.md` document the SQL Server as `10.10.8.181`; it is `10.10.10.181`. | `UpcEcommerceModule.cs:75-76`; `appsettings.json:20`; `docs/database-schema.md:30,265`; `README.md:133` |
| **D6** | **Branch code is a free-text input.** `<input type="text" placeholder="e.g. 101">` for a value that must match `Branches.BranchCode` exactly, on which all UPC item pricing depends. A typo produces a silent "No item found for code …" that reads as a missing item rather than a wrong branch. | `order-info.component.ts:16-19` |
| **D7** | **The only branch list in the application is derived from order history.** `ListBranchesAsync` runs `SELECT H.BranchCode, MAX(H.BranchName) … FROM dbo.RequestOrderHeaders GROUP BY H.BranchCode`. Branches that have never received an order do not appear, the name is a `MAX()` over whatever was denormalised into past orders, and it is scoped to the Order Requests filter bar — unusable as a branch picker for the builder. | `OrderRequestRepository.cs:343-353` |
| **D8** | **Client totals and server totals are two different implementations.** `flat-order.component.ts::recalculate()` reimplements the money math in TypeScript while `GET modules/{key}/calculate-totals` — backed by the authoritative `TotalsCalculator` that the validator itself uses — exists and is never called. Two rounding paths for one number, and the summary the operator approves is not the one the server validates. | `flat-order.component.ts:334-360`; `OrderController.cs:54-64`; `TotalsCalculator.cs` |

### 2.2 Interface — S2

| # | Defect | Evidence |
|---|---|---|
| **D9** | **Toasts are uncapped, un-deduplicated and cover the page.** `.toast-container` is fixed top-right at `z-index: 9999` with no maximum. `errorEnvelopeInterceptor` toasts *every* failed request, so D1's eight racing writes produce six identical stacked errors that obscure the content and the primary action button. | `toast.component.ts:20-30`; `error-envelope.interceptor.ts:17-27` |
| **D10** | **Two design systems in one screen.** R8 (`remediation_plan.md` B24) built the token system and `shared/ui/**`, and migrated Order Requests and the stat tiles. The entire order builder — every component under `features/flat-order/components/` — still uses `.glass-card` / `.glass-input` / `.glass-button` / `.glass-panel`, which `_gradients.css` documents as *"Legacy glass utility classes — superseded by shared/ui/gradient-card etc."* The palette is dark-first but the app renders light, so the glass surfaces have almost no contrast against the page. | `_gradients.css:70-130`; all of `features/flat-order/components/*.ts` |
| **D11** | **There is no layout — six equal-weight cards in one column.** The builder stacks `quick-stats`, `order-info`, `client-info`, `delivery-info`, `products-table`, `payments-table`, `api-config` full-width at `padding: 30px`, each with `grid-template-columns: repeat(auto-fit, minmax(220px, 1fr))` so a 1920px screen produces eight-column rows of unrelated fields. No summary rail, no sticky actions, no section navigation, no hierarchy, no indication of what is required or what is complete. | `flat-order.component.ts:57-108`; `order-info.component.ts:55`; `client-info.component.ts:85` |
| **D12** | **Collapsing the sidebar leaves a dead gutter.** `.sidebar.collapsed` narrows to `--sidebar-collapsed-width` (72px) but `.main-content` keeps a hardcoded `margin-left: var(--sidebar-width)` (280px), so the toggle shrinks the sidebar and gains nothing. | `sidebar.component.ts:70`; `module-shell.component.ts:33` |
| **D13** | **Fake loading state and an orphan input.** `ApiConfigComponent.onSend` clears its spinner on `setTimeout(…, 2500)` rather than the request lifecycle — a slow send shows "ready" while still in flight, and a fast one spins for 2.5s. Its `@Input() targetUrl` is never bound by the parent, so the "Target API URL" field is permanently blank and the real endpoint is invisible to the operator. | `api-config.component.ts:59-70`; `flat-order.component.ts:95-99` |
| **D14** | **Dead affordances.** The navbar "Docs" button calls `alert('Project Documentation available in docs/ folder')`. The order builder has no page header, and the breadcrumb carries only the module label. | `navbar.component.ts:75-77`; `breadcrumb.component.ts` |

---

## 3. Guiding decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **Repair in place; do not reopen the payload or validation layer.** | R0–R10 pinned those with key-for-key contract tests. This programme touches state, environments, one new repository, and presentation. |
| 2 | **Introspect `dbo.Branches` before writing any branch SQL.** Only `Id` and `BranchCode` are verified today. U0 introspects and records the real column list in `docs/database-schema.md` §1. | The repository's founding defect (`remediation_plan.md` B6) was invented column names presented as verified. Do not repeat it. |
| 3 | **The server owns the draft and the totals; the client owns intent.** The client never adopts a server echo mid-edit. Edits are debounced and batched into one atomic patch. The summary reads `GET calculate-totals`. | D1 and D8 are both the same mistake: two writers, one truth. |
| 4 | **Testing is the default lane at every layer.** `GetEnvironment(null)` resolves to the non-production environment explicitly, never `.First()`. The selection is persisted and always visible. Production sends and cancels require a typed confirmation. | D3 and D4 mean a refresh can put an operator on the live lane without knowing. |
| 5 | **One design system. The `.glass-*` bridge is deleted, not extended.** | R8 left it as a temporary bridge for un-migrated features; a year of "temporary" is what produced D10. U7 removes the last consumer and the classes together. |
| 6 | **Dark-first, with light as a maintained secondary theme.** | The palette was designed dark (`--bg-primary: #0B0F19`) and is being rendered light, which is why the surfaces read as washed out. |
| 7 | **Every agent-run verification hits UPC Testing only.** | `RmsMainTest2` / `http://10.10.10.181:8080`. No send, cancel or resend against Production, ever. |
| 8 | **Each session ships independently.** Green `dotnet build`, green `dotnet test`, a production `ng build` under budget, one clean commit. | The app is never left broken between sessions. |

### 3.1 Environment matrix (authoritative)

| | API host | Database | Connection string key |
|---|---|---|---|
| **UPC Testing** *(default)* | `http://10.10.10.181:8080/RmsMainServerApi/api/Order/…` | `RmsMainTest2` | `UpcEcommerceTest` |
| **UPC Production** | `http://10.10.10.181/RmsMainServerApi/api/Order/…` | `RmsMainProd` | `UpcEcommerceProd` |

`…/CreateAndAssignOrder` for sends, `…/CancelOrder` for cancels. Credentials
stay in .NET user-secrets / `CONNECTIONSTRINGS__*` environment variables — no
connection string is ever committed (`remediation_plan.md` B15 stands).

### 3.2 Scope notes

- The **draft-write race (D1)** and the **toast stack (D9)** were offered to the
  user as optional and not selected, but they are the literal subject of *"the
  workflow … has many bugs"* and are the errors visible in the reported
  screenshot. They are carried as core scope.
- The **Order Validation** page (`features/order-validation/`) is superseded by
  Order Requests per `README.md`, but the user chose to keep it. It is restyled
  in U7 and labelled as superseded in the sidebar; it is not removed.
- **GHC E-Commerce** and **GHC Uni-Commerce** remain capability-gated pending
  database credentials. Every change here is module-agnostic — no new
  module-key string comparisons (`remediation_plan.md` B21 stands).

---

## 4. Session plan

Ordered so ground truth is pinned before anything is built on it, the data
layer is correct before the interface is rebuilt on top of it, and the visual
rewrite happens in one continuous stretch rather than being split across
releases (splitting it is what produced D10).

| # | Goal | Key files | Gate |
|---|---|---|---|
| **U0** | **Ground truth: hosts and the `Branches` schema.** Correct every `10.10.9.181` / `10.10.8.181` literal to `10.10.10.181` in code, config and docs. Introspect `dbo.Branches` live against `RmsMainTest2` with a throwaway script in the scratchpad (never the repo) and add the verified column list to `docs/database-schema.md` §1. Add **failing** guard tests: no tracked source file contains a stale host, and `GetEnvironment(null)` returns the Testing environment. **No behaviour changes.** | `UpcEcommerceModule.cs`, `appsettings.json`, `docs/database-schema.md`, `README.md`, `tests/ContractTests.cs` | `git grep` for stale hosts is empty outside `docs/Prompts/`; the two new tests **fail** with a clear message; the real `Branches` column list is pasted into the report |
| **U1** | **Environment safety (D3, D4, D5).** `ModuleEnvironment` gains an explicit non-production default and `GetEnvironment(null)` returns it. `CancelOrderRequest` carries `environmentKey`; `CancelOrder` resolves it. Frontend: `activeEnvironment` defaults to Testing, is persisted per module in `localStorage` and restored on deep link; an environment switcher and a `TEST` / `PROD` badge live in the topbar; Production send **and** cancel are gated behind a typed-confirmation dialog built on `shared/ui/confirm-dialog`. | `ModuleEnvironment.cs`, `UpcEcommerceModule.cs`, `Ghc*Module.cs`, `OrderController.cs`, `ApiDtos.cs`, `module.service.ts`, `layout/navbar/`, new `shared/ui/env-badge/` | U0's two tests pass; a stubbed-client test proves cancel hits the **Testing** `CancelOrder` URL when Testing is selected; a hard refresh on `/modules/upc_ecommerce/order` still shows `TEST` |
| **U2** | **Draft state: end the write race (D1).** `DraftManager` gets a per-`(sessionId, moduleKey)` `SemaphoreSlim` and an atomic write (temp file → `File.Move(overwrite: true)`) with bounded `IOException` retry. `PUT order-field` is replaced by `PATCH modules/{key}/order-data` taking a field dictionary applied in a single load-modify-write; the old route stays as a thin adapter until U4. Frontend: a `draft.store.ts` that debounces edits ~300 ms, coalesces them into one patch, and **stops assigning the server echo into local state mid-edit**; the consumer prefill sends one patch, not eight. | `DraftManager.cs`, `OrderController.cs`, `flat-order.component.ts`, new `features/flat-order/draft.store.ts` | A consumer lookup fills first/middle/last/phone/email/birthdate/gender/address/address-code in one shot with **zero** error toasts; a concurrency test fires 20 parallel patches and asserts no field is lost |
| **U3** | **Branches: repository, endpoint, shared searchable select (D6, D7).** `IBranchRepository` / `BranchRepository` over `dbo.Branches` using **only** the columns U0 verified. `GET /api/modules/{key}/branches?envKey=` returns `{ code, name }` with a short per-environment in-memory cache. A new `shared/ui/searchable-select` (CDK overlay, full a11y, keyboard navigation, filters on name **and** code, virtualised for long lists). Wired into the builder's `branch_code`, the Add Product dialog, the Order Requests filter bar and the resend dialog; the `RequestOrderHeaders`-derived branch list is retired. | new `BranchRepository.cs`, `LookupController.cs`, new `shared/ui/searchable-select/**`, `order-info.component.ts`, `add-product-dialog.component.ts`, `order-requests/components/filter-bar.component.ts`, `resend-request-dialog.component.ts` | A live call against Testing returns the real branch list; typing a branch **name** and a branch **code** both filter correctly; selection works keyboard-only |
| **U4** | **Order builder workflow (D2, D8, D13).** The item lookup result populates the Add Product dialog — code, English and Arabic name, unit price, VAT rate, computed net — and a missing branch blocks the lookup with a pointer to the picker. The summary reads `GET calculate-totals`, replacing the client reimplementation. Real request-lifecycle loading state. `targetUrl` is bound to the active environment and read-only unless a "custom endpoint" toggle is on. `send-request` validation errors map to per-field inline errors instead of a toast dump. The U2 compatibility adapter is removed. | `flat-order.component.ts`, `add-product-dialog.component.ts`, `api-config.component.ts`, `quick-stats.component.ts` | Find Item on a real Testing item fills every field; the on-screen summary matches `calculate-totals` to the cent; sending an invalid draft highlights the offending fields rather than toasting a list |
| **U5** | **Design system: dark-first rebuild (D9, D10, D12).** Rewrite `_tokens.css` around a dark-first neutral surface ramp, one accent and semantic colours; gradients are retained for status pills and hero accents only, not as the default surface treatment. New primitives in `shared/ui/`: `ui-card`, `ui-section`, `ui-field`, `ui-input`, `ui-select`, `ui-button` (variants and sizes), `ui-table`, `ui-toolbar`. The toast is rebuilt: at most 3 visible, identical messages collapsed with a `×N` counter, auto-dismiss, positioned clear of the action rail. Sidebar collapse drives `--sidebar-width` on the shell so the gutter follows. `docs/design-system.md` and the kitchen sink are refreshed. `.glass-*` survives only until U7. | `styles/_tokens.css`, `styles/_gradients.css`, `shared/ui/**`, `shared/components/toast/`, `module-shell.component.ts`, `docs/design-system.md`, `kitchen-sink.component.ts` | `/_kitchen-sink` renders every primitive in both themes and all nine status pills; `prefers-reduced-motion` disables every animation; collapsing the sidebar leaves no gutter |
| **U6** | **Order builder layout rebuild (D11).** A two-column workspace. Left: collapsible sections — Order header · Customer · Products · Payments · Payload preview — each with a completion indicator, plus a sticky section nav. Right: a **sticky summary rail** carrying item count, subtotal, VAT, delivery, total, paid, balance, validation status, the environment badge and the Validate / Send actions. Products and payments become dense editable tables with inline edit and row totals. Below 1200px the rail collapses into a sticky bottom action bar. Skeletons on load, real empty states. | `features/flat-order/flat-order.component.ts` and every `features/flat-order/components/*`, new `order-summary-rail.component.ts` | A full build flow at 1280px and 1920px with no horizontal scroll and no overlap; the rail stays visible while scrolling; tab order runs top-to-bottom then into the rail |
| **U7** | **Migrate the rest of the app off `.glass-*`.** Restyle landing, navbar, sidebar, breadcrumb, unicommerce, order-requests with its drawer and filter bar, and order-validation (kept, labelled superseded) onto the U5 primitives. Delete `.glass-card` / `.glass-panel` / `.glass-input` / `.glass-button` from `_gradients.css`. Replace the navbar `alert()` with a real link (D14). | `layout/**`, `features/landing/**`, `features/unicommerce/**`, `features/order-requests/**`, `features/order-validation/**`, `styles/_gradients.css` | `git grep "glass-" frontend/src` returns **nothing**; every route renders correctly in both themes |
| **U8** | **End-to-end verification on Testing, docs, cleanup.** The full flow against **UPC Testing only**: pick a branch from the picker → look up an item → add it → look up a consumer → add a payment → validate → send → confirm the row lands in Order Requests with a matching `RequestJson` and a populated `ResponseJson` → open the drawer → cancel. Refresh `README.md`, `docs/api-spec.md`, `docs/design-system.md`, `docs/database-schema.md`. Confirm `var/` remains untracked. | docs, `scripts/build.ps1` | `.\scripts\build.ps1` fully green; the production bundle is under budget with zero warnings; the E2E transcript is pasted with the real order number |

**Dependencies:** `U0 → U1` · `U0 → U3` · `U2 → U4` · `U3 → U4` ·
`U5 → U6 → U7` · `U8` needs everything.

The frontend chain (`U5 → U6 → U7`) is independent of the data chain
(`U0 → U1`, `U2`, `U3 → U4`) up to U8, but running them in the listed order
keeps each session's manual verification honest — you cannot confidently
verify a rebuilt builder layout while the lookups it renders are still broken.

---

## 5. Risks

1. **`dbo.Branches` may have no usable name column.** U0 is a hard gate. If
   introspection shows no name, the picker degrades to code-only and this plan
   is amended to say so — `BranchName` is **not** to be assumed because
   `RequestOrderHeaders` happens to have a column by that name.
2. **U2 changes how drafts are persisted.** Existing `var/drafts/**` files stay
   readable (the DTO is unchanged), but the write path and the field-patch
   route change together. The compatibility adapter is what makes U2
   independently shippable; U4 removes it.
3. **U5–U7 are a visual rewrite — every screen changes at once.** Splitting them
   further is precisely what left the app with two design systems. Do not
   split them.
4. **Live database access to `10.10.10.181` is required** for U0, U3, U4 and U8.
   If it is unreachable, the session must say so explicitly and stop, not
   report success from a green build.
5. **The `10.10.10.181` SQL host is the user's statement, not yet verified.**
   `docs/database-schema.md` was written and confirmed against `10.10.8.181`.
   U0 must prove the connection succeeds before rewriting that document; if it
   does not, stop and report rather than committing a wrong host.
6. **The corrected environment default will change where orders go.** Anyone
   used to the tool silently defaulting to Production will now land on Testing.
   That is the fix working, not a regression.
7. **Removing `.glass-*` in U7 breaks any screen not yet migrated.** The
   `git grep` gate in U7 is what catches it — run it before committing, not
   after.
