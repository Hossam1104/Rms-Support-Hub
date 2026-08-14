# Current Project State

- **Updated:** 2026-08-14
- **Branch:** `main` at `615fda2` (PR #9 merged: `93d6875` review-correction commit on top of `2384414`/`e0a20bc`)
- **Repository:** `Hossam1104/Rms-Support-Hub`; local path `D:\AI Tools\DBS\Rms-Support-Hub`
- **Programme:** INT-00 through INT-08 and INT-13 are complete/accepted on the representative Testing machine. Production/customer deployment remains blocked on M-1 managed browser policy and M-2 production certificate lifecycle.
- **Current feature slice:** RMS installation discovery, sanitized Branch/Cashier diagnostics, endpoint reachability, canonical SCM visibility, typed Agent-owned Branch/Cashier database backup/restore, generated contracts, and Support Hub recovery controls are implemented and merged to `main`. A planner review of PR #9 found and this session fixed three recovery-blocking defects before merge (see below). M-1/M-2 remain open. Next slice is POS Downloader/Deployment+Cleanup/Maintenance (see `TASK.md`).

## Application and POS architecture

- Angular 22 SPA and .NET 10 Web API; `/tools/pos-maintenance` is a direct operational workspace.
- POS privileged traffic is browser -> `RmsSupportHub.Pos.Agent` over direct loopback HTTPS/HTTP/1.1, never through API/Core/Data. Production uses Negotiate, exact-origin CORS, server-resolved local Built-in Administrators authorization, and fail-closed SID handling.
- INT-05 owns versioned `/pos/openapi`, generated client, and direct `HttpBackend`; INT-07 owns device/connectivity/configuration/service reads; INT-08 owns typed service control.
- RMS discovery uses a secret-bearing internal connection-string seam and fixed `DB_NAME()` probes. Raw credentials and connection strings never enter transport or UI models.
- RMS database recovery exposes only typed `branch` and `cashier` routes. The Agent owns canonical names/service mapping, fixed roots, opaque approved artifact handles, native bounded SQL, exact restore confirmation, target-specific service coordination, one-use target/path-bound tokens, bounded idempotency/concurrency, principal-scoped REST/SSE progress, and safe privileged audit events.
- No browser-selected database, SQL, filesystem path, service, credential, generic command, or uploaded script crosses the Agent boundary.
- Restore preflights against SQL Server `master` and the approved backup artifact rather than requiring the target Branch/Cashier database to open, so it works when that database is unavailable/missing (the actual recovery scenario). Backup still requires the live target database and identity confirmation. See `RmsDatabaseDiagnostics.DiagnoseServerAsync`, `RmsSqlDatabaseOperations.InspectBackupAsync`.
- Approved database backups live in a durable, Agent-owned `RmsDatabaseBackupCatalog` (atomic schema-versioned JSON under the backup root, fail-closed corruption handling, physical revalidation on every read) that survives an Agent restart, fully decoupled from the generic in-memory `ArtifactCatalog` used for browser-download artifacts. Retention is `RmsDatabaseStorageOptions.BackupRetention` (30-day default) plus the existing per-database count cap (`MaximumBackupsPerDatabase`, default 32); a still-valid backup is never deleted by the unrelated 24h `RuntimeRetentionPolicy.ArtifactLifetime`. See ADR-0022 consequences.

## Security and evidence

- Exact Testing Support Hub origin: `https://support-hub.integration.test:4443`; direct Agent origin: `https://rms-pos-agent.localhost:5001`.
- Testing live evidence is recorded in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`; no Production calls were made.
- Live RMS backup/restore SQL and service execution were not run. Automated recovery tests use synthetic SQL/filesystem/service fixtures and never touch installed `RmsBranchSrv` or `RmsCashierSrv` databases.
- This session attempted bounded live non-destructive Backup validation against the representative Testing machine's real `RmsBranchSrv`/`RmsCashierSrv` (RMS.BranchService/RMS.CashierService/RmsSupportHub.Pos.Agent (Testing)/SQL Server MSSQLSERVER were all observed `Running`). It was blocked: the execution session's Windows token is non-elevated (`IsInRole(Administrator)=False`) and cannot open the `RmsSupportHub.Pos.Agent` service via SCM (`Cannot open ... service`), matching the same elevation-gate class of blocker recorded repeatedly in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` (e.g. INT-13E). No service was stopped/started, no SQL was run, no ACL was changed. Real Restore was never attempted (not authorized). The next safe action is for an elevated Administrator session to redeploy the corrected Agent build to the Testing service, run Backup for both targets, verify the approved artifact/checksum, restart the Agent service, and confirm the same opaque backup ID resolves afterward.

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
| POS Release build (`-warnaserror`) | Passed, 0 warnings/errors |
| POS tests | Domain 9, Application 76, Infrastructure 80, Agent.IntegrationTests 141 passed (306 total, incl. 11 new durable-catalog tests) |
| Frontend tests | 56 files / 345 tests passed |
| Frontend production build | Passed with typed RMS recovery UI and no budget warnings |
| OpenAPI/client drift | Regenerated; `git diff --exit-code` clean against committed OpenAPI doc and Angular client |
| `git diff --check` / secret scan | Clean |
| PR #9 CI | All 5 lanes green on merge commit `93d6875` -> `615fda2` |
| Existing live evidence | Agent/Support Hub/browser/service-control evidence remains in the INT-13 record |
| Live backup (this slice) | `BLOCKED` by non-elevated session; see Security and evidence above |

## Deferred boundaries

- Testing is the default; no Production calls, SQL changes, deployment, live database restore, or live RMS service control were performed for this slice.
- UPC live/fixture acceptance and deployment/Production acceptance remain deferred.
- `ConnectionStrings:UpcEcommerceTest` is absent locally; related live calls are environment setup, not an INT-07 defect.
