# Online Order Tool — REST API Specification

Base URL: `http://localhost:5200/api` (dev; see `backend/src/RmsSupportHub.Api/Properties/launchSettings.json`). The Angular dev server proxies `/api` to this host via `frontend/proxy.conf.json`.

This document describes the actual routes implemented in
`backend/src/RmsSupportHub.Api/Controllers/*.cs` as of Session R7. It is the
contract the Angular app's `core/models/*.ts` are typed against — keep it in
sync with the controllers, not the other way around.

Every `{key}` path segment is a module key: `ghc_ecommerce`, `upc_ecommerce`,
`ghc_unicommerce`, `oms`, `call_center`. Most action endpoints accept an
optional `envKey` query parameter selecting a named environment (e.g.
`"UPC Testing"`, `"UPC Production"`); when omitted, each module resolves its
explicitly flagged default environment, with Testing remaining the safe UPC
default. Environment resolution is server-owned: the client selects only the
environment key, not an API host or database catalog.

---

## 1. Module Management

### List All Modules
- **`GET /api/modules`**
- **Response `200 OK`**: `ModuleDto[]` — key, label, client, availability, the module's environments, and its `capabilities` (mirrors `ModuleCapabilities`: `draftKind`, `itemLookup`, `consumerLookup`, `orderRequests`, `cancel`, `resend`, `hasDeliveryFields` — added in R7 so the frontend can gate routes/UI on real capability data instead of hardcoded module-key checks; `hasDeliveryFields` added in R10 so the flat-order builder can show/hide the Delivery card without comparing module-key strings — `true` only for `ghc_ecommerce`). No `password`/`db_config`/raw credentials are ever emitted.

The capability object also includes `branchLookup`; it gates the branch route
described in section 4 alongside item and consumer lookup capabilities.

### Environment Reachability
- **`GET /api/modules/health`**
- **Response `200 OK`**: `EnvironmentHealthDto[]` — `{ moduleKey, environmentKey, status, checkedAt }`, one entry per environment of every module.
- `status` is `reachable` | `unreachable` | `unconfigured` (no `ApiUrl` to probe).
- The probe is a **TCP connect only**, ~3s timeout, run in parallel and cached
  process-wide for 30s. It sends no HTTP request and no payload, because the
  upstream URLs are POST-only order operations with no health route. A
  `reachable` result therefore proves a listener accepted the connection, not
  that the API is healthy.
- Reachability is **not** `EnvironmentDto.statusLabel`. That label states the
  lane (`Live`/`Test`/`Soon`) and must stay constant while a host is down; see
  ADR-0019.
- The response never carries the probed host, port, or URL, so the module
  catalog keeps endpoint topology private (B16).
- The literal `health` segment outranks `{key}`, so this never resolves as a
  module lookup.

### Get Module Details + Current Draft
- **`GET /api/modules/{key}`**
- **Response `200 OK`**: `{ module: ModuleDto, state: OrderDraft }`

---

## 2. Order Draft State Management

### Get Current Draft State
- **`GET /api/modules/{key}/state`**
- **Response `200 OK`**: `OrderDraft` object.

### Patch Draft Order Data (batched)
- **`PATCH /api/modules/{key}/order-data`**
- **Request Body**: `{ fields: { [fieldName: string]: any } }` — every field applied in one synchronised load-modify-write (U2, UI_Rework_Plan.md D1).
- **Response `200 OK`**: `{ success: true, state: OrderDraft }`
- **`400 Bad Request`**: empty `fields` object.
- The retired per-field `PUT .../order-field` adapter was removed in U4; this batched route is the only order-data write path.

### Get Active Send Endpoint
- **`GET /api/modules/{key}/endpoint?envKey=`**
- **Response `200 OK`**: `{ environmentKey: string, environment: string, apiUrl: string | null }` — the resolved environment's send endpoint for read-only display in the builder (U4, UI_Rework_Plan.md D13). Deliberately scoped: the module catalog still never carries URLs (see §1), and this route never returns `CancelUrl`, connection-string names, or database config. Uses the same `GetEnvironment` resolution as `send-request`.
- **`404 Not Found`**: unknown module key.

### Calculate Totals
- **`GET /api/modules/{key}/calculate-totals`**
- **Response `200 OK`**: `TotalsSummary` (`totalProductAmount`, `totalProductVat`, `deliveryCost`, `totalOrderAmount`, `totalPaidAmount`, `remainingBalance`)

### Export Compiled JSON Payload
- **`GET /api/modules/{key}/export-json`**
- **Response `200 OK`**: The exact JSON structure `module.BuildPayload(draft)` would send to the external API — built by the module itself, never assembled by the controller (see `IOrderModule.BuildPayload`).

### Reset Draft State
- **`POST /api/modules/{key}/load-default`**
- **`POST /api/modules/{key}/clear-all`**

---

## 3. Product & Payment CRUD

### Add / Update / Delete Product
- **`POST /api/modules/{key}/products`** — body: `Product`
- **`PUT /api/modules/{key}/products/{index}`** — body: `Product`
- **`DELETE /api/modules/{key}/products/{index}`**
- **Response `200 OK`** (all three): `{ success: true, products: Product[], totals: TotalsSummary }`

### Add / Update / Delete Payment
- **`POST /api/modules/{key}/payments`** — body: `Payment`. Enforces payment status & method restrictions (e.g. blocks `PostToCredit` for UPC — see `PaymentController`).
- **`PUT /api/modules/{key}/payments/{index}`** — body: `Payment`
- **`DELETE /api/modules/{key}/payments/{index}`**
- **Response `200 OK`** (all three): `{ success: true, payments: Payment[], totals: TotalsSummary }`

---

## 4. Lookups & Dispatch

### Item Lookup (SQL Server DB)
- **`GET /api/modules/{key}/lookup/item?code={materialNumber}&branchCode={branchCode}&envKey={envKey}`**
- **Response `200 OK`**: `{ success: boolean, message?: string, data?: Product }`
- **`501 Not Implemented`** if the module's `Capabilities.ItemLookup` is `false` (e.g. `ghc_unicommerce`).

### Consumer Lookup (SQL Server DB)
- **`GET /api/modules/{key}/lookup/consumer?phone={phone}&envKey={envKey}`**
- **Response `200 OK`**: `{ success: boolean, message?: string, data?: Consumer }`
- **`501 Not Implemented`** if `Capabilities.ConsumerLookup` is `false`.

### Branch Options (SQL Server DB)
- **`GET /api/modules/{key}/branches?envKey={envKey}&refresh={true|false}`**
- **Response `200 OK`**: `{ success: true, data: BranchOptionDto[] }`, where each
  option is `{ code, name }` and is sourced from the verified active, non-deleted
  `dbo.Branches` rows.
- The route is gated by `Capabilities.BranchLookup`, cached for five minutes,
  and refreshed from the database when `refresh=true`.
- **`501 Not Implemented`** when branch lookup is disabled; upstream database
  failures surface as **`502 Bad Gateway`**.

### Send Order Request
- **`POST /api/modules/{key}/send-request`**
- **Request Body**: `{ environmentKey?: string, customApiUrl?: string }`.
  The existing optional custom endpoint remains available for environments that
  allow it; UPC Production ignores browser overrides and always uses its
  server-owned configured endpoint.
- **Response `200 OK`**: `{ success: boolean, statusCode: int, responseText: string, urlSent: string }`
- **`400 Bad Request`**: `{ success: false, errors: string[] }` when `module.Validate(draft)` fails — the request is never sent.
- The sent request/response is **not** persisted locally; it is already recorded server-side by the upstream API into the `OrderRequests` table, which section 5 below reads from. There is no local `order-history` store or `historyEntryId` — that JSON-file feature was retired in R5.

### Cancel (ad hoc, draft-independent)
- **`POST /api/modules/{key}/cancel-order`**
- **Request Body**: `{ orderNumber: string, cancelReason: string }`
- **Response `200 OK`**: `{ success: boolean, statusCode: int, responseText: string }`
- Posts to the active environment's `CancelUrl` (never `ApiUrl`). Prefer **section 5's** `POST /order-requests/{id}/cancel` when cancelling a specific recorded request — it additionally re-checks `CancelBlockedStatuses` server-side and returns the refreshed request detail.

### Diagnostics
- **`POST /api/modules/{key}/test-endpoint?url={url}`** → `{ status: "Online"|"Offline", url }`
- **`POST /api/modules/{key}/test-db?connectionString={connectionString}`** → `{ status: "Online"|"Offline", database?, error? }`

---

## 5. Order Requests (R4/R5 — the `OrderRequests` table as source of truth)

Every route below is gated by `Capabilities.OrderRequests`. As of R5 this is
`true` only for `upc_ecommerce`; GHC returns:

```
501 Not Implemented
{ "error": "'order-requests' is not available for module 'ghc_ecommerce'." }
```

(`GhcEcommerceModule.Capabilities.OrderRequests` carries a `// TODO(db-creds)`
naming the one-line flip once GHC's live database credentials are confirmed.)

All routes accept `?envKey={envKey}` to pick the environment (and therefore
the connection string) to query.

The frontend's canonical history route is `/order-requests`; the legacy
`/requests` and `/validation` paths redirect to it for bookmarked links. The
detail route is `/order-requests/{id}` and replaces the former validation and
drawer surfaces.

### List
- **`GET /api/modules/{key}/order-requests`**
- **Query**: `q` (alias for `orderNumber`), `orderNumber`, `exactMatch` (optional; defaults to `true`), `phone`, `branchCode`, `status` (single value, 1–9), `statuses` (repeated, e.g. `?statuses=6&statuses=7` — multi-select status chips; takes precedence over `status` when both are given), `succeeded` (bool), `hasException` (bool), `dateFrom`, `dateTo`, `page` (default 1), `pageSize` (default 25, clamped to ≤200), `sort` (`order_date`\|`net_total`\|`item_count`, optionally prefixed `-`/`+`; default `order_date DESC` for filtered results). When no header-derived filters are present, the dashboard/search path intentionally returns only the ten newest matching `OrderRequests` by `Id DESC`.
- **Response `200 OK`**: `{ items: OrderRequestListItemDto[], page, pageSize, total, totalPages, stats: OrderRequestStatsDto }`.
- The list query never selects `RequestJson`/`ResponseJson` — only `DATALENGTH(RequestJson) AS requestBytes` and a `hasResponse` flag, so the grid stays fast regardless of blob size.
- Exact order searches use `R.OrderNumber = @OrderNumber` by default. `exactMatch=false` uses an escaped contains predicate for the supported partial-search behavior. Phone input is normalized to its last nine digits and matches `RIGHT(H.ConsumerMobile, 9)`; branch and status use canonical `BranchCode`/`OrderStatus` values from the latest matching header. Date bounds use `R.OrderDate >= @DateFrom` and an exclusive next-day `R.OrderDate < DATEADD(day, 1, @DateTo)` boundary.
- The list, count, and stats reads share one normalized filter model. Header-derived filters use the ranked latest-header CTE; base-only list filters page `OrderRequests` before applying the latest header/invoice projections. Count and stats use distinct request IDs, and the list never returns raw request/response JSON.
- The Angular route keeps one canonical applied-filter model. Clear All resets that model to its fresh defaults on page 1, including the current-month-to-date window, removes canonical and legacy route-filter aliases through the router's null merge (`orderNumber`, `q`, `request`, `branch`, and `statuses` included), preserves the selected page size, and issues one base-filtered list request for the ten newest matching requests. Reload, manual refresh, auto-refresh, and browser history consume the cleared model rather than restoring the removed filters.

### Detail
- **`GET /api/modules/{key}/order-requests/{id}`**
- **Response `200 OK`**: `{ request: OrderRequestDetailDto, attempts: OrderRequestAttemptDto[], lineage: OrderRequestLineageDto }`. `request` is the only shape carrying `requestJson`/`responseJson`/`exceptionMessage`. `request.header.rejectionMessage` (R9) surfaces `RequestOrderHeaders.RejectionMessage` — a verified real column that R4 never selected.
- **`404 Not Found`** if the id doesn't exist.

### Latest attempt by order number
- **`GET /api/modules/{key}/order-requests/by-order/{orderNumber}`**
- Same response shape as detail, resolved to that order's most recent (`MAX(Id)`) attempt. Used for post-send readback and deep links.

### Cancel
- **`POST /api/modules/{key}/order-requests/{id}/cancel`**
- **Request Body**: `{ reason: string, endpointKey?: string, customUrl?: string }`
- URL resolution uses the existing custom override only where the selected
  environment allows it, otherwise `environment.CancelUrl` is used. UPC
  Production always uses its server-owned `CancelUrl` and never accepts a
  browser override; it never falls back to `environment.ApiUrl` (the
  historical bug this endpoint replaces silently posted cancellations to the
  create-order URL because the endpoint-picker field name was never wired up).
- Server re-checks `OrderRequestStatus.CancelBlockedStatuses` (`{5,6,7,9}` — Rejected/CanceledByClient/CanceledByAdmin/Done) even if the client's button was enabled; a client-side check is not a trust boundary.
- **`409 Conflict`**: `{ error: string, cancelBlockedReason: string }` if blocked — the upstream API is never called.
- **`200 OK`** otherwise: `{ success, statusCode, responseText, urlSent, request: OrderRequestDetailDto }` — the detail is re-read after the call so the UI can show the refreshed status without a second round trip.

### Resend
- **`POST /api/modules/{key}/order-requests/{id}/resend`**
- **Request Body**: `{ branchCode?: string, endpointKey?: string }`
- Reuses **that specific request's own stored `RequestJson`** (not the live in-progress draft), verifies its `order_code` matches the recorded `OrderNumber`, and overrides only `branch_code` when a target is supplied. When omitted, the original stored branch is reused. The original order/request number is sent again unchanged, all unknown payload fields are preserved, and no historical row is mutated.
- The server re-checks the canonical resend rule `OrderRequestStatus.ResendBlockedStatuses` (`{1,4}` — New/With_Delegate) immediately before sending.
- **`409 Conflict`**: `{ error: string, resendBlockedReason: string }` if blocked.
- **`200 OK`** otherwise: `{ success, statusCode, responseText, urlSent }`.
- Posts to `environment.ApiUrl` (a resend is a new order attempt, not a cancellation).

---

## Retired in R5

- `GET /api/modules/{key}/order-history` and `POST /api/modules/{key}/order-history/{id}/cancel` — the local JSON-file order-history store (`OrderHistoryService`/`OrderHistoryEntry`) is gone; section 5 above, reading the real `OrderRequests` table, is its replacement. No `order_history_*.json` files existed on disk at the time of removal (confirmed via a repo-wide search), so there was nothing to archive.
- `POST /api/modules/upc_ecommerce/validation/search` and `GET /api/modules/upc_ecommerce/validation/order/{orderNumber}` (`ValidationController`/`IOrderValidationRepository`/`UpcOrderValidationRepository`) — this path read `RequestOrderHeaders` first and never surfaced `ResponseJson`/`ExceptionMessage`; superseded by section 5.
