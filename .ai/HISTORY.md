# Completed Work History

Concise milestone index. Detailed implementation evidence belongs in Git and
the linked planning/evidence documents.

## Platform and Support Hub

| Milestone | Evidence | Outcome |
|---|---|---|
| Flask to .NET/Angular rewrite | `936bdda` | Layered .NET API and Angular SPA replaced the Flask baseline. |
| Remediation R0-R10 | `936bdda`..`b011ffb` | Corrected payload/SQL contracts, capability routing, drafts, history, and legacy-tree removal. |
| UI rework U0-U8 | `682fd55`..`d3219dd` | Established Testing defaults, server-owned order lifecycle, shared token UI, toasts, and accessibility/performance baseline. |
| Order Requests unification | `44006a4`..`51ad1e4` | Canonical list/detail route, guarded resend, normalized filtering/paging, and atomic reset. |
| Rename/branding programme | Sessions 00-08; GitHub rename | RMS+ identity, typed assets, token system, shared cards, lazy decorative Hub scene, and repository rename completed. |
| Final cleanup/readiness | Git history and readiness doc | Removed superseded plans/source, closed visual review, and recorded POS integration boundary. |

## POS Maintenance integration

| Milestone | Evidence | Outcome |
|---|---|---|
| INT-00 / INT-00R | Architecture and transport docs | Direct browser-to-loopback Agent, HTTP/1.1, exact-origin CORS, Negotiate, LNA, certificate, and mutation-token boundaries accepted. |
| INT-01 / INT-02 / INT-03 | POS restore/build/test history | Isolated solution skeleton, portable Domain/Application/Contracts, Windows Infrastructure, and retained WinUI imported within scope. |
| INT-03R | Agent provenance `010abc52dc110cfde3dc2c53e057890ff6edaf97` | Corrected Agent snapshot/ignore integrity; destination tests and CI baseline restored. |
| INT-04 | POS host validation | Composed headless Windows-Service-capable Agent at fixed HTTPS loopback with foundation routes and production security. |
| INT-05 / INT-05F | OpenAPI/client validation | Added versioned Agent contract, direct `HttpBackend` transport, deterministic `openapi-typescript@7.13.0` tooling, and no global peer bypass. |
| INT-CI01 | Commit `c560d97`; Actions `31540243375` | Made portable maintenance/SMB semantics deterministic; all five POS CI lanes green. |
| INT-06H | `docs/evidence/POS_INT06_LIVE_TRANSPORT_EVIDENCE.md` | Collected real Chrome/Edge LNA/direct-Agent evidence; the initial admin-token mismatch was recorded as blocked. |
| INT-06I / F1 | Branch `int-06i-admin-auth-scalar`; PR #3 | Replaced UAC-token membership coupling with server-side local-group resolution, completed Scalar/OpenAPI contract and browser evidence, and passed independent security review. |
| INT-06I acceptance | PR #3 merge `c8706745a9ee8b423b4813badf0ca863b37a5d0e` | Reviewed security remediation landed normally; no Critical/High findings. |
| INT-07 read-only first release | PR #4 merge `3a3d58b2406b8e80954fac0174bbdc3b623962f2` | Added protected device/connectivity/configuration/service reads, generated artifacts, direct operational Support Hub workspace, and no mutation/API relay. Agent 100/100; frontend 342/342; all five POS CI lanes green. |
| INT-08 service-control mutation runtime | PR #5 merge `3907bd024acda7fa3af6e1b3ade1502fa4aabce6` | Added the typed opaque-target Start/Stop/Restart Agent route, target/method/path-bound one-use tokens, bounded idempotency/concurrency, explicit outcome truth, direct Angular controls, OpenAPI/client regeneration, and 114 Agent / 345 frontend test coverage. No live or Production service was controlled. |
| INT-13 representative-device evidence | `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` | Executed authorized INT-13 evidence run on representative machine. Live operational tests are `BLOCKED` (DNS, cert, port 5001 listener, and disposable test service absent). Automated contract tests passed 100% (Domain 7, Application 76, Infrastructure 60, Agent 114, Frontend 345, Release build 0 warnings/errors). |
| INT-13P Testing provisioning | `scripts/setup-pos-agent-testing.ps1`, `scripts/remove-pos-agent-testing.ps1`, `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` | Provisioned the exact loopback host, trusted per-device certificate, fixed Agent service, server-owned disposable allow-list target, and dedicated disposable Testing service. Anonymous transport/CORS/HTTP/1.1 and independent disposable SCM lifecycle passed; protected Negotiate/browser/Agent-dispatch proof remains open because no usable SSPI credentials or connected browser were available. |
| INT-13C browser/IWA provisioning | `b5e4de1`; `scripts/PosAgentWindowsProvisioning.psm1`; `tools/pos-browser-evidence`; timestamped live evidence | Added exact typed Chrome/Edge policy and BackConnection provisioning with ownership, WhatIf, fail-closed conflicts, and cleanup; added a Limited interactive-user Playwright channel harness. Automatic provisioning and Medium-integrity Chrome/Edge launch gates passed, but the configured exact Support Hub page was unavailable, so protected browser/session/token/service-control evidence remains open. |
| INT-13C hardening continuation | `scripts/PosAgentWindowsProvisioning.psm1`; `scripts/invoke-pos-browser-evidence.ps1`; timestamped live evidence | Added transactional fail-closed rollback, corrected the documented pre-146 policy fallback, fixed Limited-task collection timing, and exercised both installed channels against the exact origin plus the explicit localhost smoke path. Automatic provisioning passed; protected evidence remains blocked because the exact workspace origin is unavailable. |
| INT-13D secure Support Hub origin | Commit `77c5d70` on PR #7; `scripts/PosTestingConfiguration.psm1`, `scripts/PosSupportHubProvisioning.psm1`, `scripts/start-pos-agent-testing.ps1`; timestamped live evidence | Added the exact `https://support-hub.integration.test:4443` Testing origin, separately owned Support Hub host/certificate/trust, real Angular/API external staging with loopback Kestrel HTTPS/HTTP/1.1, ownership-scoped cleanup, and exact-origin Agent fixture alignment. Offline gates passed; elevated live startup and protected Chrome/Edge evidence remain blocked. |
| INT-13 live operational validation & browser closure | PR #7; `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` | Completed full live operational validation on representative Windows Testing machine. Proved secure origin `https://support-hub.integration.test:4443`, live Agent `https://rms-pos-agent.localhost:5001`, Chrome & Edge Medium-integrity non-elevated Negotiate IWA (0 prompt), server-derived local Administrator authorization (`isAuthorized=true`), protected diagnostic reads, mutation token issuance/consumption, replay rejection (403), and Agent-dispatched disposable Testing service control (`accepted`, refreshed to `running`). INT-13 is closed. |
| RMS installation discovery and diagnostics | Pending PR commit | Added read-only installed RMS discovery, fixed Branch/Cashier SQL identity probes, canonical SCM rows, sanitized API/OpenAPI/client contracts, and Support Hub dashboard integration. Testing-machine validation completed without service control or database writes. |
| Typed Branch/Cashier database backup and restore | Pending PR commit; POS Release/test gates | Added Agent-owned canonical backup/restore contracts, fixed roots and opaque artifact catalog, native bounded SQL backup/inspection/restore/identity verification, target-specific service recovery, one-use token/idempotency/concurrency/progress/audit state, generated OpenAPI/client contracts, Support Hub recovery shelves with typed restore confirmation, and synthetic integration coverage. No live database write or service control was run. |

## Current programme status

INT-00 through INT-08 and INT-13 are complete and validated. The RMS
installation/diagnostics and typed database recovery slices are implemented
and repository-validated; live RMS backup/restore remains deliberately
untested. The next milestone is a planning pass for POS Downloader /
Deployment + Cleanup / Maintenance. Production/customer deployment remains
blocked on M-1 and M-2.

