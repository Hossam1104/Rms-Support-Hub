UPC Enhancements — Execution Prompts

These are a 5-session execution sequence for the plan in `UPC_Enhancments_Plan.md`. Each prompt is self-contained (new sessions have no memory): it tells the agent to read the plan file first, assumes the prior sessions' work is already committed, and ends with a `Verify:` command and a "Report…" instruction. Run them in order — each builds on the last. All work is **UPC-only**; GHC E-Commerce must remain unchanged throughout.

------------------------------

Session 1 — DB foundation & live schema discovery

Read the plan at `UPC_Enhancments_Plan.md` in full for context, especially "Database facts", "Schema discovery", and section 2 (Per-environment DB config). This is session 1 of 5; nothing has been built yet. Read `modules/db_config.py`, `modules/base.py`, `modules/upc_ecommerce.py`, and `modules/flat_order.py` (the `FlatOrderDatabaseManager.get_db_connection` helper at lines 40-85) before starting.

1. Write a throwaway introspection script (put it in the scratchpad, not the repo) that connects to server `10.10.8.181`, database `RmsMainTest2`, user `sa`, password `P@ssw0rd`, driver `ODBC Driver 17 for SQL Server`, reusing the driver-fallback connection-string pattern from `modules/flat_order.py:40-85`. Run `SELECT TOP 1 *` against each of: `Consumers`, `LoyaltyConsumerAddresses`, `OrderRequests`, `RequestOrderHeaders`, `RequestOrderDetails`, `RequestOrderTransactions`, `Invoices`. Print each table's column names.
2. Fill the real column names into the "Schema discovery" section of `UPC_Enhancments_Plan.md`, replacing every `_(TBD …)_` placeholder. Note in particular: the phone/name columns on `Consumers`, the `consumerId` FK + address columns on `LoyaltyConsumerAddresses`, the order-number and `orderHeaderId` columns on `RequestOrderHeaders`, and the `OnlineOrderNumber` / barcode / `CloseDateLocalTime` columns on `Invoices`. If the live DB is unreachable, stop and report that — do not proceed to write queries against guessed columns.
3. In `modules/db_config.py`, add two UPC configs — one for `RmsMainProd`, one for `RmsMainTest2` — both on server `10.10.8.181`, `sa`/`P@ssw0rd`, driver `ODBC Driver 17 for SQL Server`, each with env-var overrides (`UPC_ECOM_PROD_*` / `UPC_ECOM_TEST_*`) following the existing `_db_config` prefix convention. Keep the existing `upc_ecommerce` key working (fall back to the test config) so nothing that reads `DB_CONFIGS["upc_ecommerce"]` breaks.
4. In `modules/upc_ecommerce.py`, assign the prod config to the `UPC Production` environment and the test config to `UPC Testing`. Replace the single `self._db` built at init with a per-environment DB-manager cache, and select by the active environment.
5. Make lookups environment-aware: add an optional `env_key: Optional[str] = None` to `lookup_consumer_by_phone` and `lookup_item` in `modules/base.py` and in the UPC module, and have the UPC module resolve the correct per-env DB manager. Update the callers in `app.py` (`module_get_consumer_details` at 426-443, `module_get_item_details` at 394-423) to pass `get_active_environment(module).key`. Do NOT change GHC's module — its two environments legitimately share one config.

Verify: `python -c "from modules import MODULE_REGISTRY; m=MODULE_REGISTRY['upc_ecommerce']; print(m.environments['UPC Production'].db_config['database'], m.environments['UPC Testing'].db_config['database'])"` prints `RmsMainProd RmsMainTest2`. Also confirm `verify_payload.py` still passes.

Report what you built, the discovered column names you wrote into the plan, and any deviations. Flag clearly if the DB was unreachable.

------------------------------

Session 2 — UI rearrange + live consumer lookup

Read the plan at `UPC_Enhancments_Plan.md` in full, especially sections 1 (UI rearrange) and 3 (Consumer lookup rewrite) and the now-filled "Schema discovery" section. This is session 2 of 5; session 1 wired per-environment UPC DB configs and made lookups env-aware. Read `flat_order.html` (cards at 167-380), `flat_order.js` (`prefillConsumer` 352-367, `updateOrderField` 174-191), and `modules/flat_order.py`.

1. In `flat_order.html`, add the Payment Status select (currently at 365-376, bound to `order_payment_status`) into the Order Information card's `row g-3` before it closes at ~250, wrapped in `{% if module.key == 'upc_ecommerce' %}`.
2. Wrap the entire Delivery Information `grid-item` (341-380) in `{% if module.key != 'upc_ecommerce' %}`. Confirm GHC still renders From/To time + Payment Status there, and UPC no longer renders the card at all.
3. Add a UPC consumer query using the columns recorded in Session 1: select from `Consumers` by phone, LEFT JOIN `LoyaltyConsumerAddresses` on `consumerId`, returning the client fields plus address line(s). Implement it as a dedicated UPC method / `lookup_upc_consumer_by_phone`, reusing `FlatOrderDatabaseManager.get_db_connection()` and parameterized queries. Leave GHC's existing `dbo.Customers` lookup untouched.
4. Extend `module_get_consumer_details` (`app.py:426-443`) so the returned `consumer` dict includes the address field(s) for UPC.
5. Extend `prefillConsumer()` (`flat_order.js:352-367`) to also populate the address input (select by `[data-field="address"]`, it has no id) and persist it via `updateOrderField('address', …)`.

Verify: render `/modules/upc_ecommerce/` via a Flask `test_client` and assert the strings "Delivery Information" and the From/To time fields are absent while "Payment Status" is present; render `/modules/ghc_ecommerce/` and assert the Delivery Information card is still present. Then do a live phone lookup against `RmsMainTest2` (pick a real phone from the introspection) and confirm name + address come back.

Report what changed, the exact consumer SQL used, and any deviations. Note any field the DB returns that has no matching UI input.

------------------------------

Session 3 — Order Validation backend (search + details)

Read the plan at `UPC_Enhancments_Plan.md` in full, especially section 4 (Order Validation tab → Backend), the status decode map, the resend rule, and "Schema discovery". This is session 3 of 5; sessions 1-2 delivered env-aware DB access and the consumer lookup. Read `app.py` (route patterns, `require_flat_order_module` 103-107, `get_active_environment` 75-79, `module_send_request` 1204-1276) and the UPC DB manager.

1. Add a UPC-only guard helper (reject `module_key != "upc_ecommerce"` with `abort(400)`), mirroring `require_flat_order_module`.
2. Add DB-manager methods (env-aware) for: (a) `search_orders(filters, env_key)` — `OrderRequests` ⋈ `RequestOrderHeaders` by order number, LEFT JOIN `Invoices` on `RequestOrderHeaders.OrderNumber = Invoices.OnlineOrderNumber`, selecting order number, branch, status, order creation date, invoice barcode, and `CloseDateLocalTime`; and (b) `get_order_details(order_number_or_header_id, env_key)` — the header plus `RequestOrderDetails` and `RequestOrderTransactions` joined via `orderHeaderId`, plus invoice info. Build the search WHERE clause only from the criteria supplied (order number, client phone, branch code, status, date-from/date-to) using parameterized queries.
3. Add a module-level status decode map (`1..9` → label) and return both the numeric status and its label in results.
4. Add routes: `POST /modules/<module_key>/search-orders` and `GET /modules/<module_key>/order-details/<order_number>` (both UPC-guarded, both using the active environment's DB). Return JSON.
5. Add a server-side resend-eligibility check (`status not in {4, 8, 9}`) exposed in the details response, so the frontend can enable/disable the resend action, and re-checked wherever a resend is actually triggered.

Verify: with a Flask `test_client`, POST `/modules/upc_ecommerce/search-orders` with a known order number and print the rows; GET `/modules/upc_ecommerce/order-details/<that order>` and print header + details + transactions + invoice. Confirm status labels decode correctly and `resend_allowed` is false for a status-4/8/9 order.

Report the exact SQL for search and details, the response shapes, and any deviations.

------------------------------

Session 4 — Order Validation frontend + post-send auto-validation + resend

Read the plan at `UPC_Enhancments_Plan.md` in full, especially section 4 (Frontend) and section 5 (Post-send auto-validation). This is session 4 of 5; session 3 built `search-orders` and `order-details`. Read `flat_order.html` (nav 44-70, `tab-content` ending 819, script include at 825), `unicommerce.js` (the `window.MODULE_BASE` + `url()` class pattern), and `module_send_request` (`app.py:1204-1276`).

1. In `flat_order.html`, add a `{% if module.key == 'upc_ecommerce' %}` nav item pointing at `#validation-tab` and a matching `tab-pane` inside `tab-content`. The pane holds the search form (order number, client phone, branch code, status dropdown 1-9, date-from, date-to), a results grid, and a details modal.
2. Create `upc_validation.js` (a small class prefixing `window.MODULE_BASE`, same style as `unicommerce.js`) and include it only for UPC via a `{% if module.key == 'upc_ecommerce' %}<script>` tag after `flat_order.js`. Implement: submit search → render the grid (order number, branch, decoded status, order creation date, invoice barcode, invoice date, and a creation-vs-invoice comparison), one row per result with a Details button; Details → call `order-details` and populate the modal with line items + transactions; a "Resend to another branch" control shown only when `resend_allowed`, which posts to the existing send path with a chosen `branch_code`.
3. Wire post-send auto-validation: in `module_send_request`, for UPC only, after a successful send query `RequestOrderHeaders` (⋈ `Invoices`) by the sent order number and include the status/invoice info in the JSON response. In the send-result area, show the landed status inline with a link that activates `#validation-tab`.
4. Keep everything gated so GHC's send flow and page are byte-for-byte unchanged.

Verify: run the app (`http://localhost:5002`), open the UPC page, and manually walk: build an order → send → see the inline status → open the Order Validation tab → search by each criterion → open Details → confirm line items/transactions/invoice → confirm the resend control is hidden for a status-4/8/9 order.

Report the walkthrough result with what worked and any deviations; note anything that needs real data to exercise.

------------------------------

Session 5 — Verification pass & documentation

Read the plan at `UPC_Enhancments_Plan.md` in full, especially "Verification". This is session 5 of 5; sessions 1-4 delivered the DB, UI, consumer lookup, and Order Validation feature. Read `README.md`, `User_Tutorial.md`, and `verify_payload.py`.

1. Run the full end-to-end flow on the UPC page (build → send → inline status → search → details → resend-eligibility) and confirm the GHC page is unchanged (Delivery Information card intact, consumer lookup unchanged).
2. Update `README.md`: document the UPC per-environment DB split (`RmsMainProd` / `RmsMainTest2`), the live consumer lookup against `Consumers`/`LoyaltyConsumerAddresses`, and the Order Validation tab (criteria, status codes, resend rule, invoice comparison).
3. Update `User_Tutorial.md` with the end-user workflow for the Order Validation tab and the post-send status read-back.
4. Extend `verify_payload.py` (or add a small `verify_upc.py`) to assert the UPC template no longer renders the Delivery Information card and that the env-aware DB config resolves `RmsMainProd`/`RmsMainTest2` per environment.
5. Do a final gated-scope audit: grep for every new UPC branch and confirm no code path affects GHC/other modules.

Verify: `python verify_payload.py` passes; the new UPC assertions pass; a fresh reading of `/modules/ghc_ecommerce/` shows no behavioral change.

Report the final state, everything verified live vs. only structurally, and any follow-ups (e.g. columns that turned out different from expectations, or data the test DB lacked).
