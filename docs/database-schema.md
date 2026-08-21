# Online Order Tool — SQL Server Database Query Schema (the SQL contract)

> **This file is the SQL contract.** No repository query may reference a
> table or column name that is not verified live below, in the "Schema
> discovery" table and the current repository SQL.
>
> A previous version of this document presented **invented** table/column
> names (`H.Status`, `H.CreatedDateTime`, `H.CustomerMobile`, `C.Name`,
> `A.AddressLine1`, `ItemPrices.BranchId`, …) as though they were verified.
> None of them exist on the real schema; every query built against them
> throws `Invalid column name` at runtime. The remediation milestone is indexed
> in [`.ai/HISTORY.md`](../.ai/HISTORY.md); see
> [`ContractTests.cs`](../backend/tests/RmsSupportHub.Tests/ContractTests.cs)
> for the automated guard against regressing this again.
>
> The queries below were originally ported line-for-line from the
> now-removed Python reference (`_legacy_flask/modules/flat_order.py`, part
> of R10's decommission — see git history before this commit if you need
> the original source) and have since been superseded in places by the
> actual C# implementation; where they differ, **the C# source under
> `backend/src/RmsSupportHub.Data/Repositories/` is authoritative**, not
> this document. GHC's `lookup_item` query (§3.1) is verified against the
> live GHC Testing schema on `10.10.20.199/RmsMainStg`.

## Environment routing

UPC Testing keeps its existing `ConnectionStrings:UpcEcommerceTest` resolution.
UPC Production uses the same server-owned server and authentication details,
with the application applying the approved catalog override `RmsMainProd`.
The catalog is not accepted from the browser. Production database validation,
when authorized and available, is read-only; no Production order-create,
cancel, resend, or other mutation is part of repository validation.

---

## 1. Verified schema (live-introspected)

Confirmed live against server `10.10.8.181`, database `RmsMainTest2`, via
`ODBC Driver 17 for SQL Server` (Driver 18 timed out against this host —
keep 17 first, or keep both in a fallback list with 18 first and a short
`Connect Timeout`).

> **U0 re-verification (2026-07-26): the SQL host stays `10.10.8.181`, it is
> not `10.10.10.181`.** `UI_Rework_Plan.md` assumed Testing and Production
> share the single host `10.10.10.181` for both the RMS HTTP API and the SQL
> Server. Only the API half of that is confirmed: `10.10.10.181:8080` answers
> (`HTTP 405` on a bare `GET` to the order endpoint), matching the corrected
> `UpcEcommerceModule.cs` / `appsettings.json` literals. The SQL half does
> not: TCP port 1433 on `10.10.10.181` is closed (connection refused), while
> `10.10.8.181:1433` accepts the `UpcEcommerceTest` credentials and returned
> the live `dbo.Branches` data below. Per `UI_Rework_Plan.md` §5 risk 5, this
> document is not rewritten onto an unverified host — `10.10.8.181` remains
> correct for `ConnectionStrings:UpcEcommerceTest` until someone confirms
> otherwise against a real login on `10.10.10.181`. UPC Production derives its
> connection from that server-owned Testing value and changes only the catalog.

| Table | Columns | Notes |
|---|---|---|
| `Consumers` | `Id, FirstName, MiddleName, LastName, PhoneNumber, Gender, BirthDate, Note, IsLoyality, Email, NationalID, Nationality, ConsumerCode, FullName, LastModificationDate, TenantId, Source, CreationDate` | Lookup key: `PhoneNumber`. PK: `Id`. |
| `LoyaltyConsumerAddresses` | `Id, StreetName, FlatNumber, MapLink, IsMaster, ConsumerId, AddressCode, FullAddress, CityId, DistrictId, RegionId, Building, Floor, Landmark, Area` | FK to consumer: `ConsumerId` → `Consumers.Id`. `IsMaster` (bit) flags the primary address — prefer it when a consumer has more than one row; `FullAddress` is the single best free-text address field (fall back to composing `StreetName`/`Building`/`Floor`/`Landmark`/`Area` if `FullAddress` is blank). |
| `OrderRequests` | `Id, OrderNumber, OrderDate, NetTotal, ItemCount, RequestJson, ExceptionMessage, IsSucceeded, ResponseJson` | The raw API call log — one row per send attempt. `OrderNumber` matches the `order_code` sent in the payload. `IsSucceeded` (bit) flags whether the API call itself succeeded, independent of the order's business status in `RequestOrderHeaders`. **`ResponseJson` and `ExceptionMessage` are the two columns the Order Requests feature exists to surface — do not select them in list/search queries (see §3), only in a single-row detail query.** |
| `RequestOrderHeaders` | `Id, BranchCode, OrderNumber, OrderDate, GrossTotal, NetTotal, TotalVat, TotalDiscount, TotalOfferDiscount, BranchName, ConsumerMobile, ConsumerId, AddressCode, OrderStatus, PaidStatus, CashOrdersPaymentAmount, Address, OrderMobileNumber, CashOnDeliveryFees, ConsumerCode, ParentOrderNumber, TotalItemDiscount, OrderType, HandledByPosMachineId, AddressLocation, OrderDiscountPoints, IsDelivery, OrderMinOrderFees, CouponsCode, CouponsType, CouponsValue, OrderPaymentMethod, CouponCampaignCode, PaidOrdersPaymentAmount, DeliveryFees, OrderNote, FailedOrderStatus, CallCenterId, InvoiceTypeId, ReceivedOrderJson, AttachmentIdentifier, TotalBbyDiscount, TotalCustomerDiscount, TotalPriceCutDiscount, RejectionMessage, DeliveryDate, DeliveryFromTime, DeliveryNumber, DeliveryRemarks, DeliveryToTime, ShippingAddress2, ToPrintDateTime, ToPrintRemarks, ToPrintStatus, FullfilmentPlant` | `Id` is the `orderHeaderId` that `RequestOrderDetails`/`RequestOrderTransactions` join on. `OrderNumber` joins to `OrderRequests.OrderNumber` and to `Invoices.OnlineOrderNumber`. `OrderStatus` is the 1–9 status int (§2). `OrderDate` is the order creation date. `BranchCode` is what a resend changes. **There is no `Status`, `CreatedDateTime`, `CustomerMobile`, `CustomerName`, `ShippingAddress`, `Notes`, or `UpdatedDateTime` column** — those were invented. |
| `RequestOrderDetails` | `Id, RequestOrderHeaderId, Quantity, TotalPrice, TotalOfferDiscount, OfferMessage, ItemName, MaterialNumber, UnitPriceBeforeDiscount, OfferCode, UnitOfferDiscount, UnitPrice, TotalDiscount, TotalItemDiscount, UnitItemDiscount, UnitTotalDiscount, ItemTotalVat, ItemVat, ItemVatPercentage, BbyDiscount, CustomerDiscount, PriceCutDiscount` | FK: `RequestOrderHeaderId` → `RequestOrderHeaders.Id`. **There is no `ItemCode`, `DiscountAmount`, `VatAmount`, or `LineTotal` column** — those were invented; the real names are `MaterialNumber`, `TotalDiscount`, `ItemVat`, `TotalPrice`. |
| `RequestOrderTransactions` | `Id, RequestOrderHeaderId, PaymentAmount, PaymentMethodId, BankCardId, ECommercePaymentMethod, ECommercePaymentOption, OptionCommission, PaymentStatus, TransactionCode, BankCode, CardName` | FK: `RequestOrderHeaderId` → `RequestOrderHeaders.Id`. **There is no `PaymentMethod`, `Amount`, or `TransactionId` column** — the real names are `ECommercePaymentMethod`, `PaymentAmount`, `TransactionCode`. |
| `Invoices` | `Id, Barcode, OpenDate, CloseDate, Discount, OfferDiscount, BbyCode, ManualDiscount, CustomDiscount, TotalDiscount, Tax, GrossAmount, NetAmount, PaidAmount, ChangeAmount, ShiftId, InvoiceTypeId, ParentInvoiceId, ConsumerId, PosMachineId, BranchId, ModifiedBy, LastModifiedOn, TotalBeneficiaryShare, TotalCoverage, UsedPointsDocNo, CouponCode, CouponDiscount, DeliveryFees, CourierId, MinOrderFees, OnlineOrderNumber, IsSaudi, WasfatyPrescripionId, AttachmentIdentifier, ReferenceNumber, Notes, MedicalCardId, TreatmentRecordId, BrokerId, CloseDateLocalTime, …` | Join: `Invoices.OnlineOrderNumber = RequestOrderHeaders.OrderNumber` (and = `OrderRequests.OrderNumber`). Barcode column: `Barcode`. Invoice date for creation-vs-invoice comparisons: `CloseDateLocalTime`. **There is no `Invoices.OrderNumber` or `Invoices.CreatedDateTime` column** — the real names are `OnlineOrderNumber` and `CloseDateLocalTime`. |
| `Branches` | `Id, Name, NativeName, BranchCode, IsActive, Address, VatNumber, LATIDUTE, Description, InternalDeliveryDistance, MaximumDeliveryDistance, Email, OpeningTimes, Phone1, Phone2, LONGITUDE, Area, Fax, RegionId, SalesDistrictId, SalesOfficeId, NationalSalesManagerId, StoreManagerId, RegionManagerId, ASS_StoreManagerId, StoreManager2Id, StoreManager3Id, Labour1Id, Labour2Id, Labour3Id, PhyscianId, ClinicpharmacistId, NutrionanestId, RasdGlnNumber, RasdUserName, RasdPassword, IsRasdEnable, LastOnline, MonthlyTarget, DeliveryFees, BranchInstallStatus, CreatedBy, CreatedOn, ModifiedBy, LastModifiedOn, IsDeleted, DeletedBy, WasfatyPassword, WasfatySenderId, WasfatyUserName, WasfatyDivision, LocationCityId, BranchGroupId, InstallationGuid, ReleaseNumber, MachinesAdminUserName, MachinesAdminEncryptedPassword, CommercialRegistration, IsEnableUploadCashClearanceToERP, IsEnableUploadInvoicesToERP, IsTestingBranch, QitafBranchCode, TenantId, UploadInvoicesToErpType, UploadTillTime, IsZatcaEnable, CloseTime, OpenTime, IsOnline, DefaultCurrencyId, DefaultBranchCurrencyId` | PK: `Id`. Human-readable name: `Name` (English), `NativeName` (Arabic). `BranchCode` is nullable and is what `BranchItemUnitOfMeasures`/`RequestOrderHeaders` join on. Active flag: **`IsActive`** (bit, `NOT NULL`) — filter a branch picker on this; also note `IsDeleted` (bit, `NOT NULL`) and `IsTestingBranch` (bit, `NOT NULL`), which a picker should likely also exclude/flag but which U3 must decide on explicitly rather than assume. Confirmed live against server `10.10.8.181`, database `RmsMainTest2`, via `sqlcmd`/ODBC Driver 18, 2026-07-26 — see §5 note below on why this host, not `10.10.10.181`, is used for the database connection. |

**Neither `OrderRequests` nor `Invoices` is 1:1 with `OrderNumber`** — retries
and re-invoicing both create extra rows for the same order number. Any
projection of a single latest header/invoice row must preserve the
`ORDER BY Id DESC` rule: the base-only list path uses `OUTER APPLY TOP 1`,
while header-filtered list/count/stats paths use the equivalent ranked
`LatestHeaders` CTE. A plain unranked join would multiply request rows.

## Uni-Commerce Testing history and catalog boundary

The separate Uni-Commerce Testing catalog `RmsEcommerceStg` was verified on
the approved Testing SQL host. Its request-history source is a different,
bounded integration-attempt table:

| Table | Columns | Notes |
|---|---|---|
| `ExternalInvoiceRequests` | `Id, ReferenceNumber, RequestJson, Success, Message, RequestUtcDate, ExternalInvoiceId` | One row per Uni-Commerce external-invoice attempt. `ReferenceNumber` is the searchable order/reference key; `RequestUtcDate` is the history timestamp; `Success` and `Message` provide the outcome. It has no branch, business-status, totals, line, or cancellation columns, so `GhcUnicommerceOrderRequestRepository` exposes only the common read-only attempt shape and does not invent the missing fields. |

The verified Uni-Commerce catalog contains no item master/catalog table. Item
lookup therefore remains capability-gated rather than using a guessed query.

## 2. Order status decode map

| Code | Label | Resend blocked | Cancel blocked |
|---|---|---|---|
| 1 | New | ✓ | |
| 2 | Confirmed | | |
| 3 | Ready | | |
| 4 | With_Delegate | ✓ | |
| 5 | Rejected | | ✓ |
| 6 | CanceledByClient | | ✓ |
| 7 | CanceledByAdmin | | ✓ |
| 8 | Processing | | |
| 9 | Done | | ✓ |

`RequestOrderHeaders.OrderStatus` holds this code. Resend-blocked = `{1, 4}`;
cancel-blocked = `{5, 6, 7, 9}`.

---

## 3. The four verified queries

Ported line-for-line from `_legacy_flask/modules/flat_order.py`. Do not
rephrase, reorder joins, or rename columns — treat these as the executable
spec.

### 3.1 GHC item lookup (`FlatOrderItemRepository`) — **verified live**

The query is verified read-only against GHC Testing `RmsMainStg` on
`10.10.20.199`. The live schema uses `BranchItemUnitOfMeasures` and
`ItemUnitOfMeasurePrices`; `branchCode` is optional in the shared lookup
contract and narrows the branch-specific price row when supplied.

```sql
SELECT TOP 1
    I.MaterialNumber AS ItemCode,
    IUOMB.UniversalBarCode AS ItemBarcode,
    I.Name AS ItemNameEn,
    I.NativeName AS ItemNameAr,
    IP.Price AS UnitPrice,
    TT.Rate AS VatPercentage
FROM dbo.Items AS I
    LEFT JOIN dbo.TaxTypes AS TT ON I.SapTaxCode = TT.Code
    INNER JOIN dbo.BranchItemUnitOfMeasures AS BIUOM ON I.Id = BIUOM.ItemId
    INNER JOIN dbo.ItemUnitOfMeasureBarCodes AS IUOMB ON BIUOM.Id = IUOMB.BranchItemUnitOfMeasureId
    LEFT JOIN dbo.ItemUnitOfMeasurePrices AS IP ON BIUOM.Id = IP.BranchItemUnitOfMeasureId
    LEFT JOIN dbo.Branches AS B ON B.Id = BIUOM.BranchId
WHERE RIGHT(I.MaterialNumber, 6) = @MaterialNumber
  AND I.IsDeleted = 0
  AND (@BranchCode IS NULL OR B.BranchCode = @BranchCode)
  AND IP.IsActive = 1
  AND IP.Price IS NOT NULL
  AND (IP.ToDate IS NULL OR IP.ToDate > GETDATE())
ORDER BY I.Id DESC, BIUOM.IsBase DESC, IP.Price DESC;
```

`@MaterialNumber` must be exactly 6 digits (validated before the query runs).
Returns barcode (`UniversalBarCode`), English name (`Name`), Arabic name
(`NativeName`), unit price, and VAT rate from the verified Testing schema.

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

### 3.4 Order Requests list/detail (`OrderRequestRepository`) — **verified live, implemented in R4/R5/R9**

The instruction behind this whole feature: *"The request and its response
are already saved in table `OrderRequests`, so no need to save them
locally."* **`OrderRequests` is the base table**, not `RequestOrderHeaders`
— an earlier draft of this repository (`UpcOrderValidationRepository`,
deleted in R5) queried `RequestOrderHeaders` first, which meant orders that
never produced a header (most failures) were invisible. The list repository
uses two verified shapes: base-only filters page `OrderRequests` first and
then apply the latest header/invoice lookups; phone, branch, and status
filters use a `LatestHeaders` CTE with `ROW_NUMBER() OVER (PARTITION BY
OrderNumber ORDER BY Id DESC)` so the latest header is joined before filtering.

```sql
-- List (RmsSupportHub.Data/Repositories/OrderRequestRepository.cs::BuildListSql)
-- RequestJson/ResponseJson are never selected here -- only DATALENGTH/existence,
-- so the list stays fast regardless of blob size. Paginated with OFFSET/FETCH.
-- Canonical predicates are shared by list, count, and stats.
-- Base-only list path (order number, outcome, exception, dates):
WITH PagedRequests AS (
    SELECT R.Id, R.OrderNumber, R.OrderDate, R.NetTotal, R.ItemCount,
           R.IsSucceeded, DATALENGTH(R.RequestJson) AS RequestBytes,
           CAST(CASE WHEN R.ResponseJson IS NULL THEN 0 ELSE 1 END AS BIT) AS HasResponse
    FROM dbo.OrderRequests AS R
    WHERE R.OrderDate >= @DateFrom
      AND R.OrderDate < DATEADD(day, 1, @DateTo)
    ORDER BY R.OrderDate DESC, R.Id DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
)
SELECT R.Id, R.OrderNumber, R.OrderDate, R.NetTotal, R.ItemCount,
       R.IsSucceeded, R.RequestBytes, R.HasResponse,
       H.Id AS OrderHeaderId, H.BranchCode, H.BranchName, H.OrderStatus,
       H.ParentOrderNumber,
       I.Barcode AS InvoiceBarcode, I.CloseDateLocalTime AS InvoiceDate
FROM PagedRequests AS R
OUTER APPLY (SELECT TOP 1 Id, BranchCode, BranchName, OrderStatus,
                    ParentOrderNumber, ConsumerMobile
             FROM dbo.RequestOrderHeaders
             WHERE OrderNumber = R.OrderNumber
             ORDER BY Id DESC) AS H
OUTER APPLY (SELECT TOP 1 Barcode, CloseDateLocalTime
             FROM dbo.Invoices
             WHERE OnlineOrderNumber = R.OrderNumber
             ORDER BY Id DESC) AS I;
```

**Detail** (`GetDetailAsync`) is the only query that reads the blobs, keyed
by `OrderRequests.Id`: `RequestJson`, `ResponseJson`, `ExceptionMessage`
from `OrderRequests`, then a single most-recent `RequestOrderHeaders` row by
`OrderNumber` (now including `RejectionMessage`, added in R9 — a real,
verified column that R4 never selected), its `RequestOrderDetails` /
`RequestOrderTransactions` filtered by `RequestOrderHeaderId`, and the most
recent `Invoices` row by `OnlineOrderNumber`.

**Resend** rebuilds the payload from that specific attempt's own stored
`RequestJson` (never the live in-progress draft) — the read is simply
`SELECT RequestJson FROM OrderRequests WHERE Id = @Id`. The server verifies
`order_code` against the stored `OrderNumber`, preserves that number and every
unknown field, and changes only `branch_code` before re-sending; if no target
branch is supplied, the original branch is reused.

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

---

## 6. Order Requests performance indexes

The original live reproduction showed the unfiltered list/stats path timing
out around the 15-second command limit. `RequestOrderHeaders.OrderNumber` and
`Invoices.OnlineOrderNumber` had no supporting join/order indexes in the UPC
Testing database, while the base `OrderRequests` search was already indexed.
The repository now also avoids loading raw JSON in list results, pages base
rows before lookups when filters permit, and ranks headers once when a
header-derived filter is required. The ten-row dashboard path first selects
the newest base rows, then enriches only their order numbers through bounded
set-based header/invoice CTEs so an unindexed external database does not run
one full-table lookup per returned row.

The reviewed, idempotent external-database script is
[`docs/sql/order-requests-performance-indexes.sql`](sql/order-requests-performance-indexes.sql).
It adds these covering indexes when they are absent:

```sql
IX_RequestOrderHeaders_OrderNumber_Id
    (RequestOrderHeaders.OrderNumber, Id DESC)
    INCLUDE (BranchCode, BranchName, OrderStatus, ParentOrderNumber, ConsumerMobile)
IX_Invoices_OnlineOrderNumber_Id
    (Invoices.OnlineOrderNumber, Id DESC)
    INCLUDE (Barcode, CloseDateLocalTime)
```

The script was applied only to the approved UPC Testing database during the
2026-08-04 read-only verification. A follow-up read-only API matrix on
2026-08-05 returned HTTP 200 for the combined list/count/stats endpoint: two
unfiltered page-25 reads took 1.45-1.80 seconds, page-200 reads took
1.45-2.57 seconds, branch filtering took 0.66-1.06 seconds, and the broad
status-3 filter took 3.14-3.70 seconds. A known exact fixture took
0.30-0.53 seconds. These timings are evidence for the bounded API path, not a
promise about every future data distribution. Production remains untouched and
requires separate database-owner approval. The script includes guarded
rollback statements; it is not part of the application migration pipeline.
