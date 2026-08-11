# ADR-0019: Lane label and endpoint reachability are two separate facts

- Status: Accepted
- Affected area: `ModuleEnvironment.StatusLabel`, `IModuleHealthService`, `GET /api/modules/health`, Online Order module card

## Context

The Online Order dashboard showed "Active Module" on each module card and
"Live"/"Test"/"Soon" on each environment. Both were static configuration:

- `IOrderModule.Available` is a hardcoded C# property meaning "this module is
  built".
- `ModuleEnvironment.StatusLabel` is `(Available, Environment) switch` — it
  means "this is the Production lane", never "the host answered".

Nothing had ever been probed, so a module whose endpoint was down still read as
Live. Meanwhile `IApiClient.TestEndpointAsync` (a 3s TCP connect) and
`POST /api/order/test-endpoint` had existed since the Flask port and no client
had ever called either.

Two constraints shaped the fix.

The upstream URLs are POST-only order operations —
`CreateAndAssignOrder`, `CancelOrder` — on internal hosts. There is no health
route to call, so nothing stronger than a transport-level probe is available
without sending a real order, which is prohibited against Production.

More importantly, "Live" is a safety warning, not a status. An operator reads
it to answer *"will Send hit production?"* before sending. If the label were
recomputed from a probe, a Production lane with an unreachable host would stop
saying Live and could be misread as a safe lane.

## Decision

Reachability is reported as its own field and never folded into the lane label.

- `ModuleEnvironment.StatusLabel` keeps its existing configuration-only
  derivation. No probe result may change it.
- `IModuleHealthService` sweeps every environment with a **TCP connect only**,
  in parallel, and returns `reachable` | `unreachable` | `unconfigured`
  (the last for an environment with no `ApiUrl`). No HTTP request, no payload.
- The sweep is cached process-wide for 30s (`ModuleHealthCache`), so repeated
  dashboard loads do not re-probe internal hosts.
- `GET /api/modules/health` returns `{ moduleKey, environmentKey, status,
  checkedAt }` and never the probed host, port, or URL — the catalog keeps
  endpoint topology private (B16).
- The endpoint is deliberately not folded into `GET /api/modules`, and the
  frontend calls it after the dashboard renders. The catalog must never wait on
  a connect timeout.
- The module card renders the lane badge and the reachability chip stacked,
  visibly separate.
- A missing entry is `unknown`, never `unreachable`. Losing the Support Hub's
  own API is not evidence that a module endpoint is down.

Probing Production hosts is in scope: a TCP connect opens and closes a socket
and carries no order data, so it does not "send against Production".

## Consequences

`reachable` means a listener accepted a connection. It does not mean the API is
healthy — a hung app pool still completes a TCP handshake. The status name says
"reachable" rather than "online" for that reason, and the UI wording follows.

Reachability is measured from the API host's network, not the operator's. That
is the correct vantage point, because orders are sent server-side.

If a real health route is ever exposed upstream, `ModuleHealthService.ProbeAsync`
is the single place to upgrade the probe; the DTO, cache, and UI contract do not
change.
