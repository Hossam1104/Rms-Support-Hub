# Technical and Architectural Decisions

## Decision Statuses

- Proposed
- Accepted
- Superseded
- Deprecated
- Rejected
- Status Unknown

## ADR Index

| ADR | Title | Status | Date | Evidence |
|---|---|---|---|---|
| ADR-001 | Layered .NET API plus Angular SPA | Accepted | Before 2026-07-25 | Source/configuration/Git history |
| ADR-002 | Module-owned behavior with capabilities | Accepted | 2026-07-25 | Source/tests/Git history |
| ADR-003 | Per-session, file-backed drafts | Accepted | 2026-07-25 | Source/tests |
| ADR-004 | Dapper and parameterized SQL repositories | Accepted | 2026-07-25 | Source/tests |
| ADR-005 | SQL `OrderRequests` is the request-history source of truth | Accepted | 2026-07-25 | Source/tests/documentation |
| ADR-006 | Reference payloads and live schema are executable contracts | Accepted | 2026-07-25 | Tests/documentation |
| ADR-007 | Testing is the explicit default; Production requires UI confirmation | Accepted | 2026-07-27 | Source/tests/Git history |
| ADR-008 | Secrets use named configuration outside Git | Accepted | 2026-07-25 | Configuration/tests/documentation |
| ADR-009 | Uniform API error envelope | Accepted | 2026-07-25 | Middleware/tests/source; implementation is incomplete |
| ADR-010 | Atomic batched draft mutation | Proposed | 2026-07-26 | `UI_Rework_Plan.md` U2 |
| ADR-011 | Application authentication/authorization boundary | Proposed | 2026-07-27 | Baseline security analysis |
| ADR-012 | Restrict diagnostic and custom outbound targets | Proposed | 2026-07-27 | Baseline security analysis |

## ADR-001 — Layered .NET API plus Angular SPA

- Status: Accepted
- Date: Before 2026-07-25
- Evidence classification: Confirmed from source code, configuration, and Git history.

### Context

The legacy Flask implementation was replaced by a typed API and SPA.

### Decision

Use `OnlineOrderTool.Core` for domain/services, `OnlineOrderTool.Data` for SQL repositories, `OnlineOrderTool.Api` for controllers/composition, and an Angular standalone-component SPA.

### Evidence

Project references, solution layout, `Program.cs`, Angular routes/services, and migration/remediation commits.

### Rationale

Documentation states that this provides typed contracts and clearer boundaries. Earlier selection rationale is otherwise unknown.

### Alternatives

The former Flask implementation is confirmed superseded and removed.

### Consequences

- Positive: clear backend dependencies and strong unit/contract testability.
- Negative: server and SPA can still duplicate state/calculation logic.
- Operational: two build/runtime toolchains.
- Testing: backend and frontend require distinct suites.
- Security: both surfaces must share a coherent identity boundary, currently absent.

### Affected Areas

`backend/**`, `frontend/**`, build scripts.

### Follow-up Actions

Keep API/TypeScript contracts synchronized and add feature-level frontend tests.

## ADR-002 — Module-owned behavior with capabilities

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from source/tests/Git history.

### Context

Module-key conditionals previously selected validators, builders, and repositories and drifted across layers.

### Decision

Each `IOrderModule` owns environments, capabilities, default state, payload building, validation, and lookup dispatch. UI/API access is gated by declared capabilities.

### Evidence

`IOrderModule.cs`, `ModuleRegistry.cs`, module implementations, `CapabilityGuard`, frontend guard.

### Rationale

Recorded in remediation B21: reduce identity-based branching and make module behavior declarative.

### Alternatives

Hardcoded module-key branching was superseded; some UI remnants remain.

### Consequences

- Positive: centralized feature declarations and extensibility.
- Negative: `Available` and capability enforcement are not universal server authorization boundaries.
- Testing: module registry/capability behavior is testable.

### Affected Areas

Core modules, API guards/controllers, Angular routing/navigation.

### Follow-up Actions

Remove remaining hardcoded identities, especially superseded Order Validation behavior, when approved.

## ADR-003 — Per-session, file-backed drafts

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from source/tests.

### Context

A process-global draft caused users to overwrite one another.

### Decision

Issue an HttpOnly GUID cookie and store JSON by `(sessionId, moduleKey)` under the API content root.

### Evidence

`SessionIdMiddleware`, `DraftManager`, integration/unit tests.

### Rationale

Provide simple session isolation without a database.

### Alternatives

The global draft file was superseded. Database or client-only storage is not formally evaluated.

### Consequences

- Positive: browser sessions are isolated.
- Negative: concurrent writes within a session race; no cleanup, encryption, or multi-instance coherence.
- Operational: local disk is stateful and ignored from Git.
- Security: PII may remain in plain files for at least the cookie lifetime.

### Affected Areas

Draft middleware/service and every state-mutating controller/UI.

### Follow-up Actions

Implement ADR-010 and approve retention/cleanup policy.

## ADR-004 — Dapper and parameterized SQL repositories

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from source/tests.

### Context

The tool needs client-schema-specific queries and previous invented SQL failed at runtime.

### Decision

Use Dapper with explicit SQL, bound parameters, allowlisted sort columns, and schema documentation/tests.

### Evidence

Data repositories and SQL-shape tests.

### Rationale

Direct control over verified legacy schemas and query shape.

### Consequences

- Positive: transparent SQL and injection-resistant values.
- Negative: schema drift and performance depend on external databases.
- Operational: no migration ownership is present.
- Testing: most SQL is shape-tested, not executed in CI.

### Follow-up Actions

Add safe Testing-lane integration coverage and coordinate missing indexes.

## ADR-005 — SQL `OrderRequests` is the request-history source of truth

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from source/tests/documentation/Git history.

### Context

A local JSON history omitted attempts recorded outside the tool and did not surface upstream response/exception data.

### Decision

Do not persist a local order history. Read the upstream `OrderRequests` table and related latest header/invoice data.

### Evidence

`OrderRequestRepository`, `OrderRequestsController`, retired route tests, remediation history.

### Consequences

- Positive: authoritative attempts and failures are visible.
- Negative: UI availability/performance depends directly on client SQL and indexes.
- Security: raw request/response and PII require authorization.

### Follow-up Actions

Resolve authentication and database index ownership.

## ADR-006 — Reference payloads and live schema are executable contracts

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from tests/documentation.

### Context

The first rewrite implemented invented payload keys and SQL columns.

### Decision

Treat request-example fixtures and live-introspected schema as the contract; guard keys and banned columns/hosts with tests.

### Consequences

- Positive: prevents known contract regressions.
- Negative: key-only tests do not validate every value or live database behavior.
- Testing: fixture quality and provenance matter.

### Follow-up Actions

Expand semantic value tests and revalidate GHC schema.

## ADR-007 — Testing is the explicit default; Production requires UI confirmation

- Status: Accepted
- Date: 2026-07-27
- Evidence classification: Confirmed from source/tests/Git history.

### Context

Dictionary order previously selected Production and the active lane was not visible.

### Decision

Mark one environment as default, prefer non-Production fallback, persist selection per module, show the lane, and require typed Production confirmation in relevant UI flows.

### Consequences

- Positive: safer operator default.
- Negative: direct API calls remain unaffected; Order Requests does not react to a lane switch after initialization.
- Testing: backend default/cancel routing is tested; UI behavior is not.

### Follow-up Actions

Fix REQ-REQ-005 and define server-side production authorization.

## ADR-008 — Secrets use named configuration outside Git

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from configuration/tests/documentation.

### Decision

Tracked connection strings are empty; development uses user-secrets and production uses environment variables. Browser DTOs expose booleans rather than URLs/secrets.

### Consequences

- Positive: current tree does not embed credentials.
- Negative: deployment secret management and rotation are external/unknown.

### Follow-up Actions

Confirm prior leaked credentials were rotated and document production secret ownership.

## ADR-009 — Uniform API error envelope

- Status: Accepted
- Date: 2026-07-25
- Evidence classification: Confirmed from middleware/tests/source.

### Decision

Map exceptions to `{ error: { code, message, details } }` with appropriate status codes.

### Consequences

- Positive: typed frontend interception for middleware-handled errors.
- Negative: direct controller `BadRequest`/`NotFound`/`ObjectResult` responses often use `{ error: string }`; unexpected exception messages are exposed.

### Follow-up Actions

Centralize problem responses, hide unexpected details, and update tests/specification.

## ADR-010 — Atomic batched draft mutation

- Status: Proposed
- Date: 2026-07-26
- Evidence classification: Confirmed from current plan; not implemented.

### Context

Per-field asynchronous load-modify-write operations race and lose changes.

### Decision

Proposed: serialize mutations per session/module, atomically replace files, batch/coalesce frontend edits, and stop adopting stale server echoes.

### Consequences

- Positive: fixes data loss and file-sharing failures.
- Negative: requires API and frontend state changes, concurrency cleanup, and tests.

### Follow-up Actions

Approve an amendment that also persists Uni-Commerce nested state through the same mutation boundary.

## ADR-011 — Application authentication/authorization boundary

- Status: Proposed
- Date: 2026-07-27
- Evidence classification: Inferred requirement from confirmed sensitive operations.

### Context

All controllers are anonymous while exposing PII, raw payloads, SQL-derived data, and order side effects.

### Decision

Requires stakeholder approval: select an identity provider, require authenticated users, and authorize read/send/cancel/resend/diagnostic/production actions separately.

### Consequences

Security and deployment consequences depend on the selected identity system; no implementation should begin without infrastructure/user-role confirmation.

### Follow-up Actions

Confirm current network controls, users, roles, and audit requirements.

## ADR-012 — Restrict diagnostic and custom outbound targets

- Status: Proposed
- Date: 2026-07-27
- Evidence classification: Inferred security requirement from source behavior.

### Context

Anonymous callers can supply HTTP/TCP targets and a full SQL connection string.

### Decision

Proposed: remove production diagnostics or place them behind privileged development/admin authorization; replace arbitrary URLs with allowlisted configured endpoints.

### Consequences

- Positive: reduces SSRF, network probing, credential leakage, and misrouting.
- Negative: reduces ad-hoc operator flexibility.

### Follow-up Actions

Confirm whether custom endpoints are a real business requirement.

## Existing Implicit Decisions

- No local order-history store; drafts are the only local business state.
- No background jobs or cache.
- Backend is authoritative for payload compilation and validation.
- Angular module metadata is loaded live rather than hardcoded.
- Production bundle omits the development kitchen sink.
- Raw request/response blobs are detail-only.

## Proposed Decisions Requiring Approval

1. ADR-011 identity, roles, and audit model.
2. ADR-012 diagnostics/custom target policy.
3. Draft retention, expiry, encryption, and multi-instance strategy.
4. Database-owner approval for missing nonclustered indexes.
5. Whether GHC Uni-Commerce remains publicly “available” before an endpoint and complete draft persistence exist.

## Conflicting Decisions

- “Server owns the draft” conflicts with browser-only Uni-Commerce nested state.
- “Server owns totals” conflicts with TypeScript flat-order totals.
- “Uniform error envelope” conflicts with controller-specific flat error bodies.
- “Explicit TLS configuration” conflicts with an absent tracked `Outbound` section and false-by-default global bypass.
- Endpoint metadata exists both as unused `ModuleEndpoints` configuration and module-class constants.
