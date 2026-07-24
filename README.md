# Online Order Tool

A Flask web application for building and sending pharmacy order/invoice payloads to multiple client APIs. It is organized as a **module system**: each client integration has its own request schema, its own send/cancel URLs, its own database connection, and its own dedicated view — instead of one API/schema serving everyone.

## 🧩 Module Architecture

The app is driven by a module registry (`modules/__init__.py` → `MODULE_REGISTRY`). Each module implements a common interface (`modules/base.py` → `OrderModule`): `default_state()`, `build_payload()`, `validate()`, `lookup_item()`, and `lookup_consumer_by_phone()`.

| Module | Key | Schema | Status |
|---|---|---|---|
| **GHC E-Commerce** | `ghc_ecommerce` | Flat-order (products + payment methods) | ✅ Live |
| **UPC E-Commerce** | `upc_ecommerce` | Flat-order (identical schema, different config) | ✅ Live |
| **GHC Uni-Commerce** | `ghc_unicommerce` | Invoice (`RowItems` / `InvoiceConsumer` / `DeliveryDetails`) | ✅ Live (API URL pending) |
| **OMS** | `oms` | — | 🚧 Coming soon |
| **Call Center** | `call_center` | — | 🚧 Coming soon |

- **Flat-order modules** (`ghc_ecommerce`, `upc_ecommerce`) share one serializer/validator/DB implementation in `modules/flat_order.py` (their schemas are identical; only URLs/DB differ). They use the shared view `flat_order.html` + `flat_order.js`.
- **GHC Uni-Commerce** has its own serializer/DB in `modules/ghc_unicommerce.py` and its own view `unicommerce.html` + `unicommerce.js`, built from the `request_examples/GHC Uni-Commerce/` examples (including return / partial-return cases).
- Each module keeps its **own session draft and its own autosave file** (`last_order_<module_key>.json`), so switching modules on the landing page never clobbers another in-progress order.
- **UPC diverges from GHC in several ways**, all gated so GHC's page/behavior is untouched (see `Prompts/UPC_Enhancments_Plan.md` for the full design):
  - Its order form has no Delivery Information card — `delivery_date`, `delivery_from_time/to_time`, `shipping_address_2`, `fullfilment_plant` are GHC-only fields, and Payment Status lives in the Order Information card instead.
  - Its consumer lookup queries UPC's real `Consumers` / `LoyaltyConsumerAddresses` schema (joined on `ConsumerId`) instead of the shared placeholder `dbo.Customers` query GHC still uses.
  - Its item lookup is **branch-specific pricing** (`Items` / `BranchItemUnitOfMeasures` / `ItemUnitOfMeasurePrices` / `Branches`) and lives **inside the Add Product modal** (using the order's own Branch Code) instead of the separate Database Connection tab GHC still uses.
  - It has its own **Order Validation** tab (see below) and a **per-environment database** (below), neither of which exist for any other module.

### Routing

- `/` — landing page (module + environment picker).
- `/modules/<module_key>/` — the module's order-builder view.
- `/modules/<module_key>/...` — module-scoped actions (add/update/remove products or row-items, item lookup, consumer lookup by phone, calculate totals, export JSON, send/cancel, test DB connection, test endpoint).
- `/modules/upc_ecommerce/search-orders`, `/order-details/<order_number>`, `/resend-order` — **UPC-only**, power the Order Validation tab (see below).

## 🚀 Features

- **Module picker landing page** with per-environment (Production/Testing) selection.
- **Order / invoice builders** per module with product/row-item tables, customer/consumer forms, delivery details, and computed totals.
- **Item lookup by code** and **consumer lookup by phone number** per module (database-backed).
- **Payment processing** (flat-order modules): COD, Visa, digital wallets (Tamara, Tabby, MisPay, Emkan), points (RajhiPoints, QitafPoints, NeqatyPoints), PostToCredit — with real-time validation rules.
- **Uni-Commerce returns**: `IsReturn` toggle with `ParentReferenceNumber`; payment reconciliation (`PaidOnlineAmount` + `PaidWithPointsAmount` + `CustomerCreditAmount` = `NetAmount`).
- **JSON export & request preview** per module; **send / cancel** against the module's configured URLs.
- **Order Validation (UPC only)**: search sent orders, inspect their live status, and resend an eligible order to a different branch — see below.
- **Light/dark theme** with color options; responsive layout.

## 💳 Payment Logic (flat-order modules)

- **COD**: must have `not_payment` status. Customer name/number optional.
- **Visa / Digital Wallets / Points**: must have `done_payment` status with amount equal to the order total.
- **PostToCredit**: must have `not_payment` status.
- For COD, Visa, and PostToCredit, customer name/number fields appear automatically and are stored in `credit_customer_info` if provided.

## 🔎 Order Validation (UPC only)

UPC's order-builder page has an extra **Order Validation** tab (not present for any other module) that reads directly from UPC's order-request database — separate from, and read-only with respect to, the order-drafting flow.

**Post-send status.** Immediately after a successful send, the app looks up the order it just sent and shows its landed status inline in the API Response panel, with a link straight into the Order Validation tab.

**Search.** The tab's search form filters on any combination of: order number, client phone, branch code, status, and a creation-date range. Results show one row per matching order (a resent order — see below — shows as a separate row for its new branch), with the invoice barcode and invoice date shown alongside the order's creation date for a quick comparison of how long an order took to be invoiced.

**Order status codes** (`RequestOrderHeaders.OrderStatus`):

| Code | Meaning |
|---|---|
| 1 | New |
| 2 | Confirmed — pharmacist confirmed the order |
| 3 | Ready — ready to be executed |
| 4 | With_Delegate — executed and invoiced, out for delivery |
| 5 | Rejected — rejected by the pharmacy |
| 6 | CanceledByClient |
| 7 | CanceledByAdmin |
| 8 | Processing — in the POS cart |
| 9 | Done — executed and invoiced (picked up in store) |

**Details.** Opening a result shows the full header plus its line items (`RequestOrderDetails`) and payment transactions (`RequestOrderTransactions`), joined via `orderHeaderId`.

**Resend to another branch.** An order can be resent to a different branch unless its status is `4` (With_Delegate), `8` (Processing), or `9` (Done) — this rule is enforced both in the UI (the resend control is hidden otherwise) and re-checked server-side before the resend is attempted. Resending does **not** reuse the order currently loaded in the Order Dashboard tab — it rebuilds the exact original payload from that order's own last-sent request log (`OrderRequests.RequestJson`) and overrides only the branch code, so it can never accidentally send whatever order happens to be open elsewhere in the app.

## 📋 Prerequisites

- Python 3.8 or higher
- SQL Server with the `RMSCashierSrv` database (for item/consumer lookups)
- ODBC Driver for SQL Server (17/18 recommended)

## 🛠️ Installation

```bash
git clone <your-repository-url>
cd online-order-tool

# (Recommended) virtual environment
python -m venv venv
venv\Scripts\activate          # Windows
# source venv/bin/activate     # macOS/Linux

pip install -r requirements.txt
python app.py
```

The application is available at **http://localhost:5002**.

## ⚙️ Configuration

### Database credentials (per module)

> ⚠️ **Still pending for GHC/GHC Uni-Commerce:** real database credentials are not yet supplied for those modules. Each has its own config block in `modules/db_config.py`, sourced from environment variables with these prefixes — wiring in real credentials later is a **config-only change**:

| Module | Env var prefix |
|---|---|
| GHC E-Commerce | `GHC_ECOM_DB_*` |
| GHC Uni-Commerce | `GHC_UNICOM_DB_*` |

Each prefix supports `..._DB_SERVER`, `..._DB_DATABASE`, `..._DB_USERNAME`, `..._DB_PASSWORD`, `..._DB_DRIVER`. Defaults mirror the previous single global config (`server=.`, `database=RMSCashierSrv`, `username=sa`). The item/consumer lookup SQL in `modules/flat_order.py` (GHC's query) and `modules/ghc_unicommerce.py` is marked `# TODO(db-creds)` until the real schema is confirmed.

**UPC E-Commerce is confirmed and live**, and is the one module where Production and Testing point at **two different databases** on the same server/credentials, rather than sharing one config like every other module:

| Environment | Env var prefix | Default database |
|---|---|---|
| UPC Production | `UPC_ECOM_PROD_DB_*` | `RmsMainProd` |
| UPC Testing | `UPC_ECOM_TEST_DB_*` | `RmsMainTest2` |

Both default to server `10.10.8.181` (override with `..._DB_SERVER`). UPC's consumer lookup queries `Consumers` (by `PhoneNumber`) LEFT JOINed to `LoyaltyConsumerAddresses` (via `ConsumerId`, preferring the row flagged `IsMaster`) — this is real, confirmed schema, not a placeholder. The Order Validation tab queries `OrderRequests`, `RequestOrderHeaders`, `RequestOrderDetails`, `RequestOrderTransactions`, and `Invoices` on the same per-environment database. UPC's item lookup (in the Add Product modal) queries `Items` / `BranchItemUnitOfMeasures` / `ItemUnitOfMeasurePrices` / `Branches` for **branch-specific pricing** — a material number and a branch code are both required. Material numbers are always 18 digits (12 leading zeros + a 6-digit item number); the UI accepts either the full 18-digit code or just the 6 digits.

### API endpoints

Send/cancel URLs live in each module's definition (`modules/ghc_ecommerce.py`, `modules/upc_ecommerce.py`, `modules/ghc_unicommerce.py`). GHC/UPC URLs are configured; the **GHC Uni-Commerce API URL is pending** (its environments are present but `api_url=None` until the real routing is supplied).

## 🗂️ Project Structure

```
online-order-tool/
├── app.py                    # Flask app: module-aware routing + session model
├── config.py                 # Shared constants (payment methods/statuses, AppConfig)
├── managers.py               # Module-aware session/persistence layer + APIManager
├── requirements.txt
├── modules/
│   ├── __init__.py           # MODULE_REGISTRY
│   ├── base.py               # OrderModule interface + ModuleEnvironment
│   ├── db_config.py          # Per-module DB configs (env-var based)
│   ├── flat_order.py         # Shared flat-order serializer/validator/DB
│   ├── ghc_ecommerce.py      # GHC E-Commerce module
│   ├── upc_ecommerce.py      # UPC E-Commerce module
│   ├── ghc_unicommerce.py    # GHC Uni-Commerce module (invoice schema)
│   ├── oms.py                # OMS stub (coming soon)
│   └── call_center.py        # Call Center stub (coming soon)
├── landing.html              # Module/environment picker
├── flat_order.html / .js     # Flat-order view (GHC/UPC)
├── upc_validation.js         # Order Validation tab logic (UPC only, loaded conditionally)
├── unicommerce.html / .js    # Uni-Commerce invoice view
├── partials/                 # Shared chrome (head, flash, sidebar)
├── verify_payload.py         # Flat-order schema verification + UPC-only structural checks
├── verify_unicommerce.py     # Uni-Commerce schema verification (incl. returns)
├── request_examples/         # Reference API payloads
├── Prompts/
│   ├── UPC_Enhancments_Plan.md    # Design doc for the UPC-only features above
│   └── UPC_Enhancments_Prompts.md # Session-by-session execution log for that plan
└── README.md
```

## ✅ Verification

```bash
python verify_payload.py        # flat-order modules vs reference schema, plus UPC-only structural checks
python verify_unicommerce.py    # uni-commerce module vs reference (incl. return)
```

## 🆘 Support

- Confirm dependencies are installed and ODBC drivers are available.
- Check that the configured API endpoints / database are reachable (use the in-app "Test Connection" / "Test Endpoints" tabs).
- Check application logs for detailed error messages.

## 📝 License

Internal use. Please consult your organization's licensing policies.
