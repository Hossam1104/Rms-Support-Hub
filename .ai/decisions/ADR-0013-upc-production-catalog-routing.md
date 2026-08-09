# ADR-0013: UPC Production Catalog Routing

- **Status:** Accepted
- **Date:** 2026-08-09
- **Affected area:** UPC environment resolution and SQL read-side routing

## Decision

UPC Production resolves the existing server-owned `UpcEcommerceTest`
connection details and applies the approved `RmsMainProd` catalog override in
`ConnectionStringResolver`. Testing keeps its current connection and catalog
resolution. The browser supplies only a named environment key; it cannot supply
database, server, credentials, or a connection string.

## Consequences

- Items, consumers, branches, orders, and order details follow one selected
  environment through the existing controller/repository boundaries.
- Credentials are not duplicated in user-secrets or deployment configuration.
- Production runtime verification remains read-only and does not exercise the
  order API's mutation endpoints.

## Evidence

- `backend/src/RmsSupportHub.Core/Modules/UpcEcommerceModule.cs`
- `backend/src/RmsSupportHub.Api/ConnectionStringResolver.cs`
- `backend/src/RmsSupportHub.Api/Controllers/LookupController.cs`
- `backend/src/RmsSupportHub.Api/Controllers/OrderRequestsController.cs`
