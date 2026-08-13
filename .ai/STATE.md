# Current Project State

- **Updated:** 2026-08-13
- **Branch:** `main` (synchronized with `origin/main` after PR #4 merge)
- **Repository:** `Hossam1104/Rms-Support-Hub`; local path `D:\AI Tools\DBS\Rms-Support-Hub`
- **Programme:** INT-00 through INT-06I complete/accepted; INT-07 read-only POS integration complete/accepted; INT-13 open; INT-08 staged, not executed.
- **Current gate:** INT-06I independent security review PASS; PR #3 merged at `c8706745a9ee8b423b4813badf0ca863b37a5d0e`; INT-07 PR #4 merged at `3a3d58b2406b8e80954fac0174bbdc3b623962f2`. No general API relay or POS mutation route exists.

## Application

- Angular 22 SPA and .NET 10 Web API. Prompt Studio and Online Orders are
  available; `/tools/pos-maintenance` is a direct operational read-only POS
  workspace.
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
- Production runtime OpenAPI remains hidden. INT-13 still owns certificate/
  hostname/representative-device/live operational evidence. INT-08 owns future
  typed service mutation and is not executed.

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
| POS tests | Domain 7, Application 76, Infrastructure 60, Agent 100 passed |
| Frontend tests | 56 files / 342 tests passed |
| Frontend production build | Passed; 454.73 kB initial, 26.70 kB POS lazy; no budget warnings |
| Frontend offline build | Passed; 440.41 kB initial, 26.69 kB POS lazy |
| Generated client | `openapi-typescript` 7.13.0 generation passed |
| Riyal asset verifier | Passed; 924 bytes, SHA-1 verified |
| Runtime smoke | `localhost:4200` and API `/api/modules/health` returned 200 |
| POS Agent live | Unavailable: canonical DNS/certificate prerequisites absent in this environment |
| Broad `scripts/build.ps1` | 190 backend tests passed; 2 known baseline route-status tests fail (expected 404, current 405); backend Release build passes separately |

## Deferred boundaries

- Testing is default; no Production calls, SQL changes, deployment, or
  state-changing POS actions are authorized by this state.
- UPC live/fixture acceptance, deployment/Production acceptance, and
  representative-device Agent evidence remain deferred under INT-13.
- `ConnectionStrings:UpcEcommerceTest` is absent locally; related live calls
  are environment setup, not an INT-07 defect.
