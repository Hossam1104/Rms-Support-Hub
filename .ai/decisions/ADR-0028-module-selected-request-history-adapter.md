# ADR-0028: Module-Selected Request History Adapter

- **Status:** Accepted
- **Date:** 2026-08-21
- **Affected area:** GHC Uni-Commerce request history

## Context

The GHC and UPC E-Commerce databases expose the generic `OrderRequests`
request-attempt workflow. The verified Uni-Commerce catalog instead exposes
`ExternalInvoiceRequests` with reference number, request JSON, success,
message, timestamp, and external invoice ID, but no compatible branch,
business-status, totals, line-item, or cancellation data.

## Decision

Select request-history repositories through module capability data. Keep the
generic `OrderRequestRepository` for standard modules and add a bounded,
read-only `GhcUnicommerceOrderRequestRepository` for Uni-Commerce. The adapter
supports only filters and response fields that the verified table can prove;
unsupported branch/phone/status/sort queries are rejected, and cancel/resend
remain disabled.

## Consequences

- Uni-Commerce history is visible without pretending its schema is generic.
- List reads never transfer Uni request JSON; detail is the only raw-payload
  read.
- `Success` is the verified outcome field. `Message` and
  `ExternalInvoiceId` are exposed in the bounded response projection when
  present; the schema has no verified exception flag, so failed business
  outcomes do not populate `ExceptionMessage` or `HasException`.
- Missing Uni business fields remain empty/unavailable in the shared DTO shape.
- Future Uni schema changes must be verified before expanding capabilities.

## Evidence

- `backend/src/RmsSupportHub.Data/Repositories/GhcUnicommerceOrderRequestRepository.cs`
- `backend/src/RmsSupportHub.Api/Controllers/OrderRequestsController.cs`
- `backend/tests/RmsSupportHub.Tests/OrderRequestRepositoryTests.cs`
- `docs/database-schema.md`
