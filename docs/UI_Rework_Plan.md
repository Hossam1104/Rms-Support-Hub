# UI Rework & Workflow Remediation Plan — Active U4-U8

> Companion to [`UI_Rework_Prompts.md`](UI_Rework_Prompts.md) — active
> sessions `U4` → `U8`.
>
> The .NET rewrite, remediation R0-R10, and UI sessions U0-U3 are complete
> locally. U3's safe UPC Testing read returned HTTP 500, so its live/browser
> gate remains pending.
> Their outcomes and commit evidence are indexed in
> [`.ai/HISTORY.md`](../.ai/HISTORY.md); audit plans are under `.ai/archive/`.

---

## 1. Verdict

The remediation left a **correct engine inside an unusable cockpit**.

What is sound and must not be re-derived: the payload builders and their
key-for-key contract tests, the verified SQL in
[`database-schema.md`](database-schema.md), `OrderRequestRepository`
and the Order Requests page, the `Capabilities` abstraction, the
`Api`/`Core`/`Data`/`Tests` layout, per-session drafts, and the uniform error
envelope.

The remaining operator-facing defects are:

1. **Item lookup discards its own result** (D2) — the operator retypes the
   price, VAT and name by hand.
2. **Client totals, loading, validation, and endpoint controls remain split**
   between the builder and the server (D8, D13).
3. **Two design systems coexist.** R8 built `shared/ui/**` on new tokens and
   migrated Order Requests; the entire order builder still renders through the
   `.glass-*` classes that `_gradients.css` itself labels *"legacy … superseded"*
   (D10), stacked in one undifferentiated column (D11).

Three facts frame the active continuation:

1. **`dbo.Branches` is verified and U3 now consumes it.** U0 confirmed
   `BranchCode`, `Name`, `NativeName`, `IsActive`, and `IsDeleted`; all future
   changes must use only the columns recorded in
   [`database-schema.md`](database-schema.md).
2. **Testing is the default lane.** U1 made it explicit, persisted, visible,
   and threaded through operational calls. Production actions remain gated.
3. **Draft writes are serialized and batched.** U2 removed the lost-update
   race. Its temporary `PUT order-field` adapter remains until U4 removes it.

**Recommendation: repair in place.** The backend contract work and U3 branch
workflow are correct locally and tested. This continuation changes builder
state management, environment resolution, and the frontend presentation layer.
It does not reopen `FlatOrderPayloadBuilder`, `FlatOrderValidator`, or any
existing verified query.

---

## 2. Defect register

Severity: **S1** = the tool loses data or the feature does not function ·
**S2** = the operator cannot reasonably work · **S3** = quality, drift, hygiene.

### 2.1 Workflow and data loss — S1

| # | Defect | Evidence |
|---|---|---|
| **D2** | **The item lookup result is thrown away.** `onLookupItem` toasts `Found item: {name}` and drops `res.data` on the floor. `AddProductDialogComponent` emits the lookup but has no input to receive a result, so `product.itemCode/itemName/unitPrice/vatPercentage` are never populated. Branch-specific pricing — the entire reason UPC lookups exist — never reaches the payload unless retyped by hand. | `flat-order.component.ts:248-263` vs `add-product-dialog.component.ts:76-97` |
| **D8** | **Client totals and server totals are two different implementations.** `flat-order.component.ts::recalculate()` reimplements the money math in TypeScript while `GET modules/{key}/calculate-totals` — backed by the authoritative `TotalsCalculator` that the validator itself uses — exists and is never called. Two rounding paths for one number, and the summary the operator approves is not the one the server validates. | `flat-order.component.ts:334-360`; `OrderController.cs:54-64`; `TotalsCalculator.cs` |

### 2.2 Interface — S2

| # | Defect | Evidence |
|---|---|---|
| **D9** | **Toasts are uncapped, un-deduplicated and cover the page.** `.toast-container` is fixed top-right at `z-index: 9999` with no maximum. `errorEnvelopeInterceptor` toasts *every* failed request, so D1's eight racing writes produce six identical stacked errors that obscure the content and the primary action button. | `toast.component.ts:20-30`; `error-envelope.interceptor.ts:17-27` |
| **D10** | **Two design systems in one screen.** Completed remediation session R8 built the token system and `shared/ui/**`, and migrated Order Requests and the stat tiles. The entire order builder — every component under `features/flat-order/components/` — still uses `.glass-card` / `.glass-input` / `.glass-button` / `.glass-panel`, which `_gradients.css` documents as *"Legacy glass utility classes — superseded by shared/ui/gradient-card etc."* The palette is dark-first but the app renders light, so the glass surfaces have almost no contrast against the page. | `_gradients.css:70-130`; all of `features/flat-order/components/*.ts` |
| **D11** | **There is no layout — six equal-weight cards in one column.** The builder stacks `quick-stats`, `order-info`, `client-info`, `delivery-info`, `products-table`, `payments-table`, `api-config` full-width at `padding: 30px`, each with `grid-template-columns: repeat(auto-fit, minmax(220px, 1fr))` so a 1920px screen produces eight-column rows of unrelated fields. No summary rail, no sticky actions, no section navigation, no hierarchy, no indication of what is required or what is complete. | `flat-order.component.ts:57-108`; `order-info.component.ts:55`; `client-info.component.ts:85` |
| **D12** | **Collapsing the sidebar leaves a dead gutter.** `.sidebar.collapsed` narrows to `--sidebar-collapsed-width` (72px) but `.main-content` keeps a hardcoded `margin-left: var(--sidebar-width)` (280px), so the toggle shrinks the sidebar and gains nothing. | `sidebar.component.ts:70`; `module-shell.component.ts:33` |
| **D13** | **Fake loading state and an orphan input.** `ApiConfigComponent.onSend` clears its spinner on `setTimeout(…, 2500)` rather than the request lifecycle — a slow send shows "ready" while still in flight, and a fast one spins for 2.5s. Its `@Input() targetUrl` is never bound by the parent, so the "Target API URL" field is permanently blank and the real endpoint is invisible to the operator. | `api-config.component.ts:59-70`; `flat-order.component.ts:95-99` |
| **D14** | **Dead affordances.** The navbar "Docs" button calls `alert('Project Documentation available in docs/ folder')`. The order builder has no page header, and the breadcrumb carries only the module label. | `navbar.component.ts:75-77`; `breadcrumb.component.ts` |

---

## 3. Guiding decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | **Repair in place; do not reopen the payload or validation layer.** | R0–R10 pinned those with key-for-key contract tests. This programme touches state, environments, one new repository, and presentation. |
| 2 | **Use only the U0-verified `dbo.Branches` columns.** The exact live column list and provenance are recorded in `docs/database-schema.md` §1. | An earlier rewrite invented SQL columns and presented them as verified. Do not repeat it. |
| 3 | **The server owns the draft and the totals; the client owns intent.** The client never adopts a server echo mid-edit. Edits are debounced and batched into one atomic patch. The summary reads `GET calculate-totals`. | D1 and D8 are both the same mistake: two writers, one truth. |
| 4 | **Testing is the default lane at every layer.** `GetEnvironment(null)` resolves to the non-production environment explicitly, never `.First()`. The selection is persisted and always visible. Production sends and cancels require a typed confirmation. | D3 and D4 mean a refresh can put an operator on the live lane without knowing. |
| 5 | **One design system. The `.glass-*` bridge is deleted, not extended.** | R8 left it as a temporary bridge for un-migrated features; a year of "temporary" is what produced D10. U7 removes the last consumer and the classes together. |
| 6 | **Dark-first, with light as a maintained secondary theme.** | The palette was designed dark (`--bg-primary: #0B0F19`) and is being rendered light, which is why the surfaces read as washed out. |
| 7 | **Every agent-run verification hits UPC Testing only.** | `RmsMainTest2` / `http://10.10.10.181:8080`. No send, cancel or resend against Production, ever. |
| 8 | **Each session remains independently releasable.** Green targeted tests and the appropriate production build are required; commit only when the user explicitly requests it. | The app is never left broken between sessions. |

### 3.1 Environment matrix (authoritative)

| | API host | Database | Connection string key |
|---|---|---|---|
| **UPC Testing** *(default)* | `http://10.10.10.181:8080/RmsMainServerApi/api/Order/…` | `RmsMainTest2` | `UpcEcommerceTest` |
| **UPC Production** | `http://10.10.10.181/RmsMainServerApi/api/Order/…` | `RmsMainProd` | `UpcEcommerceProd` |

`…/CreateAndAssignOrder` for sends, `…/CancelOrder` for cancels. Credentials
stay in .NET user-secrets / `CONNECTIONSTRINGS__*` environment variables — no
connection string is ever committed (see the completed remediation history).

### 3.2 Scope notes

- The draft-write race is complete. Toast capping and deduplication remain in
  U5 because repeated failures can still obscure primary actions.
- The **Order Validation** page (`features/order-validation/`) is superseded by
  Order Requests per `README.md`, but the user chose to keep it. It is restyled
  in U7 and labelled as superseded in the sidebar; it is not removed.
- **GHC E-Commerce** and **GHC Uni-Commerce** remain capability-gated pending
  database credentials. Every change here is module-agnostic — no new
  module-key string comparisons.

---

## 4. Session plan

Ordered so ground truth is pinned before anything is built on it, the data
layer is correct before the interface is rebuilt on top of it, and the visual
rewrite happens in one continuous stretch rather than being split across
releases (splitting it is what produced D10).

| # | Goal | Key files | Gate |
|---|---|---|---|
| **U4** | **Order builder workflow (D2, D8, D13).** The item lookup result populates the Add Product dialog — code, English and Arabic name, unit price, VAT rate, computed net — and a missing branch blocks the lookup with a pointer to the picker. The summary reads `GET calculate-totals`, replacing the client reimplementation. Real request-lifecycle loading state. `targetUrl` is bound to the active environment and read-only unless a "custom endpoint" toggle is on. `send-request` validation errors map to per-field inline errors instead of a toast dump. The U2 compatibility adapter is removed. | `flat-order.component.ts`, `add-product-dialog.component.ts`, `api-config.component.ts`, `quick-stats.component.ts` | Find Item on a real Testing item fills every field; the on-screen summary matches `calculate-totals` to the cent; sending an invalid draft highlights the offending fields rather than toasting a list |
| **U5** | **Design system: dark-first rebuild (D9, D10, D12).** Rewrite `_tokens.css` around a dark-first neutral surface ramp, one accent and semantic colours; gradients are retained for status pills and hero accents only, not as the default surface treatment. New primitives in `shared/ui/`: `ui-card`, `ui-section`, `ui-field`, `ui-input`, `ui-select`, `ui-button` (variants and sizes), `ui-table`, `ui-toolbar`. The toast is rebuilt: at most 3 visible, identical messages collapsed with a `×N` counter, auto-dismiss, positioned clear of the action rail. Sidebar collapse drives `--sidebar-width` on the shell so the gutter follows. `docs/design-system.md` and the kitchen sink are refreshed. `.glass-*` survives only until U7. | `styles/_tokens.css`, `styles/_gradients.css`, `shared/ui/**`, `shared/components/toast/`, `module-shell.component.ts`, `docs/design-system.md`, `kitchen-sink.component.ts` | `/_kitchen-sink` renders every primitive in both themes and all nine status pills; `prefers-reduced-motion` disables every animation; collapsing the sidebar leaves no gutter |
| **U6** | **Order builder layout rebuild (D11).** A two-column workspace. Left: collapsible sections — Order header · Customer · Products · Payments · Payload preview — each with a completion indicator, plus a sticky section nav. Right: a **sticky summary rail** carrying item count, subtotal, VAT, delivery, total, paid, balance, validation status, the environment badge and the Validate / Send actions. Products and payments become dense editable tables with inline edit and row totals. Below 1200px the rail collapses into a sticky bottom action bar. Skeletons on load, real empty states. | `features/flat-order/flat-order.component.ts` and every `features/flat-order/components/*`, new `order-summary-rail.component.ts` | A full build flow at 1280px and 1920px with no horizontal scroll and no overlap; the rail stays visible while scrolling; tab order runs top-to-bottom then into the rail |
| **U7** | **Migrate the rest of the app off `.glass-*`.** Restyle landing, navbar, sidebar, breadcrumb, unicommerce, order-requests with its drawer and filter bar, and order-validation (kept, labelled superseded) onto the U5 primitives. Delete `.glass-card` / `.glass-panel` / `.glass-input` / `.glass-button` from `_gradients.css`. Replace the navbar `alert()` with a real link (D14). | `layout/**`, `features/landing/**`, `features/unicommerce/**`, `features/order-requests/**`, `features/order-validation/**`, `styles/_gradients.css` | `git grep "glass-" frontend/src` returns **nothing**; every route renders correctly in both themes |
| **U8** | **End-to-end verification on Testing, docs, cleanup.** The full flow against **UPC Testing only**: pick a branch from the picker → look up an item → add it → look up a consumer → add a payment → validate → send → confirm the row lands in Order Requests with a matching `RequestJson` and a populated `ResponseJson` → open the drawer → cancel. Refresh `README.md`, `docs/api-spec.md`, `docs/design-system.md`, `docs/database-schema.md`. Confirm `var/` remains untracked. | docs, `scripts/build.ps1` | `.\scripts\build.ps1` fully green; the production bundle is under budget with zero warnings; the E2E transcript is pasted with the real order number |

**Dependencies:** U0-U3 are complete locally, with U3 live/browser evidence
pending · `U4` is next · `U5 → U6 → U7` · `U8` needs U3-U7.

Run U4-U8 in order so each session's verification exercises the completed data
flow beneath the presentation work. U3's unresolved live gate is tracked
separately and must not be reported as passed by a green build.

---

## 5. Risks

1. **U4 removes U2's compatibility adapter.** Existing draft JSON remains
   readable because the DTO did not change.
2. **U5–U7 are a visual rewrite — every screen changes at once.** Splitting them
   further is precisely what left the app with two design systems. Do not
   split them.
3. **Live database access to the configured UPC Testing SQL connection is
   required** for U3, U4 and U8. If it is unreachable, report the unavailable
   live gate separately rather than claiming it passed from a green build.
4. **Removing `.glass-*` in U7 breaks any screen not yet migrated.** The
   `git grep` gate in U7 is what catches it — run it before declaring the
   session complete.
