# ADR-0021: Typed target-bound POS service-control mutation runtime

- Status: Accepted
- Date: 2026-08-13
- Affected area: POS Agent service control, mutation tokens, direct Support Hub transport
- Gate: INT-08

## Decision

The first POS mutation capability is limited to
`POST /api/v1/services/{serviceId}/actions` with the existing typed
`ServiceActionRequestDto(Action, IdempotencyKey)` contract. The browser may
send only an opaque service identifier, one of `Start`, `Stop`, or `Restart`,
and a bounded idempotency key. The Agent resolves the identifier against its
configured allow-list and owns the concrete Windows service name.

The registered `services.control` operation binds a short-lived, one-use
mutation token to the authenticated server-resolved Windows SID, exact Support
Hub Origin, operation ID, POST method, and concrete opaque-target path. The
token is consumed immediately before the typed `IServiceManager.ControlAsync`
dispatch boundary. Tokens remain in process memory, never appear in URLs or
response bodies, and are not used as UI state or idempotency identity.

The runtime uses two bounded process-local guards:

- idempotency is keyed only by `(serviceId, idempotencyKey)` and repeats the
  original safe response; a conflicting action is rejected;
- a non-blocking per-service gate prevents overlapping service dispatches.

The response reports operational truth independently of HTTP status:

- `NotAttempted` means a pre-dispatch gate rejected the request;
- `Accepted` means the typed manager acknowledged completion;
- `Failed` means an authoritative service-control rejection was classified;
- `OutcomeUnknown` means cancellation, timeout, or another dispatch ambiguity.

`OutcomeUnknown` is never retried automatically. The response contains only a
safe code, operator detail, and correlation identifier.

## Rationale

This preserves the direct Angular-to-loopback-Agent trust boundary and ADR-0020
authorization semantics while making the target, method, and token binding
explicit. It prevents raw service-name/command/path expansion and avoids
pretending that a timeout proves a failed SCM operation. Process-local bounded
state is sufficient for the current per-device Agent scope; durable or
fleet-wide idempotency is outside INT-08.

## Consequences

- The frontend must request a fresh token after explicit confirmation and keep
  it only in local memory for the immediate submit.
- The service read model must advertise only actions valid for the observed
  `Running` or `Stopped` state; unknown and unavailable states expose none.
- Live disposable-service and representative-device validation remain open
  under INT-13; INT-08 tests use the existing Agent fakes and never control
  Production services.
