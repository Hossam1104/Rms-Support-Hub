Refactor: Multi-Module Order Tool (GHC E-Commerce, UPC E-Commerce, GHC Uni-Commerce, OMS/Call Center stubs)
Context
The tool today (app.py, managers.py, config.py, index.html, script.js) is built around one hard-coded JSON schema (branch_code, order_products, payment_methods_with_options, ...) and one global Flask-session state. CLIENT_ENDPOINTS in config.py already lists UPC/GHC Production+Testing as separate URLs, but they all funnel through the same OrderManager.prepare_order_data() serializer and the same DatabaseManager (one global DB_CONFIG).

Comparing the example payloads confirms two real, incompatible schemas exist today, not one:

request_examples/GHC E-Commerce/request_body.json and everything in request_examples/UPC/ share one flat schema — this is what managers.OrderManager already builds. GHC E-Commerce and UPC E-Commerce are the same shape, different URL/DB only.
request_examples/GHC Uni-Commerce/*.json (15 examples incl. returns/partial-returns across Amazon, Aramex, Naqel, SMSA, Tabby, Tamara, Trendyol, Redbox, OwnFleet) is a completely different, flat invoice schema: ReferenceNumber, OnlineOrderNumber, IsReturn/ParentReferenceNumber, InvoiceConsumer{FirstName, MiddleName, LastName, ConsumerCode, Gender, BirthDate, PrimaryPhoneNumber, Email, NationalId, Nationality}, DeliveryDetails{DeliveryPhoneNumber, DeliveryAddress, DeliveryLocationUrl, DeliveryNotes, DeliveryFees}, RowItems[{Quantity, MaterialNumber, ItemPrice, ItemDiscount, RowTotalDiscount, ItemVat, RowTotalVat, BatchNumber, ExpireDate, SerialNumber, Barcode, ScannedCode, GrossAmount, NetAmount, VatPercentage, OfferIdentifier}], and top-level TotalDiscount/TotalVat/GrossAmount/NetAmount/CustomerName/CustomerCreditAmount/PaidOnlineAmount/PaidWithPointsAmount instead of a payment_methods_with_options array. There is no way to represent this in the current OrderManager/session shape.
The requested refactor turns this into a module system: each client integration (GHC E-Commerce, UPC E-Commerce, GHC Uni-Commerce now; OMS and Call Center as disabled "coming soon" placeholders) gets its own request schema, its own send/cancel URL(s), its own DB connection, and its own view — instead of one API/schema serving everyone. Real DB connection strings will be supplied later; this plan scaffolds per-module DB config now (env-var based, placeholders) so wiring in credentials later is a config-only change. New requirement: item lookup by code (existing) and consumer/customer lookup by phone number (new) per module.

Decisions confirmed with the user:

Keep the current stack — Flask + Jinja + vanilla JS + Bootstrap — restructured into a landing page (module picker) + one dedicated view per module. No SPA/build-pipeline rewrite.
Build GHC Uni-Commerce strictly from the schema inferred out of the 15 example payloads above (the source PDF couldn't be parsed in this environment).
Each module keeps its own session draft and its own autosave file (last_order_<module>.json), so switching modules on the landing page doesn't clobber other in-progress orders.
Scaffold a per-module DB config + a per-module "Test Connection" UI now, even though real credentials arrive later.
Target architecture
Introduce a modules/ package, one file per module, each exposing a common interface so app.py stops special-casing any one client:

modules/
  __init__.py          # MODULE_REGISTRY: dict[module_key] -> module definition
  base.py              # shared dataclasses/protocols: ModuleDefinition, OrderModule ABC
  ghc_ecommerce.py      # wraps the existing flat-order schema
  upc_ecommerce.py      # same schema as ghc_ecommerce, different config -> can literally subclass/reuse ghc_ecommerce's serializer+validator, only config differs
  ghc_unicommerce.py    # new invoice/RowItems schema
  oms.py                # stub: available=False, no logic yet
  call_center.py        # stub: available=False, no logic yet
Each module exposes:

key, display metadata (label, logo, accent, environments: Production/Testing with their own api_url/cancel_url/db config) — this replaces CLIENT_ENDPOINTS in config.py, restructured to be nested under modules instead of a flat dict of 6 hand-written entries.
default_state() — the module's blank/default draft shape (replaces the one global get_default_data()).
DatabaseManager (module-scoped): lookup_item(code, **filters) (reuse the existing SQL pattern from managers.py:94-165) and new lookup_consumer_by_phone(phone) — scaffolded with a placeholder query shape (mirrors the existing item query style) marked with a TODO(db-creds) until the user supplies real schema/connection details.
serializer.build_payload(state) — turns session draft state into the exact API JSON (this is where GHC/UPC E-Commerce reuse OrderManager's current logic almost verbatim, and GHC Uni-Commerce gets new logic).
validator.validate(state) — module-specific business rules (the existing COD/Visa/PostToCredit/digital-wallet rules in managers.py:594-648 apply only to the flat-order modules; Uni-Commerce needs its own, simpler rule set since it has no payment-method list, just PaidOnlineAmount/PaidWithPointsAmount/CustomerCreditAmount reconciliation against NetAmount).
config.py keeps the payment-method/status constants (still used by the two flat-order modules) but the CLIENT_ENDPOINTS/API_URLS/CANCEL_API_URLS/CLIENT_OPTIONS structures move into modules/*.py + a DB_CONFIGS map keyed by module_key with env vars like GHC_ECOM_DB_SERVER, UPC_ECOM_DB_SERVER, GHC_UNICOM_DB_SERVER, etc. (placeholders/defaults identical in shape to today's single DB_CONFIG).

Session & persistence model
Replace the single session['order_data'/'products'/'payments'] with a per-module namespace:

session['modules'][module_key] = { ... module-specific draft shape ... }
session['active_module'] = module_key   # set by the landing page selection
OrderManager.initialize_session_data() becomes module-aware: it only initializes the active module's draft if missing, seeding from that module's own autosave file (last_order_<module_key>.json) or its default_state(). save_last_order() likewise writes to the module-specific file. This directly extends the existing pattern in managers.py:246-334, just parameterized by module_key instead of hard-coded last_order.json.

Routes (app.py)
/ — becomes the landing page: module/environment picker. Reuses/extends the existing (currently unused by any template) select-client POST handler at app.py:75-109 — wire it up to real cards this time, generalized to read from MODULE_REGISTRY instead of CLIENT_ENDPOINTS, and persist session['active_module'].
/modules/<module_key>/ — the module's order-builder view (was the single index.html); renders the flat-order template for ghc_ecommerce/upc_ecommerce and a new unicommerce.html template for ghc_unicommerce. OMS/Call Center cards stay disabled (available: False) and are not routable, matching today's "Whites UniCommerce ... available: False" pattern in config.py:72-99.
Existing item/product/payment endpoints (/get-item-details, /add-product, /update-product, /add-payment, ...) get a <module_key> prefix or read session['active_module'] to dispatch to the right module's DB manager/serializer — same handler bodies, parameterized dispatch instead of always calling the single global DatabaseManager/OrderManager.
New: /modules/<module_key>/get-consumer-details?phone=... — calls that module's lookup_consumer_by_phone, prefilling consumer/customer fields client-side the same way /get-item-details prefills a product row today (script.js:494 / app.py:212-240 as the pattern to mirror).
New: /modules/<module_key>/test-database-connection — generalizes app.py:771-833 to accept/target a specific module's DB config placeholder.
/send-request and /cancel-order stay conceptually the same (APIManager.send_order in managers.py:651-693 is already schema-agnostic — it just POSTs whatever JSON it's given) but pull the URL and payload-building from the active module instead of the global API_URLS/OrderManager.
Frontend
New landing template (module/environment picker), replacing the never-wired CLIENT_OPTIONS context data (app.py:853-863) with actual cards — reuse the accent/icon/status_label styling already defined in config.py:121-143 and style.css.
templates/flat_order.html (+ its own flat_order.js) — refactor of today's index.html/script.js, used by both ghc_ecommerce and upc_ecommerce (branding/logo/URL swapped via module context, logic identical).
templates/unicommerce.html (+ unicommerce.js) — new view: consumer lookup-by-phone box (auto-fills InvoiceConsumer), delivery-details subform, return toggle (IsReturn + conditional ParentReferenceNumber field), row-item table matching RowItems fields (barcode/material lookup reusing the same "fetch item by code" UX pattern as today, plus batch/expiry/serial inputs), and payment summary fields as plain inputs (PaidOnlineAmount, PaidWithPointsAmount) instead of the payment-method list UI.
Shared partials (theme toggle, flash messages, sidebar shell) factored out of the current 1142-line index.html so both views don't duplicate chrome.
Verification
python verify_payload.py-style script per flat-order module (reuse/extend the existing structural assertions in verify_payload.py) to confirm ghc_ecommerce/upc_ecommerce payloads still match request_examples/GHC E-Commerce/request_body.json / request_examples/UPC/*.json field-for-field.
New equivalent script asserting a built Uni-Commerce payload's keys/shape match the union of fields seen across request_examples/GHC Uni-Commerce/*.json (including the IsReturn/ParentReferenceNumber and partial-return cases).
Manual run: python app.py, walk the landing page → pick each of the 3 available modules → confirm item lookup, (placeholder) consumer lookup, add/edit rows, totals, and /export-json all reflect the right schema per module; confirm OMS/Call Center cards render disabled and are not routable.
Confirm switching modules and back preserves each module's separate draft (last_order_<module_key>.json files created independently).