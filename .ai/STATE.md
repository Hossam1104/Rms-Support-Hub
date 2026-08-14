# Current Project State

- **Updated:** 2026-08-15
- **Repository:** `Hossam1104/Rms-Support-Hub`; the POS Downloader / Deployment + Cleanup / Maintenance slice is implemented and validated for normal PR delivery.
- **Programme:** INT-00 through INT-08 and INT-13 are complete/accepted on the representative Testing machine. The completed RMS discovery/diagnostics and typed Branch/Cashier database recovery work remains intact. Production/customer deployment remains blocked on M-1 managed browser policy and M-2 Production certificate lifecycle.
- **Next executable task:** `TASK.md` is the full `POS Final Functional Integration + UX + Production Packaging` task. Do not execute it as part of the completed Downloader/Maintenance delivery.

## Application and POS architecture

- Angular 22 SPA and .NET 10 Web API; `/tools/pos-maintenance` is the direct operational workspace.
- POS privileged traffic is browser -> `RmsSupportHub.Pos.Agent` over direct loopback HTTPS/HTTP/1.1, never through API/Core/Data. Production uses Negotiate, exact-origin CORS, server-resolved local Built-in Administrators authorization, and fail-closed SID handling.
- INT-05 owns versioned `/pos/openapi`, the generated client, and direct `HttpBackend`; INT-07 owns device/connectivity/configuration/service reads; INT-08 owns typed service control and the later POS operation slices.
- RMS discovery uses a secret-bearing internal connection-string seam and fixed identity probes. Raw credentials and connection strings never enter transport or UI models.
- RMS database recovery exposes only typed `branch` and `cashier` routes. The Agent owns canonical names/service mapping, fixed roots, the durable approved backup catalog, opaque database-backup artifacts, native bounded SQL, exact restore confirmation, target-specific service coordination, one-use target/path-bound tokens, bounded idempotency/concurrency, principal-scoped REST/SSE progress, and safe privileged audit events.
- Downloader routes are typed and server-owned: approved branch catalog, batch trigger, principal-scoped REST/SSE operation state, and `/api/v1/artifacts/{artifactId}` for expiring principal-scoped opaque artifact capabilities. `AgentRuntimeSettingsFactory` projects stored configuration and encrypted secrets into the existing `DbDownloadService` seam; remote SMB/API values never cross to Angular.
- Maintenance routes are typed and server-owned: cleanup/reset preview and execute, principal-bound expiring preview challenges, exact confirmation, re-evaluated `MaintenanceService` policy, bounded operation state/SSE, and sanitized partial/recovery/unknown outcome evidence. Browser requests never supply service, database, table, or filesystem targets.
- All new downloader/maintenance operation, challenge, and idempotency stores are bounded and process-local; durable approved RMS database backups remain separate from the generic in-memory artifact catalog.

## Security and evidence

- Exact Testing Support Hub origin: `https://support-hub.integration.test:4443`; direct Agent origin: `https://rms-pos-agent.localhost:5001`.
- No Production calls, customer execution, real Restore, real branch reset, real cleanup, or real downloader trigger/download were performed in this slice. Automated endpoint coverage uses synthetic HTTP/SMB/filesystem/database/service seams and temporary test roots only.
- The pending representative-device, non-destructive Branch/Cashier Backup proof remains open. The required elevated Administrator session must redeploy the corrected Agent to Testing, run Backup for both targets, verify approved artifact/checksum evidence, restart the Agent, and confirm the opaque backup handle survives the restart. Do not use Restore as part of that proof.
- Any future live downloader or maintenance verification must remain Testing-only, use approved disposable/synthetic targets, and be separately evidenced. An unavailable elevated session is a recorded live-evidence blocker, not a reason to weaken the implementation boundary.

## Validation baseline

| Gate | Result |
|---|---|
| POS Release build (`-warnaserror`) | Passed, 0 warnings/errors |
| POS tests | Domain 9, Application 76, Infrastructure 80, Agent.IntegrationTests 146 passed (311 total) |
| Focused Downloader/Maintenance endpoint tests | 5 passed, including artifact download, unauthorized/invalid token, cleanup/reset fake seams, ambiguous trigger, and concurrency paths |
| Frontend tests | 56 files / 345 tests passed |
| Frontend production build | Passed; initial bundle 460.78 kB and no component-style budget warnings |
| OpenAPI/client regeneration | Regenerated from the Agent document; second generation pass byte-stable |
| `git diff --check` / secret review | Passed; no credential-like literals or credential-bearing URLs; the only UNC-like value is an isolated synthetic downloader fake used to prove it does not cross the response boundary |

## Deferred boundaries

- M-1 managed Chrome/Edge browser policy and Local Network Access/IWA fleet behavior remain open external gates.
- M-2 Production certificate issuance, trust distribution, renewal/rotation, private-key ACL, hostname, and rollback ownership remain open external gates.
- The next task prepares final operator UX and the real Agent package/install boundary; it must not close M-1/M-2 or perform Production/customer actions.
- `ConnectionStrings:UpcEcommerceTest` remains absent locally; unrelated UPC live calls are environment setup, not a POS defect.
