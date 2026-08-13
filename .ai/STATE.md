# Current Project State

- **Updated:** 2026-08-14
- **Branch:** `int-13p-testing-agent-provisioning` (INT-13D follow-up on PR #7; current head `77c5d70`)
- **Repository:** `Hossam1104/Rms-Support-Hub`; local path `D:\AI Tools\DBS\Rms-Support-Hub`
- **Programme:** INT-00 through INT-07 complete/accepted; INT-08 complete/validated; INT-13P Testing prerequisites provisioned; INT-13 remains open.
- **Current gate:** INT-06I independent security review PASS; PR #3 merged at `c8706745a9ee8b423b4813badf0ca863b37a5d0e`; INT-07 PR #4 merged at `3a3d58b2406b8e80954fac0174bbdc3b623962f2`; INT-08 PR #5 merged at `3907bd024acda7fa3af6e1b3ade1502fa4aabce6`. INT-08 adds only the typed target-bound service-control route; no general API relay or generic POS mutation surface exists.

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
  truthful typed outcomes. Production runtime OpenAPI remains hidden. INT-13
  owns certificate/hostname/representative-device/live operational evidence.
- INT-13P has repository-owned, idempotent, reversible Testing provisioning and
  cleanup scripts plus a separate disposable Windows Service harness. Earlier
  authorized prerequisite evidence is retained in the INT-13 evidence record;
  the current session does not assert those services are running. No Production
  or customer state was touched.
- INT-13C adds `scripts/PosAgentWindowsProvisioning.psm1` for exact, typed,
  version-selected Chrome/Edge IWA and loopback policy provisioning plus the
  exact `BackConnectionHostNames` REG_MULTI_SZ entry. It preserves unrelated
  values, records ownership, supports WhatIf, and fails closed on wildcard,
  block-policy, malformed-value, and registry-type conflicts. Installed Chrome
  and Edge are generation 151 and use `LoopbackNetworkAllowedForUrls`.
- INT-13C adds `tools/pos-browser-evidence` and
  `scripts/invoke-pos-browser-evidence.ps1`: pinned Playwright channel launch,
  fresh profile, Limited interactive-user task, Medium-integrity verification,
  sanitized output, and optional opaque-target one-action path. Chrome and Edge
  launch gates passed on 2026-08-13, but the configured exact Support Hub origin
  did not serve the real workspace; protected browser evidence remains open.
- INT-13D adds `scripts/PosTestingConfiguration.psm1` with the one exact
  Testing-only Support Hub origin `https://support-hub.integration.test:4443`
  and the unchanged direct Agent origin. `scripts/PosSupportHubProvisioning.psm1`
  owns the separate Support Hub hosts entry and LocalMachine certificate/trust;
  the certificate requires one exact SAN, Server Authentication EKU, Microsoft
  Software KSP, and a non-exportable private key.
- INT-13D adds `scripts/start-pos-agent-testing.ps1`, which builds the real
  Angular production bundle, publishes the existing API to external
  machine-local staging, serves it from API `wwwroot` over loopback HTTPS
  `127.0.0.1:4443` with HTTP/1.1 and the exact allowed host, and proves both
  Support Hub shell routes before success. Cleanup verifies the owned assembly
  before stopping/removing the runtime. Live start was blocked by the current
  non-elevated session; no protected browser or service-action evidence is
  claimed.

## Security and review record

- INT-06/06F/06G/06H blocked states are historical. INT-06I remediated local
  Administrator resolution with indirect local-group membership and the
  Built-in Administrators SID; independent review PASS found no Critical/High.
- Post-remediation Chrome/Edge browser authorization and Scalar/OpenAPI
  evidence passed; no SID/token exposure. PR #3 is merged normally.

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
| Frontend offline build | Passed; 440.41 kB initial, 26.69 kB POS lazy |
| Generated client | `openapi-typescript` 7.13.0 generation passed |
| Riyal asset verifier | Passed; 924 bytes, SHA-1 verified |
| Runtime smoke | `localhost:4200` and API `/api/modules/health` returned 200 |
| POS Agent live | INT-13C automatic Chrome/Edge policy and BackConnection verification plus prior anonymous transport/browser launch evidence are recorded. Current INT-13D secure-origin startup is `BLOCKED` by the non-elevated session; ports 4443 and 5001 had no listeners, and protected Negotiate/browser reads and Agent-dispatched service-control evidence remain `NOT RUN`; see `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` |
| INT-13D focused validation | Pester `22/22`, all modified PowerShell parse checks, browser harness `node --check`, POS Release build/tests, WinUI publish, frontend `56/56` files / `345/345` tests, frontend production build, and backend Release build passed. Repository-wide build remains `190/192` with two known unchanged 404-vs-405 assertions. |
| Broad `scripts/build.ps1` | Reached backend tests after the verified stale `RmsSupportHub.Api` Debug lock was stopped; 190 passed and 2 known unchanged 404-vs-405 route-status assertions failed. POS Release build and frontend gates passed separately. |

## Deferred boundaries

- Testing is default; no Production calls, SQL changes, deployment, or live
  service actions were performed for INT-08. Fakes covered service dispatch.
- UPC live/fixture acceptance and deployment/Production acceptance remain
  deferred. INT-13 remains open pending an elevated Testing-only start of the
  real Support Hub workspace at the configured exact HTTPS origin, followed by
  protected Agent reads, server-derived authorization, mutation-token, and
  Agent-dispatched service-control evidence from the Limited non-elevated
  Chrome/Edge harness.
- `ConnectionStrings:UpcEcommerceTest` is absent locally; related live calls
  are environment setup, not an INT-07 defect.
