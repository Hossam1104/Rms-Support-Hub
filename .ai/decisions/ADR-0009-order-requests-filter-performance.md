# ADR-0009: Canonical Order Requests filtering and external join indexes

- **Status:** Accepted
- **Date:** 2026-08-04
- **Scope:** Order Requests list, count, stats, and search workbench

## Decision

Treat `OrderRequests` as the canonical request-grain source for every list
read. Normalize the filter once in the API, then use that same model for the
grid, distinct count, and filtered summary. Exact order-number search remains
the default; partial search is opt-in and escaped. Phone search binds the
normalized last nine digits, branch/status use the latest matching
`RequestOrderHeaders` row, and date bounds use the base `OrderRequests.OrderDate`
column with an exclusive next-day end boundary.

For base-only filters, page `OrderRequests` before applying latest header and
invoice projections. For header-derived filters, rank headers once with a
`LatestHeaders` CTE and join only rank 1. List projections expose blob length
and response existence, while detail reads remain the only raw JSON reads.

The UPC database is external to the application migration pipeline. The
reviewed script `docs/sql/order-requests-performance-indexes.sql` adds guarded
covering indexes on `RequestOrderHeaders(OrderNumber, Id DESC)` and
`Invoices(OnlineOrderNumber, Id DESC)` when absent. It may be applied to
Testing for validation; Production requires separate database-owner approval.

## Rationale

The prior correlated `OUTER APPLY` shape evaluated header/invoice lookups for
the full request history and timed out on the unfiltered Testing read. The
ranked CTE, base-first paging, reduced projection, bounded command timeout,
and approved Testing indexes preserve latest-row semantics while keeping
valid failures retryable and sanitized at the API boundary.
