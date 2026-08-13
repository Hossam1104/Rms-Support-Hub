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
| ADR-0010 | Accepted | Clear All is a single fresh-default reset transaction with route alias cleanup and request-generation invalidation | Order Requests store, filter bar, route state, refresh lifecycle | `.ai/decisions/ADR-0010-order-requests-clear-all-reset.md` |
| ADR-0011 | Accepted | QA Support Hub hosts all tools in the current Angular/.NET app with lazy feature routes, a native Prompt Studio rebuild, preserved Online Orders behavior, pending POS migration, and no iframe integration | Programme architecture, routing, feature boundaries | `.ai/decisions/ADR-0011-qa-support-hub-baseline.md` |
| ADR-0012 | Accepted | One token-owned card contract with grid-driven equal heights, and a single decorative lazy-loaded Three.js scene on the Hub that always degrades to a static gradient | Design tokens, shared card surfaces, Hub landing, bundle strategy | `.ai/decisions/ADR-0012-shared-card-system-and-hub-scene.md` |
| ADR-0013 | Accepted | UPC Production reuses the server-owned UPC Testing connection details and overrides only the approved database catalog | UPC environment routing and read-side database resolution | `.ai/decisions/ADR-0013-upc-production-catalog-routing.md` |
| ADR-0014 | Accepted | Allowed payment methods are a flat-order variant policy; UPC accepts only Visa, Tamara, Tabby | Flat-order validation, Add Payment dialog, payments table | `.ai/decisions/ADR-0014-variant-scoped-payment-methods.md` |
| ADR-0015 | Accepted | Privileged POS work belongs to a separate Windows Agent reached directly by the Support Hub browser | POS process isolation, privileged operations, Support Hub API boundary | `.ai/decisions/ADR-0015-separate-pos-agent-trust-boundary.md` |
| ADR-0016 | Accepted | POS browser transport uses exact loopback HTTPS, LNA, Negotiate, exact-origin CORS, and an explicit cross-origin antiforgery contract | POS browser security, certificates, CORS, CSP, antiforgery | `.ai/decisions/ADR-0016-pos-browser-transport-security-boundary.md` |
| ADR-0017 | Accepted | POS enters through a clean tracked snapshot in an isolated destination boundary; raw history merge is prohibited | POS import, project layout, portable/Windows boundary | `.ai/decisions/ADR-0017-pos-clean-snapshot-and-project-isolation.md` |
| ADR-0018 | Accepted | The Agent owns POS OpenAPI and trigger semantics; Support Hub owns the final generated Angular client and destination CI ownership | OpenAPI, frontend ownership, CI lanes, remote outcomes | `.ai/decisions/ADR-0018-pos-contract-and-client-ownership.md` |
| ADR-0019 | Accepted | The Live/Test lane label stays configuration-only; endpoint reachability is a separate TCP-connect probe reported beside it | `ModuleEnvironment.StatusLabel`, `GET /api/modules/health`, module card | `.ai/decisions/ADR-0019-lane-label-and-endpoint-reachability.md` |
| ADR-0021 | Accepted | POS service control uses opaque target-bound one-use tokens, bounded idempotency/concurrency, and explicit outcome truth | POS Agent service control, mutation tokens, direct Support Hub transport | `.ai/decisions/ADR-0021-pos-service-control-mutation-runtime.md` |
