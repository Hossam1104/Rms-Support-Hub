# ADR-0003: SQL-Backed Request History

- **Status:** Accepted
- **Date:** 2026-07-25
- **Affected area:** Order Requests list/detail, cancel, resend

## Context

Local JSON history could only observe activity from one application instance and diverged from the RMS database. Failed attempts also need their stored response or exception exposed.

## Decision

Use SQL `OrderRequests` as the base and sole request-history source. Read large request/response blobs only for detail. Resolve related headers and invoices to the most recent matching row. Resend from the selected attempt's stored request, overriding only the selected branch.

## Consequences

- History reflects upstream attempts rather than local process activity.
- List queries avoid transferring large blobs.
- Missing live indexes can make filters over related tables expensive.
- Capability flags remain disabled where a module's database contract is unverified.

## Revisit When

- The upstream system introduces a supported history API or event stream with stronger consistency and access controls.

## Evidence

- `backend/src/OnlineOrderTool.Data/Repositories/OrderRequestRepository.cs`
- `backend/src/OnlineOrderTool.Api/Controllers/OrderRequestsController.cs`
- `backend/tests/OnlineOrderTool.Tests/OrderRequestRepositoryTests.cs`
- `docs/database-schema.md`
