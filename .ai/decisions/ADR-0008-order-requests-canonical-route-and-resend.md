# ADR-0008: Canonical Order Requests Route and Same-Number Resend

- **Status:** Accepted
- **Date:** 2026-08-04
- **Affected area:** Order Requests frontend, resend API, request-history docs

## Context

The application had overlapping Order Requests and UPC Order Validation
surfaces, and the request detail experience was a drawer with tabs. Resend
behavior also needed one authoritative rule and had to operate on the exact
historical attempt selected by the operator.

## Decision

Use `/order-requests` as the single history route and
`/order-requests/{id}` as its full-page detail route. Keep `/requests` and
`/validation` as compatibility redirects only. Render the detail page in the
fixed order Items, Order Info, Transactions, Request JSON, and Response JSON;
the JSON sections are collapsed by default.

Resend is allowed for every valid status except `New` (1) and `With_Delegate`
(4). The API reads the selected row's stored `RequestJson`, verifies that its
`order_code` matches `OrderNumber`, changes only `branch_code` (or retains the
stored branch when omitted), and sends the same original order number again.
Unknown payload fields and the stored historical row remain unchanged. The
server repeats the status and payload checks even when the UI disables the
action.

## Consequences

- There is one active list/detail implementation and one status rule shared by
  the UI, API, tests, and documentation.
- Deep links and old bookmarks continue to resolve without maintaining the
  retired validation component tree.
- Resends are auditable against the selected attempt and cannot silently send a
  draft payload or generate a replacement number.
- Browser and safe Testing verification remain external acceptance evidence;
  no Production operation is part of local validation.

## Evidence

- `frontend/src/app/app.routes.ts`
- `frontend/src/app/features/order-requests/components/order-request-details.component.ts`
- `backend/src/OnlineOrderTool.Api/Controllers/OrderRequestsController.cs`
- `backend/src/OnlineOrderTool.Core/OrderRequestStatus.cs`
- `backend/tests/OnlineOrderTool.Tests/OrderRequestsControllerTests.cs`
