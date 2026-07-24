UPC Enhancements: UI Rearrange, Live Consumer Lookup, Per-Environment DB, and Order Validation

**Context**
The UPC E-Commerce module currently shares `flat_order.html` / `flat_order.js` and the serializer/DB logic in `modules/flat_order.py` with GHC E-Commerce — only the environment URLs and (nominally) the DB config differ. Four UPC-specific requirements now diverge from GHC and must be implemented **without changing GHC behavior**:

1. The "Delivery Information" card is effectively empty for UPC (its From/To time fields were already hidden via `{% if module.key != 'upc_ecommerce' %}` in `flat_order.html:349-364`); only the "Payment Status" select remains (`flat_order.html:365-376`). Move Payment Status into the "Order Information" card and delete the empty Delivery Information card, reflowing the layout.
2. Replace the *guessed* consumer lookup (`modules/flat_order.py:157-201`, querying a placeholder `dbo.Customers`, marked `TODO(db-creds)`) with a real query against `Consumers` joined to `LoyaltyConsumerAddresses` (FK `consumerId`), auto-filling name, address, and the remaining client fields from the phone number.
3. Add order validation — both an automatic post-send status read-back and a new "Order Validation" tab — that queries the order-request tables to show each order's current status, line-item details, transactions, and invoice info.
4. UPC needs a **different database per environment** on the same server/credentials, whereas the codebase currently shares one `db_config` across a module's environments (`modules/db_config.py:22-26`, `modules/upc_ecommerce.py:43,58`).

Everything below is UPC-only and gated so GHC continues to render the Delivery Information card and use its existing consumer lookup unchanged. The companion execution prompts live in `UPC_Enhancments_Prompts.md`.

**Decisions confirmed with the user**
- Order Validation is a **new tab inside the existing UPC page** (gated to `upc_ecommerce`), not a separate page.
- Exact DB column names are **discovered by introspecting the live database** (`SELECT TOP 1 *`) during implementation and recorded in the "Schema discovery" section below before any query is written — no guessing.
- Post-send validation **auto-queries and shows the order status inline** in the send-result area, plus a link into the Order Validation tab.
- Order Validation search supports: **order number, client phone, branch code + status, date range**. The results grid also shows the **invoice barcode** and **invoice date** (`Invoices.CloseDateLocalTime`) joined via `RequestOrderHeaders.OrderNumber = Invoices.OnlineOrderNumber`, displayed alongside the **order creation date** for comparison.

**Database facts (supplied by the user)**
- Connection: server `10.10.8.181`, username `sa`, password `P@ssw0rd`, same connection for both environments.
- Production database: `RmsMainProd`. Test database: `RmsMainTest2`.
- Consumer tables: `Consumers`, and `LoyaltyConsumerAddresses` related to a consumer via `consumerId`.
- Order-request tables: `OrderRequests` (all requests sent from the APIs) → `RequestOrderHeaders` (the request header + current status, related by order number) → `RequestOrderDetails` and `RequestOrderTransactions` (related to the header via `orderHeaderId`).
- Invoice table: `Invoices`, joined `RequestOrderHeaders.OrderNumber = Invoices.OnlineOrderNumber`; invoice barcode from `Invoices`, invoice date from `Invoices.CloseDateLocalTime`.

**Order status codes (decode map, used server-side and mirrored in JS)**
`1 = New` · `2 = Confirmed` (pharmacist confirmed) · `3 = Ready` · `4 = With_Delegate` (executed & invoiced, out for delivery) · `5 = Rejected` (by pharmacy) · `6 = CanceledByClient` · `7 = CanceledByAdmin` · `8 = Processing` (in the POS cart) · `9 = Done` (executed & invoiced, picked up in store).
**Resend rule:** the same order may be sent to another branch **unless** its status is `4 (With_Delegate)`, `8 (Processing)`, or `9 (Done)`.

---

**Schema discovery (confirmed live against `10.10.8.181` / `RmsMainTest2`, ODBC Driver 17 for SQL Server; Driver 18 timed out — keep the existing fallback order)**

- `Consumers`: `Id, FirstName, MiddleName, LastName, PhoneNumber, Gender, BirthDate, Note, IsLoyality, Email, NationalID, Nationality, ConsumerCode, FullName, LastModificationDate, TenantId, Source, CreationDate`. Lookup key: `PhoneNumber`. PK: `Id`.
- `LoyaltyConsumerAddresses`: `Id, StreetName, FlatNumber, MapLink, IsMaster, ConsumerId, AddressCode, FullAddress, CityId, DistrictId, RegionId, Building, Floor, Landmark, Area`. FK to consumer: `ConsumerId` → `Consumers.Id`. `IsMaster` (bit) flags the primary address — prefer it when a consumer has more than one row; `FullAddress` is the single best free-text address field (fall back to composing `StreetName`/`Building`/`Floor`/`Landmark`/`Area` if `FullAddress` is blank).
- `OrderRequests`: `Id, OrderNumber, OrderDate, NetTotal, ItemCount, RequestJson, ExceptionMessage, IsSucceeded, ResponseJson`. `OrderNumber` matches the `order_code` sent in the payload; `IsSucceeded` (bit) flags whether the API call itself succeeded (independent of the order's business status in `RequestOrderHeaders`).
- `RequestOrderHeaders`: `Id, BranchCode, OrderNumber, OrderDate, GrossTotal, NetTotal, TotalVat, TotalDiscount, TotalOfferDiscount, BranchName, ConsumerMobile, ConsumerId, AddressCode, OrderStatus, PaidStatus, CashOrdersPaymentAmount, Address, OrderMobileNumber, CashOnDeliveryFees, ConsumerCode, ParentOrderNumber, TotalItemDiscount, OrderType, HandledByPosMachineId, AddressLocation, OrderDiscountPoints, IsDelivery, OrderMinOrderFees, CouponsCode, CouponsType, CouponsValue, OrderPaymentMethod, CouponCampaignCode, PaidOrdersPaymentAmount, DeliveryFees, OrderNote, FailedOrderStatus, CallCenterId, InvoiceTypeId, ReceivedOrderJson, AttachmentIdentifier, TotalBbyDiscount, TotalCustomerDiscount, TotalPriceCutDiscount, RejectionMessage, DeliveryDate, DeliveryFromTime, DeliveryNumber, DeliveryRemarks, DeliveryToTime, ShippingAddress2, ToPrintDateTime, ToPrintRemarks, ToPrintStatus, FullfilmentPlant`. `Id` is the `orderHeaderId` that `RequestOrderDetails`/`RequestOrderTransactions` join on. `OrderNumber` joins to `OrderRequests.OrderNumber` and to `Invoices.OnlineOrderNumber`. `OrderStatus` is the 1-9 status int (this plan's decode map). `OrderDate` is the order creation date. `BranchCode` is used for search + is the field a resend changes.
- `RequestOrderDetails`: `Id, RequestOrderHeaderId, Quantity, TotalPrice, TotalOfferDiscount, OfferMessage, ItemName, MaterialNumber, UnitPriceBeforeDiscount, OfferCode, UnitOfferDiscount, UnitPrice, TotalDiscount, TotalItemDiscount, UnitItemDiscount, UnitTotalDiscount, ItemTotalVat, ItemVat, ItemVatPercentage, BbyDiscount, CustomerDiscount, PriceCutDiscount`. FK: `RequestOrderHeaderId` → `RequestOrderHeaders.Id`.
- `RequestOrderTransactions`: `Id, RequestOrderHeaderId, PaymentAmount, PaymentMethodId, BankCardId, ECommercePaymentMethod, ECommercePaymentOption, OptionCommission, PaymentStatus, TransactionCode, BankCode, CardName`. FK: `RequestOrderHeaderId` → `RequestOrderHeaders.Id`.
- `Invoices`: `Id, Barcode, OpenDate, CloseDate, Discount, OfferDiscount, BbyCode, ManualDiscount, CustomDiscount, TotalDiscount, Tax, GrossAmount, NetAmount, PaidAmount, ChangeAmount, ShiftId, InvoiceTypeId, ParentInvoiceId, ConsumerId, PosMachineId, BranchId, ModifiedBy, LastModifiedOn, TotalBeneficiaryShare, TotalCoverage, UsedPointsDocNo, CouponCode, CouponDiscount, DeliveryFees, CourierId, MinOrderFees, OnlineOrderNumber, IsSaudi, WasfatyPrescripionId, AttachmentIdentifier, ReferenceNumber, Notes, MedicalCardId, TreatmentRecordId, BrokerId, CloseDateLocalTime, ...` (additional trailing columns omitted for brevity — none needed by this feature). Join: `Invoices.OnlineOrderNumber = RequestOrderHeaders.OrderNumber`. Barcode column: `Barcode`. Invoice date for the creation-vs-invoice comparison: `CloseDateLocalTime`.

---

**Guiding constraint: UPC-only, GHC untouched**
Every change is gated. The two idioms already in the codebase are `{% if module.key == 'upc_ecommerce' %}` in templates and a `module_key != "upc_ecommerce"` guard (mirroring `require_flat_order_module`, `app.py:103-107`) in routes. GHC continues to render the Delivery Information card and use its existing consumer lookup unchanged.

**1. UI rearrange — `flat_order.html` (UPC only)**
- Add a `{% if module.key == 'upc_ecommerce' %}` `col-12` block holding the Payment Status select (bound to `order_payment_status`, options looped from `payment_statuses`) at the end of the Order Information card's `row g-3`, before it closes at `flat_order.html:250`.
- Wrap the entire Delivery Information `grid-item` (`flat_order.html:341-380`) in `{% if module.key != 'upc_ecommerce' %}` so it renders for GHC but disappears for UPC.
- No JS change: Payment Status is a plain `.order-field` persisted by the generic `change` → `updateOrderField` path (`flat_order.js:166-191`); the CSS `grid-container` auto-reflows when a `grid-item` is removed.

**2. Per-environment DB config — `modules/db_config.py`, `modules/base.py`, `modules/upc_ecommerce.py`**
- `ModuleEnvironment` already carries a `db_config` field (`modules/base.py:39`); the shortfall is only that UPC passes the *same* object to both environments (`modules/upc_ecommerce.py:43,58`) and builds one DB manager at init (`modules/upc_ecommerce.py:61`).
- Give UPC two configs — `RmsMainProd` and `RmsMainTest2`, server `10.10.8.181`, `sa`/`P@ssw0rd`, driver `ODBC Driver 17 for SQL Server` — with env-var overrides (`UPC_ECOM_PROD_*` / `UPC_ECOM_TEST_*`) so real creds remain config-only (matching the existing `_db_config` env-prefix convention, `modules/db_config.py:11-18`). Assign prod config to `UPC Production`, test config to `UPC Testing`.
- Make lookups environment-aware: cache a `FlatOrderDatabaseManager` per environment in the UPC module and select by the active environment. Add an optional `env_key: Optional[str] = None` to the lookup signatures in `modules/base.py` (`lookup_consumer_by_phone`, `lookup_item`) and the flat-order module; routes pass `get_active_environment(module).key` (`app.py:75-79`). GHC (envs share one config) is unaffected.

**3. Consumer lookup rewrite — `modules/flat_order.py`, `modules/upc_ecommerce.py`, `app.py`, `flat_order.js`**
- Add a UPC-specific consumer query (a dedicated method / `lookup_upc_consumer_by_phone`) that selects from `Consumers` by phone and LEFT JOINs `LoyaltyConsumerAddresses` on `consumerId`, using the exact columns recorded in "Schema discovery". Reuse the connection helper `FlatOrderDatabaseManager.get_db_connection()` (`modules/flat_order.py:40-85`, driver-fallback + caching) and the parameterized-query pattern. Leave GHC's existing query untouched.
- `module_get_consumer_details` (`app.py:426-443`) already returns `{found, consumer}`; extend the `consumer` dict with the address field(s).
- Extend `prefillConsumer()` (`flat_order.js:352-367`) to also set the address input (`querySelector('[data-field="address"]')` — it has no `id`, `flat_order.html:331-335`) and persist it via `updateOrderField('address', …)` / `order_address`.

**4. Order Validation tab — `flat_order.html`, new `upc_validation.js`, `app.py`, UPC DB manager (UPC only)**
- Frontend: add a sidebar nav item + `tab-pane` (`#validation-tab`), both wrapped `{% if module.key == 'upc_ecommerce' %}`, following the pure-Bootstrap tab pattern (nav `flat_order.html:44-70`, panes within `tab-content` closing at `flat_order.html:819` — no JS registration needed). Load a new `upc_validation.js` only for UPC via a `{% if module.key == 'upc_ecommerce' %}<script>` tag (mirrors the dedicated-file precedent of `unicommerce.js`; keeps shared `flat_order.js` lean). Its fetch calls prefix `window.MODULE_BASE`.
- Search form: order number, client phone, branch code, status dropdown (1–9 with labels), date-from / date-to.
- Results grid: order number, branch, decoded status, **Order Creation Date**, **Invoice Barcode**, **Invoice Date** (`Invoices.CloseDateLocalTime`), and a creation-vs-invoice comparison. Multiple matches all render as rows; each row has a **Details** button.
- Details view (modal/expand): header fields + `RequestOrderDetails` line items + `RequestOrderTransactions` (joined via `orderHeaderId`), plus a **"Resend to another branch"** action enabled only when status ∉ {4, 8, 9}.
- Backend routes (UPC-only guard, mirroring `require_flat_order_module`, `app.py:103-107`):
  - `POST /modules/<module_key>/search-orders` — parameterized WHERE from supplied criteria; `OrderRequests` ⋈ `RequestOrderHeaders` (by order number) LEFT JOIN `Invoices` (`RequestOrderHeaders.OrderNumber = Invoices.OnlineOrderNumber`); returns grid rows using the active environment's DB.
  - `GET /modules/<module_key>/order-details/<order_number>` — header + `RequestOrderDetails` + `RequestOrderTransactions` + invoice info.
- Define the status-decode map once server-side; re-check the resend rule server-side before allowing a resend (which reuses `module_send_request`, `app.py:1204-1276`, with a different `branch_code`).

**5. Post-send auto-validation — `app.py` (UPC only)**
- After a successful send in `module_send_request` (`app.py:1204-1276`), for UPC only, query `RequestOrderHeaders` (⋈ `Invoices`) by the sent order number and include status/invoice info in the JSON response so the send-result area shows the landed status inline, with a link that switches to `#validation-tab`. Non-UPC modules keep current behavior.

---

**Critical files**
- `flat_order.html` — UI rearrange (§1); validation-tab markup + conditional `upc_validation.js` include (§4).
- `flat_order.js` — extend `prefillConsumer` with address (§3).
- `upc_validation.js` (new) — validation tab logic (§4).
- `modules/db_config.py` — per-environment UPC configs (§2).
- `modules/base.py` — add `env_key` to lookup signatures (§2).
- `modules/upc_ecommerce.py` — per-env DB managers, UPC consumer lookup, order search/details methods (§2–4).
- `modules/flat_order.py` — UPC consumer query + order-request/invoice queries reusing `get_db_connection` (§3–4).
- `app.py` — extend `module_get_consumer_details` (§3); new `search-orders` + `order-details` routes (§4); post-send read-back (§5).
- `README.md` / `User_Tutorial.md` — document the new tab + UPC per-env DB (Session 5).

**Reuse (do not reinvent)**
- Connection + driver fallback: `FlatOrderDatabaseManager.get_db_connection()` (`modules/flat_order.py:40-85`).
- Outbound API call for resend: `APIManager.send_order` via `module_send_request` (`app.py:1204-1276`).
- Module/env resolution: `get_module_or_404`, `get_active_environment` (`app.py:68-79`).
- Bootstrap tab pattern (`flat_order.html:44-70`) and dedicated-template/JS precedent (`unicommerce.html`/`unicommerce.js`).

**Frontend note**
`flat_order.html` loads **`flat_order.js`** (`flat_order.html:825`), not `script.js` — `script.js` is stale/dead for this page. All JS work targets `flat_order.js` and the new `upc_validation.js`.

---

**Verification**
- GHC regression: `/modules/ghc_ecommerce/` still shows the Delivery Information card with all its fields.
- UPC page: no Delivery card; Payment Status appears under Order Information; consumer lookup fills name + address from the live DB against the active environment's database (`RmsMainProd` vs `RmsMainTest2`).
- Order Validation tab: multi-criteria search returns a grid; Details shows line items + transactions; invoice barcode/date vs creation date are visible; resend is blocked for status 4/8/9.
- Post-send: a successful UPC send shows the order's DB status inline.
- App runs on `http://localhost:5002`; `verify_payload.py` still passes.

---

**Addendum — Item Lookup moved into the Add Product modal, branch-aware pricing (post Session 5, user-requested)**

Two more UPC-only changes, confirmed live against `RmsMainTest2`:

- **UI**: the item-lookup form (previously only on the Database Connection tab, shared with GHC) now also appears **inside the Add Product modal** on UPC (`flat_order.html`, gated `{% if module.key == 'upc_ecommerce' %}`), so a user doesn't have to leave the Add Product dialog to find an item. It reads the order's own Branch Code field (`[data-field="branch_code"]`) rather than asking for one separately. On UPC, the Database Connection tab's old "Item Lookup" card is replaced with a short note pointing at the modal; GHC's tab-based lookup is untouched.
- **Backend**: UPC's item pricing is **branch-specific**, confirmed via this reference query (`RmsMainTest2`):
  ```sql
  WITH ItemPricesRanked AS (
      SELECT B.Id AS Branch_Id, B.BranchCode AS Branch_Code, I.Id AS Item_Id,
             RIGHT(I.MaterialNumber, 6) AS Item_Number, I.Name AS Item_Name_EN, I.NativeName AS Item_Name_AR,
             IUOMP.Price AS Item_Price, BIUOM.IsBase AS IsBase, TT.Rate AS Tax_Rate, ...
             ROW_NUMBER() OVER (PARTITION BY I.Id, B.Id ORDER BY BIUOM.IsBase DESC, IUOMP.Price DESC) AS rn
      FROM dbo.Items AS I
      JOIN dbo.BranchItemUnitOfMeasures AS BIUOM ON BIUOM.ItemId = I.Id
      JOIN dbo.ItemUnitOfMeasurePrices AS IUOMP ON IUOMP.BranchItemUnitOfMeasureId = BIUOM.Id
      JOIN dbo.Branches AS B ON B.Id = BIUOM.BranchId
      LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
      WHERE I.MaterialNumber = @PaddedMaterial AND B.BranchCode = @branch
  )
  SELECT ... FROM ItemPricesRanked WHERE rn = 1;
  ```
  Implemented as `FlatOrderDatabaseManager.lookup_upc_item(material_number, branch_code)` in `modules/flat_order.py`, wired through `UpcEcommerceModule.lookup_item` (replacing the shared, still-guessed `lookup_item` for UPC only — GHC keeps using it unchanged) and `app.py`'s existing `/get-item-details` route (now also reads/forwards `branch_code` for UPC).
- **MaterialNumber format**: always 18 digits (observed as 12 leading zeros + a 6-digit item number, e.g. `000000000000207350`). Per the user's instruction, the UI/backend accept **either** the full 18-digit code **or** just the 6-digit item number (`normalize_upc_material_number()` in `modules/flat_order.py` pads the 6-digit form with 12 leading zeros); any other length is rejected.
- **Live verification**: material `207350` / branch `P001` (the user's own example) resolved to a real item ("Mothersnest Ashwagandha Gummy", price 130.0, VAT 15%, net 149.5) via both the 6-digit and 18-digit input forms, through the actual HTTP route. Missing `branch_code` correctly 400s. GHC's `/get-item-details` route and page are unaffected.
