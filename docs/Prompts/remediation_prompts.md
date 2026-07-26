# Remediation Execution Prompts — 11 Sessions (R0 → R10)

Companion to [`remediation_plan.md`](remediation_plan.md). One prompt per session, executed in order, one conversation each.

**Rules that apply to every session** (repeat them if the agent drifts):

- Read [`remediation_plan.md`](remediation_plan.md) in full before touching anything, especially §2 (defect register) and §3 (guiding decisions).
- **Never invent a SQL column name or a JSON key.** The only two sources of truth are `docs/Prompts/UPC_Enhancments_Plan.md` §"Schema discovery" and `docs/request_examples/**`. `_legacy_flask/modules/flat_order.py` is the behavioural reference. If something is not in one of those, stop and ask.
- Work only inside the files listed for the session.
- Run the verification block and paste its real output before declaring done. Do not claim success from a build alone.
- End with `dotnet build` clean, `dotnet test` green, and a single clean commit using the given message.
- Shell is PowerShell on Windows, from the repo root `d:\AI Tools\DBS\online_order_tool`.

---

## Session R0 — Ground truth & safety net

The .NET 10 + Angular 22 rewrite is complete but its core is wrong: the payload schema was invented, the SQL columns do not exist, and the 21 passing tests validate the invention rather than the contract. This session changes **no production logic**. It commits the migration cleanly, removes secrets, deletes scaffolding, corrects the misleading schema doc, and — most importantly — introduces the contract tests that must **fail** so the following sessions have a real gate.

Read `remediation_plan.md` §2.1, §2.7 and §3 first. This is session 1 of 11.

1. Commit the pending migration as-is so remediation starts from a clean tree. `git status --short` currently shows ~102 entries mixing staged renames with unstaged deletes: stage everything, verify no secrets are being added beyond what is already tracked, and commit as a checkpoint.
2. Purge credentials from tracked files. Replace the four connection strings in `backend/src/OnlineOrderTool.Api/appsettings.json` with empty placeholders. Remove the hardcoded `DbConnectionConfig` literals (`sa` / `<redacted-password>` / `10.10.8.181`) from `UpcEcommerceModule.cs`, `GhcEcommerceModule.cs` and `GhcUnicommerceModule.cs`. Move real values to .NET user-secrets (`dotnet user-secrets init` + `set`) and document the keys in a new tracked `.env.example`-style section in `README.md`. Add a startup guard that throws a clear `ConfigurationException` naming the missing key rather than failing inside Dapper. Fix `.gitignore`: stop ignoring `appsettings.Development.json` as if it were the secret-bearing file, and ignore `appsettings.Local.json`, `var/`, `order_history_*.json`, `last_order_*.json`.
3. Delete scaffolding: `WeatherForecastController.cs`, `WeatherForecast.cs`, `OnlineOrderTool.Core/Class1.cs`, `OnlineOrderTool.Data/Class1.cs`, `tests/OnlineOrderTool.Tests/UnitTest1.cs`, `OnlineOrderTool.Api.http`.
4. Upgrade `Microsoft.OpenApi` past the NU1903 advisory in both `OnlineOrderTool.Api.csproj` and `OnlineOrderTool.Tests.csproj`.
5. Copy `docs/request_examples/**` to `backend/tests/fixtures/payloads/` and mark them `CopyToOutputDirectory`. Exclude the two `xx`-prefixed UPC files (superseded).
6. Add `backend/tests/OnlineOrderTool.Tests/ContractTests.cs`:
   - A key-for-key test that builds a GHC payload from a draft equivalent to `fixtures/payloads/GHC E-Commerce/request_body.json` and asserts the **exact top-level key set** matches, plus the key sets of `order_products[0]` and `payment_methods_with_options[0]`. On mismatch the failure message must print `missing` and `unexpected` key lists — the diff is the working spec for R1.
   - The same for UPC against `fixtures/payloads/UPC/4- Invoice without discount, with delivery and paid by visa.json`.
   - A SQL sanity test that reflects over `UpcOrderValidationRepository` (or reads its source file) and asserts none of these appear: `H.Status`, `CreatedDateTime`, `CustomerMobile`, `CustomerName`, `ShippingAddress`, `H.Notes`, `UpdatedDateTime`, `I.OrderNumber`, `ItemCode`, `DiscountAmount`, `VatAmount`, `LineTotal`, `TransactionId`.
   - Mark all of them `[Fact]` — they are **expected to fail now**. Do not add `Skip`.
7. Rewrite `docs/database-schema.md`. Delete the invented queries and replace them with the verified schema copied from `docs/Prompts/UPC_Enhancments_Plan.md` §"Schema discovery", plus the four real queries lifted verbatim from `_legacy_flask/modules/flat_order.py` (`lookup_item`, `lookup_upc_item`, `lookup_upc_consumer_by_phone`, `search_upc_orders`). Add a header stating this file is the SQL contract and that no query may deviate from it.

**Verify** (paste real output):
```powershell
dotnet build backend/OnlineOrderTool.slnx --nologo   # 0 errors, 0 NU1903 warnings
dotnet test  backend/OnlineOrderTool.slnx --nologo   # existing tests green, 3 NEW contract tests FAILING
git grep -in "password="   # expect no source hits (checks tracked file CONTENTS for any inline-password connection string)
git status --short                                    # clean
```
Report the exact missing/unexpected key lists printed by the two payload tests — R1 depends on them.

**Commit:** `chore(r0): clean migration, purge secrets, remove scaffolding, add failing contract tests`

---

## Session R1 — Rebuild the flat-order payload builders

R0 pinned the contract with failing tests. This session makes them pass. The current `FlatOrderPayloadBuilder` emits an invented key set (`client_name`, `client_code`, `client_mobile`, `shipping_address`, `district_name`, `city_name`) and is missing the entire client identity block, every computed total, `order_creation_date`, `order_gps` and `address_code`. This is the single highest-risk session in the plan.

Read `remediation_plan.md` §2.1 and B1–B4. Read `_legacy_flask/modules/flat_order.py` (`build_payload`, `build_upc_payload`, `_prepare_products`, `_prepare_payments`, `_format_birthdate`, `_get_payment_method_string`, `_determine_payment_status`) and both reference payloads. This is session 2 of 11; sessions R2+ depend on this being exactly right.

1. Extend `OrderDraft` / `Product` / `Payment` in `OnlineOrderTool.Core/Models` with every field the contract requires and the draft currently lacks — client country code, phone, first/middle/last name, email, birthdate, gender, order address, address code, GPS pair, order creation date, and the per-line VAT and net-total fields.
2. Replace `BuildGhcPayload` / `BuildUpcPayload` with **one** `BuildPayload(OrderDraft draft, FlatVariant variant)` plus `GhcVariant` / `UpcVariant` records. The only differences are the five trailing GHC delivery keys (`delivery_date`, `delivery_from_time`, `delivery_to_time`, `shipping_address_2`, `fullfilment_plant`) and whether payments carry `credit_customer_info`.
3. Emit the full contract key set in the reference order. Preserve the existing **correct** VAT normalisation (`vat_percentage` as a decimal fraction — `15` → `0.15`); it matches `_prepare_products` and both references. Use value domains from the references: `order_status: "new"`, `order_payment_status: "done_payment"`, `is_delivery` as an **int** `1`/`0`, ISO-8601 `...Z` timestamps for `order_creation_date` and `client_birthdate`.
4. Compute and emit `order_product_total_value`, `order_total_discount`, `order_final_total_value` and `order_payment_method` from the draft — do not read them from `OrderData`. Round money to 2 decimals exactly as `_prepare_products` does.
5. Rewrite `FormatProduct` to emit `item_code`, `item_name`, `quantity`, `unit_price`, `offer_code`, `offer_message`, `row_total_discount`, `unit_vat_amount`, `total_vat_amount`, `vat_percentage`, `row_net_total`.
6. Rewrite `FormatPayment`: nest `credit_customer_info: { customer_number, customer_name }` for GHC PostToCredit (not flattened), and include `card_name` and `bank_code`.
7. Do **not** touch `FlatOrderValidator` — R2 owns it. Existing tests asserting the old invented shape should be updated or deleted, not worked around.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
```
Both payload contract tests must pass key-for-key. Then print a built UPC payload and diff it by eye against `docs/request_examples/UPC/4- ….json` — confirm value domains, not just key names.

**Commit:** `fix(r1): rebuild flat-order payload builders against the reference contract`

---

## Session R2 — Validator and totals

The validator currently enforces the invented schema (`client_name`, `client_code`, `client_mobile`, `shipping_address`), so it approves broken payloads and would reject correct ones. With R1 done, it must be rewritten against the real contract and the real payment rules.

Read `remediation_plan.md` B5, and `_legacy_flask/modules/flat_order.py` (`validate`, `calculate_payment_summary`, `calculate_product_totals`) plus the payment-rules section of `README.md` in `_legacy_flask/`. Session 3 of 11.

1. Rewrite `FlatOrderValidator.ValidatePayload` against the corrected keys: require `branch_code`, `order_code`, `client_phone`, `client_first_name`, `order_address`, at least one product and at least one payment.
2. Port the real payment rules from the legacy validator: CashOnDelivery → status must be `not_payment`; Visa / digital wallets / points → status `done_payment` **and** the payment must cover the full `order_final_total_value`; PostToCredit → `not_payment`, and it is **not permitted for UPC**.
3. Change the signature to take the computed totals alongside the payload, so the "covers the full amount" rules compare against a real number. The legacy version compared against a `total_paid` key that was never emitted, making those rules silently inert — do not reproduce that.
4. Align `TotalsCalculator` with `calculate_product_totals` / `calculate_payment_summary`, including discount handling per line and rounding order.
5. Wire `backend/tests/fixtures/payloads/edge_cases.json` (each entry → **zero** errors) and `invalid_payloads.json` (each entry → **at least one** error, and assert the error mentions the documented `violation`) as `[Theory]` tests.
6. Apply the same treatment to `UniCommerceValidator` against the `GHC Uni-Commerce` references.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
```
All contract, edge-case and invalid-payload tests green. Report how many fixture cases ran.

**Commit:** `fix(r2): rebuild validator and totals against the real payment rules`

---

## Session R3 — Item and consumer repositories

Every lookup query was invented. UPC item lookup uses `ItemPrices`/`IP.BranchId` when the verified query is a ranked CTE over `BranchItemUnitOfMeasures` + `ItemUnitOfMeasurePrices` + `Branches`. GHC item lookup dropped the barcode join and the Arabic name. Branch-aware pricing — the core UPC daily workflow — cannot work today.

Read `remediation_plan.md` B7, B8, and the four reference queries now in `docs/database-schema.md` (written in R0). `_legacy_flask/modules/flat_order.py` is authoritative. Session 4 of 11.

1. Add `OnlineOrderTool.Core/Normalizers.cs`: `NormalizeUpcMaterialNumber` (6-digit ↔ 18-digit zero-padded, 12 zeros + 6 digits) and `NormalizePhoneSearch` (strip `+966` / `966` / leading `0` → last 9 digits, matched with `RIGHT(col, 9) = @p`). Unit-test both.
2. Rewrite `UpcItemRepository.LookupItemAsync` with the verified CTE verbatim: rank `ROW_NUMBER() OVER (PARTITION BY I.Id, B.Id ORDER BY BIUOM.IsBase DESC, IUOMP.Price DESC)`, take `rn = 1`, and require `branch_code` — it is mandatory for UPC, not optional. Return English name, Arabic name, price and tax rate, and compute the net price.
3. Rewrite `FlatOrderItemRepository.LookupItemAsync` with the GHC query including the `INNER JOIN dbo.ItemUnitOfMeasureBarCodes` for `UniversalBarCode` and `I.NativeName`, and the optional `customer_number` / `sap_tax_code` / `sap_mat_generic` filters.
4. Rewrite `UpcConsumerRepository` with the verified `Consumers` + `OUTER APPLY` over `LoyaltyConsumerAddresses` ordered `IsMaster DESC, Id DESC`, returning `FullAddress` with the compose-from-parts fallback and `AddressCode`.
5. Keep `GhcConsumerRepository` as-is but carry over the legacy `TODO(db-creds)` comment stating the GHC table shape is unconfirmed.
6. Ensure every query is parameterised — no string interpolation of user values anywhere.

**Verify** (needs `10.10.8.181` reachable; UPC Testing credentials in user-secrets):
```powershell
dotnet run --project backend/src/OnlineOrderTool.Api
# second shell:
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/lookup/item?code=207350&branchCode=P001&envKey=UPC%20Testing"
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/lookup/consumer?phone=0556028080&envKey=UPC%20Testing"
```
Both must return real data — a named item with a branch-specific price, and a consumer with a resolved address. Paste the responses. If the DB is unreachable, say so explicitly rather than reporting success.

**Commit:** `fix(r3): restore verified item and consumer SQL, add normalizers`

---

## Session R4 — OrderRequestRepository on the real table

This is the session that delivers what the whole project was for. The Order Requests feature currently reads a local `order_history_<module>.json` file that only ever sees orders sent from this tool. The real data is in the SQL Server `OrderRequests` table — `Id, OrderNumber, OrderDate, NetTotal, ItemCount, RequestJson, ExceptionMessage, IsSucceeded, ResponseJson`. `ResponseJson` and `ExceptionMessage` are currently read nowhere in the codebase.

Read `remediation_plan.md` §2.3 and B6, B9, and `docs/database-schema.md`. Session 5 of 11. Do not modify controllers yet — R5 owns those.

1. Create `OnlineOrderTool.Data/Repositories/OrderRequestRepository.cs` and matching DTOs in `Core/DTOs/OrderRequestDtos.cs`.
2. Base every query on `FROM dbo.OrderRequests AS R`, joining headers and invoices with `OUTER APPLY (SELECT TOP 1 … ORDER BY Id DESC)` — **not** `LEFT JOIN`. Neither table is 1:1 with `OrderNumber` (retries and re-invoicing create extra rows) and a join multiplies rows.
3. Use the **verified** column names: `H.OrderStatus`, `H.OrderDate`, `H.ConsumerMobile`, `H.Address`, `H.OrderNote`, `H.ParentOrderNumber`, `I.OnlineOrderNumber`, `I.Barcode`, `I.CloseDateLocalTime`; details `MaterialNumber`, `TotalDiscount`, `ItemVat`, `ItemVatPercentage`, `TotalPrice`, `UnitPrice`, `OfferCode`, `OfferMessage`; transactions `ECommercePaymentMethod`, `ECommercePaymentOption`, `PaymentAmount`, `OptionCommission`, `PaymentStatus`, `TransactionCode`, `BankCode`, `CardName`.
4. `ListAsync(filters, page, pageSize, sort)` — **must not select `RequestJson` or `ResponseJson`.** Select `DATALENGTH(R.RequestJson) AS RequestBytes` and `CASE WHEN R.ResponseJson IS NULL THEN 0 ELSE 1 END AS HasResponse`. Page with `ORDER BY R.Id DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY` — order by `Id`, not `OrderDate`, for stable boundaries.
5. One private `BuildFilters(filters)` returning the WHERE fragment and parameters, shared by `ListAsync`, `CountAsync` and `StatsAsync` (conditional aggregates: total, succeeded, failed, cancelled via `OrderStatus IN (6,7)`).
6. `GetDetailAsync(requestId)` is the **only** method that reads the blobs. Return `RequestJson`, `ResponseJson`, `ExceptionMessage`, `IsSucceeded`, the header, line items, transactions and invoice.
7. Add `ListAttemptsAsync(orderNumber)` (every `OrderRequests` row for that order, newest first), `GetLineageAsync(orderNumber, parentOrderNumber)` and `ListBranchesAsync()`.
8. Add the status decode map (1 New … 9 Done) and `ResendBlockedStatuses = {4,8,9}`, `CancelBlockedStatuses = {5,6,7,9}` as shared constants in `Core`.
9. Add tests asserting the list SQL string contains `DATALENGTH` and `OUTER APPLY` and does **not** contain `ResponseJson`, and that paging parameters are bound.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
```
Then run a live query against UPC Testing through a temporary console harness or a scratch test, and paste: one page of list rows, and one detail row **for a request whose `ExceptionMessage` is not null** — proving the previously-invisible columns now surface.

**Commit:** `feat(r4): OrderRequests repository reading RequestJson, ResponseJson and ExceptionMessage`

---

## Session R5 — Order Requests API, cancel and resend

Two data-corrupting bugs live here. `HistoryController.CancelFromHistory` sends the cancel body to `entry.ApiUrl` — the **CreateAndAssignOrder** endpoint — so cancelling attempts to create an order. And "resend" calls `send-request`, which builds from whatever draft is currently loaded, not the historical order; `OrderController.ResendOrder` also mutates and persists the draft's `branch_code`. The legacy Python guarded against exactly this and documented why.

Read `remediation_plan.md` §2.3 (B12, B13) and B21, and `_legacy_flask/app.py::module_resend_order`. Session 6 of 11.

1. Add `Capabilities` to `IOrderModule` (`DraftKind`, `ItemLookup`, `ConsumerLookup`, `OrderRequests`, `Cancel`, `Resend`). Set `OrderRequests = true` for `upc_ecommerce` only; `false` for GHC with a `// TODO(db-creds)` comment naming the one-line flip. Add a `RequireCapability` helper returning **501** with a typed error when absent.
2. Remove every module-key string comparison from `OrderController`, `LookupController`, `ValidationController` and `FlatOrderValidator`; dispatch on capabilities and `DraftKind` instead. Implement `BuildPayload` / `Validate` properly on each module so `IOrderModule` stops being decorative, and delete the `switch` in `OrderController.BuildPayloadForModule`.
3. Create `OrderRequestsController` at `api/modules/{key}/order-requests`: `GET /` (list with `q, orderNumber, phone, branchCode, status, succeeded, hasException, dateFrom, dateTo, page, pageSize≤200, sort`, returning `{items,page,pageSize,total,totalPages,stats}`), `GET /{id}`, `GET /by-order/{orderNumber}`, `GET /branches`, `POST /{id}/cancel`, `POST /{id}/resend`.
4. **Cancel:** resolve the URL as `customUrl → endpointKey → environment.CancelUrl` — never `ApiUrl`. Re-check `CancelBlockedStatuses` server-side and return **409** with a reason when blocked; a client-side check is not a boundary. After the call, re-read the detail and return it alongside the raw upstream response so the UI can show what came back.
5. **Resend:** rebuild the payload from **that order's own stored `RequestJson`**, override only `branch_code`, re-check `ResendBlockedStatuses`, and never read or write the live draft. Delete the draft mutation in `OrderController.ResendOrder`.
6. Delete `OrderHistoryService.cs`, `IOrderHistoryService`, `HistoryController.cs`, `OrderHistoryEntry.cs`, their DI registration, and any `order_history_*.json` files. If such a file exists with data, move it to `var/archive/` first and say so.
7. Update `docs/api-spec.md` to the new surface — it is the contract R7 generates models from.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
Select-String -Path backend/src -Include *.cs -Pattern '== "upc_ecommerce"' -Recurse   # expect no hits
curl.exe -s -o NUL -w "%{http_code}" "http://localhost:5200/api/modules/ghc_ecommerce/order-requests"   # expect 501
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/order-requests?page=1&pageSize=5"
```
Add a test with a stubbed API client asserting that cancel posts to a URL containing `CancelOrder` and never one containing `CreateAndAssignOrder`. Attempt a cancel on a status-9 order and paste the 409.

**Commit:** `feat(r5): OrderRequests API; fix cancel posting to the create-order URL and resend using the live draft`

---

## Session R6 — State, config and error contract

`DraftManager` is a singleton writing `last_order_<key>.json` to the process working directory, so every browser shares one draft and overwrites the others — while CORS is configured `AllowCredentials()` for a session that does not exist. Error handling is also inconsistent: `LookupController` returns HTTP 200 with `{success:false}` on database failure while `ValidationController` returns 400.

Read `remediation_plan.md` §2.5. Session 7 of 11.

1. Make drafts per-session. Add a session cookie (or an explicit client-id header issued on first contact) and key drafts by `(sessionId, moduleKey)`. Store under `var/drafts/` rather than the process CWD, and ensure `var/` is gitignored.
2. Give `ExceptionMiddleware` one uniform envelope — `{ "error": { "code", "message", "details" } }` — with a typed exception hierarchy (`NotFound`, `BadRequest`, `Conflict`, `Upstream`, `FeatureNotSupported`, `Configuration`). Map DB failures to 5xx.
3. Remove `Ok(new {success=false})` from `LookupController` and any other controller returning 200 on failure; use the envelope and correct status codes throughout.
4. Put the global TLS bypass behind a config flag (`Outbound:VerifyTls`, default `false` for these self-signed internal hosts) instead of an unconditional `=> true`, and log once at startup when it is disabled.
5. Drop `ApiUrl` from `EnvironmentDto`; expose `HasApiUrl` / `HasCancelUrl` booleans instead, and add a separate authenticated-by-obscurity-free endpoint only if the UI genuinely needs the selectable URLs for the endpoint picker.
6. Set `AllowCredentials` only if step 1 actually uses cookies; otherwise remove it.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
```
Open the app in two different browsers, edit the draft in each, and confirm they do not overwrite one another. Stop SQL Server (or point at a bad host) and confirm the lookup endpoint returns a 5xx envelope rather than 200. Confirm `/api/modules` no longer contains any `apiUrl`.

**Commit:** `fix(r6): per-session drafts, uniform error envelope, scoped TLS bypass`

---

## Session R7 — Frontend contract layer

The Angular app hardcodes the entire module list (~90 lines of environments, logos and URLs) instead of consuming `/api/modules`, types the Order Requests feature as `any`, hardcodes `http://localhost:5200/api` with no production replacement, loads Bootstrap Icons twice, ships a dead `src/assets/` directory, and exceeds its own bundle budget.

Read `remediation_plan.md` §2.6. Session 8 of 11. Do not restyle anything — R8 owns the design system.

1. Delete the hardcoded module list from `module.service.ts` and load `/api/modules` once at bootstrap via `provideAppInitializer`, exposing it as signals.
2. Create `core/models/*.ts` typed against `docs/api-spec.md`: `ModuleDto`, `EnvironmentDto`, `OrderDraft`, `Product`, `Payment`, `TotalsSummary`, `OrderRequestListItem`, `OrderRequestDetail`, `ApiError`. Remove `any` from all feature code.
3. Add `environment.production.ts` and the `fileReplacements` entry in `angular.json` so the production bundle does not call `localhost`. Reconcile the port: `launchSettings.json` binds 5200 while `cmd.md` documents 5000 — pick 5200 and correct `cmd.md`.
4. Add `proxy.conf.json` routing `/api` to `http://localhost:5200` and wire it into the `serve` target, so development does not depend on CORS.
5. Consolidate assets: keep `frontend/public/assets/`, delete `frontend/src/assets/`, and confirm every `assets/…` reference still resolves in a production build.
6. Remove the CDN `@import url(...)` for Bootstrap Icons from `_variables.css`; keep only the npm import in `styles.css`.
7. Make every route lazy with `loadComponent`. Delete the duplicate `api`, `database` and `test` routes that all resolve to `FlatOrderComponent`. Add a capability guard that reads the capabilities from `/api/modules`.
8. Bring the production bundle back under its 500 kB budget (lazy routes should largely do it); do not simply raise the budget.
9. Add an HTTP interceptor that unwraps the R6 error envelope into a typed `ApiError` and surfaces it through the existing toast service.

**Verify:**
```powershell
cd frontend
npm run build              # under budget, zero warnings
Select-String -Path dist -Pattern "localhost:5200" -Recurse   # expect no hits
Select-String -Path src/app/features -Include *.ts -Pattern ": any" -Recurse   # expect no hits
npx ng serve               # app loads modules from the API, not from a hardcoded list
```

**Commit:** `refactor(r7): typed API contract, live module metadata, prod env, proxy, asset and budget fixes`

---

## Session R8 — Design system swap to bold gradient / vibrant

The approved direction was **bold gradient / vibrant** — saturated brand gradients, large rounded cards, colourful status pills, spring animations, big count-up stat tiles. What was built is dark glassmorphism. Swap the system now, before the Order Requests page is rebuilt on top of it, so the page is not built twice.

Read `remediation_plan.md` B24. Session 9 of 11.

1. Replace `_glassmorphism.css` and the glass variables in `_variables.css` with `_tokens.css` + `_gradients.css`. Tokens: `--grad-brand` (violet → pink → orange, 135deg), `--grad-success`, `--grad-danger`, `--grad-info`, `--grad-muted`, `--grad-mesh` (three radial layers); radius scale `12/18/26/34/999px`; shadow scale including a brand `--sh-glow`; easings `--ease-spring: cubic-bezier(.34,1.56,.64,1)` and `--ease-out: cubic-bezier(.16,1,.3,1)`; durations `140/240/420ms`. Support light and dark via `[data-theme]`; keep the existing `ThemeService` contract.
2. **No component stylesheet may contain a raw hex value** — everything reads a token. Record the rule in a new `docs/design-system.md`.
3. Add a status-pill gradient map keyed to the nine order statuses: 1 New → info, 2 Confirmed → indigo, 3 Ready → teal, 4 With_Delegate → amber, 5 Rejected → danger, 6 CanceledByClient → rose-slate, 7 CanceledByAdmin → rose, 8 Processing → brand with a pulsing ring, 9 Done → success.
4. Build `shared/ui/`: `gradient-card`, `stat-tile` (count-up directive using `requestAnimationFrame` + easeOutCubic, ~900ms, thousands-separated), `status-pill` (pop animation on change), `json-tree` (collapsible, type-coloured, click-to-copy path, search with highlight and auto-expand, raw toggle, download, and a danger-banner fallback when the string is not valid JSON), `drawer` (CDK overlay, focus trap, Esc/backdrop close, slide-in), `confirm-dialog` (danger variant, required-reason slot), `empty-state`, `skeleton` (shimmer), `data-table` (virtual scroll, staggered row entrance), `pagination`, `riyal` (preserve the `Saudi_Riyal.svg` CSS-mask technique from the legacy `style.css`), `copy-button`, `filter-chip`, `page-header` (mesh hero with a slow drift).
5. Collapse all animation durations to near-zero under `@media (prefers-reduced-motion: reduce)`, and short-circuit the count-up directive to set its final value immediately.
6. Add a dev-only `/_kitchen-sink` route rendering every component in every state.
7. Restyle the existing landing, shell and flat-order components onto the new tokens. Do not change their behaviour.

**Verify:**
```powershell
cd frontend
npm run build       # under budget
npx ng serve        # visit /_kitchen-sink
Select-String -Path src/app -Include *.ts,*.css -Pattern "#[0-9a-fA-F]{3,6}" -Recurse   # expect hits only in styles/_tokens.css and _gradients.css
```
On the kitchen-sink page confirm: all nine status pills, all gradients, a json-tree rendering both a nested payload and a deliberately malformed string, drawer open/close, confirm dialog, skeletons, empty states, toasts. Then turn Windows animation effects off and confirm the page renders instantly with no motion.

**Commit:** `feat(r8): bold gradient design system and shared UI kit`

---

## Session R9 — Rebuild the Order Requests page

The page is currently a flat list of locally-recorded cards with no detail view at all — no drawer, no `:requestId` route, no request or response inspection, no exception message, no line items, no payments, no invoice, no attempts timeline, no lineage. Rebuild it on the R5 API and the R8 kit.

Read `remediation_plan.md` §2.3 (B14) and the Order Requests specification in `implementation_plan.md`. Session 10 of 11.

1. Create a typed signal store: `filters`, `page`, `items`, `stats`, `total`, `status` (`idle|loading|ready|empty|error`), `selected`, `detailStatus`, with `computed` derivations and a debounced effect calling the list endpoint.
2. Page layout: mesh-gradient hero with the module and environment pills (amber for Production, cyan for Testing), a refresh button and a 30-second auto-refresh toggle that pauses while the drawer is open or the tab is hidden.
3. Four clickable count-up stat tiles — Requests, Succeeded, Failed, Cancelled — computed from the same filter as the list, each applying its own filter on click.
4. A sticky filter bar: search (debounced 300ms), phone, branch dropdown from `/branches`, nine status chips (multi-select), an outcome segmented control, date from/to, quick ranges (Today / 7d / 30d), and Clear all. **Serialise every filter to query params** so reload and back/forward preserve state. Show active filters as removable chips.
5. A virtualised table with staggered row entrance: outcome dot, order number (monospace, click opens the drawer), date (absolute + relative), branch code and name, gradient status pill, item count, net total with the Riyal glyph, invoice barcode, payload badges (`REQ` always, `RES` lit only when a response exists), and row actions. Failed rows get a danger-gradient left border; cancelled rows (status 6/7) render dimmed with a struck-through order number. Pagination footer with page-size selector.
6. **Route-driven detail drawer** at `:requestId` — deep-linkable and bookmarkable — with six tabs:
   - **Overview** — header fields, an amber callout for `RejectionMessage`, a totals strip, and a consistency check comparing `OrderRequests.NetTotal` / `RequestOrderHeaders.NetTotal` / `Invoices.NetAmount`. If no header exists, show a clear empty state and point the user at the Response tab.
   - **Request** — `json-tree` over `RequestJson`, with the raw-string danger fallback when it will not parse.
   - **Response** — `json-tree` over `ResponseJson` under a success/danger outcome banner, and a full-width danger card rendering `ExceptionMessage` in wrapped monospace with a copy button. Explicit empty state when both are null. *This tab is the reason the feature exists — do not abbreviate it.*
   - **Line items** — the details table with the 18↔6-digit material-number dual display and per-column footer sums flagged against the header totals.
   - **Payments** — transaction cards with the method as a gradient pill and a sum-vs-`NetTotal` match chip.
   - **Invoice & lineage** — invoice card, the **attempts timeline** (every `OrderRequests` row for the order, newest first, each linking to that attempt), and the parent → this → children lineage trail.
7. Cancel: confirm dialog with a **required** reason, quick-fill chips, and an endpoint selector. Handle all four outcomes — success (refresh detail in place, pop the status pill, dim the row without a full refetch), upstream non-2xx (keep the dialog open and render the raw body), blocked 409 (inline reason), and network/5xx (retry). Disable the action per the server's `canCancel` with the reason in the tooltip.
8. Resend: dialog with a branch selector, respecting the server's `canResend`.
9. Implement every state: first-load skeletons, refetch dimming, empty (filtered and unfiltered), the capability-off card for GHC, DB-error with retry, and detail-404 clearing the route param.

**Verify** with the API running and UPC Testing selected:
- Rows show real order numbers, statuses and totals from SQL Server.
- Filtering by status and by date changes the row count **and** all four tiles together; reload preserves the filters.
- Clicking a row deep-links to `/…/requests/<id>`; pasting that URL in a new tab opens the drawer directly.
- A request with a non-null `ExceptionMessage` renders it in the danger card.
- The attempts timeline lists every `OrderRequests` row for that order number.
- In DevTools, the **list** response contains `requestBytes`/`hasResponse` and **not** `requestJson`.
- Cancelling a test order flips the pill to CanceledByClient and dims the row; a status-9 order is blocked in the UI and returns 409 if forced with curl.

**Commit:** `feat(r9): rebuild Order Requests with detail drawer, response inspection, cancel and resend`

---

## Session R10 — Order builder, decommission and docs

Final session. The builder UI still binds to the invented pre-R1 fields, the legacy Flask app is still in the tree twice, and the docs describe ports and schemas that no longer hold.

Session 11 of 11.

1. Rebind the flat-order builder to the corrected schema from R1: the client identity block (country code, phone, first/middle/last name, email, birthdate, gender), `order_address` and `address_code`, `order_gps`, and server-computed totals. Remove the invented `client_name` / `client_code` / `client_mobile` / `shipping_address` / `district_name` / `city_name` inputs.
2. Wire item lookup to the R3 branch-aware endpoint (branch code required for UPC) and consumer lookup to prefill name **and** address. Show the send result using the `json-tree`, with a landed-status card linking straight to that order's Order Requests drawer.
3. Drive the UPC/GHC differences (UPC has no Delivery card) from capabilities, not string comparison.
4. Remove the legacy Flask app: delete `_legacy_flask/`, and the root-level `static/`, `templates/`, `tests/`, `requirements.txt`, `__pycache__/`, `.venv/` references in tracked files. Confirm nothing in `backend/` or `frontend/` still references them.
5. Rewrite `README.md` (architecture, prerequisites, user-secrets setup, dev run, prod build, test commands, per-module credential status and the one-line GHC capability flip), correct `cmd.md` (port 5200, not 5000), and refresh `docs/api-spec.md` and `docs/database-schema.md`.
6. Add `scripts/dev.ps1` (API + `ng serve`) and `scripts/build.ps1`.

**Verify** — full end-to-end, paste the evidence:
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo    # green
cd frontend; npm run build                            # under budget, zero warnings
Get-ChildItem -Path . -Include *.py -Recurse -File | Select-Object -First 5   # expect none outside .venv
```
Then: landing → select UPC Testing → look up an item at a branch → add it → add a payment → send → confirm the order appears in Order Requests with a `RequestJson` matching what was previewed and a populated `ResponseJson` → open the drawer and check all six tabs → cancel it and watch the status pill change.

**Commit:** `feat(r10): rebind order builder to the corrected schema, remove the legacy Flask app, refresh docs`
