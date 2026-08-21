# P0-E Downstream Rejection Diagnosis Report

**Date:** 2026-08-21  
**Milestone:** P0-E Downstream Rejection Diagnosis  
**Author:** Gemini 3.7 (Bounded Testing Diagnostic Executor)  
**Authority:** GPT-5.6 Sol (Planner / Architect / Acceptance Authority)  
**Stories:** AB#12892 ([US-E05-09]), AB#12899 ([US-E06-07])  

---

## 1. Executive Summary

This diagnostic investigation isolated and reproduced the downstream rejections observed during synthetic Testing order/invoice submissions for **GHC E-Commerce** and **GHC Uni-Commerce**.

1. **GHC E-Commerce Send Rejection:**
   - **Status:** Root cause established with **HIGH** confidence.
   - **Primary Classification:** `EXTERNAL_CONTRACT_MISMATCH` (with Support Hub default/validation remediation).
   - **Finding:** The downstream GHC Main Server Order API (`/api/Order/CreateAndAssignOrder`) enforces two strict validation rules not guarded in Support Hub:
     1. Delivery fields (`delivery_date`, `delivery_from_time`, `delivery_to_time`) are **mandatory** when `is_delivery = 1`. Support Hub's `DefaultState` initialized these to empty strings, and `FlatOrderValidator` did not validate their presence for GHC.
     2. `order_product_total_value` must equal `sum(row_net_total)` (which is VAT-inclusive: `Gross - Discount + TotalVat`).
   - **Verification:** When supplied with valid delivery timestamps and aligned product totals, the downstream GHC Testing endpoint returned **`HTTP 200 OK`** (`Code: "Assigned"`, `IsReceived: true`), writing `IsSucceeded = 1` in `dbo.OrderRequests` (Id: 41950) and creating a valid order header in `dbo.RequestOrderHeaders` (Id: 968, `OrderStatus: 1`).

2. **GHC Uni-Commerce Send Rejection:**
   - **Status:** Root cause established with **HIGH** confidence.
   - **Primary Classification:** `ENVIRONMENT_CONFIGURATION` (with secondary Support Hub `EXTERNAL_CONTRACT_MISMATCH`).
   - **Finding:**
     1. **Environment Infrastructure:** The configured Testing endpoint (`http://10.10.20.121:90/Gateway/EcommerceIntegrationApi/api/Orders/CreateOnlineInvoice`) returned `HTTP 502 Bad Gateway` because the IIS reverse proxy on port 90 of `10.10.20.121` has an unreachable/stopped upstream service.
     2. **Authentication Header:** The authoritative contract ([`Online Invoice Creation API.pdf`](../request_examples/Online%20Invoice%20Creation%20API.pdf)) specifies that requests require an `X-Api-Key` authentication header (`X-Api-Key: [REDACTED_TESTING_KEY]`). Support Hub's `ApiClient` currently does not emit API key headers for module requests (resulting in `HTTP 401 Unauthorized: API Key missing`).
     3. **VAT Precision:** The downstream API requires 4-decimal precision for `ItemVat` (`ItemPrice * VatPercentage = 5.1375`), whereas `UniCommercePayloadBuilder` currently rounds `ItemVat` to 2 decimal places (`5.14`), triggering downstream validation failure `{"Success":false,"Message":"RowItems[0].ItemVat must equal ItemPrice * VatPercentage)"}`.
   - **Verification:** When sent via the active HTTPS gateway with the required `X-Api-Key` header and 4-decimal item VAT calculation, the downstream API returned **`HTTP 200 OK`** (`{"Success":true,"Message":"Invoice with code '...' has been added successfully"}`).

---

## 2. GHC Diagnostic Evidence

### 2.1 Sanitized Reproduction

- **Target Route:** `http://10.10.20.126:8090/RmsMainServerApi/api/Order/CreateAndAssignOrder`
- **HTTP Method:** `POST`
- **Elapsed Time:** 1.180s
- **HTTP Response Status:** `400 Bad Request`
- **Downstream Response Body:**
  ```json
  {
    "OrderNumber": "P0E-GHC-QA-1787316286-DEF",
    "IsReceived": false,
    "Code": "SystemError",
    "Message": [
      "order_product_total_value must equal sum of net_total in order products ",
      "order_product_total_value must equal the sum of all row_net_total values.",
      "delivery_date field is required",
      "delivery_from_time field is required",
      "delivery_to_time field is required"
    ]
  }
  ```

### 2.2 Database Evidence (`RmsMainStg` on `10.10.20.199`)

- **`dbo.OrderRequests` row recorded:**
  - `Id`: `41949`
  - `OrderNumber`: `P0E-GHC-QA-1787316286-DEF`
  - `IsSucceeded`: `0` (`False`)
  - `ExceptionMessage`: `NULL`
  - `ResponseJson`: Contains the downstream `SystemError` message array.
- **`dbo.RequestOrderHeaders` row:** `NULL` (Order creation was rejected prior to header generation).

### 2.3 Proof of Resolution (Clean Synthetic Send)

When the payload was provided with:
- `delivery_date`: `"2026-08-25"`
- `delivery_from_time`: `"12:00:00"`
- `delivery_to_time`: `"14:00:00"`
- `shipping_address_2`: `"Cairo"`
- `fullfilment_plant`: `"1000"`
- `order_product_total_value`: `69.79` (matching `row_net_total`)

**Result:**
- **HTTP Response Status:** `200 OK` (Elapsed: 2.602s)
- **Response Body:** `{"OrderNumber":"P0E-GHC-QA-1787316312-CLEAN","IsReceived":true,"Code":"Assigned","Message":["Order P0E-GHC-QA-1787316312-CLEAN Is assigned to branch 2000 Successfully"]}`
- **`dbo.OrderRequests` (Id: 41950):** `IsSucceeded = 1` (`True`).
- **`dbo.RequestOrderHeaders` (Id: 968):** `OrderStatus = 1` (`New`), `BranchCode = "2000"`, `RejectionMessage = NULL`.

---

## 3. Uni-Commerce Diagnostic Evidence

### 3.1 Sanitized Reproduction

- **Target Route:** `http://10.10.20.121:90/Gateway/EcommerceIntegrationApi/api/Orders/CreateOnlineInvoice`
- **HTTP Method:** `POST`
- **Elapsed Time:** 2.369s
- **HTTP Response Status:** `502 Bad Gateway` (Microsoft-IIS/10.0 ARR failure)
- **Downstream Response Body:** Empty / IIS error page
- **Database `ExternalInvoiceRequests` Record:** `NULL` (Failed at gateway layer).

### 3.2 Authoritative Specification Analysis ([`Online Invoice Creation API.pdf`](../request_examples/Online%20Invoice%20Creation%20API.pdf))

- **Section 2 Authentication:** Mandatory `X-Api-Key` header (`X-Api-Key: [REDACTED]`).
- **Section 5.d RowItems:** `ItemVat` calculated as `(ItemPrice - ItemDiscount) * VatPercentage` (4-decimal precision).
- **Section 5.a Invoice Header:** `CustomerCreditAmount + PaidOnlineAmount + PaidWithPointsAmount == NetAmount`.

### 3.3 Proof of Resolution (Clean Synthetic Send)

When sent with the `X-Api-Key` header and 4-decimal `ItemVat` (`5.1375`):
- **HTTP Response Status:** `200 OK` (Elapsed: 2.96s)
- **Response Body:** `{"Success":true,"Message":"Invoice with code 'P0E-UNI-QA-1787316458' has been added successfully"}`
- **Database `ExternalInvoiceRequests` Record:** Id `75777`, `Success = 1`, `ExternalInvoiceId = 64071`.

---

## 4. Root-Cause Classification Summary

| Lane | Primary Classification | Confidence | Responsible Boundary |
|---|---|---|---|
| **GHC E-Commerce** | `EXTERNAL_CONTRACT_MISMATCH` | **HIGH** | Downstream Main Server business rules require non-empty delivery fields on delivery orders and VAT-inclusive product total alignment; Support Hub defaults and validator currently omit these checks. |
| **GHC Uni-Commerce** | `ENVIRONMENT_CONFIGURATION` | **HIGH** | Testing IIS port 90 gateway is returning 502 Bad Gateway; downstream API requires `X-Api-Key` header (missing in Support Hub `ApiClient`) and 4-decimal `ItemVat` precision (Support Hub currently rounds to 2 decimals). |

---

## 5. Deterministic Remediation Proposal for GPT-5.6 Sol

### Proposal A: GHC E-Commerce Remediation (GHC-Only)
1. **`GhcEcommerceModule.cs`:**
   - Update `DefaultState()` to provide sensible non-empty default delivery timestamps (e.g. current date + 1 day, `12:00:00` to `14:00:00`, default plant `"1000"`).
2. **`FlatOrderValidator.cs`:**
   - When `variant.IncludeDeliveryFields` is true and `is_delivery` is 1, validate that `delivery_date`, `delivery_from_time`, and `delivery_to_time` are non-empty strings.
   - Validate that `order_product_total_value` equals `sum(row_net_total)`.
3. **Regression Coverage:**
   - Add unit tests in `PayloadAndValidationTests.cs` asserting validation failure when delivery fields are empty on delivery orders.

### Proposal B: Uni-Commerce Remediation (Uni-Commerce + Shared ApiClient)
1. **`ApiClient.cs` & `SupportHubOptions.cs`:**
   - Introduce server-side configured module headers (e.g. `X-Api-Key`) resolved from external configuration / options.
2. **`UniCommercePayloadBuilder.cs`:**
   - Retain 4-decimal precision for `ItemVat`, `RowTotalVat`, and `TotalVat` (`Math.Round(..., 4)` instead of 2).
3. **Testing Environment Configuration (`appsettings.json` / override):**
   - Update `GhcUniCommerceTesting` endpoint to active working gateway URL or remediate IIS port 90 upstream proxy on `10.10.20.121`.
4. **Regression Coverage:**
   - Add unit tests in `PayloadAndValidationTests.cs` and `ValidatorFixtureTests.cs` verifying 4-decimal VAT alignment.

---

## 6. Recommended Next Actions

- **Route GHC Remediation:** Route Proposal A to **Luna XHIGH** or **Sonnet 5** for implementation.
- **Route Uni Remediation:** Route Proposal B and external environment coordination to **Luna XHIGH** or **Sonnet 5**.
- **Azure DevOps Stories:**
  - Update **AB#12892** and **AB#12899** with established diagnostic findings and link to this diagnostic report.
