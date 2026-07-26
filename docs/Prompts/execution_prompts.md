# Rewrite to .NET 10 + Angular 22 — Execution Prompts (14 Sessions)

Each prompt below is **100% self-contained** and designed for **Gemini 3.6** to execute in order. 
Paste one prompt per session. Each session builds incrementally upon the previous one.

---

## Session 1 — Archiving & .NET 10 Solution Scaffolding

```markdown
Read `implementation_plan.md` for full background.
This is Session 1 of 14. Your goal is to clean up dead runtime files, archive the existing Python codebase to `_legacy_flask/`, and scaffold the .NET 10 Solution.

1. **Delete Dead Runtime Files & Unneeded Virtual Environments**:
   - Delete `.venv-1/` directory.
   - Delete all `last_order_*.json` files in the root.
   - Delete `index.html`, `script.js`, `partials/sidebar.html`, `Prompts/excute_plan.md`.

2. **Archive Python Codebase**:
   - Create directory `_legacy_flask/`.
   - Move `app.py`, `config.py`, `managers.py`, `modules/`, `flat_order.html`, `landing.html`, `unicommerce.html`, `module_placeholder.html`, `partials/`, `assets/`, `style.css`, `flat_order.js`, `unicommerce.js`, `upc_validation.js` into `_legacy_flask/`.
   - Create `docs/request_examples/` and move `request_examples/` contents into it for permanent reference.

3. **Scaffold .NET 10 Web API Solution**:
   - In `backend/`, create solution `OnlineOrderTool.sln`.
   - Create `src/OnlineOrderTool.Api` (Web API project, target `.net10.0`).
   - Create `src/OnlineOrderTool.Core` (Class Library project, target `.net10.0`).
   - Create `src/OnlineOrderTool.Data` (Class Library project, target `.net10.0`).
   - Create `tests/OnlineOrderTool.Tests` (xUnit test project, target `.net10.0`).
   - Link project dependencies: `Api` -> `Core` & `Data`, `Data` -> `Core`, `Tests` -> `Core` & `Api`.

4. **Configure `Program.cs` & `appsettings.json`**:
   - Enable `AddControllers()` in `Program.cs` (Controller-based architecture).
   - Configure CORS policy allowing `http://localhost:4200` (Angular dev server).
   - Configure System.Text.Json to use `JsonNamingPolicy.CamelCase`.
   - Add NuGet packages: `Dapper`, `Microsoft.Data.SqlClient`, `Serilog.AspNetCore`.

5. **Root `.gitignore`**:
   - Create a clean root `.gitignore` covering `.net` (`bin/`, `obj/`), Angular (`node_modules/`, `dist/`), Python (`__pycache__/`, `*.pyc`), and OS files.

**Verification Steps**:
- Run `dotnet build backend/OnlineOrderTool.sln` — must succeed cleanly with 0 errors.
- Run `dotnet run --project backend/src/OnlineOrderTool.Api` — Web API starts on port 5000/5001.
- Verify `_legacy_flask/` contains all historical Python code.
```

---

## Session 2 — Core Domain Models, DTOs & Module Registry in C#

```markdown
Read `implementation_plan.md` and inspect `_legacy_flask/modules/` and `_legacy_flask/config.py`.
This is Session 2 of 14. Your goal is to build the Core domain models, DTOs, and Module Registry in `OnlineOrderTool.Core`.

1. **Domain Models (`OnlineOrderTool.Core/Models/`)**:
   - `ModuleEnvironment`: `Key`, `Environment` ("Production"|"Testing"), `Description`, `Accent`, `Cue`, `Icon`, `RouteLabel`, `VisualUrl`, `VisualAlt`, `Available`, `ApiUrl`, `CancelUrl`, `DbConfig` (Server, Database, Username, Password, Driver), `StatusLabel` computed property.
   - `OrderDraft`: Represents a module's session draft (OrderData dictionary, Products list, Payments list, Consumer, Delivery, RowItems).
   - `Product`: `ItemCode`, `ItemName`, `Quantity`, `UnitPrice`, `VatPercentage`, `Discount`, `OfferCode`, `OfferMessage`, `UnitVat`, `TotalVat`, `EstimatedTotal`.
   - `Payment`: `PaymentMethod`, `PaymentStatus`, `PaymentAmount`, `TransactionId`, `PaymentOption`, `OptionCommission`, `CustomerName`, `CustomerNumber`.
   - `Consumer`: `FirstName`, `MiddleName`, `LastName`, `ConsumerCode`, `Gender`, `BirthDate`, `PrimaryPhoneNumber`, `Email`, `NationalId`, `Nationality`.
   - `RowItem`: Uni-Commerce invoice item (`Quantity`, `MaterialNumber`, `ItemPrice`, `ItemDiscount`, `VatPercentage`, `BatchNumber`, `ExpireDate`, `SerialNumber`, `Barcode`, `ScannedCode`, `GrossAmount`, `NetAmount`, `RowTotalDiscount`, `ItemVat`, `RowTotalVat`).
   - `UniCommerceInvoice`: `ReferenceNumber`, `OnlineOrderNumber`, `IsReturn`, `ParentReferenceNumber`, `OrderCreationDate`, `GrossAmount`, `TotalDiscount`, `TotalVat`, `NetAmount`, `CustomerName`, `CustomerCreditAmount`, `PaidOnlineAmount`, `PaidWithPointsAmount`, `Consumer`, `Delivery`, `RowItems`.
   - `OrderHistoryEntry`: `Id` (Guid), `OrderCode`, `Timestamp`, `ModuleKey`, `EnvironmentKey`, `ApiUrl`, `RequestPayloadJson`, `ResponseStatusCode`, `ResponseBodyJson`, `IsCancelled`, `CancelResponseJson`.

2. **DTOs (`OnlineOrderTool.Core/DTOs/`)**:
   - Create request/response DTOs corresponding to every endpoint (ModuleDto, EnvironmentDto, OrderStateDto, ProductDto, PaymentDto, SendOrderRequest, CancelOrderRequest, LookupResultDto, OrderSearchRequest, OrderSearchResultDto).

3. **Module System Interface & Classes (`OnlineOrderTool.Core/Modules/`)**:
   - `IOrderModule`: `Key`, `Label`, `Client`, `Available`, `Environments` dict, `GetEnvironment(key)`, `DefaultState()`, `BuildPayload(state)`, `Validate(payload)`.
   - Implement concrete modules:
     - `GhcEcommerceModule`: keys `"GHC Production"`, `"GHC Testing"`.
     - `UpcEcommerceModule`: keys `"UPC Production"`, `"UPC Testing"`.
     - `GhcUnicommerceModule`: keys `"GHC Uni-Commerce Production"`, `"GHC Uni-Commerce Testing"`.
     - `OmsModule` & `CallCenterModule` stubs.
   - `ModuleRegistry`: Singleton service registering all 5 modules.

**Verification Steps**:
- Run `dotnet build backend/OnlineOrderTool.sln` — must compile cleanly.
- Create a unit test in `OnlineOrderTool.Tests` verifying `ModuleRegistry.GetAllModules()` returns 5 modules and `UpcEcommerceModule.GetEnvironment("UPC Production")` resolves correctly.
```

---

## Session 3 — Dapper Data Access Layer & SQL Server Repositories

```markdown
Read `implementation_plan.md` and inspect `_legacy_flask/modules/flat_order.py` and `_legacy_flask/modules/ghc_unicommerce.py`.
This is Session 3 of 14. Your goal is to build the Dapper data access layer in `OnlineOrderTool.Data`.

1. **Connection Factory (`OnlineOrderTool.Data/SqlServerConnectionFactory.cs`)**:
   - Create `IDbConnectionFactory` interface.
   - Implement `SqlServerConnectionFactory` supporting SQL Server connectivity with `Microsoft.Data.SqlClient` / `System.Data.SqlClient` fallback.

2. **Repositories (`OnlineOrderTool.Data/Repositories/`)**:
   - `FlatOrderItemRepository` (`IItemRepository`): Port `lookup_item` query from `flat_order.py` (SELECT TOP 1 FROM dbo.Items + TaxTypes + ItemPrices).
   - `UpcItemRepository`: Port `lookup_upc_item` query from `flat_order.py` with branch code filter (`dbo.ItemUnitOfMeasureBarCodes`, `dbo.ItemPrices`, `dbo.TaxTypes`, branch specific filtering).
   - `UpcConsumerRepository` (`IConsumerRepository`): Port `lookup_upc_consumer_by_phone` query joined with `dbo.Consumers` and `dbo.LoyaltyConsumerAddresses`.
   - `GhcConsumerRepository`: Port GHC `dbo.Customers` query.
   - `UpcOrderValidationRepository` (`IOrderValidationRepository`):
     - `SearchOrdersAsync(OrderSearchRequest filters)`: Search query across `dbo.OrderRequests` and `dbo.RequestOrderHeaders`.
     - `GetOrderDetailsAsync(string orderNumber)`: Joins across `RequestOrderHeaders`, `RequestOrderDetails`, `RequestOrderTransactions`, `Invoices`.
     - `GetLatestRequestJsonAsync(string orderNumber)`: Queries `RequestJson` column.

**Verification Steps**:
- Run `dotnet build backend/OnlineOrderTool.sln` — clean build.
- Create repository mock/unit tests in `OnlineOrderTool.Tests` to verify SQL query strings match original Python implementations line-for-line.
```

---

## Session 4 — Serialization, Payload Builders, Validation & Business Logic Services

```markdown
Read `implementation_plan.md` and inspect `_legacy_flask/modules/flat_order.py` and `_legacy_flask/modules/ghc_unicommerce.py`.
This is Session 4 of 14. Your goal is to port all serialization, validation rules, and business services to `OnlineOrderTool.Core.Services`.

1. **Payload Builders (`OnlineOrderTool.Core/Services/`)**:
   - `FlatOrderPayloadBuilder`:
     - Implement `BuildGhcPayload(OrderDraft draft)` matching `flat_order.py:build_payload`.
     - Implement `BuildUpcPayload(OrderDraft draft)` matching `flat_order.py:build_upc_payload` (omits delivery date, delivery times, shipping_address_2, fulfillment_plant).
     - Implement unit price, VAT calculation (`normalize_vat_percentage`), discount, total estimation per product line.
   - `UniCommercePayloadBuilder`:
     - Implement `BuildInvoicePayload(UniCommerceInvoice invoice)` matching `ghc_unicommerce.py:build_payload`.
     - Compute GrossAmount, RowTotalDiscount, ItemVat, RowTotalVat, NetAmount per row.
     - Compute GrossAmount, TotalDiscount, TotalVat, NetAmount, CustomerCreditAmount at invoice level.

2. **Validation Rules (`OnlineOrderTool.Core/Services/`)**:
   - `FlatOrderValidator`:
     - Enforce payment status rules (COD -> `not_payment`, Visa/Tamara/Tabby -> `done_payment`).
     - Enforce UPC rule: block `PostToCredit` payment method.
     - Enforce GHC rule: require customer name & number if `PostToCredit` selected.
     - Enforce digital wallet exclusivity (only one Tamara or Tabby).
   - `UniCommerceValidator`:
     - Require `ReferenceNumber` and `CustomerName`.
     - Require `ParentReferenceNumber` if `IsReturn` is true.
     - Enforce payment reconciliation equation (`PaidOnlineAmount + PaidWithPointsAmount + CustomerCreditAmount == NetAmount`).

3. **Services**:
   - `TotalsCalculator`: Calculate product summary totals, payment totals, and remaining balance.
   - `DraftManager` (`IDraftManager`): Save and load drafts per module to `last_order_{moduleKey}.json`.
   - `ApiClient` (`IApiClient`): `HttpClient` wrapper with SSL bypass (`HttpClientHandler.ServerCertificateCustomValidationCallback`), custom headers, 30s timeout.
   - `OrderHistoryService` (`IOrderHistoryService`): File-backed JSON history (`order_history_{moduleKey}.json`) for storing sent requests, responses, and cancellation status.

**Verification Steps**:
- Run `dotnet test backend/tests/OnlineOrderTool.Tests` with tests verifying:
  - GHC payload build against `docs/request_examples/GHC E-Commerce/request_body.json`.
  - UPC payload build against `docs/request_examples/UPC/request_body.json`.
  - Uni-Commerce payload calculation against examples in `docs/request_examples/GHC Uni-Commerce/`.
```

---

## Session 5 — Controller API Endpoints - Part 1 (Module, Order CRUD, Product/Payment, Lookup)

```markdown
Read `implementation_plan.md`.
This is Session 5 of 14. Your goal is to create the ASP.NET Core API Controllers for Module selection, Order Draft CRUD, Product/Payment management, and Item/Consumer Lookups.

1. **`ModuleController` (`OnlineOrderTool.Api/Controllers/ModuleController.cs`)**:
   - `GET /api/modules`: Returns list of all modules with environments.
   - `GET /api/modules/{key}`: Returns specific module details + active environment + draft state.
   - `POST /api/modules/{key}/select-environment`: Updates active environment in session/state.

2. **`OrderController` (`OnlineOrderTool.Api/Controllers/OrderController.cs`)**:
   - `GET /api/modules/{key}/state`: Returns current `OrderDraft`.
   - `PUT /api/modules/{key}/order-field`: Updates a top-level draft field.
   - `GET /api/modules/{key}/calculate-totals`: Returns updated product & payment totals.
   - `GET /api/modules/{key}/export-json`: Returns compiled JSON payload.
   - `POST /api/modules/{key}/load-default`: Resets draft to `DefaultState()`.
   - `POST /api/modules/{key}/clear-all`: Clears draft.

3. **`ProductController` (`OnlineOrderTool.Api/Controllers/ProductController.cs`)**:
   - `POST /api/modules/{key}/products`: Adds product to draft.
   - `PUT /api/modules/{key}/products/{index}`: Updates product at index.
   - `DELETE /api/modules/{key}/products/{index}`: Deletes product at index.

4. **`PaymentController` (`OnlineOrderTool.Api/Controllers/PaymentController.cs`)**:
   - `POST /api/modules/{key}/payments`: Validates & adds payment method.
   - `PUT /api/modules/{key}/payments/{index}`: Updates payment method.
   - `DELETE /api/modules/{key}/payments/{index}`: Deletes payment method.

5. **`LookupController` (`OnlineOrderTool.Api/Controllers/LookupController.cs`)**:
   - `GET /api/modules/{key}/lookup/item?code=...&branch_code=...`: Dispatches to `IItemRepository` (UPC or GHC).
   - `GET /api/modules/{key}/lookup/consumer?phone=...`: Dispatches to `IConsumerRepository`.

**Verification Steps**:
- Run `dotnet run --project backend/src/OnlineOrderTool.Api`.
- Use curl/Postman to test:
  - `GET http://localhost:5000/api/modules` returns 200 with 5 modules.
  - `POST http://localhost:5000/api/modules/ghc_ecommerce/products` adds a product.
  - `GET http://localhost:5000/api/modules/ghc_ecommerce/export-json` returns compiled JSON payload.
```

---

## Session 6 — Controller API Endpoints - Part 2 (Send/Cancel/Resend, History, UPC Order Validation)

```markdown
Read `implementation_plan.md`.
This is Session 6 of 14. Your goal is to complete the backend by implementing API execution endpoints, Order History management, and UPC Order Validation controllers.

1. **API Execution (`OrderController.cs` additions)**:
   - `POST /api/modules/{key}/send-request`: Compiles payload, runs validator, sends via `IApiClient`, logs entry to `IOrderHistoryService`, returns API response.
   - `POST /api/modules/{key}/cancel-order`: Sends cancellation request to active environment's `CancelUrl`.
   - `POST /api/modules/{key}/resend-order`: Updates branch code and re-triggers order send.
   - `POST /api/modules/{key}/test-endpoint`: Tests socket connectivity to configured API URL.
   - `POST /api/modules/{key}/test-db`: Tests DB connection string.

2. **`HistoryController` (`OnlineOrderTool.Api/Controllers/HistoryController.cs`)**:
   - `GET /api/modules/{key}/order-history`: Returns list of sent orders for specified module.
   - `GET /api/modules/{key}/order-history/{id}`: Returns full details (request payload JSON, response JSON, status).
   - `POST /api/modules/{key}/order-history/{id}/cancel`: Executes cancel API call and marks history entry `IsCancelled = true`.

3. **`ValidationController` (`OnlineOrderTool.Api/Controllers/ValidationController.cs`)**:
   - `POST /api/modules/upc_ecommerce/validation/search`: Accepts search filters (OrderNumber, Phone, BranchCode, Status 1-9, Dates) and executes `UpcOrderValidationRepository.SearchOrdersAsync`.
   - `GET /api/modules/upc_ecommerce/validation/order/{orderNumber}`: Returns order headers, details, transactions, and invoice info.

4. **Global Exception Handling**:
   - Add `ExceptionMiddleware.cs` catching `ValidationError`, returning structured 400 Bad Request responses.

**Verification Steps**:
- Run `dotnet test backend/tests/OnlineOrderTool.Tests` — all tests pass.
- Test endpoints via curl:
  - `GET http://localhost:5000/api/modules/ghc_ecommerce/order-history` returns JSON array.
  - `POST http://localhost:5000/api/modules/upc_ecommerce/validation/search` returns status 200.
```

---

## Session 7 — Angular 22 Project Setup, Glassmorphism Design System & Core Services

```markdown
Read `implementation_plan.md`.
This is Session 7 of 14. Your goal is to scaffold the Angular 22 project in `frontend/`, implement the custom Glassmorphism CSS design system, and create core Angular Services with Signals.

1. **Angular 22 Scaffolding**:
   - Create Angular project in `frontend/` using standalone components, routing, and standard CSS.
   - Install dependencies: `npm install bootstrap-icons @angular/cdk`.
   - Configure `environment.ts` pointing to `http://localhost:5000/api`.

2. **CSS Glassmorphism Design System (`frontend/src/styles/`)**:
   - `_variables.css`: CSS Variables for Dark (`hsl(222, 25%, 8%)`) and Light (`hsl(220, 20%, 96%)`) themes, Primary indigo-blue (`hsl(230, 80%, 65%)`), Success/Warning/Danger colors, border-radii (12px cards, 8px inputs).
   - `_typography.css`: Import Google Fonts Inter & JetBrains Mono.
   - `_glassmorphism.css`: Backdrop blur `backdrop-filter: blur(12px)`, translucent dark/light card backgrounds, subtle gradient borders.
   - `_animations.css`: `@keyframes fadeInUp`, `@keyframes slideUp`, `@keyframes shimmer` (skeletons), `@keyframes slideInRight` (toasts), `@keyframes scaleIn` (modals).
   - `styles.css`: Main bundle importing all modular stylesheets.

3. **Core Angular Services (`frontend/src/app/core/services/`)**:
   - `ApiService`: Generic HTTP wrapper for Backend API.
   - `ThemeService`: Theme state signal (`'dark' | 'light'`), toggle function, localStorage sync, body attribute binding.
   - `ToastService`: Signal-backed toast manager (`showSuccess`, `showError`, `showWarning`, `showInfo`).
   - `ModuleService`: Signal-backed active module & environment state tracker.

4. **Shared Components (`frontend/src/app/shared/components/`)**:
   - `ToastComponent`: Floating top-right slide-in notifications.
   - `LoadingSkeletonComponent`: Shimmer placeholder loader.
   - `JsonViewerComponent`: Syntax-highlighted, expandable JSON tree viewer.
   - `StatusBadgeComponent`: Styled status pill.

**Verification Steps**:
- Run `npm start` in `frontend/` — app compiles cleanly on Angular 22.
- Verify `ToastComponent` renders toasts with smooth slide-in animations.
- Verify `ThemeService` toggles `data-bs-theme` or `data-theme` on document element.
```

---

## Session 8 — Angular Shell, Responsive Sidebar & Animated Landing Page

```markdown
Read `implementation_plan.md`.
This is Session 8 of 14. Your goal is to build the main layout shell (collapsible glassmorphic sidebar, top navbar, breadcrumbs) and the Module Landing Page.

1. **Layout Components (`frontend/src/app/layout/`)**:
   - **Navbar Component (`navbar.component.ts`)**:
     - Glassmorphic top bar with gradient logo text "Online Order Tool".
     - Docs modal button & Theme toggle button with sun/moon icons.
   - **Sidebar Component (`sidebar.component.ts`)**:
     - Glassmorphic dark panel with collapsible state (`280px` expanded <-> `72px` icon-only).
     - Smooth CSS width transition.
     - Module branding header, active tab pill links with indicator bar, quick stats cards, "Back to Modules" link.
   - **Breadcrumb Component (`breadcrumb.component.ts`)**:
     - Navigation path: Module Picker -> [Module Name] -> [Current Tab].

2. **Landing Page (`frontend/src/app/features/landing/`)**:
   - `LandingComponent`: Hero banner with subtle gradient backdrop.
   - `ModuleCardComponent`: Glassmorphic module card with left accent border:
     - Shows module logo, label, client name.
     - Environment pills (Live/Test status indicators with hover glow).
     - Clicking environment selects environment via API and redirects to `/modules/:key`.
     - Shimmer "Coming Soon" badge for disabled integrations.

3. **Routing (`app.routes.ts`)**:
   - `/` -> `LandingComponent`
   - `/modules/:key` -> `ModuleShellComponent` (with child tab routes: `order`, `api`, `database`, `test`, `requests`, `validation`).

**Verification Steps**:
- Run Angular app and open `http://localhost:4200`.
- All 5 module cards render with staggered fade-in animation.
- Clicking an active environment pill triggers API call and navigates to module shell.
- Sidebar collapses and expands smoothly on icon click.
```

---

## Session 9 — Flat Order Feature Module (GHC & UPC Order Builder)

```markdown
Read `implementation_plan.md`.
This is Session 9 of 14. Your goal is to build the Flat Order module interface used by GHC E-Commerce and UPC E-Commerce.

1. **Flat Order Shell (`frontend/src/app/features/flat-order/`)**:
   - `FlatOrderComponent`: Manages state synchronization with `.NET` API.
   - Conditional rendering: Automatically hides Delivery Card and specific GHC delivery fields when `module.key === 'upc_ecommerce'`.

2. **Sub-components**:
   - `OrderInfoComponent`: Branch code, order code, status selectors, order notes.
   - `ClientInfoComponent`: Consumer lookup by phone + client detail inputs.
   - `DeliveryInfoComponent`: GHC delivery times & delivery payment status (hidden for UPC).
   - `ProductsTableComponent`: Item table with unit price, qty, VAT, discount, estimated totals, and action buttons.
   - `PaymentsTableComponent`: Payment method grid showing method, status, amount, transaction ID, payment options. Enforces UPC restriction against `PostToCredit`.
   - `QuickStatsComponent`: Live visual cards for Total Amount, Paid Amount, Remaining Balance with animated count-up.
   - `ApiConfigComponent`: Send Request form, API endpoint selector, compile JSON viewer, Send Order button with loading spinner, Cancel Order inline section.

3. **Modals (using Angular CDK Overlay / Dialog)**:
   - `AddProductDialog`: Includes in-modal UPC material lookup by 6-digit number when module is UPC.
   - `EditProductDialog`: Update existing product entry.
   - `AddPaymentDialog` & `EditPaymentDialog`: Payment method picker with option commission & customer credit inputs.

**Verification Steps**:
- Open GHC E-Commerce module -> verify Delivery card appears, Add Product/Payment works, totals compute correctly.
- Open UPC E-Commerce module -> verify Delivery card is absent, PostToCredit payment method is blocked, in-modal item lookup functions.
- Test "Export JSON" button -> compiled JSON matches backend output.
```

---

## Session 10 — Uni-Commerce Feature Module (GHC Invoice Builder)

```markdown
Read `implementation_plan.md`.
This is Session 10 of 14. Your goal is to build the GHC Uni-Commerce invoice builder feature module.

1. **Uni-Commerce Shell (`frontend/src/app/features/unicommerce/`)**:
   - `UnicommerceComponent`: Invoice state manager calling backend `/api/modules/ghc_unicommerce/*`.

2. **Sub-components**:
   - `ConsumerSectionComponent`: Consumer phone lookup, populates `InvoiceConsumer` fields (`FirstName`, `LastName`, `Email`, `NationalId`, etc.).
   - `DeliverySectionComponent`: Delivery phone, address, location URL, delivery notes, delivery fees input.
   - `RowItemsTableComponent`: Flat invoice item table (Quantity, MaterialNumber, Barcode, ItemPrice, ItemDiscount, computed GrossAmount, RowTotalDiscount, ItemVat, RowTotalVat, NetAmount). Includes item lookup modal.
   - `OrderFieldsComponent`: ReferenceNumber, OnlineOrderNumber, IsReturn checkbox (conditionally displays `ParentReferenceNumber`), OrderCreationDate picker, CustomerName dropdown (AMAZON, ARAMEX, etc.), PaidOnlineAmount & PaidWithPointsAmount inputs.
   - `InvoiceSummaryComponent`: Live displays for computed GrossAmount, TotalDiscount, TotalVat, NetAmount, and calculated CustomerCreditAmount.

**Verification Steps**:
- Open GHC Uni-Commerce module -> UI loads invoice builder layout.
- Add row item -> GrossAmount, ItemVat, RowTotalVat, NetAmount compute automatically matching Uni-Commerce formulas.
- Check `IsReturn` -> `ParentReferenceNumber` field becomes visible and required.
- Test export JSON -> payload matches examples in `docs/request_examples/GHC Uni-Commerce/`.
```

---

## Session 11 — Order Requests Feature Module (Sent Order History with Cancel & Resend)

```markdown
Read `implementation_plan.md`.
This is Session 11 of 14. Your goal is to build the Order Requests feature module (per-module order history, full JSON viewer, cancel, and resend).

1. **Order Requests Shell (`frontend/src/app/features/order-requests/`)**:
   - `OrderRequestsComponent`: Tab component embedded in every module's shell.

2. **Sub-components**:
   - `FilterBarComponent`: Search input (by order code), status filter dropdown (All/Success/Failed/Cancelled), date range pickers, environment filter dropdown, clear button. (Client-side fast filtering).
   - `OrderCardComponent`: Expandable accordion row for each sent order:
     - **Header Bar**: Status dot (green success, red fail, yellow cancelled), Order Code, relative timestamp (e.g. "5 minutes ago"), Environment badge, HTTP Status code badge.
     - **Expanded Content**:
       - Metadata card: Order code, API URL sent, timestamp.
       - Request Payload card: Collapsible JSON viewer with "Copy Request" button.
       - Response card: Collapsible JSON viewer with "Copy Response" button.
       - Action bar: 🔄 **Resend Order** button, ❌ **Cancel Order** button, 📋 **Copy Payload** button.
   - `CancelDialogComponent`: Inline modal requesting order number and cancellation reason.

3. **Flow Integration**:
   - Sending an order in Flat Order or Uni-Commerce automatically adds the entry to `OrderHistoryService` and provides a quick link to Order Requests.
   - Resend Order opens confirmation modal, re-posts payload with updated branch/time, and appends new history entry.

**Verification Steps**:
- Send an order from GHC E-Commerce -> click "Order Requests" tab -> order appears at top of list.
- Click order card to expand -> view full Request and Response JSON blocks.
- Click "Copy Request" -> notification toast displays "Copied to clipboard!".
- Click "Cancel Order" -> cancel dialog submits request and marks card as Cancelled.
```

---

## Session 12 — UPC Order Validation Feature Module (DB Order Search & Details Modal)

```markdown
Read `implementation_plan.md`.
This is Session 12 of 14. Your goal is to build the UPC-only Order Validation tab for searching SQL Server DB order tables and displaying details.

1. **Order Validation Shell (`frontend/src/app/features/order-validation/`)**:
   - `OrderValidationComponent`: Registered exclusively under `upc_ecommerce` module routes.

2. **Sub-components**:
   - `SearchFormComponent`: Multi-criteria filter form (Order Number, Client Phone, Branch Code, Status 1-9 dropdown, Date From, Date To).
   - `ResultsGridComponent`: Data table displaying:
     - Order #, Branch, Status badge (1-New, 2-Confirmed, 3-Ready, 4-With Delegate, 5-Rejected, 6-Canceled Client, 7-Canceled Admin, 8-Processing, 9-Done).
     - Order Creation Date, Invoice Barcode, Invoice Date, Creation vs Invoice comparison pill.
     - Actions: "View Details" button, "Resend to Branch" button.
   - `OrderDetailsModalComponent`: Modal showing full order headers, line items, transaction history, and invoice status fetched directly from DB.
   - `ResendOrderModalComponent`: Modal allowing user to specify a new branch code for re-dispatching. Blocks resend if status is 4 (With Delegate), 8 (Processing), or 9 (Done).

**Verification Steps**:
- Open UPC E-Commerce module -> "Order Validation" tab is visible.
- Open GHC E-Commerce module -> "Order Validation" tab is NOT visible.
- Perform search in UPC Order Validation -> results populate grid.
- Click "View Details" -> order details modal loads line items and transactions.
```

---

## Session 13 — UI Polish, Animations, Toasts & Signal-based State Refinements

```markdown
Read `implementation_plan.md`.
This is Session 13 of 14. Your goal is to polish the entire Angular 22 user interface, verify animations, and ensure clean Signal state management.

1. **Animation Refinements**:
   - Staggered card entrance using CSS `animation-delay`.
   - Micro-interactions: button hover scale, glassmorphic card lift on hover, glowing focus rings on input elements.
   - Smooth tab switching transitions.
   - Animated count-up for statistics numbers (`AnimateNumberDirective`).

2. **Notification & Feedback Polish**:
   - Verify all legacy `alert()` calls are eliminated; 100% replaced by `ToastService`.
   - Loading skeleton loaders during lookup & search API calls.
   - Empty state components displayed when lists (products, payments, order history) are empty.

3. **Accessibility & Usability**:
   - Keyboard navigation for modals (ESC to close, ENTER to submit).
   - ARIA labels on button controls and icon-only buttons.
   - Custom themed scrollbars across dark and light themes.

**Verification Steps**:
- Walk through all 5 modules in both Dark Mode and Light Mode.
- Verify smooth 60fps glassmorphism animations and transitions.
- Check browser console for 0 errors or warnings.
```

---

## Session 14 — Automated Tests, Full E2E Verification & Documentation

```markdown
Read `implementation_plan.md`.
This is Session 14 of 14. Your final goal is to run all verification test suites, conduct a complete manual walkthrough, and update documentation.

1. **Backend Verification**:
   - Run `dotnet test backend/tests/OnlineOrderTool.Tests`.
   - Verify payload generation for GHC E-Commerce, UPC E-Commerce, and GHC Uni-Commerce matches `docs/request_examples/` 100%.

2. **Frontend Verification**:
   - Run `ng test` in `frontend/` if test specs are present.
   - Test production build: `ng build --configuration production`.

3. **Full End-to-End Walkthrough**:
   - Launch .NET 10 API on port 5000 and Angular on port 4200.
   - Landing page -> module selection -> order creation -> payload export -> send request -> order history verification -> order validation search -> theme toggle.

4. **Documentation**:
   - Update `README.md` reflecting the new .NET 10 + Angular 22 architecture, project layout, setup, and execution commands.
   - Create `docs/api-spec.md` listing all REST endpoints.
   - Create `docs/database-schema.md` detailing SQL Server tables and queries.

**Verification Steps**:
- `dotnet test` passes with 0 failures.
- `ng build` completes without errors.
- `README.md` accurately documents the new stack.
```
