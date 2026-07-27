# UI Rework Execution Prompts — 9 Sessions (U0 → U8)

Companion to [`UI_Rework_Plan.md`](UI_Rework_Plan.md). One prompt per session,
executed in order, **one conversation each**. Copy the whole section — rules
block included — into a fresh session.

---

## Rules that apply to every session

Repeat these verbatim if the agent drifts.

- **Read [`UI_Rework_Plan.md`](UI_Rework_Plan.md) in full before touching
  anything**, especially §2 (defect register D1–D14) and §3 (guiding
  decisions). Read [`docs/Prompts/remediation_plan.md`](docs/Prompts/remediation_plan.md)
  §3 for the constraints that still bind (no invented columns or keys, no
  committed credentials, no module-key string comparisons).
- **Never invent a SQL column name or a JSON key.** The two sources of truth
  are [`docs/database-schema.md`](docs/database-schema.md) (SQL) and
  `docs/request_examples/**` (payload). If something is in neither, introspect
  it live and write it down, or stop and ask. Do not guess a column because a
  similarly-named one exists on another table.
- **Every live call goes to UPC Testing.** `RmsMainTest2`,
  `http://10.10.10.181:8080/RmsMainServerApi/…`. **Never** send, cancel or
  resend against Production. If you need to prove Production wiring, prove it
  with a stubbed `IApiClient` in a test, not a live call.
- **No credential in any tracked file.** Connection strings come from
  `dotnet user-secrets` (development) or `CONNECTIONSTRINGS__*` environment
  variables. Throwaway introspection scripts go in the scratchpad directory,
  never in the repo.
- **Work only inside the files listed for the session.** If the work genuinely
  requires a file that is not listed, say so and explain why before editing it.
- **Run the Verify block and paste its real output before declaring done.**
  A green build is not verification. If the database is unreachable, say so
  explicitly — do not report success.
- **End every session with:** `dotnet build` clean, `dotnet test` green,
  `ng build --configuration production` under budget with zero warnings, and
  **one** clean commit using the message given.
- Shell is **PowerShell on Windows**, repo root `d:\AI Tools\DBS\online_order_tool`.
  `.\scripts\dev.ps1` runs the API (`http://localhost:5200`) and `ng serve`
  (`http://localhost:4200`) together. `.\scripts\build.ps1` runs the full gate.

---

## Session U0 — Ground truth: hosts and the `Branches` schema

The order tool's Testing lane points at the wrong host, the documented SQL
server is stale, and `dbo.Branches` — the table the next three sessions build
on — has never been introspected. This session establishes ground truth and
adds the guard tests that must **fail** so the following sessions have a real
gate. It changes **no behaviour**.

Read `UI_Rework_Plan.md` §2.1 (D5), §3.1 (the environment matrix) and §3
decision 2. This is session 1 of 9.

1. Correct the host literals. In
   `backend/src/OnlineOrderTool.Core/Modules/UpcEcommerceModule.cs`, the
   `UPC Testing` environment's `ApiUrl` and `CancelUrl` must be
   `http://10.10.10.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder`
   and `…/CancelOrder`. `UPC Production` already reads `http://10.10.10.181/…`
   — confirm it, do not change it. Update
   `backend/src/OnlineOrderTool.Api/appsettings.json` `ModuleEndpoints:UpcTesting`
   to match.
2. Introspect `dbo.Branches` live. Write a throwaway script **in the scratchpad
   directory, not the repo**, that connects to `10.10.10.181` / `RmsMainTest2`
   using the `UpcEcommerceTest` connection string from user-secrets, and runs
   `SELECT TOP 1 * FROM dbo.Branches`. Print the full column list and one
   sample row with values redacted where they look sensitive. **This is the
   gate for U3** — report the exact column names, especially whichever column
   holds the human-readable branch name, and whether the table has an
   active/inactive flag worth filtering on.
3. Record the result. Add a `dbo.Branches` row to the verified-schema table in
   `docs/database-schema.md` §1, in the same format as the existing rows, with
   the same "confirmed live against …" provenance note. Correct the two stale
   `10.10.8.181` references in that file (§1 header and §5) to `10.10.10.181`,
   and the one in `README.md:133`. **Leave `docs/Prompts/**` untouched** — those
   are a historical record of what was true at the time.
4. Add guard tests to `backend/tests/OnlineOrderTool.Tests/ContractTests.cs`:
   - A test that reads the tracked source of `backend/src/**` and
     `backend/tests/**` and asserts **no** file contains `10.10.9.181` or
     `10.10.8.181`. Failure message must name the offending files.
   - A test asserting `new UpcEcommerceModule(…).GetEnvironment(null).Environment == "Testing"`.
     This is **expected to fail now** — U1 makes it pass. Do not add `Skip`.
5. Do not change `GetEnvironment`, any controller, or any frontend file. Step 4's
   second test failing is the correct end state for this session.

**Verify** (paste real output):
```powershell
dotnet build backend/OnlineOrderTool.slnx --nologo
dotnet test  backend/OnlineOrderTool.slnx --nologo
# expect: the stale-host test PASSES, the Testing-default test FAILS

git grep -n "10\.10\.9\.181" -- backend frontend docs/database-schema.md README.md   # expect empty
git grep -n "10\.10\.8\.181" -- backend frontend docs/database-schema.md README.md   # expect empty
```
Then paste the full `dbo.Branches` column list from step 2. **U3 cannot start
without it.** If `10.10.10.181` / `RmsMainTest2` is unreachable, stop and say
so — do not rewrite `docs/database-schema.md` on an unverified host.

**Commit:** `chore(u0): correct RMS host literals, introspect dbo.Branches, add environment guard tests`

---

## Session U1 — Environment safety: Testing by default, Production on purpose

Today the tool silently defaults to Production. `ModuleService.activeEnvironment`
falls back to `environments[0]` — `UPC Production` for UPC — it is never
persisted, so a refresh or a deep link resets to it, there is no environment
indicator anywhere inside the module shell, and `OrderController.CancelOrder`
ignores the operator's selection entirely and always resolves Production.

Read `UI_Rework_Plan.md` D3, D4 and §3 decision 4. Session 2 of 9. U0's
Testing-default test must go green in this session.

1. Make the default explicit, not positional. Add an `IsDefault` (or
   equivalent) flag to `OnlineOrderTool.Core/Models/ModuleEnvironment.cs`, set
   it on `UPC Testing` and on the Testing environment of each other module, and
   rewrite `GetEnvironment(string? envKey)` in every `*Module.cs` to fall back
   to the flagged default rather than `Environments.Values.First(e => e.Available)`.
   If no environment is flagged, prefer one whose `Environment != "Production"`
   before falling back to first-available.
2. Fix cancel (D3). Add `EnvironmentKey` to `CancelOrderRequest` in
   `OnlineOrderTool.Core/DTOs/ApiDtos.cs` and have
   `OrderController.CancelOrder` resolve `module.GetEnvironment(request.EnvironmentKey)`.
   Return a clear 400 when the resolved environment has no `CancelUrl`.
3. Audit every other environment resolution in the API for the same bug —
   `LookupController`, `OrderRequestsController`, `OrderController.SendRequest`
   and `ExportJson` — and make sure each threads the caller's `envKey` through
   rather than defaulting silently. List what you found in your report.
4. Frontend default and persistence. In
   `frontend/src/app/core/services/module.service.ts`: pick the module's
   default environment (the flag from step 1, exposed on `EnvironmentDto`)
   instead of `environments[0]`; persist the operator's choice per module in
   `localStorage` under a namespaced key; restore it in `loadModuleDetails`,
   validating that the stored key still exists on the module.
5. Make the lane visible and switchable. Add a `shared/ui/env-badge` component
   rendering `TEST` (calm/neutral) or `PROD` (danger) and mount it in
   `layout/navbar/navbar.component.ts` alongside a switcher listing the active
   module's environments. Switching environments must clear any environment-scoped
   cached state and re-fetch the draft.
6. Gate Production. Sends and cancels while `PROD` is active must open a
   confirmation built on the existing `shared/ui/confirm-dialog` that requires
   the operator to type the environment name to proceed. Reuse the existing
   component — do not write a second dialog.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo   # U0's Testing-default test now PASSES
```
Add and run a test with a stubbed `IApiClient` asserting that a cancel with
`environmentKey = "UPC Testing"` posts to
`http://10.10.10.181:8080/RmsMainServerApi/api/Order/CancelOrder` — not the
Production URL, and not the CreateAndAssignOrder URL.

Then, in the browser: open `http://localhost:4200/modules/upc_ecommerce/order`
**directly** (not via the landing page), confirm the badge reads `TEST`, hard-refresh,
confirm it still reads `TEST`. Switch to Production, refresh, confirm it reads
`PROD` and that pressing Send opens the typed confirmation. **Cancel out of it —
do not send.** Paste a screenshot or describe each step's result.

**Commit:** `feat(u1): default to the Testing lane, persist and surface the environment, gate Production`

---

## Session U2 — Draft state: end the write race

This is the bug in the user's screenshot. A consumer lookup reports
`Found consumer: Mohamed Elbanna` while the Last Name, Phone and Address
inputs sit empty behind six stacked
`The process cannot access the file … because it is being used by another process`
errors.

The cause is a lost-update race in three parts:
`flat-order.component.ts::onFieldChange` fires one `PUT modules/{key}/order-field`
per field with no debounce; `OrderController.UpdateOrderField` does an
unsynchronised load-modify-write against a single JSON file; and the client
then assigns each response back into its own state (`this.draft.set(res.state)`).
`onLookupConsumer` fires **eight** of these back to back, so late responses
built from stale reads overwrite fields written by earlier ones, while the
concurrent writes collide on the file handle.

Read `UI_Rework_Plan.md` D1 and §3 decision 3. Session 3 of 9.

1. Serialise and make the write atomic. In
   `backend/src/OnlineOrderTool.Core/Services/DraftManager.cs`: hold a
   `SemaphoreSlim` per `(sessionId, moduleKey)` in a `ConcurrentDictionary` and
   take it around the whole load-modify-write, not just the write. Write to
   `<file>.tmp` then `File.Move(tmp, path, overwrite: true)` so a reader never
   observes a partial file. Retry a small bounded number of times on
   `IOException` with a short backoff, then surface a real error rather than
   swallowing it. Keep `LoadDraftAsync`'s existing tolerant behaviour.
2. Batch the write path. Add `PATCH modules/{key}/order-data` to
   `OrderController` taking `{ fields: Dictionary<string, object?> }` and
   applying every field inside **one** load-modify-write. Keep
   `PUT order-field` as a thin adapter that forwards a single-entry dictionary,
   so nothing breaks mid-programme — U4 deletes it.
3. Stop the client clobbering itself. Add
   `frontend/src/app/features/flat-order/draft.store.ts`: a signal-backed store
   owning the draft, exposing `patch(fields)` which updates local state
   immediately, debounces ~300 ms, coalesces all pending fields into one
   `PATCH`, and **does not** assign the response back into local state. The
   server echo is authoritative only on an explicit full reload
   (`GET state`, `load-default`, `clear-all`). Handle overlapping in-flight
   patches so a later one always wins for the fields it carries.
4. Rewire `flat-order.component.ts` onto the store. `onLookupConsumer` must
   send **one** `patch({...})` carrying every prefilled field — first, middle,
   last name, phone, email, birthdate, gender, address, address code — not
   eight sequential calls. Report in the toast which fields were actually
   prefilled and which came back empty from the lookup, so an empty Last Name
   is visibly the data's fault rather than the tool's.
5. Add tests: a `DraftManagerTests` case firing 20 concurrent patches of
   distinct fields at one `(sessionId, moduleKey)` and asserting all 20 are
   present afterwards; and a `ControllerIntegrationTests` case asserting
   `PATCH order-data` applies a multi-field body in one call.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\dev.ps1
```
Then, at `http://localhost:4200/modules/upc_ecommerce/order` with the badge
reading `TEST`: look up consumer `0556028080`. **Every** field the lookup
returns must be populated, and there must be **zero** error toasts. Paste a
screenshot. Then check the draft file under
`backend/src/OnlineOrderTool.Api/var/drafts/<session>/upc_ecommerce.json` and
confirm it holds the same values the form shows.

**Commit:** `fix(u2): serialise and batch draft writes, stop the client clobbering its own state`

---

## Session U3 — Branches: real table, real endpoint, searchable picker

Branch code is a free-text input for a value that must match
`Branches.BranchCode` exactly and on which all UPC item pricing depends — a
typo reads as a missing item. The only branch list in the application derives
from order history (`GROUP BY H.BranchCode` over `RequestOrderHeaders`), so
branches with no orders do not exist and the name is a `MAX()` over whatever
was denormalised into past orders.

Read `UI_Rework_Plan.md` D6, D7 and §3 decision 2. **This session depends on
U0's `dbo.Branches` introspection — use those exact column names and nothing
else.** Session 4 of 9.

1. Add `backend/src/OnlineOrderTool.Data/Repositories/BranchRepository.cs` with
   an `IBranchRepository` in `Core/Repositories`. One method,
   `ListBranchesAsync(connectionString)`, selecting the branch code and name
   columns U0 verified from `dbo.Branches`, filtered by the active flag **only
   if U0 confirmed one exists**, ordered by name. Return a
   `BranchOptionDto(string Code, string Name)`. Parameterised throughout; no
   string interpolation of user values.
2. Expose `GET /api/modules/{key}/branches?envKey=` — extend `LookupController`
   rather than adding a controller, so environment resolution and the
   `Connect Timeout` handling in its existing `GetConnectionString` helper are
   reused. Gate it on a capability, not a module-key comparison. Cache the
   result in memory per connection-string key with a short TTL (5 minutes) and
   an explicit way to bypass; a branch list does not change during a session.
3. Register the repository in `Program.cs` alongside the existing singletons.
4. Build `frontend/src/app/shared/ui/searchable-select/`: a standalone
   component over Angular CDK `Overlay` (already a dependency — see the drawer
   and confirm-dialog components for the established pattern). Requirements:
   filters on **both** label and code as the operator types; full keyboard
   support (arrow keys, Enter, Escape, type-ahead, focus return on close);
   `role="combobox"` with correct `aria-*` wiring; virtual scroll via
   `@angular/cdk/scrolling` for long lists; loading, empty and error states;
   works in both themes. Export it from `shared/ui/index.ts` and add it to the
   kitchen sink.
5. Wire it in, replacing free-text and derived-list branch inputs:
   - `features/flat-order/components/order-info.component.ts` — `branch_code`,
     displaying `Name (Code)` and emitting the code. Route the change through
     U2's draft store.
   - `features/flat-order/components/add-product-dialog.component.ts` — show
     the resolved branch name in the "set a branch first" hint.
   - `features/order-requests/components/filter-bar.component.ts` and
     `features/order-requests/components/resend-request-dialog.component.ts` —
     switch both to the new endpoint.
6. Retire `ListBranchesAsync` from `OrderRequestRepository` and its
   `OrderRequestsController` route once nothing consumes them. Remove the now-dead
   `BranchSummaryDto` if it has no other caller.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\dev.ps1
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/branches?envKey=UPC%20Testing"
```
Paste the real branch list. Then in the browser: open the branch picker, filter
by a branch **name**, clear it, filter by a branch **code**, and select one
using only the keyboard. Confirm the selected code lands in the draft file.
Report the branch count returned.

**Commit:** `feat(u3): branch repository over dbo.Branches with a shared searchable picker`

---

## Session U4 — Order builder workflow correctness

Three defects that make the builder untrustworthy. `Find Item` calls the API,
toasts the item name and **discards the result** — the Add Product dialog has
no way to receive it, so the operator retypes the branch-specific price, VAT
and name by hand, which is the entire point of the lookup. The on-screen
summary is a TypeScript reimplementation of the money math while the
authoritative `GET calculate-totals` exists and is never called. And the send
button's spinner is a `setTimeout(…, 2500)`.

Read `UI_Rework_Plan.md` D2, D8, D13. Depends on U2 (draft store) and U3
(branch picker). Session 5 of 9.

1. Make the item lookup fill the form (D2). Move the lookup call into
   `add-product-dialog.component.ts` — or pass the result back into it via an
   input signal — so a successful lookup populates `itemCode`, `itemName`
   (English and Arabic where the repository returns both), `unitPrice`,
   `vatPercentage` and the computed net price. Show which fields were filled
   from the database and keep them editable. A lookup with no branch selected
   must block with a message pointing at the branch picker, not fail silently.
   A "not found" 200 must be visibly distinct from a database error.
2. Adopt the server totals (D8). Delete `recalculate()` from
   `flat-order.component.ts` and read `GET modules/{key}/calculate-totals`
   after every draft mutation, debounced through the U2 store so it is not
   re-fetched per keystroke. Type the response against the real `TotalsSummary`
   shape — `totalProductAmount`, `totalProductVat`, `orderDiscount`,
   `totalPaidAmount` and the rest as the backend actually returns them; check
   `TotalsCalculator.cs` rather than assuming. Extend
   `quick-stats.component.ts` to show the full breakdown.
3. Fix the send affordance (D13). Drive the loading state from the request
   lifecycle — `finalize()` on the send observable — and remove the
   `setTimeout`. Bind `targetUrl` from the parent to the active environment's
   `ApiUrl`, rendered read-only, with an explicit "use a custom endpoint"
   toggle that unlocks it. The operator must always be able to see where the
   order is going.
4. Surface validation properly. `POST send-request` returns
   `{ success: false, errors: [...] }` on a validation failure. Map those to
   per-field inline errors next to the offending inputs and scroll the first
   one into view, instead of dumping a list into a toast. Keep a summary count
   in the summary rail's validation status.
5. Delete the `PUT order-field` compatibility adapter added in U2 and its
   frontend callers.
6. Do not touch `FlatOrderPayloadBuilder`, `FlatOrderValidator` or
   `TotalsCalculator` — this session consumes them, it does not change them.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\dev.ps1
```
Browser, badge reading `TEST`: select a real branch from the picker, open Add
Product, look up a real item code from that branch, and confirm every field
populates with the branch-specific price. Add it. Compare the on-screen
summary against:
```powershell
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/calculate-totals"
```
They must match to the cent. Then clear a required field and press Send —
confirm the error appears **on the field**, not only in a toast. Paste both
outputs.

**Commit:** `fix(u4): populate the item lookup result, adopt server totals, real send lifecycle`

---

## Session U5 — Design system: dark-first rebuild

R8 built the token system and `shared/ui/**` but migrated only Order Requests
and the stat tiles. The entire order builder still renders through the
`.glass-card` / `.glass-input` / `.glass-button` / `.glass-panel` classes that
`_gradients.css` itself documents as *"Legacy … superseded"*. The palette was
designed dark (`--bg-primary: #0B0F19`) and is being viewed light, so those
glass surfaces have almost no contrast. On top of that, the toast container is
uncapped and un-deduplicated at `z-index: 9999`, and collapsing the sidebar
leaves a 208px dead gutter.

Read `UI_Rework_Plan.md` D9, D10, D12 and §3 decisions 5 and 6. This is the
first of three consecutive visual sessions — **do not split it further.**
Session 6 of 9.

1. Rewrite `frontend/src/styles/_tokens.css` dark-first: a neutral surface ramp
   with real elevation separation (page → panel → raised → overlay), a
   readable text ramp against each, one accent plus semantic success / warning
   / danger / info, and border and focus-ring tokens. Keep the existing
   `[data-theme="light"]` override block complete and genuinely usable — light
   is secondary, not abandoned. Preserve the radius, shadow and motion scales
   and the `prefers-reduced-motion` block. Keep the back-compat aliases only
   for tokens still referenced after this session.
2. Demote gradients. In `_gradients.css`, keep the nine status-pill gradients
   and the mesh hero; stop gradients being the default surface treatment.
   Leave the `.glass-*` block in place — U7 removes it with its last consumer.
3. Build the primitives in `frontend/src/app/shared/ui/`, each standalone,
   signal-based, exported from `index.ts`: `ui-card`, `ui-section`
   (collapsible, with a title, optional completion indicator and slotted
   actions), `ui-field` (label, required marker, hint, error), `ui-input`,
   `ui-select`, `ui-button` (primary / secondary / ghost / danger; sm / md;
   loading state), `ui-table` (dense, sticky header, zebra optional), and
   `ui-toolbar`. They must be composable with `searchable-select` from U3.
   Every interactive element needs a visible focus ring and a real disabled
   state.
4. Rebuild `shared/components/toast/toast.component.ts` and its service
   (D9): at most **3** visible at once with the rest queued, identical
   consecutive messages collapsed into one entry with a `×N` counter,
   auto-dismiss with a pause on hover, and positioning that never covers the
   primary action area — bottom-right, above the fold, clear of where U6 puts
   the summary rail. Keep the error/success/info/warning variants.
5. Fix the sidebar gutter (D12). Have `sidebar.component.ts` publish its
   collapsed state to `module-shell.component.ts` (an output or a small shared
   signal service) and drive the shell's left offset from a CSS custom
   property that follows the actual width. Persist the collapsed preference.
6. Update `docs/design-system.md` — the token catalogue, the "no raw hex" rule,
   the primitive inventory and a short migration note pointing at U6/U7. Extend
   `features/kitchen-sink/kitchen-sink.component.ts` to render every new
   primitive in every state, all nine status pills, the toast stack at its cap,
   and `searchable-select`.

**Verify:**
```powershell
cd frontend; npm run build     # under budget, zero warnings
cd ..; .\scripts\dev.ps1
```
Open `http://localhost:4200/_kitchen-sink` and confirm every primitive renders
in **both** themes with a visible focus ring on every interactive element.
Trigger four identical errors and confirm the toast shows one entry with `×4`.
Enable "Reduce motion" in Windows settings and confirm every animation stops.
Collapse the sidebar and confirm the content reflows with no gutter. Paste
screenshots of the kitchen sink in dark and light.

**Commit:** `feat(u5): dark-first token system, UI primitives, capped toast stack`

---

## Session U6 — Order builder layout rebuild

The builder is six equal-weight full-width cards stacked in one column, each
with `repeat(auto-fit, minmax(220px, 1fr))` grids that spread unrelated fields
across eight columns on a wide screen. There is no summary, no sticky action,
no section navigation, no indication of what is required or what is done. This
session gives it a shape.

Read `UI_Rework_Plan.md` D11. Depends on U5's primitives; consumes U3's branch
picker and U4's corrected data flow. Session 7 of 9.

1. Restructure `features/flat-order/flat-order.component.ts` into a two-column
   workspace built on `ui-section`:
   - **Left (fluid):** Order header · Customer · Products · Payments · Payload
     preview — each collapsible, each showing a completion indicator derived
     from the same required-field set the validator enforces (read it from the
     validation response, do not hardcode a second list). A sticky in-page
     section nav scroll-spies the active section.
   - **Right (fixed ~340px, sticky):** the summary rail.
2. Add `features/flat-order/components/order-summary-rail.component.ts`: item
   count, subtotal, product VAT, discount, delivery, **total**, paid, balance —
   sourced from U4's server totals, with `shared/ui/riyal` for currency — plus
   validation status (clean / N issues, clicking an issue focuses its field),
   the U1 environment badge, and the Validate and Send actions. Send stays
   disabled with a stated reason while the draft is invalid.
3. Rebuild the products and payments tables on `ui-table`: dense rows, sticky
   header, per-row computed totals matching the payload's `row_net_total`
   semantics, inline quantity and discount editing routed through U2's draft
   store, and a clear per-row delete. Real `empty-state` when there are none.
4. Rebuild the field groups on `ui-field` with deliberate column spans rather
   than `auto-fit` — group related fields (country code + phone on one row;
   first, middle, last name on one row; GPS lat + lng paired) and give address
   the full width. Mark required fields consistently.
5. Move the consumer-lookup and item-lookup affordances into their section
   headers as `ui-toolbar` actions instead of inline input groups.
6. Responsive: below 1200px the rail collapses into a sticky bottom action bar
   carrying the total and the Send action; below 768px sections become single
   column. No horizontal page scroll at any width — wide tables scroll inside
   their own container.
7. Loading and empty behaviour: `skeleton` placeholders while the draft loads,
   never a flash of empty inputs.

**Verify:**
```powershell
cd frontend; npm run build
cd ..; .\scripts\dev.ps1
```
Browser, badge reading `TEST`, at **1280px** and **1920px**: complete a full
build — select a branch, look up and add an item, look up a consumer, add a
payment — and confirm no horizontal scroll, no overlap, and the rail visible
throughout. Then narrow to 900px and 600px and confirm the bottom bar appears
and nothing is clipped. Tab from the top of the page to the Send button and
confirm the order is sensible. Paste screenshots at 1920px, 1280px and 600px.

**Commit:** `feat(u6): two-column order builder workspace with a sticky summary rail`

---

## Session U7 — Migrate the rest of the app, delete the legacy classes

U5 built the primitives and U6 used them on the builder. Everything else —
landing, navbar, sidebar, breadcrumb, unicommerce, order-requests and its
drawer, order-validation — is still on the `.glass-*` bridge. This session
finishes the migration and removes the bridge, so the app has exactly one
design system.

Read `UI_Rework_Plan.md` D10, D14 and §3 decision 5. Session 8 of 9.

1. `layout/navbar/navbar.component.ts` — rebuild on `ui-toolbar` / `ui-button`,
   keep the U1 environment switcher and badge, keep the theme toggle, and
   replace the `alert('Project Documentation available in docs/ folder')` with
   a real link to the docs route or an external URL (D14). If there is nowhere
   meaningful to link, remove the button rather than leaving a stub.
2. `layout/sidebar/` and `layout/breadcrumb/` — rebuild on the primitives,
   preserving U5's collapse behaviour. In the sidebar, label the Order
   Validation entry as superseded by Order Requests (a small badge or muted
   caption). **The page stays** — the user chose to keep it; do not remove the
   route or the components.
3. `features/landing/` — restyle `landing.component.ts` and
   `module-card.component.ts`. Environment chips on the cards must reflect
   U1's default so the Testing lane is the visually obvious choice, and
   Production is marked.
4. `features/order-requests/**` — migrate the page, `filter-bar`,
   `requests-table`, `order-request-drawer`, `cancel-request-dialog` and
   `resend-request-dialog` onto the primitives. The stat tiles, status pills,
   json-tree and drawer already come from `shared/ui` — keep them, restyle
   only what still uses `.glass-*`. Preserve every existing behaviour: the
   route-driven drawer, all six tabs, the `ExceptionMessage` danger card, the
   cancel-blocked statuses, and the resend branch selector (now U3's picker).
5. `features/unicommerce/**` and `features/order-validation/**` — migrate onto
   the primitives. Match the builder's section rhythm where the shapes are
   comparable; these do not need U6's full two-column treatment.
6. Delete `.glass-card`, `.glass-card:hover`, `.glass-panel`, `.glass-input`,
   `.glass-input:focus`, `.glass-button`, `.glass-button:hover` and
   `.glass-button:active` from `frontend/src/styles/_gradients.css`, along with
   the block comment introducing them. **Run the grep gate before committing,
   not after.**
7. Remove any back-compat token aliases in `_tokens.css` that no longer have a
   consumer, and note the removal in `docs/design-system.md`.

**Verify:**
```powershell
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src   # expect EMPTY
cd frontend; npm run build     # under budget, zero warnings
cd ..; .\scripts\dev.ps1
```
Walk every route in **both** themes and confirm nothing is unstyled, clipped or
low-contrast: `/`, `/modules/upc_ecommerce/order`,
`/modules/upc_ecommerce/requests`, a request drawer,
`/modules/upc_ecommerce/validation`, `/modules/ghc_unicommerce/unicommerce`,
`/_kitchen-sink`. Paste a screenshot per route in dark mode and confirm in
light. Report any route you could not reach and why.

**Commit:** `refactor(u7): migrate every remaining feature to the new primitives, delete the legacy glass classes`

---

## Session U8 — End-to-end verification, documentation, cleanup

Everything is built. This session proves it works against the real Testing
environment, brings the documentation back in line, and closes the programme.

Read `UI_Rework_Plan.md` §4 (the U8 row) and §5 (risks). Session 9 of 9. This
session is mostly verification — resist the urge to add features.

1. Run the full gate and fix anything it surfaces:
   ```powershell
   .\scripts\build.ps1
   ```
2. Run the end-to-end flow against **UPC Testing only**, in the browser, badge
   reading `TEST`, and record each step's real result:
   1. Open `/modules/upc_ecommerce/order` directly. Confirm `TEST`.
   2. Select a branch from the searchable picker by typing its **name**.
   3. Add Product → look up a real item code → confirm the branch-specific
      price populates → add it.
   4. Look up consumer `0556028080` → confirm every returned field populates,
      with **zero** error toasts.
   5. Add a payment consistent with the payment rules (a `CashOnDelivery` line
      with `not_payment`, or a card line covering the full total).
   6. Confirm the summary rail matches `GET calculate-totals` to the cent.
   7. Send. Record the order number and the HTTP status.
   8. Open Order Requests, find that order number, open the drawer, and confirm
      the **Request** tab matches what was sent and the **Response** tab has a
      populated `ResponseJson`.
   9. Cancel that order from the drawer with a reason, and confirm the status
      pill changes and that the cancel hit the **Testing** `CancelOrder` URL.
3. Refresh the documentation to match reality:
   - `README.md` — the corrected hosts and environment matrix, the Testing-by-default
     behaviour, the branch picker, the new design system, and a pointer to
     `UI_Rework_Plan.md` beside the existing `remediation_plan.md` reference.
   - `docs/api-spec.md` — `PATCH modules/{key}/order-data`, the branches
     endpoint, `environmentKey` on cancel, and the removal of
     `PUT order-field` and the old order-requests branches route.
   - `docs/design-system.md` — final token catalogue and primitive inventory.
   - `docs/database-schema.md` — confirm the `dbo.Branches` row from U0 and the
     branch query as implemented in U3.
4. Cleanup: confirm `var/` is still untracked and no draft JSON was ever
   committed (`git ls-files | Select-String "var/drafts"` empty); confirm no
   credential is in any tracked file; remove any scratch or throwaway file that
   made it into the tree.
5. Write a short closing section at the end of `UI_Rework_Plan.md` recording
   what shipped, anything deliberately deferred, and any defect that turned out
   to be different from how it was described here.

**Verify** (paste all of it):
```powershell
.\scripts\build.ps1

git grep -n "10\.10\.9\.181\|10\.10\.8\.181" -- backend frontend README.md docs/api-spec.md docs/database-schema.md docs/design-system.md
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src
git ls-files | Select-String "var/drafts"
git status --short
```
All four greps empty, the working tree clean. Paste the full E2E transcript
from step 2 including the **real order number**. If any step could not be
completed — the database unreachable, the API host down — say so explicitly and
name the step. Do not report a green programme from a green build.

**Commit:** `docs(u8): verify the reworked tool end-to-end on UPC Testing and refresh the documentation`
