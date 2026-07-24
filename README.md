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

### Routing

- `/` — landing page (module + environment picker).
- `/modules/<module_key>/` — the module's order-builder view.
- `/modules/<module_key>/...` — module-scoped actions (add/update/remove products or row-items, item lookup, consumer lookup by phone, calculate totals, export JSON, send/cancel, test DB connection, test endpoint).

## 🚀 Features

- **Module picker landing page** with per-environment (Production/Testing) selection.
- **Order / invoice builders** per module with product/row-item tables, customer/consumer forms, delivery details, and computed totals.
- **Item lookup by code** and **consumer lookup by phone number** per module (database-backed).
- **Payment processing** (flat-order modules): COD, Visa, digital wallets (Tamara, Tabby, MisPay, Emkan), points (RajhiPoints, QitafPoints, NeqatyPoints), PostToCredit — with real-time validation rules.
- **Uni-Commerce returns**: `IsReturn` toggle with `ParentReferenceNumber`; payment reconciliation (`PaidOnlineAmount` + `PaidWithPointsAmount` + `CustomerCreditAmount` = `NetAmount`).
- **JSON export & request preview** per module; **send / cancel** against the module's configured URLs.
- **Light/dark theme** with color options; responsive layout.

## 💳 Payment Logic (flat-order modules)

- **COD**: must have `not_payment` status. Customer name/number optional.
- **Visa / Digital Wallets / Points**: must have `done_payment` status with amount equal to the order total.
- **PostToCredit**: must have `not_payment` status.
- For COD, Visa, and PostToCredit, customer name/number fields appear automatically and are stored in `credit_customer_info` if provided.

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

> ⚠️ **Pending:** real per-module database credentials are not yet supplied. Each module has its own config block in `modules/db_config.py`, sourced from environment variables with these prefixes — wiring in real credentials later is a **config-only change**:

| Module | Env var prefix |
|---|---|
| GHC E-Commerce | `GHC_ECOM_DB_*` |
| UPC E-Commerce | `UPC_ECOM_DB_*` |
| GHC Uni-Commerce | `GHC_UNICOM_DB_*` |

Each prefix supports `..._DB_SERVER`, `..._DB_DATABASE`, `..._DB_USERNAME`, `..._DB_PASSWORD`, `..._DB_DRIVER`. Defaults mirror the previous single global config (`server=.`, `database=RMSCashierSrv`, `username=sa`). The item/consumer lookup SQL in `modules/flat_order.py` and `modules/ghc_unicommerce.py` is marked `# TODO(db-creds)` until the real schema is confirmed.

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
├── unicommerce.html / .js    # Uni-Commerce invoice view
├── partials/                 # Shared chrome (head, flash, sidebar)
├── verify_payload.py         # Flat-order schema verification
├── verify_unicommerce.py     # Uni-Commerce schema verification (incl. returns)
├── request_examples/         # Reference API payloads
└── README.md
```

## ✅ Verification

```bash
python verify_payload.py        # flat-order modules vs reference schema
python verify_unicommerce.py    # uni-commerce module vs reference (incl. return)
```

## 🆘 Support

- Confirm dependencies are installed and ODBC drivers are available.
- Check that the configured API endpoints / database are reachable (use the in-app "Test Connection" / "Test Endpoints" tabs).
- Check application logs for detailed error messages.

## 📝 License

Internal use. Please consult your organization's licensing policies.
