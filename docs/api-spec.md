# Online Order Tool — REST API Specification

Base URL: `http://localhost:5200/api` (dev; see `backend/src/RmsSupportHub.Api/Properties/launchSettings.json`). The Angular dev server proxies `/api` to this host via `frontend/proxy.conf.json`.

This document describes the actual routes implemented in
`backend/src/RmsSupportHub.Api/Controllers/*.cs` as of Session R7. It is the
contract the Angular app's `core/models/*.ts` are typed against — keep it in
sync with the controllers, not the other way around.

Every `{key}` path segment is a module key: `ghc_ecommerce`, `upc_ecommerce`,
`ghc_unicommerce`, `oms`, `call_center`. Most action endpoints accept an
optional `envKey` query parameter selecting a named, server-registered
environment (e.g. `"UPC Testing"`, `"UPC Production"`); when omitted, each
module resolves its explicitly flagged default environment. The API starts in
the `Testing` deployment tier. In that tier, a Production environment is
never resolved, probed, queried, or used for a mutation. A deployment must
explicitly select the `Production` tier before a registered Production
environment can become effective. Environment resolution is server-owned: the
client selects only the module and environment keys, not an API host, endpoint,
database catalog, or connection string.

---

## 1. Module Management

### List All Modules
- **`GET /api/modules`**
- **Response `200 OK`**: `ModuleDto[]` — key, label, client, effective availability, the module's registered environments, and its `capabilities` (mirrors `ModuleCapabilities`: `draftKind`, `itemLookup`, `consumerLookup`, `orderRequests`, `cancel`, `resend`, `branchLookup`, `hasDeliveryFields`). The frontend must render only environments marked available by this response. `GhcEcommerceModule.Capabilities.Resend` is `false` until that integration is explicitly enabled. No `password`/`db_config`/raw credentials are ever emitted.

The capability object also includes `branchLookup`; it gates the branch route
described in section 4 alongside item and consumer lookup capabilities.

GHC Uni-Commerce environments that require downstream authentication carry
only a server-owned API-key configuration reference. The key value is resolved
outside tracked application settings and is sent only as the fixed
`X-Api-Key` header by the backend outbound client. It is never accepted from,
returned to, or logged for the browser.

### Environment Reachability
- **`GET /api/modules/health`**
- **Response `200 OK`**: `EnvironmentHealthDto[]` — `{ moduleKey, environmentKey, status, checkedAt }`, one entry per environment of every module.
- `status` is `reachable` | `unreachable` | `unconfigured` | `policy_disabled`.
  `policy_disabled` means the environment is registered but is outside the
  active deployment tier; it is never probed. Missing server configuration,
  endpoint registration, or server secret is reported as `unconfigured` and
  does not expose a secret or connection string.
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
- **Response `200 OK`**: `{ environmentKey: string, environment: string, apiUrl: string | null }` — the server-resolved send endpoint for read-only display in the builder (U4, UI_Rework_Plan.md D13). The value is not an authority input: action routes resolve the endpoint again from server configuration. Deliberately scoped: the module catalog still never carries URLs (see §1), and this route never returns `CancelUrl`, connection-string names, or database config. Uses the same policy resolution as `send-request`.
- **`404 Not Found`**: unknown module key.
- **`400` / `403` / `503`**: the safe error envelope described below for an invalid, policy-disallowed, or unconfigured environment.

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
- **Request Body**: `{ environmentKey?: string }`. The environment key must be
  registered by the server and available in the active deployment tier. There
  is no browser-supplied URL, endpoint key, database name, or connection
  string. Unknown or policy-disallowed environments fail before any downstream
  network call.
- **Response `200 OK`**: `{ success: boolean, statusCode: int, responseText: string, urlSent: string }`
- **`400 Bad Request`**: `{ success: false, errors: string[] }` when `module.Validate(draft)` fails — the request is never sent.
- The sent request/response is **not** persisted locally; it is already recorded server-side by the upstream API into the `OrderRequests` table, which section 5 below reads from. There is no local `order-history` store or `historyEntryId` — that JSON-file feature was retired in R5.

### Cancel (ad hoc, draft-independent)
- **`POST /api/modules/{key}/cancel-order`**
- **Request Body**: `{ orderNumber: string, cancelReason: string }`
- **Response `200 OK`**: `{ success: boolean, statusCode: int, responseText: string }`
- Posts to the active environment's `CancelUrl` (never `ApiUrl`). Prefer **section 5's** `POST /order-requests/{id}/cancel` when cancelling a specific recorded request — it additionally re-checks `CancelBlockedStatuses` server-side and returns the refreshed request detail.

### Production Mutation Unlock
- **`POST /api/modules/{key}/production-unlock`**
- **Request Body**: `{ password: string }` — the value is checked only against the server-owned owner-configured Production unlock secret.
- **Response `200 OK`**: `{ token: string, expiresAt: string }`. The password, secret configuration, and downstream credentials are never returned.
- Production unlock requests over effective HTTP return `400` with the stable `production_secure_transport_required` error. Effective HTTPS may be established only by direct TLS or by an explicitly configured trusted proxy/network forwarding `X-Forwarded-Proto`; unconfigured forwarding headers are ignored.
- The opaque token is random, short-lived, held only in frontend memory, bound to the original server session, module, and `Production` lane, and accepted only by Production mutation routes. A page reload or module/environment switch clears the browser context; it does not revoke the server-side token early. The server token automatically expires after its bounded lifetime, and no browser persistence is used.
- **`401`**: `production_unlock_failed` or `production_unlock_expired`; **`423`**: `production_mutation_locked`; **`503`**: `production_unlock_unavailable` when the owner-configured secret is not provisioned.
- Production `send-request`, `cancel-order`, and supported Order Requests cancel/resend routes require `X-SupportHub-Production-Unlock`. Testing requests do not require or receive this header. Read-only Order Requests GET routes do not require unlock.
- Failed unlock attempts are bounded by three server-side partitions: session/module, server-observed remote-source/module, and a conservative process-wide module ceiling. The source identity is taken from the connection and does not trust arbitrary browser-supplied forwarding headers. All buckets expire automatically; logs contain only non-sensitive module/operation/outcome context and never contain the password or token.

### Diagnostics
- **`POST /api/modules/{key}/test-endpoint?envKey={envKey}`** → `{ status: "Online"|"Offline" }`.
  The server resolves the URL from the registered module/environment mapping.
  A raw `url` query value, if sent by an old client, is ignored and never
  becomes an outbound destination.
- **`POST /api/modules/{key}/test-db?envKey={envKey}`** → `{ status: "Online"|"Offline", database? }`.
  The server resolves the named connection string and optional database
  override. A raw `connectionString` query value, if sent by an old client,
  is ignored. Database credentials and exception text are never returned.

---

## 5. Order Requests (R4/R5 — the `OrderRequests` table as source of truth)

Every route below is gated by `Capabilities.OrderRequests`. Standard UPC and
GHC E-Commerce history uses the verified `OrderRequests` workflow. GHC
Uni-Commerce uses a separate bounded read-only adapter over its verified
`ExternalInvoiceRequests` table. That adapter supports reference/order-number,
outcome, and date filters only; the verified table has no exception flag, so
failed business outcomes are not presented as application exceptions. Branch,
phone, status, totals, line-item, cancel, and resend behavior are not
fabricated for the narrower Uni schema.

Modules without an applicable history source return:

```
501 Not Implemented
{ "error": "'order-requests' is not available for module 'oms'." }
```

The module capability selects the repository shape; controllers do not compare
module-key strings to choose a database query.

All routes accept `?envKey={envKey}` to pick the registered environment (and
therefore the server-side connection mapping) to query. The active deployment
tier is enforced for every route, including read-only lookup and history
routes.

The frontend's canonical history route is `/order-requests`; the legacy
`/requests` and `/validation` paths redirect to it for bookmarked links. The
detail route is `/order-requests/{id}` and replaces the former validation and
drawer surfaces.

### List

For Uni-Commerce, `hasException` is rejected because the verified table has no
exception flag. Its `HasResponse` value is true only when a verified message or
external invoice identifier is present.

- **`GET /api/modules/{key}/order-requests`**
- **Query**: `q` (alias for `orderNumber`), `orderNumber`, `exactMatch` (optional; defaults to `true`), `phone`, `branchCode`, `status` (single value, 1–9), `statuses` (repeated, e.g. `?statuses=6&statuses=7` — multi-select status chips; takes precedence over `status` when both are given), `succeeded` (bool), `hasException` (bool), `dateFrom`, `dateTo`, `page` (default 1), `pageSize` (default 25, clamped to ≤200), `sort` (`order_date`\|`net_total`\|`item_count`, optionally prefixed `-`/`+`; default `order_date DESC` for filtered results). When no header-derived filters are present, the dashboard/search path intentionally returns only the ten newest matching `OrderRequests` by `Id DESC`.
- **Response `200 OK`**: `{ items: OrderRequestListItemDto[], page, pageSize, total, totalPages, stats: OrderRequestStatsDto }`.
- The list query never selects `RequestJson`/`ResponseJson` — only `DATALENGTH(RequestJson) AS requestBytes` and a `hasResponse` flag, so the grid stays fast regardless of blob size.
- Exact order searches use `R.OrderNumber = @OrderNumber` by default. `exactMatch=false` uses an escaped contains predicate for the supported partial-search behavior. Phone input is normalized to its last nine digits and matches `RIGHT(H.ConsumerMobile, 9)`; branch and status use canonical `BranchCode`/`OrderStatus` values from the latest matching header. Date bounds use `R.OrderDate >= @DateFrom` and an exclusive next-day `R.OrderDate < DATEADD(day, 1, @DateTo)` boundary.
- The list, count, and stats reads share one normalized filter model. Header-derived filters use the ranked latest-header CTE; base-only list filters page `OrderRequests` before applying the latest header/invoice projections. Count and stats use distinct request IDs, and the list never returns raw request/response JSON.
- The Angular route keeps one canonical applied-filter model. Clear All resets that model to its fresh defaults on page 1, including the current-month-to-date window, removes canonical and legacy route-filter aliases through the router's null merge (`orderNumber`, `q`, `request`, `branch`, and `statuses` included), preserves the selected page size, and issues one base-filtered list request for the ten newest matching requests. Reload, manual refresh, auto-refresh, and browser history consume the cleared model rather than restoring the removed filters.

### Detail

For Uni-Commerce, the bounded `responseJson` projection contains verified
`Message`/`ExternalInvoiceId` values when present. `exceptionMessage` and
attempt `hasException` remain empty/false because no exception column is
verified.

- **`GET /api/modules/{key}/order-requests/{id}`**
- **Response `200 OK`**: `{ request: OrderRequestDetailDto, attempts: OrderRequestAttemptDto[], lineage: OrderRequestLineageDto }`. `request` is the only shape carrying `requestJson`/`responseJson`/`exceptionMessage`. `request.header.rejectionMessage` (R9) surfaces `RequestOrderHeaders.RejectionMessage` — a verified real column that R4 never selected.
- **`404 Not Found`** if the id doesn't exist.

### Latest attempt by order number
- **`GET /api/modules/{key}/order-requests/by-order/{orderNumber}`**
- Same response shape as detail, resolved to that order's most recent (`MAX(Id)`) attempt. Used for post-send readback and deep links.

### Cancel
- **`POST /api/modules/{key}/order-requests/{id}/cancel`**
- **Request Body**: `{ reason: string }`
- URL resolution always uses the server-owned `CancelUrl` for the selected
  environment. Browser endpoint overrides are disabled; the route never falls
  back to `environment.ApiUrl` (the historical bug this endpoint replaces
  silently posted cancellations to the create-order URL because the
  endpoint-picker field name was never wired up).
- Server re-checks `OrderRequestStatus.CancelBlockedStatuses` (`{5,6,7,9}` — Rejected/CanceledByClient/CanceledByAdmin/Done) even if the client's button was enabled; a client-side check is not a trust boundary.
- **`409 Conflict`**: `{ error: string, cancelBlockedReason: string }` if blocked — the upstream API is never called.
- **`200 OK`** otherwise: `{ success, statusCode, responseText, urlSent, request: OrderRequestDetailDto }` — the detail is re-read after the call so the UI can show the refreshed status without a second round trip.

### Resend
- **`POST /api/modules/{key}/order-requests/{id}/resend`**
- **Request Body**: `{ branchCode?: string }`
- Reuses **that specific request's own stored `RequestJson`** (not the live in-progress draft), verifies its `order_code` matches the recorded `OrderNumber`, and overrides only `branch_code` when a target is supplied. When omitted, the original stored branch is reused. The original order/request number is sent again unchanged, all unknown payload fields are preserved, and no historical row is mutated.
- The server re-checks the canonical resend rule `OrderRequestStatus.ResendBlockedStatuses` (`{1,4}` — New/With_Delegate) immediately before sending.
- **`409 Conflict`**: `{ error: string, resendBlockedReason: string }` if blocked.
- **`200 OK`** otherwise: `{ success, statusCode, responseText, urlSent }`.
- Posts to `environment.ApiUrl` (a resend is a new order attempt, not a cancellation).

### Environment and safety errors

Environment and downstream failures use the stable envelope below; raw
exception text, connection strings, and arbitrary destinations are not sent to
the browser:

```json
{ "error": { "code": "environment_not_allowed", "message": "...", "details": null } }
```

The principal codes are `invalid_environment` (`400`),
`environment_not_allowed` (`403`), `environment_unconfigured` (`503`),
`capability_unavailable` (`501`), `production_mutation_locked` (`423`),
`production_unlock_failed`/`production_unlock_expired` (`401`),
`production_unlock_unavailable` (`503`), `downstream_unreachable` (`502`), and
`downstream_timeout` (`504`).

---

## Retired in R5

- `GET /api/modules/{key}/order-history` and `POST /api/modules/{key}/order-history/{id}/cancel` — the local JSON-file order-history store (`OrderHistoryService`/`OrderHistoryEntry`) is gone; section 5 above, reading the real `OrderRequests` table, is its replacement. No `order_history_*.json` files existed on disk at the time of removal (confirmed via a repo-wide search), so there was nothing to archive.
- `POST /api/modules/upc_ecommerce/validation/search` and `GET /api/modules/upc_ecommerce/validation/order/{orderNumber}` (`ValidationController`/`IOrderValidationRepository`/`UpcOrderValidationRepository`) — this path read `RequestOrderHeaders` first and never surfaced `ResponseJson`/`ExceptionMessage`; superseded by section 5.

---

## POS Agent boundary (Slice C)

The privileged POS surface is a separate Windows host, not an endpoint in the
Support Hub API composition root. The permanent product/service identity is
`RmsSupportAgent`, hosted at the exact secure Testing origin
`https://rms-pos-agent.localhost:5001`. The browser reaches it directly with
Negotiate and the explicit cross-origin antiforgery contract; the Hub API is
not a privileged relay.

The current read-side operational contract is:

- **`GET /api/v1/rms/operational-health`** — authenticated, bounded summaries
  for the fixed RMS roots, update/release state, and insurance-attachment
  aggregate. It never returns raw paths, filenames, log contents, or attachment
  bytes.
- **`GET /health/live`** and **`GET /health/ready`** — host health probes.
- Support-bundle and mutation routes remain Agent-owned and require their
  existing session, antiforgery, capability, and one-use mutation controls.

The source of truth for the versioned Agent schema is
`pos/openapi/RmsSupportHub.Pos.Agent.json`; the Angular client under
`frontend/src/app/core/pos-agent/generated/` is regenerated from that document
and must not be hand-edited. Package, certificate, browser-policy, migration,
and audit behavior is documented in
`docs/POS_SLICE_C_IMPLEMENTATION.md`.
