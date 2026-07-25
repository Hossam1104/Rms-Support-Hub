# Online Order Tool — SQL Server Database Query Schema (the SQL contract)

> **This file is the SQL contract.** No repository query may reference a table
> or column name that is not either (a) verified live below, in the "Schema
> discovery" table lifted verbatim from
> [`Prompts/UPC_Enhancments_Plan.md`](Prompts/UPC_Enhancments_Plan.md), or
> (b) one of the four real queries below, ported line-for-line from the
> known-good Python reference at
> [`_legacy_flask/modules/flat_order.py`](../_legacy_flask/modules/flat_order.py).
>
> A previous version of this document presented **invented** table/column
> names (`H.Status`, `H.CreatedDateTime`, `H.CustomerMobile`, `C.Name`,
> `A.AddressLine1`, `ItemPrices.BranchId`, …) as though they were verified.
> None of them exist on the real schema; every query built against them
> throws `Invalid column name` at runtime. See
> [`remediation_plan.md`](../remediation_plan.md) §2.2 (B6–B9) for the full
> defect list and [`ContractTests.cs`](../backend/tests/OnlineOrderTool.Tests/ContractTests.cs)
> for the automated guard against regressing this again.
>
> GHC's `lookup_item` query below is carried over from the legacy code
> **unverified** (its comment says "still-guessed") — treat it as the best
> available reference, not as confirmed, until GHC database credentials are
> supplied and it can be checked live the same way the UPC queries were.

---

## 1. Verified schema (live-introspected)

Confirmed live against server `10.10.8.181`, database `RmsMainTest2`, via
`ODBC Driver 17 for SQL Server` (Driver 18 timed out against this host —
keep 17 first, or keep both in a fallback list with 18 first and a short
`Connect Timeout`).

| Table | Columns | Notes |
|---|---|---|
| `Consumers` | `Id, FirstName, MiddleName, LastName, PhoneNumber, Gender, BirthDate, Note, IsLoyality, Email, NationalID, Nationality, ConsumerCode, FullName, LastModificationDate, TenantId, Source, CreationDate` | Lookup key: `PhoneNumber`. PK: `Id`. |
| `LoyaltyConsumerAddresses` | `Id, StreetName, FlatNumber, MapLink, IsMaster, ConsumerId, AddressCode, FullAddress, CityId, DistrictId, RegionId, Building, Floor, Landmark, Area` | FK to consumer: `ConsumerId` → `Consumers.Id`. `IsMaster` (bit) flags the primary address — prefer it when a consumer has more than one row; `FullAddress` is the single best free-text address field (fall back to composing `StreetName`/`Building`/`Floor`/`Landmark`/`Area` if `FullAddress` is blank). |
| `OrderRequests` | `Id, OrderNumber, OrderDate, NetTotal, ItemCount, RequestJson, ExceptionMessage, IsSucceeded, ResponseJson` | The raw API call log — one row per send attempt. `OrderNumber` matches the `order_code` sent in the payload. `IsSucceeded` (bit) flags whether the API call itself succeeded, independent of the order's business status in `RequestOrderHeaders`. **`ResponseJson` and `ExceptionMessage` are the two columns the Order Requests feature exists to surface — do not select them in list/search queries (see §3), only in a single-row detail query.** |
| `RequestOrderHeaders` | `Id, BranchCode, OrderNumber, OrderDate, GrossTotal, NetTotal, TotalVat, TotalDiscount, TotalOfferDiscount, BranchName, ConsumerMobile, ConsumerId, AddressCode, OrderStatus, PaidStatus, CashOrdersPaymentAmount, Address, OrderMobileNumber, CashOnDeliveryFees, ConsumerCode, ParentOrderNumber, TotalItemDiscount, OrderType, HandledByPosMachineId, AddressLocation, OrderDiscountPoints, IsDelivery, OrderMinOrderFees, CouponsCode, CouponsType, CouponsValue, OrderPaymentMethod, CouponCampaignCode, PaidOrdersPaymentAmount, DeliveryFees, OrderNote, FailedOrderStatus, CallCenterId, InvoiceTypeId, ReceivedOrderJson, AttachmentIdentifier, TotalBbyDiscount, TotalCustomerDiscount, TotalPriceCutDiscount, RejectionMessage, DeliveryDate, DeliveryFromTime, DeliveryNumber, DeliveryRemarks, DeliveryToTime, ShippingAddress2, ToPrintDateTime, ToPrintRemarks, ToPrintStatus, FullfilmentPlant` | `Id` is the `orderHeaderId` that `RequestOrderDetails`/`RequestOrderTransactions` join on. `OrderNumber` joins to `OrderRequests.OrderNumber` and to `Invoices.OnlineOrderNumber`. `OrderStatus` is the 1–9 status int (§2). `OrderDate` is the order creation date. `BranchCode` is what a resend changes. **There is no `Status`, `CreatedDateTime`, `CustomerMobile`, `CustomerName`, `ShippingAddress`, `Notes`, or `UpdatedDateTime` column** — those were invented. |
| `RequestOrderDetails` | `Id, RequestOrderHeaderId, Quantity, TotalPrice, TotalOfferDiscount, OfferMessage, ItemName, MaterialNumber, UnitPriceBeforeDiscount, OfferCode, UnitOfferDiscount, UnitPrice, TotalDiscount, TotalItemDiscount, UnitItemDiscount, UnitTotalDiscount, ItemTotalVat, ItemVat, ItemVatPercentage, BbyDiscount, CustomerDiscount, PriceCutDiscount` | FK: `RequestOrderHeaderId` → `RequestOrderHeaders.Id`. **There is no `ItemCode`, `DiscountAmount`, `VatAmount`, or `LineTotal` column** — those were invented; the real names are `MaterialNumber`, `TotalDiscount`, `ItemVat`, `TotalPrice`. |
| `RequestOrderTransactions` | `Id, RequestOrderHeaderId, PaymentAmount, PaymentMethodId, BankCardId, ECommercePaymentMethod, ECommercePaymentOption, OptionCommission, PaymentStatus, TransactionCode, BankCode, CardName` | FK: `RequestOrderHeaderId` → `RequestOrderHeaders.Id`. **There is no `PaymentMethod`, `Amount`, or `TransactionId` column** — the real names are `ECommercePaymentMethod`, `PaymentAmount`, `TransactionCode`. |
| `Invoices` | `Id, Barcode, OpenDate, CloseDate, Discount, OfferDiscount, BbyCode, ManualDiscount, CustomDiscount, TotalDiscount, Tax, GrossAmount, NetAmount, PaidAmount, ChangeAmount, ShiftId, InvoiceTypeId, ParentInvoiceId, ConsumerId, PosMachineId, BranchId, ModifiedBy, LastModifiedOn, TotalBeneficiaryShare, TotalCoverage, UsedPointsDocNo, CouponCode, CouponDiscount, DeliveryFees, CourierId, MinOrderFees, OnlineOrderNumber, IsSaudi, WasfatyPrescripionId, AttachmentIdentifier, ReferenceNumber, Notes, MedicalCardId, TreatmentRecordId, BrokerId, CloseDateLocalTime, …` | Join: `Invoices.OnlineOrderNumber = RequestOrderHeaders.OrderNumber` (and = `OrderRequests.OrderNumber`). Barcode column: `Barcode`. Invoice date for creation-vs-invoice comparisons: `CloseDateLocalTime`. **There is no `Invoices.OrderNumber` or `Invoices.CreatedDateTime` column** — the real names are `OnlineOrderNumber` and `CloseDateLocalTime`. |

**Neither `OrderRequests` nor `Invoices` is 1:1 with `OrderNumber`** — retries
and re-invoicing both create extra rows for the same order number. Every join
onto either table must be `OUTER APPLY (SELECT TOP 1 … ORDER BY Id DESC)`,
never a plain `JOIN`/`LEFT JOIN`, or a single header row silently multiplies
into duplicate result rows.

## 2. Order status decode map

| Code | Label | Resend blocked | Cancel blocked |
|---|---|---|---|
| 1 | New | | |
| 2 | Confirmed | | |
| 3 | Ready | | |
| 4 | With_Delegate | ✓ | |
| 5 | Rejected | | ✓ |
| 6 | CanceledByClient | | ✓ |
| 7 | CanceledByAdmin | | ✓ |
| 8 | Processing | ✓ | |
| 9 | Done | ✓ | ✓ |

`RequestOrderHeaders.OrderStatus` holds this code. Resend-blocked = `{4, 8, 9}`;
cancel-blocked = `{5, 6, 7, 9}`.

---

## 3. The four verified queries

Ported line-for-line from `_legacy_flask/modules/flat_order.py`. Do not
rephrase, reorder joins, or rename columns — treat these as the executable
spec.

### 3.1 GHC item lookup (`FlatOrderItemRepository`) — **unverified, best-guess**

Legacy source calls this "still-guessed" — GHC database credentials have
never been confirmed, so this is the best available reference, not a
verified query. Confirm it live the same way §3.2–3.4 were confirmed as soon
as GHC credentials are available.

```sql
SELECT TOP 1
    I.MaterialNumber, IUOMB.UniversalBarCode,
    I.Name AS EnglishName,
    I.NativeName AS ArabicName,
    IP.Price AS UnitPrice,
    TT.Rate AS VatRate,
    CAST(ROUND(((IP.Price * TT.Rate) / 100) + IP.Price, 2) AS DECIMAL(10, 2)) AS NetPrice
FROM dbo.Items AS I
    LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
    INNER JOIN dbo.ItemUnitOfMeasures AS IUM ON I.Id = IUM.ItemId
    INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON IUM.Id = IUOMB.ItemUnitOfMeasureId
    LEFT JOIN dbo.ItemPrices AS IP ON IUM.Id = IP.ItemUnitOfMeasureId
WHERE RIGHT(I.MaterialNumber, 6) = @MaterialNumber
  AND IP.IsActive = 1
  AND IP.Price IS NOT NULL
  AND IP.ToDate > GETDATE()
  -- optional, only when supplied:
  -- AND EXISTS (SELECT 1 FROM dbo.Customers WHERE CustomerNumber = @CustomerNumber AND IsActive = 1)
  -- AND I.SapTaxCode = @SapTaxCode
  -- AND I.SapMatGeneric = @SapMatGeneric
ORDER BY I.Id DESC;
```

`@MaterialNumber` must be exactly 6 digits (validated before the query runs).
Returns barcode (`UniversalBarCode`), English name (`Name`), Arabic name
(`NativeName`), unit price, VAT rate, and a computed net price — all four of
which were dropped by the previous rewrite (barcode and Arabic name).

### 3.2 UPC branch-ranked item lookup (`UpcItemRepository`) — **verified live**

Branch-specific pricing is the core UPC daily workflow; `branch_code` is
**required**, not optional. When an item has more than one unit-of-measure /
price row for the same branch, the base unit of measure wins, then the
highest price.

```sql
WITH ItemPricesRanked AS (
    SELECT
        B.BranchCode AS Branch_Code,
        I.MaterialNumber AS Full_Material_Number,
        I.Name AS Item_Name_EN,
        I.NativeName AS Item_Name_AR,
        IUOMP.Price AS Item_Price,
        BIUOM.IsBase AS IsBase,
        TT.Rate AS Tax_Rate,
        ROW_NUMBER() OVER (
            PARTITION BY I.Id, B.Id
            ORDER BY BIUOM.IsBase DESC, IUOMP.Price DESC
        ) AS rn
    FROM dbo.Items AS I
        JOIN dbo.BranchItemUnitOfMeasures AS BIUOM ON BIUOM.ItemId = I.Id
        JOIN dbo.ItemUnitOfMeasurePrices AS IUOMP ON IUOMP.BranchItemUnitOfMeasureId = BIUOM.Id
        JOIN dbo.Branches AS B ON B.Id = BIUOM.BranchId
        LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
    WHERE I.MaterialNumber = @MaterialNumber AND B.BranchCode = @BranchCode
)
SELECT TOP 1
    Full_Material_Number, Item_Name_EN, Item_Name_AR, Item_Price, Tax_Rate
FROM ItemPricesRanked
WHERE rn = 1;
```

`@MaterialNumber` is the full 18-digit `MaterialNumber` — pad a 6-digit input
with 12 leading zeros first (see `NormalizeUpcMaterialNumber` below). This
query does **not** touch `dbo.ItemPrices` or `IP.BranchId` — those do not
exist; the branch relationship is `BranchItemUnitOfMeasures.BranchId`.

### 3.3 UPC consumer & address lookup (`UpcConsumerRepository`) — **verified live**

`LoyaltyConsumerAddresses` has no single "the" address per consumer, so the
`OUTER APPLY` prefers the row flagged `IsMaster`, falling back to the most
recently added address if no master is set.

```sql
SELECT TOP 1
    C.Id, C.FirstName, C.MiddleName, C.LastName,
    C.Email, C.PhoneNumber, C.Gender, C.BirthDate, C.ConsumerCode,
    A.FullAddress, A.AddressCode, A.StreetName, A.Building,
    A.Floor, A.Landmark, A.Area
FROM Consumers AS C
    OUTER APPLY (
        SELECT TOP 1
            FullAddress, AddressCode, StreetName, Building, Floor, Landmark, Area
        FROM LoyaltyConsumerAddresses
        WHERE ConsumerId = C.Id
        ORDER BY IsMaster DESC, Id DESC
    ) AS A
WHERE RIGHT(C.PhoneNumber, 9) = @Phone9
ORDER BY C.Id DESC;
```

`@Phone9` is the last 9 digits of the phone number after stripping any `+`,
country code (`966`) and leading trunk `0` (see `NormalizePhoneSearch`
below) — never an exact string match, since the stored format varies. When
`FullAddress` is blank, compose from `StreetName`, `Building`, `Floor`,
`Landmark`, `Area` (join non-empty parts with `", "`).

### 3.4 UPC order search (`UpcOrderValidationRepository` / Order Requests list) — **verified live**

Base table for search is `RequestOrderHeaders`; `OrderRequests` and
`Invoices` are joined via `OUTER APPLY TOP 1` for the reason in §1.

```sql
SELECT TOP 200
    H.Id, H.OrderNumber, H.BranchCode, H.BranchName, H.OrderStatus,
    H.OrderDate, H.ConsumerMobile, H.NetTotal, H.ParentOrderNumber,
    OR2.IsSucceeded,
    I.Barcode, I.CloseDateLocalTime
FROM RequestOrderHeaders AS H
    OUTER APPLY (
        SELECT TOP 1 IsSucceeded
        FROM OrderRequests
        WHERE OrderNumber = H.OrderNumber
        ORDER BY Id DESC
    ) AS OR2
    OUTER APPLY (
        SELECT TOP 1 Barcode, CloseDateLocalTime
        FROM Invoices
        WHERE OnlineOrderNumber = H.OrderNumber
        ORDER BY Id DESC
    ) AS I
WHERE 1 = 1
  -- AND H.OrderNumber = @OrderNumber
  -- AND RIGHT(H.ConsumerMobile, 9) = @Phone9
  -- AND H.BranchCode = @BranchCode
  -- AND H.OrderStatus = @Status
  -- AND H.OrderDate >= @DateFrom
  -- AND H.OrderDate < DATEADD(day, 1, @DateTo)
ORDER BY H.OrderDate DESC;
```

The detail view (`get_upc_order_details` in the legacy source) follows the
same `OUTER APPLY` pattern keyed by `H.Id` (preferred) or `H.OrderNumber`,
and additionally reads `RequestOrderDetails` and `RequestOrderTransactions`
filtered by `RequestOrderHeaderId`. The latest raw payload for a resend
(`get_latest_request_json`) is:

```sql
SELECT TOP 1 RequestJson
FROM OrderRequests
WHERE OrderNumber = @OrderNumber
ORDER BY Id DESC;
```

**The Order Requests feature (see `remediation_plan.md` §2.3, B10/B11) must
additionally select `ResponseJson` and `ExceptionMessage` in its single-row
detail query** — the query above and the legacy source only ever needed
`RequestJson` for resend, so those two columns were never read anywhere in
either codebase. They are the reason the feature exists.

---

## 4. Normalizers

Both are pure functions with no DB dependency; port them verbatim.

**`NormalizeUpcMaterialNumber(raw)`** — UPC's `MaterialNumber` is always 18
digits (typically 12 leading zeros + a 6-digit item number, e.g.
`"000000000000212401"`). Accepts either the full 18-digit code or the bare
6-digit item number and pads the latter with 12 leading zeros. Throws if the
input isn't all-digit or isn't 6 or 18 characters long.

**`NormalizePhoneSearch(phone)`** — strips every non-digit character, then
takes the last 9 digits. Handles `+966556028080`, `966556028080`,
`0556028080`, and `556028080` identically, all collapsing to `556028080`,
matched via `RIGHT(column, 9) = @p`. Throws if fewer than 9 digits remain.

---

## 5. Driver fallback order

`ODBC Driver 18 for SQL Server → ODBC Driver 17 for SQL Server → ODBC Driver 13
for SQL Server → SQL Server Native Client 11.0 → SQL Server`, with the most
recently successful driver tried first and cached for the life of the
connection factory. Driver 18 has been observed to time out against
`10.10.8.181`; keep a short `Connect Timeout` (5s) so the fallback to 17
settles quickly rather than hanging.
