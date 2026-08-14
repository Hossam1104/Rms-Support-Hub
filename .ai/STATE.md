# Current Project State

- **Updated:** 2026-08-14
- **Branch:** `agent/rms-installation-diagnostics` (based on synchronized `main` `8349ef4`)
- **Repository:** `Hossam1104/Rms-Support-Hub`; local path `D:\AI Tools\DBS\Rms-Support-Hub`
- **Programme:** INT-00 through INT-08 and INT-13 are complete/accepted on the representative Testing machine. Production/customer deployment remains blocked on M-1 managed browser policy and M-2 production certificate lifecycle.
- **Current feature slice:** RMS installation discovery, sanitized Branch/Cashier diagnostics, endpoint reachability, canonical SCM visibility, typed Agent-owned Branch/Cashier database backup/restore, generated contracts, and Support Hub recovery controls are implemented. M-1/M-2 remain open.

## Application and POS architecture

- Angular 22 SPA and .NET 10 Web API; `/tools/pos-maintenance` is a direct operational workspace.
- POS privileged traffic is browser -> `RmsSupportHub.Pos.Agent` over direct loopback HTTPS/HTTP/1.1, never through API/Core/Data. Production uses Negotiate, exact-origin CORS, server-resolved local Built-in Administrators authorization, and fail-closed SID handling.
- INT-05 owns versioned `/pos/openapi`, generated client, and direct `HttpBackend`; INT-07 owns device/connectivity/configuration/service reads; INT-08 owns typed service control.
- RMS discovery uses a secret-bearing internal connection-string seam and fixed `DB_NAME()` probes. Raw credentials and connection strings never enter transport or UI models.
- RMS database recovery exposes only typed `branch` and `cashier` routes. The Agent owns canonical names/service mapping, fixed roots, opaque approved artifact handles, native bounded SQL, exact restore confirmation, target-specific service coordination, one-use target/path-bound tokens, bounded idempotency/concurrency, principal-scoped REST/SSE progress, and safe privileged audit events.
- No browser-selected database, SQL, filesystem path, service, credential, generic command, or uploaded script crosses the Agent boundary.

## Security and evidence

- Exact Testing Support Hub origin: `https://support-hub.integration.test:4443`; direct Agent origin: `https://rms-pos-agent.localhost:5001`.
- Testing live evidence is recorded in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`; no Production calls were made.
- Live RMS backup/restore SQL and service execution were not run. Automated recovery tests use synthetic SQL/filesystem/service fixtures and never touch installed `RmsBranchSrv` or `RmsCashierSrv` databases.

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

Raw colors stay in token files; all current UI feature styles consume design tokens.

## Validation baseline

| Gate | Result |
|---|---|
| POS Release build | Passed with Testing origin and warnings treated as errors |
| POS tests | Domain 9, Application 76, Infrastructure 71, Agent 128 passed |
| Frontend tests | 56 files / 345 tests passed |
| Frontend production build | Passed with typed RMS recovery UI and no budget warnings |
| OpenAPI/client | Regenerated and consumed by the Angular transport |
| Existing live evidence | Agent/Support Hub/browser/service-control evidence remains in the INT-13 record |

## Deferred boundaries

- Testing is the default; no Production calls, SQL changes, deployment, live database restore, or live RMS service control were performed for this slice.
- UPC live/fixture acceptance and deployment/Production acceptance remain deferred.
- `ConnectionStrings:UpcEcommerceTest` is absent locally; related live calls are environment setup, not an INT-07 defect.
