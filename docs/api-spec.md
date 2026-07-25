# Online Order Tool — REST API Specification

Base URL: `http://localhost:5000/api`

---

## 1. Module Management

### List All Modules
- **`GET /api/modules`**
- **Response `200 OK`**: List of module objects with available environments.

### Get Module Details
- **`GET /api/modules/{key}`**
- **Response `200 OK`**: `{ module: ModuleDto, state: OrderDraft }`

---

## 2. Order Draft State Management

### Get Current Draft State
- **`GET /api/modules/{key}/state`**
- **Response `200 OK`**: `OrderDraft` object.

### Update Single Draft Field
- **`PUT /api/modules/{key}/order-field`**
- **Request Body**: `{ fieldName: string, value: any }`
- **Response `200 OK`**: `{ success: true, state: OrderDraft }`

### Calculate Totals
- **`GET /api/modules/{key}/calculate-totals`**
- **Response `200 OK`**: `TotalsSummary` (`totalProductAmount`, `totalProductVat`, `deliveryCost`, `totalOrderAmount`, `totalPaidAmount`, `remainingBalance`)

### Export Compiled JSON Payload
- **`GET /api/modules/{key}/export-json`**
- **Response `200 OK`**: Exact JSON structure expected by external API.

### Reset Draft State
- **`POST /api/modules/{key}/load-default`**
- **`POST /api/modules/{key}/clear-all`**

---

## 3. Product & Payment CRUD

### Add Product
- **`POST /api/modules/{key}/products`**
- **Request Body**: `Product` object.

### Delete Product
- **`DELETE /api/modules/{key}/products/{index}`**

### Add Payment
- **`POST /api/modules/{key}/payments`**
- **Request Body**: `Payment` object. Enforces payment status & method restrictions (blocks `PostToCredit` for UPC).

### Delete Payment
- **`DELETE /api/modules/{key}/payments/{index}`**

---

## 4. Lookups & Execution

### Item Lookup (SQL Server DB)
- **`GET /api/modules/{key}/lookup/item?code={materialNumber}&branchCode={branchCode}`**

### Consumer Lookup (SQL Server DB)
- **`GET /api/modules/{key}/lookup/consumer?phone={phone}`**

### Send Order Request
- **`POST /api/modules/{key}/send-request`**
- **Request Body**: `{ environmentKey?: string, customApiUrl?: string }`
- **Response `200 OK`**: `{ success: boolean, statusCode: int, responseText: string, urlSent: string, historyEntryId: Guid }`

### Order History & Cancellation
- **`GET /api/modules/{key}/order-history`**: Returns list of all sent orders.
- **`POST /api/modules/{key}/order-history/{id}/cancel`**: Cancels order and updates history status.

---

## 5. UPC Order Validation (Database Query)

- **`POST /api/modules/upc_ecommerce/validation/search`**: Search DB order requests.
- **`GET /api/modules/upc_ecommerce/validation/order/{orderNumber}`**: Get headers, line items, transactions, and invoice info.
