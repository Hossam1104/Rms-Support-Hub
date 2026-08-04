# Decision Index

Read only when the current task touches an affected area.
Keep this as a compact index; detailed rationale belongs in individual ADR files.

| ID | Status | Decision | Affected Area | Detail |
|---|---|---|---|---|
| ADR-0001 | Accepted | Capability-driven layered API and typed SPA | Backend layers, frontend routing | `.ai/decisions/ADR-0001-capability-driven-architecture.md` |
| ADR-0002 | Accepted | Verified fixtures and schemas define external contracts | Payload builders, validators, SQL repositories | `.ai/decisions/ADR-0002-verified-external-contracts.md` |
| ADR-0003 | Accepted | SQL `OrderRequests` is the sole request-history source | Request history, detail, cancel, resend | `.ai/decisions/ADR-0003-sql-backed-request-history.md` |
| ADR-0004 | Accepted | Drafts are per-session, batched, serialized, and atomically file-backed | Draft middleware, service, order editing | `.ai/decisions/ADR-0004-atomic-session-drafts.md` |
| ADR-0005 | Accepted | Testing is the default environment and Production is visibly gated | Environment resolution and actions | `.ai/decisions/ADR-0005-testing-default-safety.md` |
| ADR-0006 | Accepted | An empty payment list is Cash on Delivery, not a validation error | Flat-order validation, payload, summary UI | `.ai/decisions/ADR-0006-optional-payment-cash-on-delivery.md` |
| ADR-0007 | Accepted | The phone field carries the local number only; the country code has its own key | Phone normalization, payload, client UI | `.ai/decisions/ADR-0007-local-phone-country-code-split.md` |
| ADR-0008 | Accepted | Order Requests is the canonical history/detail route; resend reuses the selected stored payload and original number | Order Requests UI, resend API, route compatibility | `.ai/decisions/ADR-0008-order-requests-canonical-route-and-resend.md` |
| ADR-0009 | Accepted | Order Requests uses one normalized filter model, latest-header ranking, base-first paging, and approved external join indexes | Order Requests repository, API query binding, Testing database support | `.ai/decisions/ADR-0009-order-requests-filter-performance.md` |
