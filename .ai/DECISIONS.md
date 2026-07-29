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
