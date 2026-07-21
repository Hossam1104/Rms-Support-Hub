Here's an 8-session execution sequence for C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md. Each prompt is self-contained (new sessions have no memory of this conversation), assumes the prior sessions' work is already committed in the repo, and tells Sonnet to read the plan file first for full context. Run them in order — each builds on the last.

------------------------------

Session 1 — Scaffolding: module registry & config restructuring

Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context.
This is session 1 of 8 implementing that plan. Do ONLY the "Target architecture" scaffolding piece — no routes, no frontend yet.

1. Create a modules/ package: modules/__init__.py (MODULE_REGISTRY dict), modules/base.py (an OrderModule ABC / dataclass defining: key, label, environments with api_url/cancel_url/db_config per environment, available flag, default_state(), plus abstract hooks for serializer/validator/db lookups).
2. Create module definition files for all 5 modules described in the plan: ghc_ecommerce.py, upc_ecommerce.py, ghc_unicommerce.py, oms.py (available=False stub), call_center.py (available=False stub). For now these can have placeholder/minimal serializer & validator bodies (raise NotImplementedError) — the real logic comes in sessions 2-3. Port the existing CLIENT_ENDPOINTS entries from config.py (UPC Production/Testing -> upc_ecommerce, GHC Production/Testing -> ghc_ecommerce) into these module definitions' environments. Drop/replace the old "Whites UniCommerce" placeholder entries with the real ghc_unicommerce module (available=True, but api_url/db config left as None/placeholder since real URL isn't finalized in config yet — check config.py for whether a Uni-Commerce URL already exists before assuming it doesn't).
3. Add DB_CONFIGS scaffolding in config.py (or a new modules/db_config.py): one config block per module, sourced from module-prefixed env vars (GHC_ECOM_DB_*, UPC_ECOM_DB_*, GHC_UNICOM_DB_*), same shape as the existing DB_CONFIG dict in config.py, with the same defaults/placeholders pattern.
4. Do NOT touch app.py routes, index.html, or script.js yet — this session is additive scaffolding only. The existing app must still run exactly as before (import the new modules/ package but don't wire it into any route).
5. Verify: `python -c "from modules import MODULE_REGISTRY; print(MODULE_REGISTRY.keys())"` succeeds, and `python app.py` still starts and serves the app unchanged.

Report what you built and any deviations from the plan you had to make (e.g. if config.py already had partial Uni-Commerce config you didn't expect).

------------------------------

Session 2 — GHC E-Commerce & UPC E-Commerce module logic


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context.
This is session 2 of 8. Session 1 already created modules/base.py, modules/__init__.py, and stub files modules/ghc_ecommerce.py, modules/upc_ecommerce.py, modules/ghc_unicommerce.py, modules/oms.py, modules/call_center.py with placeholder serializer/validator bodies — read those files first to see the exact interface you need to implement.

Implement the real serializer + validator + db manager for ghc_ecommerce.py and upc_ecommerce.py:
1. Port the existing logic from managers.py's OrderManager (prepare_order_data, _determine_payment_status, _format_birthdate, _format_time, _prepare_products, _prepare_payments, validate_order_data — lines ~336-648) and ProductCalculator (calculate_product_totals, normalize_vat_percentage) into a shared implementation both modules use (ghc_ecommerce and upc_ecommerce have the IDENTICAL schema per request_examples/GHC E-Commerce/request_body.json and request_examples/UPC/*.json — only their config differs, so the serializer/validator code should be written once and reused by both module definitions, not copy-pasted).
2. Port DatabaseManager.lookup_item (managers.py lines ~94-165) into a module-scoped DB manager for these two modules, parameterized by that module's DB_CONFIGS entry from session 1.
3. Add a new lookup_consumer_by_phone(phone) method to that DB manager — scaffold it following the same query-building pattern as lookup_item (parameterized SQL, same connection-fallback approach via get_db_connection), querying for a customer by phone number. Since we don't have the real DB schema/table names confirmed for this yet, mark the exact table/column names with a `# TODO(db-creds): confirm real schema` comment and use a reasonable guess based on the existing dbo.Customers reference in lookup_item's customer_number filter (managers.py line ~136).
4. Implement default_state() for both modules matching config.py's get_default_data() shape.
5. Do NOT touch app.py, managers.py, or the frontend yet. managers.py should stay as-is for now (untouched) — it'll be deprecated/removed once app.py is rewired in session 4.
6. Verify: write a quick throwaway script (or extend verify_payload.py's approach) confirming a payload built via the new ghc_ecommerce serializer, given the same session-shaped input verify_payload.py uses, produces the same output shape/keys as today's managers.OrderManager.prepare_order_data(). Delete the throwaway script when done if you made one; don't leave scratch files in the repo root.

Report what you implemented and confirm the output matches the existing behavior.

------------------------------

Session 3 — GHC Uni-Commerce module logic


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context, especially the GHC Uni-Commerce schema description.
This is session 3 of 8. Sessions 1-2 built modules/base.py and the ghc_ecommerce/upc_ecommerce modules — read modules/base.py first to see the OrderModule interface you must implement, and read modules/ghc_ecommerce.py as a reference example of how a module is wired together.

Also read every file in "request_examples/GHC Uni-Commerce/" (15 example JSONs covering Amazon/Aramex/Naqel/SMSA/Tabby/Tamara/Trendyol/Redbox/OwnFleet, including return and partial-return variants) to confirm the full field set before writing code: ReferenceNumber, OnlineOrderNumber, IsReturn, ParentReferenceNumber, OrderCreationDate, TotalDiscount, TotalVat, GrossAmount, NetAmount, CustomerName, CustomerCreditAmount, PaidOnlineAmount, PaidWithPointsAmount, InvoiceConsumer{...}, DeliveryDetails{...}, RowItems[{...}].

Implement modules/ghc_unicommerce.py fully:
1. default_state(): a session-draft shape covering all the above fields (mirror the pattern used in ghc_ecommerce's default_state but for this schema).
2. Serializer (build_payload(state)): computes GrossAmount/NetAmount/TotalDiscount/TotalVat from RowItems the same way the examples imply (cross-check your math against at least 3 of the example files' RowItems vs top-level totals to confirm the aggregation formula), and assembles the final payload matching the exact key set/nesting seen in the examples.
3. Validator: business rules appropriate to this schema — e.g. if IsReturn is true, ParentReferenceNumber must be present; PaidOnlineAmount + PaidWithPointsAmount + CustomerCreditAmount should reconcile against NetAmount within rounding tolerance (look at how the examples relate these fields — CustomerCreditAmount appears to be the COD/credit portion, PaidOnlineAmount the card/wallet portion); RowItems must not be empty. Keep validation proportionate — don't invent rules not evidenced by the examples.
4. DB manager for this module: lookup_item(material_number) returning the fields needed to populate a RowItem (Quantity/MaterialNumber/ItemPrice/ItemVat/Barcode/VatPercentage at minimum — reuse the query pattern from ghc_ecommerce's DB manager as a starting point since it queries the same kind of Items table), plus lookup_consumer_by_phone(phone) populating InvoiceConsumer fields, both marked `# TODO(db-creds): confirm real schema` since real Uni-Commerce DB credentials/schema aren't provided yet.
5. Register this module properly in modules/__init__.py's MODULE_REGISTRY (session 1 may have left it as a stub — replace the stub entry, don't duplicate).
6. Verify: write a script asserting a payload built by your serializer against representative input matches the key set/shape of request_examples/GHC Uni-Commerce/AMAZON_AMZ-00011.json (non-return) AND request_examples/GHC Uni-Commerce/AMAZON_PARTIAL_RETURN_AMZ-RET-003.json (return case). Delete any throwaway scripts when done; don't leave scratch files in the repo root — a small dedicated test file is fine if placed sensibly.

Report the exact schema/validation decisions you made and flag anything ambiguous in the examples that you had to guess on.
Session 4 — Routing, session model, and app.py rewiring


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context, especially "Session & persistence model" and "Routes (app.py)".
This is session 4 of 8. Sessions 1-3 built out modules/__init__.py (MODULE_REGISTRY with ghc_ecommerce, upc_ecommerce, ghc_unicommerce fully implemented, oms/call_center as available=False stubs), each module exposing default_state(), a serializer, a validator, and a DB manager with lookup_item + lookup_consumer_by_phone. Read modules/base.py and all 5 module files to understand exactly what's available before touching app.py.

Rewire app.py and managers.py:
1. Replace the single session['order_data'/'products'/'payments'] model with session['modules'][module_key] + session['active_module'], as described in the plan. Update (or replace) OrderManager.initialize_session_data/save_last_order in managers.py to be module-aware, reading/writing last_order_<module_key>.json per module instead of the single last_order.json.
2. Rewire "/" to be the landing/module-picker route (backend only for now — it's fine if it renders a placeholder template this session; the real landing template comes in session 5). Generalize the existing /select-client handler (app.py lines ~75-109) to read from MODULE_REGISTRY instead of the old CLIENT_ENDPOINTS, and to set session['active_module'].
3. Add /modules/<module_key>/ as the module's order-builder route (can render a placeholder/minimal template this session; real templates come in sessions 6-7).
4. Generalize /get-item-details, /add-product, /update-product, /add-payment, /update-payment, /calculate-totals, /calculate-payment-summary, /export-json, /send-request, /cancel-order, /test-database-connection to dispatch based on session['active_module'] (or a <module_key> URL segment — pick whichever is cleaner given the /modules/<module_key>/ route structure you built) to the right module's DB manager / serializer / validator instead of the old global DatabaseManager/OrderManager/API_URLS.
5. Add the new /modules/<module_key>/get-consumer-details?phone=... route calling that module's lookup_consumer_by_phone.
6. Keep APIManager.send_order/test_endpoint (managers.py ~651-693) as-is — they're already schema-agnostic, just have callers pass the active module's URL + built payload.
7. It's OK if index.html/script.js are temporarily broken/mismatched at the end of this session — sessions 5-7 handle the frontend. Note clearly in your final report which routes/behaviors are backend-only-verified vs need frontend session catch-up.
8. Verify with curl/requests against the running app (python app.py) that: selecting a module via /select-client persists session['active_module']; /modules/ghc_ecommerce/get-item-details and /modules/ghc_unicommerce/get-item-details both work and return module-appropriate data (or the expected DB-connection-failed error, since real DB creds aren't wired yet); /modules/<key>/get-consumer-details is routable.

Report exactly which routes changed shape/URL so the frontend sessions know what to call.
Session 5 — Landing page + shared partials


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context.
This is session 5 of 8. Session 4 rewired app.py's routing: "/" is now the module-picker landing route (reading from modules.MODULE_REGISTRY), "/select-client" persists session['active_module'], and "/modules/<module_key>/" is the module order-builder route. Read app.py's current state and modules/__init__.py before starting, since the exact route names/payloads from session 4 are what you must match in the frontend.

Build:
1. A real landing page template (module/environment picker) rendered by "/", replacing whatever placeholder session 4 left. Cards for all 5 modules from MODULE_REGISTRY: 3 clickable (GHC E-Commerce, UPC E-Commerce, GHC Uni-Commerce) each showing their Production/Testing environment choice, 2 disabled "coming soon" (OMS, Call Center) — reuse the accent/icon/status_label visual pattern already defined in config.py's CLIENT_OPTIONS (check if config.py still has this or if it moved into modules/ in session 1) and style.css. Selecting a module+environment card should POST to /select-client then redirect to /modules/<module_key>/.
2. Extract shared chrome (theme toggle light/dark + color theme, flash messages block, sidebar shell/nav) out of the current 1142-line index.html into reusable Jinja partials/includes, so both the landing page and the per-module views (built in sessions 6-7) can include them without duplicating markup. Don't delete index.html yet — sessions 6-7 will replace its content; just factor out the chrome now so those sessions can jinja-include it.
3. Verify manually: run python app.py, load "/", confirm all 5 cards render with correct enabled/disabled states, click into each of the 3 available modules and confirm you land on /modules/<key>/ with the module correctly persisted in session (check via a subsequent request or dev tools).

Report what partials you extracted and their include paths, since sessions 6-7 need to reference them.
Session 6 — Flat-order view (GHC/UPC E-Commerce frontend)


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context.
This is session 6 of 8. By now: app.py routes are module-aware (/modules/<module_key>/... from session 4), the landing page exists with shared chrome partials (session 5), and modules/ghc_ecommerce.py + modules/upc_ecommerce.py have working serializers/validators/DB managers (session 2). Read the current index.html and script.js (the pre-refactor originals — they're still the templates being served for module order-builder views right now, just disconnected from the new routing) plus the shared partials from session 5 before starting.

Build templates/flat_order.html + a corresponding flat_order.js, served by /modules/ghc_ecommerce/ and /modules/upc_ecommerce/ (same template/JS for both — only branding/logo/URL differ, driven by the active module's config):
1. Port the existing order-builder UI from index.html (product table, payment table, customer/order-detail fields, totals sidebar, API config/test tabs) into flat_order.html, using the session-5 shared partials for chrome instead of duplicating it.
2. Port script.js's logic into flat_order.js, updating every fetch() call (item lookup, add/update product, add/update payment, calculate totals, export JSON, send request, test endpoint — see the endpoint list in script.js) to hit the new /modules/<module_key>/... URLs from session 4 instead of the old flat routes.
3. Add a consumer-lookup-by-phone UI element (input + lookup button near the customer fields) wired to the new /modules/<module_key>/get-consumer-details endpoint from session 4, prefilling first/last name, email, etc. on a match — mirror the existing item-lookup-by-code UX pattern (script.js ~line 494) for consistency.
4. Verify manually: run python app.py, pick GHC E-Commerce from the landing page, build a full order (add products via lookup, add payments, fill customer info, use consumer lookup), export JSON and confirm it matches the flat schema from request_examples/GHC E-Commerce/request_body.json. Repeat quickly for UPC E-Commerce to confirm branding swaps but behavior is identical.

Report any UI behavior from the old script.js you couldn't port 1:1 and why.
Session 7 — GHC Uni-Commerce view


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context, especially the "Frontend" section describing templates/unicommerce.html.
This is session 7 of 8. By now modules/ghc_unicommerce.py has a working serializer/validator/DB manager (session 3), app.py routes are module-aware (session 4), shared chrome partials exist (session 5), and session 6 built templates/flat_order.html as a reference example of how a module view is structured/wired to routes — read that file and its JS before starting, since this new view follows the same wiring pattern against a different schema.

Build templates/unicommerce.html + unicommerce.js, served by /modules/ghc_unicommerce/:
1. Consumer section: phone lookup box (wired to /modules/ghc_unicommerce/get-consumer-details) that prefills InvoiceConsumer fields (FirstName, MiddleName, LastName, ConsumerCode, Gender, BirthDate, Email, NationalId, Nationality), with manual entry fallback if no match.
2. Delivery details subform: DeliveryPhoneNumber, DeliveryAddress, DeliveryLocationUrl, DeliveryNotes, DeliveryFees.
3. Return toggle: IsReturn checkbox that reveals a ParentReferenceNumber field when checked (look at request_examples/GHC Uni-Commerce/AMAZON_PARTIAL_RETURN_AMZ-RET-003.json for the expected relationship).
4. Row-item table matching RowItems: item lookup by MaterialNumber/Barcode (reuse the same lookup UX pattern from flat_order.js, hitting /modules/ghc_unicommerce/get-item-details) plus manual fields for BatchNumber, ExpireDate, SerialNumber, ScannedCode, OfferIdentifier, Quantity, ItemPrice, ItemDiscount.
5. Order-level fields: ReferenceNumber, OnlineOrderNumber, CustomerName (the carrier/marketplace — consider a dropdown seeded from the values observed across the example files: AMAZON, ARAMEX, NAQEL, SMSAECOM, TABBY, TAMARA, TRENDYOL, REDBOX, OWNFLEET), PaidOnlineAmount, PaidWithPointsAmount — with CustomerCreditAmount/GrossAmount/NetAmount/TotalDiscount/TotalVat computed and shown read-only (calculated by the backend serializer, not hand-entered).
6. Wire submit to /modules/ghc_unicommerce/send-request the same way flat_order.js does, and export/JSON preview using the module's /export-json equivalent.
7. Verify manually: run python app.py, pick GHC Uni-Commerce from the landing page, build a non-return order and a return order (with ParentReferenceNumber), export/preview JSON for both and confirm the shape matches request_examples/GHC Uni-Commerce/AMAZON_AMZ-00011.json and .../AMAZON_PARTIAL_RETURN_AMZ-RET-003.json respectively.

Report any field from the example JSONs you couldn't confidently map to a UI control and why.
Session 8 — Cleanup & verification pass


Read the plan at C:\Users\Win11\.claude\plans\according-to-the-example-jolly-finch.md in full for context, especially the "Verification" section.
This is the final session (8 of 8) of this refactor. All 3 modules (ghc_ecommerce, upc_ecommerce, ghc_unicommerce) should now have working backend logic (sessions 2-3), module-aware routing (session 4), and dedicated frontend views (sessions 5-7). Read through app.py, managers.py, and modules/*.py in full to get the actual current state before making changes — prior sessions may have deviated from the plan in ways noted in their reports; trust the code over the plan where they conflict.

1. Remove now-dead code: the old global DatabaseManager/OrderManager logic in managers.py that's been fully superseded by modules/ghc_ecommerce.py and modules/upc_ecommerce.py (check nothing still imports the old paths before deleting — grep for `from managers import` across app.py and modules/*.py). Remove the old single last_order.json handling if fully replaced by per-module files. Remove old CLIENT_ENDPOINTS/API_URLS/CANCEL_API_URLS/CLIENT_OPTIONS from config.py if modules/__init__.py's MODULE_REGISTRY fully replaced them (only if nothing else references them).
2. Extend/rewrite verify_payload.py into one script per flat-order module (or one script covering both, since they share a serializer) plus a new script for ghc_unicommerce, asserting built payloads match the field-for-field shape of the relevant request_examples/ files, including the Uni-Commerce return/partial-return variants.
3. Do a full manual walkthrough: `python app.py`, land on "/", confirm all 5 module cards (2 disabled, 3 live), go through each of the 3 live modules end-to-end (item lookup, consumer lookup, build order/invoice including a Uni-Commerce return case, export JSON, send-request against whatever URL is configured — expect/accept a connection failure gracefully since real endpoints/DB creds aren't wired in yet, just confirm the request is built and attempted correctly), and confirm switching between modules preserves each one's separate draft (check last_order_<module_key>.json files are created independently and repopulate their respective module view on return).
4. Update README.md to describe the new module-based architecture (replace the outdated single-schema description) and note that DB credentials per module are pending and where to configure them (env vars per modules/db config).

Report a final summary: what's fully done, what's explicitly still pending on the user (real DB credentials, real Uni-Commerce API URL, OMS/Call Center specs), and any test/verification gaps.
A few notes on using these:

Session 1 is safe to run without confirmation — pure additive scaffolding.
Sessions 4, 6, 7 touch app.py/templates in ways that can temporarily break the running app mid-refactor — expected, since later sessions fix it.
You'll need to supply real DB connection strings and the actual GHC Uni-Commerce API URL before send-request/DB-lookup features work end-to-end; every session above scaffolds around that gap with TODO(db-creds) markers rather than blocking on it.