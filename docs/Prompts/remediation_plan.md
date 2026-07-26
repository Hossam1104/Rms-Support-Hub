# Remediation Plan — Fixing the .NET 10 + Angular 22 Rewrite

> Companion to [`implementation_plan.md`](implementation_plan.md) and [`execution_prompts.md`](execution_prompts.md), which describe the rewrite **as it was intended**. This document catalogues what the executed rewrite actually produced, and defines the work to bring it to a correct, shippable state.
>
> Execution prompts: [`remediation_prompts.md`](remediation_prompts.md) — 11 sessions, `R0` → `R10`.

---

## 1. Verdict

The rewrite delivered a **well-shaped skeleton with a broken core**.

What is sound and worth keeping: the solution layout (`Api` / `Core` / `Data` / `Tests`), the controller surface, DI wiring, Dapper adoption, the Angular 22 workspace, the component decomposition, the toast/theme services, and the general routing shape.

What is broken: **every layer that touches real data or the real API contract**. The tool's entire reason to exist is that the JSON it POSTs matches the RMS contract to the cent, and that it reads the client's SQL Server correctly. Both are currently wrong, and the test suite — 21 green tests — validates the wrong contract, which is why none of it surfaced.

Two facts frame the whole plan:

1. **The reference payloads in `docs/request_examples/` are the contract.** They were never compared against. Doing so is the primary gate for all payload work.
2. **The verified live SQL schema exists** in `docs/Prompts/UPC_Enhancments_Plan.md` ("Schema discovery", introspected against `10.10.8.181` / `RmsMainTest2`). It was not used. `docs/database-schema.md` documents *invented* queries as though verified and is actively misleading.

**Recommendation: do not revert.** The defects are concentrated in roughly eight files. Reverting would discard genuinely useful scaffolding to re-derive it. The legacy Python in `_legacy_flask/` is the known-good behavioural reference and must be kept until R10.

---

## 2. Defect register

Severity: **S1** = tool produces wrong data or the feature does not function · **S2** = security / architectural defect · **S3** = quality, contract drift, hygiene.

### 2.1 Payload contract — S1

| # | Defect | Evidence |
|---|---|---|
| **B1** | **The flat-order payload schema is entirely invented.** Emitted keys bear almost no relation to the API contract. **Missing:** `order_creation_date`, `order_product_total_value`, `order_total_discount`, `order_final_total_value`, `order_payment_method`, `client_country_code`, `client_phone`, `client_first_name`, `client_middle_name`, `client_last_name`, `client_email`, `client_birthdate`, `client_gender`, `order_address`, `address_code`, `order_gps`. **Invented (not in the contract):** `client_name`, `client_code`, `client_mobile`, `client_national_id`, `shipping_address`, `district_name`, `city_name`. Every order POST will be rejected upstream. | `FlatOrderPayloadBuilder.cs:60-123` vs `docs/request_examples/UPC/4- ….json` and `docs/request_examples/GHC E-Commerce/request_body.json` |
| **B2** | **Product line items are missing every computed money field.** Contract requires `unit_vat_amount`, `total_vat_amount`, `row_total_discount`, `row_net_total`. Builder emits a bare `discount` and none of the others. It also drops `item_Barcode` and `item_AR_Name`, which the legacy item lookup returned. | `FlatOrderPayloadBuilder.FormatProduct:21-37` |
| **B3** | **GHC payment shape is wrong.** Contract nests `credit_customer_info: { customer_number, customer_name }`; the builder flattens them to top-level `customer_name` / `customer_number`, and omits `card_name` and `bank_code`. | `FlatOrderPayloadBuilder.FormatPayment:39-58` |
| **B4** | **Value domains wrong.** Contract uses `order_status: "new"`, `order_payment_status: "done_payment"`, `is_delivery: 1` (int). Builder defaults to `"1"`, `"1"`, and a JSON boolean. | `FlatOrderPayloadBuilder.cs:72-74, 107-109` |
| **B5** | **The validator enforces the invented schema**, so it green-lights broken payloads and would reject correct ones. It requires `client_name`, `client_code`, `client_mobile`, `shipping_address` — none of which exist in the contract. | `FlatOrderValidator.cs:16-33` |

*Note:* VAT normalisation (`vat_percentage` emitted as a decimal fraction, e.g. `0.15`) is **correct** and matches both the legacy `_prepare_products` and the reference payloads. Keep it.

### 2.2 Data access — S1

| # | Defect | Evidence |
|---|---|---|
| **B6** | **Every UPC Order Validation query references columns that do not exist.** `H.Status`→ real `OrderStatus`; `H.CreatedDateTime`→ `OrderDate`; `H.CustomerMobile`→ `ConsumerMobile`; `H.CustomerName`, `H.ShippingAddress`, `H.Notes`, `H.UpdatedDateTime` do not exist (real: `Address`, `OrderNote`); `I.OrderNumber`→ `OnlineOrderNumber`; `I.CreatedDateTime`→ `CloseDateLocalTime`; details `ItemCode/DiscountAmount/VatAmount/LineTotal`→ `MaterialNumber/TotalDiscount/ItemVat/TotalPrice`; transactions `PaymentMethod/Amount/TransactionId`→ `ECommercePaymentMethod/PaymentAmount/TransactionCode`. **Every call throws `Invalid column name` at runtime.** | `UpcOrderValidationRepository.cs:32-146` vs `docs/Prompts/UPC_Enhancments_Plan.md` §"Schema discovery" |
| **B7** | **UPC item lookup queries the wrong tables.** Uses `ItemPrices` + `IP.BranchId` + `Branches`. The verified branch-aware query is a CTE over `BranchItemUnitOfMeasures` + `ItemUnitOfMeasurePrices` + `Branches`, ranked `ROW_NUMBER() OVER (PARTITION BY I.Id, B.Id ORDER BY BIUOM.IsBase DESC, IUOMP.Price DESC)`. Branch-specific pricing — the core UPC daily workflow — cannot work. | `UpcItemRepository.cs` / `docs/database-schema.md` §2 vs `_legacy_flask/modules/flat_order.py::lookup_upc_item` |
| **B8** | **GHC item lookup drops the barcode and the Arabic name.** Legacy `INNER JOIN dbo.ItemUnitOfMeasureBarCodes` for `UniversalBarCode` and selects `I.NativeName`; both were dropped. | `docs/database-schema.md` §1 vs `_legacy_flask/modules/flat_order.py::lookup_item` |
| **B9** | `LEFT JOIN dbo.Invoices` instead of `OUTER APPLY (SELECT TOP 1 …)`. Neither `OrderRequests` nor `Invoices` is 1:1 with `OrderNumber` (retries and re-invoicing create extra rows), so a single header multiplies into duplicate result rows. The legacy code carries an explicit comment about exactly this. | `UpcOrderValidationRepository.cs:42` |

### 2.3 The headline requirement was inverted — S1

| # | Defect | Evidence |
|---|---|---|
| **B10** | **Order Requests reads a local JSON file, not the `OrderRequests` table.** The explicit instruction was *"The Request and its response already saved in table OrderRequests so no need to save them locally."* What was built is `OrderHistoryService` writing `order_history_<module>.json`. Consequences: only orders sent *from this tool* ever appear; nothing survives deleting the file; orders placed by any other channel are invisible; and there is no `IsSucceeded`, no order status, no invoice, no retry history. | `OrderHistoryService.cs:16` |
| **B11** | **`ResponseJson` and `ExceptionMessage` are still never read anywhere in the codebase.** These two columns were the entire point of the feature. The only `OrderRequests` query in the solution selects `RequestJson` alone. | `UpcOrderValidationRepository.GetLatestRequestJsonAsync:150-154`; `grep -r ResponseJson backend/` → no hits |
| **B12** | **Cancel-from-history POSTs to the CREATE-order URL.** `CancelFromHistory` sends the cancel body to `entry.ApiUrl`, which is the `CreateAndAssignOrder` endpoint captured at send time — not `CancelUrl`. On Production this attempts to create an order instead of cancelling one. | `HistoryController.cs:49` |
| **B13** | **Resend re-sends the current draft, not the historical order.** The UI calls `POST send-request`, which builds from whatever draft is loaded in the module. The legacy implementation guarded against precisely this and documented why. Worse, `OrderController.ResendOrder` mutates and **persists** the draft's `branch_code` as a side effect. | `order-requests.component.ts:119-134`; `OrderController.cs:206-217` vs `_legacy_flask/app.py::module_resend_order` |
| **B14** | **No detail view exists at all.** The Order Requests page is a flat card list. There is no drawer, no `:requestId` route, no Request tab, no Response tab, no exception display, no line items, no payments, no invoice, no attempts timeline, no lineage. The entire detail specification is absent. | `frontend/src/app/features/order-requests/` |

### 2.4 Security — S2

| # | Defect | Evidence |
|---|---|---|
| **B15** | **Plaintext `sa` / `<redacted-password>` in two tracked files.** Four connection strings in `appsettings.json`, and the same credentials hardcoded again in the module classes. `.gitignore` ignores `appsettings.Development.json` (no secrets) while the file that *does* hold them is tracked. | `appsettings.json:10-14`; `UpcEcommerceModule.cs:12-26`; `.gitignore` |
| **B16** | `/api/modules` exposes `ApiUrl` for every environment. Internal endpoint topology should not be published to the browser. | `ApiDtos.cs:15` |
| **B17** | **TLS validation disabled globally** for all outbound HTTP, with no opt-out. The legacy `verify=False` was at least scoped to one call. | `Program.cs:53-56` |
| **B18** | `Microsoft.OpenApi 2.0.0` carries a known **high-severity** advisory (NU1903) on both the API and test projects. Build emits four warnings. | `dotnet build` output |

### 2.5 Architecture — S2

| # | Defect | Evidence |
|---|---|---|
| **B19** | **Draft state is a process-global file, not per-user.** `DraftManager` is a singleton writing `last_order_<key>.json`; two browsers share one draft and overwrite each other. CORS is configured `AllowCredentials()`, implying a session that does not exist. | `DraftManager.cs:14`; `Program.cs:28` |
| **B20** | Drafts and history are written to the API process's working directory rather than a dedicated `var/` path. | `DraftManager.cs:14`; `OrderHistoryService.cs:16` |
| **B21** | **`IOrderModule.BuildPayload` / `Validate` are stubs returning empty**, so the interface is decorative. `OrderController` bypasses it with a `switch` on module key. Module-key string comparisons remain in `OrderController`, `LookupController`, `ValidationController`, and `FlatOrderValidator` — the `Capabilities` abstraction from the plan was never implemented. | `UpcEcommerceModule.cs:96-104`; `OrderController.cs:242-262` |
| **B22** | Inconsistent error contract: `LookupController` returns **HTTP 200** with `{success:false}` on database failure, while `ValidationController` returns 400 for the same class of error. | `LookupController.cs:56-59` vs `ValidationController.cs:37-40` |
| **B23** | Scaffolding never removed: `WeatherForecastController.cs`, `WeatherForecast.cs`, `Class1.cs` (×2), `UnitTest1.cs`, `OnlineOrderTool.Api.http`. | `backend/src/**` |

### 2.6 Frontend — S3

| # | Defect | Evidence |
|---|---|---|
| **B24** | **The approved design direction was not followed.** The brief was *bold gradient / vibrant* — saturated gradients, large rounded cards, colourful status pills, spring animations, big count-up stat tiles. Delivered is dark **glassmorphism**. No `--grad-*` token set, no status-pill gradient map, no stat tiles, no mesh hero. | `frontend/src/styles/_glassmorphism.css`, `_variables.css` |
| **B25** | **The module list is hardcoded in the frontend** (~90 lines of environments, logos and URLs) instead of consuming `/api/modules`. Guaranteed drift between the two definitions. | `module.service.ts:40-130` |
| **B26** | `environment.ts` hardcodes `http://localhost:5200/api`; there is no `environment.production.ts` and no `fileReplacements`, so the **production bundle calls localhost**. `cmd.md` additionally documents port 5000 while `launchSettings.json` binds 5200. | `environment.ts`; `angular.json`; `cmd.md` |
| **B27** | No dev proxy (`proxy.conf.json`); the app depends entirely on CORS. | `angular.json` `serve` block |
| **B28** | Assets duplicated: `frontend/src/assets/` is **not** in the build's asset globs (only `public` is), so those three SVGs are dead weight while `public/assets/` is the live copy. | `angular.json` `assets`; `frontend/src/assets/` |
| **B29** | Bootstrap Icons loaded **twice** — once from a CDN `@import url()` inside `_variables.css`, once from the npm package in `styles.css`. Also reintroduces an external CDN dependency. | `_variables.css:1`; `styles.css:1` |
| **B30** | Production build **exceeds its own budget**: 537.45 kB against a 500 kB ceiling. Warning on every build. | `npm run build` output |
| **B31** | Routes `api`, `database` and `test` all resolve to `FlatOrderComponent`; no route is lazy (`loadComponent`); no guards. | `app.routes.ts:16-22` |
| **B32** | The Order Requests feature is untyped (`signal<any[]>`, `any` throughout) and mixes `*ngIf` with `@for`. Its "cancelled" filter reads `responseStatusCode` — a local HTTP status — as though it were an order status. | `order-requests.component.ts` |

### 2.7 Testing & process — S3

| # | Defect | Evidence |
|---|---|---|
| **B33** | **No test compares a built payload against the reference JSONs.** The plan made this a hard gate. 21 tests pass while the payload is wrong, because the suite asserts against the invention. | `PayloadAndValidationTests.cs`; `grep -rn "request_examples" backend/tests/` → no source hits |
| **B34** | Repository tests only assert argument-guard exceptions. **Zero SQL is exercised**, which is exactly why every invalid column name went unnoticed. | `RepositoryTests.cs` |
| **B35** | `docs/database-schema.md` presents the invented queries as verified. It contradicts the real introspected schema and must be corrected or deleted. | `docs/database-schema.md` |
| **B36** | The working tree still carries the entire legacy Flask app at the repo root (`static/`, `templates/`, `tests/`, `requirements.txt`, `__pycache__/`) **plus** a copy in `_legacy_flask/`. `git status` shows 102 pending changes with unstaged deletes interleaved with staged renames — the migration was never committed cleanly. | `git status --short` |

---

## 3. Guiding decisions for the remediation

| # | Decision | Rationale |
|---|---|---|
| 1 | **Do not revert. Repair in place.** | Defects concentrate in ~8 files; the scaffolding is sound and re-deriving it wastes work. |
| 2 | **`docs/request_examples/*.json` is the payload contract.** Key-for-key comparison tests are written **first** (R0) and must fail before any builder is touched. | The 21 green tests are precisely what let a wrong payload ship. Invert that. |
| 3 | **`docs/Prompts/UPC_Enhancments_Plan.md` §"Schema discovery" is the SQL contract.** `_legacy_flask/modules/flat_order.py` is the behavioural reference for every query. | Both are live-introspected and known-good. Nothing may be invented. |
| 4 | **`OrderRequests` is the sole source for the Order Requests page.** `OrderHistoryService` and its JSON files are deleted, not extended. | The original instruction, and the local log cannot see orders from other channels. |
| 5 | **Keep `_legacy_flask/` until R10.** | It is the only executable reference for the correct behaviour. |
| 6 | **Introduce `Capabilities` and remove every module-key string comparison.** | Already specified in the plan and skipped; it is what makes the GHC path a one-line flip when credentials arrive. |
| 7 | **Secrets move to user-secrets / environment variables; no credential in any tracked file.** | They are already in git history — call that out to the user separately; rotation is their decision. |
| 8 | **Swap glassmorphism for the approved bold-gradient system in one pass (R8)**, before the Order Requests page is rebuilt on top of it. | Rebuilding the page twice is wasted effort. |

---

## 4. Session plan

Ordered so the contract is pinned before anything is rewritten against it, and so the app is never broken for long. Each session ends with a green build, a green test run, and a clean commit.

| # | Goal | Key files | Gate |
|---|---|---|---|
| **R0** | **Ground truth & safety net.** Commit the migration cleanly. Purge secrets from tracked files into user-secrets/env. Delete scaffolding (`WeatherForecast*`, `Class1.cs` ×2, `UnitTest1.cs`). Correct `docs/database-schema.md` to the verified schema. Copy `docs/request_examples/**` into `backend/tests/fixtures/` and add **failing** key-for-key payload tests + a SQL-column-name assertion test. Bump `Microsoft.OpenApi`. **No production logic changes.** | `.gitignore`, `appsettings*.json`, `*.csproj`, `backend/tests/fixtures/**`, `ContractTests.cs`, `docs/database-schema.md` | `dotnet build` clean, no NU1903; new contract tests **fail** with a clear key diff; `git ls-files \| grep -i password` empty |
| **R1** | **Rebuild the flat-order payload builders** against the reference: full key set, correct value domains (`"new"`, `"done_payment"`, `is_delivery` as int), computed totals, `order_gps`, client identity block, `order_creation_date`. Line items gain `unit_vat_amount`, `total_vat_amount`, `row_total_discount`, `row_net_total`. GHC payments nest `credit_customer_info` and carry `card_name`/`bank_code`. Single builder + variant flags, not two copies. | `FlatOrderPayloadBuilder.cs`, `Product.cs`, `Payment.cs`, `OrderDraft.cs` | R0 contract tests **pass** key-for-key for GHC and UPC |
| **R2** | **Rebuild the validator and totals** against the corrected schema. Port the real payment rules from `_legacy_flask` (COD → `not_payment`; Visa/wallets/points → `done_payment` at full total; PostToCredit → `not_payment`, GHC-only). Validate against the **computed summary**, not a phantom `total_paid`. Wire `edge_cases.json` (must pass) and `invalid_payloads.json` (must error) as parametrised tests. | `FlatOrderValidator.cs`, `TotalsCalculator.cs`, `UniCommerceValidator.cs` | Fixture-driven validation tests green |
| **R3** | **Rebuild the item and consumer repositories** from the legacy SQL verbatim: UPC branch-ranked CTE, GHC barcode + `NativeName` join, `Consumers` + `LoyaltyConsumerAddresses` `OUTER APPLY` preferring `IsMaster`. Port `normalize_upc_material_number` (6↔18 digit) and `normalize_phone_search` (last-9-digit `RIGHT()` match). | `UpcItemRepository.cs`, `FlatOrderItemRepository.cs`, `UpcConsumerRepository.cs`, `GhcConsumerRepository.cs`, `Normalizers.cs` | Live lookup against UPC Testing returns a real item at a real branch and a real consumer with address |
| **R4** | **Build `OrderRequestRepository` on the real `OrderRequests` table.** `FROM OrderRequests R` + `OUTER APPLY TOP 1` onto headers and invoices. List query selects `DATALENGTH(RequestJson)` and a `has_response` flag — **never the blobs**; `OFFSET/FETCH` paging ordered by `R.Id DESC`; one shared `_BuildFilters` for list/count/stats. Detail query is the only place reading `RequestJson`, **`ResponseJson`** and **`ExceptionMessage`**. Plus attempts, lineage, branches. | `OrderRequestRepository.cs`, `OrderRequestDtos.cs` | Live query returns rows; a failed request exposes a non-null `ExceptionMessage`; a test asserts the list SQL contains `DATALENGTH` and **not** `ResponseJson` |
| **R5** | **Order Requests API + fix cancel and resend.** New `OrderRequestsController`: list, detail, by-order, branches, cancel, resend. Cancel resolves `custom → endpoint key → environment.CancelUrl` and re-checks `CANCEL_BLOCKED_STATUSES = {5,6,7,9}` server-side. Resend rebuilds from that order's stored `RequestJson` and overrides only `branch_code` — never the live draft, no draft mutation. **Delete `OrderHistoryService`, `HistoryController`, `OrderHistoryEntry` and the JSON files.** Introduce `Capabilities`; remove every module-key string comparison. | `OrderRequestsController.cs`, `OrderRequestService.cs`, `IOrderModule.cs`, `Capabilities.cs`, `OrderController.cs` | Cancel hits the **CancelOrder** URL (asserted with a stubbed client); status-9 cancel returns 409; `ghc_ecommerce` returns 501; `grep -rn '== "upc_ecommerce"' backend/src` empty |
| **R6** | **State, config and error contract.** Per-session drafts (session id cookie or explicit client id) instead of a process-global file; drafts to `var/drafts/`. One uniform error envelope from `ExceptionMiddleware`; controllers stop returning 200 on failure. Scope TLS bypass behind a config flag. Drop `ApiUrl` from `EnvironmentDto`. | `DraftManager.cs`, `ExceptionMiddleware.cs`, `Program.cs`, `ApiDtos.cs` | Two browser sessions hold independent drafts; DB failure returns a 5xx envelope, not 200 |
| **R7** | **Frontend contract layer.** Delete the hardcoded module list; consume `/api/modules`. Typed models generated from the API contract — no `any` in feature code. `environment.production.ts` + `fileReplacements`; `proxy.conf.json`; consolidate assets to `public/assets/`; remove the duplicate CDN icon import; lazy `loadComponent` on all routes; drop the duplicate `api`/`database`/`test` routes; fix the bundle budget. | `module.service.ts`, `core/models/*.ts`, `angular.json`, `environment*.ts`, `app.routes.ts`, `_variables.css` | `ng build --configuration production` under budget with zero warnings; no `localhost` in the prod bundle; `grep -rn ": any" src/app/features` empty |
| **R8** | **Design system swap to bold gradient / vibrant.** Replace `_glassmorphism.css` with `_tokens.css` + `_gradients.css`: brand/success/danger/info/muted gradients, mesh hero, radius and shadow scales, spring easings. Status-pill gradient map keyed to the 9 order statuses. Shared UI kit: `gradient-card`, `stat-tile` (count-up), `status-pill`, `json-tree`, `drawer`, `confirm-dialog`, `empty-state`, `skeleton`, `data-table`, `pagination`, `riyal`. Honour `prefers-reduced-motion`. Dev-only kitchen-sink route. | `frontend/src/styles/**`, `frontend/src/app/shared/ui/**` | Kitchen sink renders all 9 pills, all gradients, a json-tree with malformed input, drawer, dialog, skeletons; motion disabled with reduced-motion on |
| **R9** | **Rebuild the Order Requests page** on the real API: hero, four clickable count-up stat tiles, full filter bar serialised to query params, virtualised table with the specified columns, and the **route-driven detail drawer** (`:requestId`) with all six tabs — Overview, **Request**, **Response** (incl. the `ExceptionMessage` danger card), Line items, Payments, Invoice & lineage incl. the attempts timeline. Cancel dialog with required reason and all four response branches; resend dialog with branch selector. | `features/order-requests/**` | Row click deep-links; a failed request shows its exception; cancelling a test order flips the pill and dims the row; a status-9 cancel is blocked client- and server-side |
| **R10** | **Order builder wiring, decommission and docs.** Bind the builder UI to the corrected schema (client identity block, address, GPS, computed totals) and the corrected lookups. Remove the legacy Flask tree from the repo root and `_legacy_flask/`. Rewrite `README.md`, `cmd.md` (correct ports), `docs/api-spec.md`, `docs/database-schema.md`. | `features/flat-order/**`, root cleanup, docs | Full E2E: lookup → add → pay → send to UPC Testing → the sent order appears in Order Requests with matching `RequestJson` and a populated `ResponseJson` |

**Dependencies:** `R0 → R1 → R2` (contract chain) · `R0 → R3 → R4 → R5` (data chain) · `R7 → R8 → R9` (frontend chain, needs `R5`) · `R10` needs everything.

---

## 5. Risks

1. **R1 is the highest-risk session.** Correcting the payload changes what every downstream consumer expects. The contract tests from R0 must be green before R2 begins — do not proceed on a partial match.
2. **R3/R4 require live DB access** to `10.10.8.181` / `RmsMainTest2`. If unreachable, the session can still be completed against recorded fixtures, but must be re-verified live before R9.
3. **Credentials are already in git history.** Purging the working tree in R0 does not remove them from past commits. Rotating `sa` and rewriting history are the user's decisions and are out of scope here.
4. **GHC and Uni-Commerce database credentials remain unconfirmed**, and Uni-Commerce has no API URL. Order Requests will return live data for UPC only; the GHC path is built and capability-gated, awaiting a one-line flip.
5. **The corrected validator will reject drafts that previously "passed."** That is the fix working, not a regression.
6. **Deleting `OrderHistoryService` discards any locally-recorded history.** If `order_history_*.json` files exist with data worth keeping, archive them before R5.
