# Total Rewrite: .NET 10 Web API + Angular 22

## Background

The Online Order Tool is currently a **Python Flask + Jinja + vanilla JS + Bootstrap 5** app managing order creation for multiple modules (UPC E-Commerce, GHC E-Commerce, GHC Uni-Commerce, OMS/Call Center stubs). Two refactoring rounds are complete (module system + UPC enhancements).

This plan replaces the **entire stack**:
- **Backend**: Python Flask → **.NET 10 Web API** (C#, Controller-based `[ApiController]`)
- **Frontend**: Jinja templates + vanilla JS → **Angular 22** (TypeScript, standalone components, Angular CDK, custom CSS glassmorphism)
- **Database**: Same SQL Server connections (Dapper for data access)
- **API contracts**: Same external API payloads (GHC/UPC order schemas preserved exactly)

The existing Python codebase serves as the **reference implementation** — all business logic, SQL queries, validation rules, and module configurations are ported faithfully.

---

## User-Confirmed Architectural Decisions

- **Angular Version**: **Angular 22** (latest standalone architecture with Signals)
- **Backend Architecture**: **Controller-based .NET 10 Web API** (`[ApiController]`)
- **UI Design & Styling**: **Custom CSS Glassmorphism Design System** + Angular CDK for overlays/modals
- **Order Requests Scope**: **Per-Module** (each module maintains its own dedicated sent order history)
- **Cleanup**: Delete `.venv-1/` and runtime `last_order_*.json` files
- **Reference Preservation**: Keep `request_examples/` intact in `docs/request_examples/`

---

## Existing Business Logic Inventory (to port)

### Modules & Environments
| Module | Key | Environments | API URLs | DB |
|--------|-----|-------------|----------|-----|
| GHC E-Commerce | `ghc_ecommerce` | Production, Testing | `10.10.20.200`, `10.10.20.126:8090` | Shared GHC config |
| UPC E-Commerce | `upc_ecommerce` | Production, Testing | `10.10.10.181`, `10.10.9.181:8080` | `RmsMainProd`, `RmsMainTest2` on `10.10.8.181` |
| GHC Uni-Commerce | `ghc_unicommerce` | Production, Testing | Pending (unavailable) | Shared GHC config |
| OMS | `oms` | — | Stub (unavailable) | — |
| Call Center | `call_center` | — | Stub (unavailable) | — |

### Flat Order Schema (GHC + UPC)
`branch_code`, `order_code`, `parent_order_code`, `order_delivery_cost`, `is_delivery`, `order_status`, `order_payment_status`, `delivery_date`, `delivery_from_time`, `delivery_to_time`, `shipping_address_2`, `fullfilment_plant`, `order_notes`, client fields, `order_products[]`, `payment_methods_with_options[]`

**UPC differences**: No `delivery_date`, `delivery_from_time`, `delivery_to_time`, `shipping_address_2`, `fullfilment_plant`. UPC has branch-specific pricing. UPC has `PostToCredit` payment blocked.

### Uni-Commerce Invoice Schema (GHC)
`ReferenceNumber`, `OnlineOrderNumber`, `IsReturn`, `ParentReferenceNumber`, `OrderCreationDate`, `InvoiceConsumer{}`, `DeliveryDetails{}`, `RowItems[]`, `TotalDiscount`, `TotalVat`, `GrossAmount`, `NetAmount`, `CustomerCreditAmount`, `PaidOnlineAmount`, `PaidWithPointsAmount`

### UPC Order Validation
Queries `OrderRequests`, `RequestOrderHeaders`, `RequestOrderDetails`, `RequestOrderTransactions`, `Invoices` tables. Status codes 1-9. Resend blocked for status 4/8/9.

### Payment Rules
11 payment methods, each with sub-options. COD requires `not_payment` status. Visa/Tamara/Tabby require `done_payment`. PostToCredit (GHC only) requires customer info. Max one COD, digital wallets mutually exclusive with same type.

---

## Target Architecture

### Folder Structure

```
online_order_tool/
├── _legacy_flask/                          # Archived Python codebase (reference only)
│   ├── app.py, config.py, managers.py
│   ├── modules/
│   ├── *.html, *.js, *.css
│   └── ...
│
├── backend/                                # .NET 10 Web API
│   ├── OnlineOrderTool.sln
│   │
│   ├── src/
│   │   ├── OnlineOrderTool.Api/            # API host (controllers, middleware, DI)
│   │   │   ├── Controllers/
│   │   │   │   ├── ModuleController.cs      # GET /api/modules, GET /api/modules/{key}
│   │   │   │   ├── OrderController.cs       # Order CRUD, send, cancel, resend
│   │   │   │   ├── ProductController.cs     # Add/edit/remove products
│   │   │   │   ├── PaymentController.cs     # Add/edit/remove payments
│   │   │   │   ├── LookupController.cs      # Item lookup, consumer lookup
│   │   │   │   ├── ValidationController.cs  # UPC order validation search/details
│   │   │   │   └── HistoryController.cs     # Order request history
│   │   │   ├── Middleware/
│   │   │   │   └── ExceptionMiddleware.cs   # Global error handling
│   │   │   ├── Program.cs                   # DI registration, middleware pipeline
│   │   │   ├── appsettings.json             # Module configs, DB connections
│   │   │   ├── appsettings.Development.json
│   │   │   └── OnlineOrderTool.Api.csproj
│   │   │
│   │   ├── OnlineOrderTool.Core/           # Domain models + business logic
│   │   │   ├── Modules/
│   │   │   │   ├── IOrderModule.cs          # Module interface (= Python OrderModule ABC)
│   │   │   │   ├── ModuleEnvironment.cs     # Environment config model
│   │   │   │   ├── ModuleRegistry.cs        # Module registry (DI-friendly)
│   │   │   │   ├── FlatOrder/
│   │   │   │   │   ├── FlatOrderModule.cs   # Base for GHC/UPC (shared logic)
│   │   │   │   │   ├── GhcEcommerceModule.cs
│   │   │   │   │   ├── UpcEcommerceModule.cs
│   │   │   │   │   ├── FlatOrderPayloadBuilder.cs  # Serializer
│   │   │   │   │   ├── FlatOrderValidator.cs        # Validation rules
│   │   │   │   │   └── PaymentRules.cs              # Payment business rules
│   │   │   │   ├── UniCommerce/
│   │   │   │   │   ├── GhcUnicommerceModule.cs
│   │   │   │   │   ├── UniCommercePayloadBuilder.cs
│   │   │   │   │   └── UniCommerceValidator.cs
│   │   │   │   └── Stubs/
│   │   │   │       ├── OmsModule.cs
│   │   │   │       └── CallCenterModule.cs
│   │   │   ├── Models/
│   │   │   │   ├── OrderDraft.cs            # Session draft state
│   │   │   │   ├── Product.cs
│   │   │   │   ├── Payment.cs
│   │   │   │   ├── Consumer.cs
│   │   │   │   ├── UniCommerceInvoice.cs
│   │   │   │   ├── RowItem.cs
│   │   │   │   ├── OrderHistoryEntry.cs
│   │   │   │   └── OrderValidationResult.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── ModuleDto.cs
│   │   │   │   ├── EnvironmentDto.cs
│   │   │   │   ├── OrderStateDto.cs
│   │   │   │   ├── ProductDto.cs
│   │   │   │   ├── PaymentDto.cs
│   │   │   │   ├── SendOrderRequest.cs
│   │   │   │   ├── CancelOrderRequest.cs
│   │   │   │   ├── LookupResultDto.cs
│   │   │   │   ├── OrderSearchRequest.cs
│   │   │   │   └── OrderSearchResultDto.cs
│   │   │   ├── Services/
│   │   │   │   ├── IApiClient.cs            # HTTP client for external APIs
│   │   │   │   ├── ApiClient.cs
│   │   │   │   ├── IDraftManager.cs         # Session/file draft persistence
│   │   │   │   ├── DraftManager.cs
│   │   │   │   ├── IOrderHistoryService.cs  # Order request history
│   │   │   │   ├── OrderHistoryService.cs
│   │   │   │   └── TotalsCalculator.cs      # Product/payment total calculations
│   │   │   └── OnlineOrderTool.Core.csproj
│   │   │
│   │   └── OnlineOrderTool.Data/            # Data access (Dapper)
│   │       ├── IDbConnectionFactory.cs       # Connection factory with driver fallback
│   │       ├── SqlServerConnectionFactory.cs
│   │       ├── Repositories/
│   │       │   ├── IItemRepository.cs
│   │       │   ├── FlatOrderItemRepository.cs    # GHC item lookup
│   │       │   ├── UpcItemRepository.cs          # UPC branch-specific lookup
│   │       │   ├── IConsumerRepository.cs
│   │       │   ├── GhcConsumerRepository.cs
│   │       │   ├── UpcConsumerRepository.cs      # Consumers + LoyaltyConsumerAddresses
│   │       │   ├── IOrderValidationRepository.cs
│   │       │   └── UpcOrderValidationRepository.cs  # OrderRequests/Headers/Details/Invoices
│   │       └── OnlineOrderTool.Data.csproj
│   │
│   └── tests/
│       └── OnlineOrderTool.Tests/
│           ├── PayloadBuilderTests.cs       # Verify payloads match request_examples/
│           ├── ValidatorTests.cs
│           └── OnlineOrderTool.Tests.csproj
│
├── frontend/                               # Angular 19
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/                       # Singleton services, guards, interceptors
│   │   │   │   ├── services/
│   │   │   │   │   ├── api.service.ts       # HTTP client wrapper
│   │   │   │   │   ├── module.service.ts    # Module state management
│   │   │   │   │   ├── order.service.ts     # Order CRUD operations
│   │   │   │   │   ├── lookup.service.ts    # Item/consumer lookup
│   │   │   │   │   ├── history.service.ts   # Order request history
│   │   │   │   │   ├── validation.service.ts # UPC order validation
│   │   │   │   │   ├── theme.service.ts     # Dark/light theme management
│   │   │   │   │   └── toast.service.ts     # Toast notification system
│   │   │   │   ├── interceptors/
│   │   │   │   │   └── error.interceptor.ts
│   │   │   │   ├── guards/
│   │   │   │   │   └── module.guard.ts      # Ensure module is selected
│   │   │   │   └── models/
│   │   │   │       ├── module.model.ts
│   │   │   │       ├── environment.model.ts
│   │   │   │       ├── order.model.ts
│   │   │   │       ├── product.model.ts
│   │   │   │       ├── payment.model.ts
│   │   │   │       └── history.model.ts
│   │   │   │
│   │   │   ├── shared/                     # Reusable UI components
│   │   │   │   ├── components/
│   │   │   │   │   ├── toast/
│   │   │   │   │   ├── loading-skeleton/
│   │   │   │   │   ├── json-viewer/         # Syntax-highlighted JSON display
│   │   │   │   │   ├── status-badge/
│   │   │   │   │   ├── confirm-dialog/
│   │   │   │   │   └── empty-state/
│   │   │   │   ├── directives/
│   │   │   │   │   └── animate-number.directive.ts
│   │   │   │   └── pipes/
│   │   │   │       ├── relative-time.pipe.ts
│   │   │   │       └── riyal-currency.pipe.ts
│   │   │   │
│   │   │   ├── layout/                     # App shell
│   │   │   │   ├── sidebar/
│   │   │   │   │   ├── sidebar.component.ts
│   │   │   │   │   ├── sidebar.component.html
│   │   │   │   │   └── sidebar.component.css
│   │   │   │   ├── navbar/
│   │   │   │   └── breadcrumb/
│   │   │   │
│   │   │   ├── features/                   # Feature modules (lazy-loaded routes)
│   │   │   │   ├── landing/
│   │   │   │   │   ├── landing.component.ts
│   │   │   │   │   ├── landing.component.html
│   │   │   │   │   ├── landing.component.css
│   │   │   │   │   └── module-card/
│   │   │   │   │       └── module-card.component.ts
│   │   │   │   │
│   │   │   │   ├── flat-order/             # GHC + UPC order builder
│   │   │   │   │   ├── flat-order.component.ts
│   │   │   │   │   ├── flat-order.component.html
│   │   │   │   │   ├── order-info/
│   │   │   │   │   ├── client-info/
│   │   │   │   │   ├── delivery-info/      # GHC only
│   │   │   │   │   ├── products-table/
│   │   │   │   │   ├── payments-table/
│   │   │   │   │   ├── quick-stats/
│   │   │   │   │   ├── api-config/
│   │   │   │   │   ├── add-product-dialog/
│   │   │   │   │   ├── edit-product-dialog/
│   │   │   │   │   ├── add-payment-dialog/
│   │   │   │   │   └── edit-payment-dialog/
│   │   │   │   │
│   │   │   │   ├── unicommerce/            # GHC Uni-Commerce invoice builder
│   │   │   │   │   ├── unicommerce.component.ts
│   │   │   │   │   ├── consumer-section/
│   │   │   │   │   ├── delivery-section/
│   │   │   │   │   ├── row-items-table/
│   │   │   │   │   └── order-fields/
│   │   │   │   │
│   │   │   │   ├── order-requests/         # Order history (all modules)
│   │   │   │   │   ├── order-requests.component.ts
│   │   │   │   │   ├── order-card/
│   │   │   │   │   ├── filter-bar/
│   │   │   │   │   └── cancel-dialog/
│   │   │   │   │
│   │   │   │   └── order-validation/       # UPC only
│   │   │   │       ├── order-validation.component.ts
│   │   │   │       ├── search-form/
│   │   │   │       ├── results-grid/
│   │   │   │       └── order-details-dialog/
│   │   │   │
│   │   │   ├── app.component.ts
│   │   │   ├── app.routes.ts
│   │   │   └── app.config.ts
│   │   │
│   │   ├── assets/
│   │   │   ├── icons/
│   │   │   │   ├── Saudi_Riyal.svg
│   │   │   │   ├── upc_logo.svg
│   │   │   │   └── whites_logo.svg
│   │   │   └── images/
│   │   │
│   │   ├── styles/
│   │   │   ├── _variables.css              # CSS custom properties (design tokens)
│   │   │   ├── _animations.css             # All @keyframes
│   │   │   ├── _glassmorphism.css           # Glass effect mixins
│   │   │   ├── _typography.css             # Font imports, text styles
│   │   │   ├── _components.css             # Base component styles
│   │   │   └── styles.css                  # Main entry (imports all above)
│   │   │
│   │   └── environments/
│   │       ├── environment.ts
│   │       └── environment.development.ts
│   │
│   ├── angular.json
│   ├── package.json
│   ├── tsconfig.json
│   └── tsconfig.app.json
│
├── docs/
│   ├── request_examples/                   # Preserved from original
│   ├── Prompts/                            # Planning docs
│   ├── api-spec.md                         # API endpoint documentation
│   └── database-schema.md                  # DB tables and queries
│
├── README.md
├── .gitignore
└── docker-compose.yml                      # Optional: containerized deployment
```

---

## API Endpoint Specification

All endpoints prefixed with `/api`. Angular calls these via `HttpClient`.

### Module Endpoints
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/modules` | List all modules with environments |
| `GET` | `/api/modules/{key}` | Get module details + current draft state |
| `POST` | `/api/modules/{key}/select-environment` | Set active environment |

### Order Draft Endpoints (Flat Order: GHC/UPC)
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/modules/{key}/state` | Get current draft state |
| `PUT` | `/api/modules/{key}/order-field` | Update a single order field |
| `POST` | `/api/modules/{key}/products` | Add product |
| `PUT` | `/api/modules/{key}/products/{index}` | Update product |
| `DELETE` | `/api/modules/{key}/products/{index}` | Remove product |
| `GET` | `/api/modules/{key}/products/{index}` | Get product for editing |
| `POST` | `/api/modules/{key}/payments` | Add payment |
| `PUT` | `/api/modules/{key}/payments/{index}` | Update payment |
| `DELETE` | `/api/modules/{key}/payments/{index}` | Remove payment |
| `GET` | `/api/modules/{key}/payments/{index}` | Get payment for editing |
| `GET` | `/api/modules/{key}/calculate-totals` | Recalculate totals |
| `GET` | `/api/modules/{key}/calculate-payment-summary` | Payment summary |
| `GET` | `/api/modules/{key}/remaining-amount` | Remaining amount |
| `GET` | `/api/modules/{key}/export-json` | Export built payload |
| `POST` | `/api/modules/{key}/load-default` | Reset to default state |
| `POST` | `/api/modules/{key}/clear-all` | Clear all data |

### Uni-Commerce Draft Endpoints
| Method | Path | Description |
|--------|------|-------------|
| `PUT` | `/api/modules/{key}/consumer-field` | Update consumer field |
| `PUT` | `/api/modules/{key}/delivery-field` | Update delivery field |
| `PUT` | `/api/modules/{key}/invoice-field` | Update invoice-level field |
| `POST` | `/api/modules/{key}/row-items` | Add row item |
| `PUT` | `/api/modules/{key}/row-items/{index}` | Update row item |
| `DELETE` | `/api/modules/{key}/row-items/{index}` | Remove row item |
| `GET` | `/api/modules/{key}/calculate-invoice-totals` | Recalculate |

### Lookup Endpoints
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/modules/{key}/lookup/item?code=&branch_code=` | Item lookup |
| `GET` | `/api/modules/{key}/lookup/consumer?phone=` | Consumer lookup |

### API Communication Endpoints
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/modules/{key}/send-request` | Send order to external API |
| `POST` | `/api/modules/{key}/resend-order` | Resend with different branch |
| `POST` | `/api/modules/{key}/cancel-order` | Cancel order |
| `POST` | `/api/modules/{key}/test-endpoint` | Test endpoint connectivity |
| `POST` | `/api/modules/{key}/test-db` | Test database connection |

### Order History Endpoints
| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/modules/{key}/order-history` | List all sent orders |
| `GET` | `/api/modules/{key}/order-history/{id}` | Get specific order details |
| `POST` | `/api/modules/{key}/order-history/{id}/cancel` | Cancel from history |

### UPC Order Validation Endpoints
| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/modules/{key}/validation/search` | Search orders in DB |
| `GET` | `/api/modules/{key}/validation/order/{orderNumber}` | Order details from DB |

---

## Proposed Changes (14 Sessions)

### Phase 1 — Session 1: Project Setup & Cleanup

#### Archive legacy codebase
- Move ALL existing Python files to `_legacy_flask/` (preserve for reference)
- Create `.gitignore` for .NET + Angular + Python artifacts

#### Scaffold .NET 10 Solution
- `dotnet new sln -n OnlineOrderTool`
- `dotnet new webapi -n OnlineOrderTool.Api` (controller-based, no OpenAPI initially)
- `dotnet new classlib -n OnlineOrderTool.Core`
- `dotnet new classlib -n OnlineOrderTool.Data`
- `dotnet new xunit -n OnlineOrderTool.Tests`
- Wire project references: Api → Core → Data
- Configure `Program.cs`: CORS (Angular dev server), JSON serialization (camelCase), static file serving
- NuGet packages: `Dapper`, `Microsoft.Data.SqlClient`, `Serilog`

---

### Phase 2 — Session 2: Domain Models & Module System

#### Port module system to C#
- `IOrderModule` interface (mirrors Python `OrderModule` ABC)
- `ModuleEnvironment` record (mirrors Python `ModuleEnvironment` dataclass)
- `ModuleRegistry` (DI-registered singleton, mirrors `MODULE_REGISTRY`)
- `GhcEcommerceModule`, `UpcEcommerceModule`, `GhcUnicommerceModule`, `OmsModule`, `CallCenterModule`
- All environment configs ported from Python (URLs, DB configs, accents, icons)

#### Port configuration
- `appsettings.json`: all module configs, DB connection strings, payment constants
- Environment-specific overrides in `appsettings.Development.json`

#### Port all models/DTOs
- `OrderDraft`, `Product`, `Payment`, `Consumer`, `RowItem`, `UniCommerceInvoice`
- Request/response DTOs for every API endpoint

---

### Phase 3 — Session 3: Data Access Layer

#### Port all SQL queries from Python to Dapper
- `SqlServerConnectionFactory` with ODBC driver fallback (same fallback order as Python)
- `FlatOrderItemRepository`: GHC item lookup (from `flat_order.py:lookup_item`)
- `UpcItemRepository`: UPC branch-specific lookup with ranked pricing (from `flat_order.py:lookup_upc_item`)
- `GhcConsumerRepository`: GHC placeholder consumer lookup
- `UpcConsumerRepository`: UPC `Consumers` + `LoyaltyConsumerAddresses` join (from `flat_order.py:lookup_upc_consumer_by_phone`)
- `UpcOrderValidationRepository`: search orders, get order details, joins across `OrderRequests`/`RequestOrderHeaders`/`RequestOrderDetails`/`RequestOrderTransactions`/`Invoices` (from `flat_order.py:search_upc_orders`, `get_upc_order_details`)
- Connection string per module per environment (UPC has separate prod/test DBs)

---

### Phase 4 — Session 4: Business Logic Services

#### Port serializers (payload builders)
- `FlatOrderPayloadBuilder`: `build_payload()` and `build_upc_payload()` (from `flat_order.py`)
  - Product calculations: `normalize_vat_percentage`, unit VAT, row totals
  - Payment preparation: format, status determination
  - Date/time formatting: birthdate, delivery times
- `UniCommercePayloadBuilder`: `build_payload()` (from `ghc_unicommerce.py`)
  - Row item calculations: GrossAmount, RowTotalDiscount, ItemVat, RowTotalVat, NetAmount
  - Invoice-level aggregation: totals + delivery fees + payment reconciliation

#### Port validators
- `FlatOrderValidator`: COD/Visa/Tamara/Tabby rules, PostToCredit (GHC only), payment exclusion rules
- `UniCommerceValidator`: ReferenceNumber required, IsReturn → ParentReferenceNumber, payment reconciliation

#### Port services
- `TotalsCalculator`: product totals, payment summary, remaining amount
- `DraftManager`: file-based autosave (`last_order_{module_key}.json`)
- `ApiClient`: POST to external APIs with SSL bypass, timeout, error handling (mirrors `APIManager`)
- `OrderHistoryService`: file-based order history (CRUD)

---

### Phase 5 — Session 5: API Controllers (Part 1 — Module + Order CRUD)

#### `ModuleController`
- `GET /api/modules` — list all modules with environments
- `GET /api/modules/{key}` — module details + draft state
- `POST /api/modules/{key}/select-environment` — set active environment

#### `OrderController`
- Draft state management (get, update field, load default, clear all, export JSON)
- Calculate totals, payment summary, remaining amount

#### `ProductController`
- Add, get, update, remove products (all via session state, not DB)

#### `PaymentController`
- Add, get, update, remove payments with payment rule validation

#### `LookupController`
- Item lookup (dispatches to GHC or UPC repository based on module)
- Consumer lookup (dispatches to GHC or UPC repository based on module)

---

### Phase 6 — Session 6: API Controllers (Part 2 — Send/Cancel + History + Validation)

#### `OrderController` (extended)
- `POST send-request` — build payload, validate, POST to external API, save to history
- `POST cancel-order` — send cancel request to external cancel URL
- `POST resend-order` — rebuild with new branch, send again
- `POST test-endpoint` — socket connectivity test
- `POST test-db` — test DB connection

#### `HistoryController`
- `GET order-history` — list all sent orders for module
- `GET order-history/{id}` — single order details
- `POST order-history/{id}/cancel` — cancel from history

#### `ValidationController` (UPC only)
- `POST validation/search` — multi-criteria order search against DB
- `GET validation/order/{orderNumber}` — full order details + line items + transactions + invoice

---

### Phase 7 — Session 7: Angular Project Scaffolding + Design System

#### Scaffold Angular 19
- `npx -y @angular/cli@latest new frontend --routing --style=css --ssr=false --standalone`
- Install: `bootstrap-icons`, `@angular/cdk`, `@angular/animations`
- Configure `angular.json`: styles entry, assets
- Configure `environment.ts`: API base URL

#### Build CSS Design System (`styles/`)
- `_variables.css`: Full CSS custom properties
  - Dark theme (default): body `hsl(222, 25%, 8%)`, cards `hsl(222, 20%, 12%)`
  - Light theme: body `hsl(220, 20%, 96%)`, cards white
  - Primary: `hsl(230, 80%, 65%)` indigo-blue
  - Glassmorphism: `backdrop-filter: blur(12px)`, `rgba` backgrounds
  - Border radius: `12px` cards, `8px` buttons, `20px` pills
  - Transitions: `0.3s cubic-bezier(0.4, 0, 0.2, 1)`
- `_typography.css`: Google Fonts Inter (400-700) + JetBrains Mono
- `_animations.css`: fadeInUp, slideUp, shimmer, pulseGlow, spin, slideInRight, scaleIn
- `_glassmorphism.css`: reusable glass card, glass sidebar, glass modal classes
- `_components.css`: buttons, forms, tables, badges, pills, cards

#### Build core services
- `api.service.ts` — base HTTP client with error handling
- `theme.service.ts` — dark/light toggle with localStorage persistence
- `toast.service.ts` — toast notification system (slide-in, auto-dismiss)
- `module.service.ts` — module state, active environment tracking

#### Build shared components
- Toast component (animated slide-in)
- Loading skeleton component (shimmer animation)
- JSON viewer component (syntax-highlighted, collapsible)
- Status badge component (colored dot + label)
- Empty state component (illustrated placeholder)
- Confirm dialog component (glassmorphic modal)

---

### Phase 8 — Session 8: Landing Page + Layout

#### Layout components
- **Navbar**: Fixed top, glassmorphic, app title with gradient text, theme toggle, docs button
- **Sidebar**: Collapsible (280px → 72px), glassmorphic, animated nav links, stats, quick actions
- **Breadcrumb**: Module Picker → Module → Tab

#### Landing page
- Hero section with animated gradient background
- Module cards grid (glassmorphic, staggered fade-in)
- Environment buttons (pill-shaped, status dots, hover glow)
- Coming-soon shimmer badges
- Route: `/` → `LandingComponent`

#### Routing setup
- `/` — Landing
- `/modules/:key` — Module shell (sidebar + tabs)
- `/modules/:key/order` — Order Dashboard tab
- `/modules/:key/api` — API Configuration tab
- `/modules/:key/database` — Database tab
- `/modules/:key/test` — Test Endpoints tab
- `/modules/:key/requests` — Order Requests tab
- `/modules/:key/validation` — Order Validation tab (UPC only)

---

### Phase 9 — Session 9: Flat Order Module (GHC/UPC)

#### Components
- `flat-order.component` — Shell with tab routing
- `order-info.component` — Order fields (branch, code, status, etc.) with UPC/GHC conditional fields
- `client-info.component` — Consumer lookup + client fields
- `delivery-info.component` — GHC only (from/to time, payment status)
- `products-table.component` — Product list with edit/delete actions
- `payments-table.component` — Payment list with edit/delete actions
- `quick-stats.component` — Animated stat cards (totals, paid, remaining)
- `api-config.component` — Send request form, response display, cancel section
- Add/Edit Product Dialog — Modal with item lookup (UPC: in-modal branch-aware lookup)
- Add/Edit Payment Dialog — Modal with payment method rules

#### Data flow
- Component loads draft state from `GET /api/modules/{key}/state`
- Field changes → `PUT /api/modules/{key}/order-field`
- Products/payments CRUD → respective API calls
- Totals auto-recalculate on changes
- Send → `POST /api/modules/{key}/send-request` → navigate to Order Requests

---

### Phase 10 — Session 10: Uni-Commerce Module

#### Components
- `unicommerce.component` — Shell with tab routing
- `consumer-section.component` — Phone lookup + InvoiceConsumer fields
- `delivery-section.component` — Delivery details subform
- `row-items-table.component` — RowItems CRUD with item lookup
- `order-fields.component` — ReferenceNumber, OnlineOrderNumber, IsReturn toggle, CustomerName dropdown, payment amounts
- Computed totals: read-only GrossAmount/TotalDiscount/TotalVat/NetAmount/CustomerCreditAmount

---

### Phase 11 — Session 11: Order Requests Page

#### Components
- `order-requests.component` — Main page
- `filter-bar.component` — Search, status filter, date range, environment filter
- `order-card.component` — Expandable accordion card per order
  - Summary: status dot, order code, timestamp, environment, response code
  - Expanded: request JSON, response JSON, order info, actions (resend, cancel, copy)
- `cancel-dialog.component` — Inline cancel form

#### Features
- Load history from `GET /api/modules/{key}/order-history`
- Client-side filtering and search
- Expand/collapse with smooth animation
- Copy to clipboard with "Copied!" toast
- Cancel flow: confirmation → API call → update card
- Resend flow: confirmation → re-send → add new entry
- Relative timestamps ("2 minutes ago")
- Auto-navigate here after sending an order

---

### Phase 12 — Session 12: UPC Order Validation

#### Components
- `order-validation.component` — UPC-only tab
- `search-form.component` — Multi-criteria search (order number, phone, branch, status 1-9, date range)
- `results-grid.component` — Table with order number, branch, decoded status, creation date, invoice barcode, invoice date, creation vs invoice comparison
- `order-details-dialog.component` — Modal with header fields + line items + transactions + invoice info + resend action

#### Features
- Search → `POST /api/modules/{key}/validation/search`
- Details → `GET /api/modules/{key}/validation/order/{orderNumber}`
- Status decode (1=New, 2=Confirmed, ..., 9=Done)
- Resend eligibility (blocked for status 4/8/9)
- Resend action → confirmation → send → refresh

---

### Phase 13 — Session 13: Animations, Polish & Integration

#### Animations (`@angular/animations`)
- Page transitions (route animations)
- Card staggered entry
- Tab content fade transitions
- Dialog open/close (scale + blur)
- List item add/remove (slide + fade)
- Number counter animations
- Button loading spinner states
- Toast slide-in/slide-out

#### Polish
- Form validation feedback (inline errors, red borders)
- Keyboard navigation
- Mobile responsiveness
- Print styles
- Custom scrollbar
- Loading interceptor (global loading bar)

---

### Phase 14 — Session 14: Testing, Documentation & Deployment

#### Backend tests
- Payload builder tests (verify against `request_examples/` JSON files)
- Validator tests
- Controller integration tests

#### Frontend
- Verify all routes and components render
- Full end-to-end flow per module
- Cross-browser testing

#### Documentation
- Update `README.md` with new architecture
- `docs/api-spec.md` — full API documentation
- `docs/database-schema.md` — all DB tables and queries
- Setup instructions (.NET + Angular)

#### Deployment config
- `docker-compose.yml` (optional)
- Angular production build → served by .NET's `UseStaticFiles` + SPA fallback

---

## Verification Plan

### Automated Tests
```bash
# Backend
cd backend && dotnet test

# Frontend
cd frontend && ng test
```

### Manual Verification
- Landing page: all 5 module cards, environment selection, theme toggle
- GHC E-Commerce: full order build, send, response display
- UPC E-Commerce: no delivery card, branch-specific item lookup, consumer lookup
- GHC Uni-Commerce: invoice builder, row items, consumer section
- Order Requests: history displays, expand/collapse, cancel, resend, copy
- UPC Order Validation: search, details, resend eligibility
- Dark/light mode on all pages
- Responsive design (320px → 1440px)
- API payloads match `request_examples/` exactly
