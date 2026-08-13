# Current Project State

- **Updated:** 2026-08-14
- **Branch:** `int-13p-testing-agent-provisioning` (INT-13 closure on PR #7)
- **Repository:** `Hossam1104/Rms-Support-Hub`; local path `D:\AI Tools\DBS\Rms-Support-Hub`
- **Programme:** INT-00 through INT-08 complete/accepted; INT-13 complete and validated live on representative Testing machine; next milestone is the independent POS First-Release Security & Readiness Review.
- **Current gate:** INT-06I independent security review PASS (PR #3 merged); INT-07 PR #4 merged; INT-08 PR #5 merged; INT-13 PR #7 complete with live operational evidence (Chrome/Edge Medium-integrity Negotiate IWA, protected reads, mutation-token binding/replay rejection, Agent-dispatched disposable service control).

## Application

- Angular 22 SPA and .NET 10 Web API. Prompt Studio and Online Orders are
  available; `/tools/pos-maintenance` is a direct operational POS evidence and
  service-control workspace.
- Routes are lazy and typed through `ToolRouteData`. Business/API, payload,
  SQL, module-key, and persisted-storage contracts remain unchanged.
- POS feature ownership is the separate `RmsSupportHub.Pos.Agent`; the Hub
  never relays privileged POS traffic through `RmsSupportHub.Api`, `Core`, or
  `Data`.

## POS architecture and gates

- Agent origin: `https://rms-pos-agent.localhost:5001`; headless,
  Windows-Service-capable, loopback-only, HTTPS/HTTP/1.1, production Negotiate,
  exact-origin CORS, server-resolved local Built-in Administrators membership,
  and fail-closed SID handling.
- INT-03R Agent provenance: `010abc52dc110cfde3dc2c53e057890ff6edaf97`;
  historical INT-01/02/03 import provenance: `25922b499d33bd73f241ffc26c212dd000e81433`.
- INT-05 owns versioned `/pos/openapi`, generated client, and direct
  `HttpBackend` transport. INT-06I owns UAC-safe authorization and non-
  production Scalar/OpenAPI. INT-07 owns device, connectivity, redacted
  configuration, service-status reads, and the direct Angular workspace.
- INT-08 owns only typed `services.control`: opaque allow-listed service IDs,
  target/method/path-bound one-use tokens, bounded idempotency/concurrency, and
  truthful typed outcomes. Production runtime OpenAPI remains hidden.
- INT-13 owns certificate/hostname/representative-device/live operational evidence:
  - Exact Support Hub origin `https://support-hub.integration.test:4443`
  - Direct Agent origin `https://rms-pos-agent.localhost:5001`
  - Automated Chrome & Edge IWA and loopback network policy provisioning
  - Automated `BackConnectionHostNames` REG_MULTI_SZ provisioning
  - Non-elevated Medium-integrity interactive browser evidence harness
  - Disposable Windows Service harness `RmsSupportHub.Pos.Int13.TestService`
  - Live Negotiate authentication, protected reads, mutation token issuance/consumption/replay rejection, and disposable service restart verified.

## Security and review record

- INT-06/06F/06G/06H blocked states are historical. INT-06I remediated local
  Administrator resolution with indirect local-group membership and the
  Built-in Administrators SID; independent review PASS found no Critical/High.
- Post-remediation Chrome/Edge browser authorization and Scalar/OpenAPI
  evidence passed; no SID/token exposure. PR #3 is merged normally.
- INT-13 live operational evidence recorded in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`
  with zero credential prompts, server-derived authorization, token binding, and replay rejection.

## Compatibility contracts

Persisted keys are byte-exact; no migration exists:

```text
onlineOrderTool.activeEnvironment.<moduleKey>
qa-support-hub:theme
qa-support-hub:motion
qa-support-hub.prompt-studio.history
qa-support-hub.prompt-studio.bug-draft
qa-support-hub.prompt-studio.story-draft
qa-support-hub.prompt-studio.test-case-draft
order-tool.sidebar-collapsed
```

Raw colors stay in token files. The Hub scene is decorative/lazy and safely
degrades; all current UI feature styles consume design tokens.

## Validation baseline

| Gate | Result |
|---|---|
| POS Release build | Passed, 0 warnings/errors with Testing origin set |
| POS tests | Domain 7, Application 76, Infrastructure 60, Agent 114 passed |
| Frontend tests | 56 files / 345 tests passed |
| Frontend production build | Passed; 454.73 kB initial, 26.70 kB POS lazy; no budget warnings |
| Pester tests | 22/22 passed (`scripts/tests/*.Tests.ps1`) |
| POS Agent live | `https://rms-pos-agent.localhost:5001/health/live` and `/health/ready` returned 200 (HTTP/1.1) |
| Secure Support Hub live | `https://support-hub.integration.test:4443/` and `/tools/pos-maintenance` returned 200 |
| Chrome normal-user IWA | Passed (`Medium` integrity, non-elevated, Negotiate auth, 6 protected reads 200, local Admin authorized) |
| Edge normal-user IWA | Passed (`Medium` integrity, non-elevated, Negotiate auth, 6 protected reads 200, local Admin authorized) |
| Mutation token & replay | Passed (one-use consumption 200 accepted, replay rejection 403 Forbidden) |
| Disposable service control | Passed (`RmsSupportHub.Pos.Int13.TestService` restarted via Agent SCM dispatch, refreshed to `running`) |
| Evidence record | `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` updated with PASS closure matrix |

## Deferred boundaries

- Testing is default; no Production calls, SQL changes, or deployment were performed.
- UPC live/fixture acceptance and deployment/Production acceptance remain deferred.
- `ConnectionStrings:UpcEcommerceTest` is absent locally; related live calls
  are environment setup, not an INT-07 defect.

