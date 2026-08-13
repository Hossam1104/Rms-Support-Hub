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

## Current programme status

INT-00 through INT-08 are complete within their approved boundaries. INT-13
representative-device/live operational evidence is executed and recorded as
`BLOCKED` on machine prerequisites in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`.
The broad build was environment-blocked by a running local API process; its safe no-build
regression run still has the two known unchanged route-status assertions (404
expected vs 405 actual), unrelated to POS.
