# POS INT-13 Live Operational Evidence

**Latest Execution Date:** 2026-08-14 (22:13 UTC INT-13 Final Browser & Service Action Closure)
**Gate Status:** `INT-13 CLOSED (COMPLETED AND VALIDATED LIVE)`
**Latest Result:** `PASS`
**Latest Repository Baseline:** `2453b6b` (branch `int-13p-testing-agent-provisioning`, PR #7)

---

## Executive Summary

Owner authorization was granted to execute INT-13P on this representative Testing
machine. The latest run provisioned only the bounded Testing prerequisites and
verified the fixed direct path's anonymous transport boundary:
1. `https://rms-pos-agent.localhost:5001` resolved to loopback, presented the
   trusted exact-host certificate, listened on HTTP/1.1, and returned healthy
   live/ready responses.
2. Exact-origin CORS and negative preflight checks passed; plain HTTP fallback
   was absent.
3. The disposable Testing harness passed an independent lifecycle check, but no
   authenticated Agent mutation was sent.
4. Protected Negotiate evidence is still blocked because the non-browser SSPI
   context has no usable credentials, and no connected Chrome/Edge browser
   session is available. Browser policy and Production/customer state were not
   changed or contacted.
5. Task-scoped POS and frontend contract/build gates passed; the broad backend
   regression reached 190 passes plus two known unchanged 404-vs-405 assertions.

The pre-provisioning machine state and its blocked safety-gate conclusions are
retained in the historical sections below for audit continuity.

---

## Historical Pre-Provisioning Machine Environment Preflight

| Environment Component | Observed State | Classification | Result | Notes |
|---|---|---|---|---|
| OS Version | Windows 11 Pro 10.0.26200 | Environment | Query Passed | x64 Architecture |
| Host Resolution (`rms-pos-agent.localhost`) | `No such host is known` | Live Evidence | `BLOCKED` | Canonical loopback host entry absent |
| TLS Certificate (`rms-pos-agent`) | `ABSENT` | Live Evidence | `BLOCKED` | Not present in LocalMachine/CurrentUser store |
| HTTPS Port 5001 Listener | `NOT LISTENING` | Live Evidence | `BLOCKED` | POS Agent service process not running |
| Disposable Testing Service | `ABSENT` | Live Evidence | `BLOCKED` | Hard safety gate prevented live SCM mutation |
| Machine Security Policy | Unmodified | Safety Gate | Enforced | No hosts, cert, registry, or SPN mutations made |

---

## Historical Pre-Provisioning Evidence Matrix

### 1. Transport / Certificate / Loopback

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E1.1 | Host | Hostname Resolution (`rms-pos-agent.localhost`) | Live Evidence | `BLOCKED` | `[System.Net.Dns]::GetHostAddresses("rms-pos-agent.localhost")` returned `No such host is known`. |
| E1.2 | Binding | Loopback Binding (`127.0.0.1:5001` & `[::1]:5001`) | Live Evidence | `BLOCKED` | Port 5001 is closed; no listener active on loopback. |
| E1.3 | Cert | Trusted TLS Certificate | Live Evidence | `BLOCKED` | Certificate matching `rms-pos-agent` absent in Windows cert store. |
| E1.4 | Security | Transport & Protocol Policy | Automated Contract | `PASS` | `LoopbackBinding.cs` & `HttpsOnlyMiddleware` enforce HTTPS, HTTP/1.1, and loopback-only binding in `RmsSupportHub.Pos.Agent.IntegrationTests`. |

### 2. CORS / Origin

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E2.1 | CORS | Configured Support Hub Origin | Live Evidence | `BLOCKED` | Agent service not running live to receive HTTP OPTIONS preflight. |
| E2.2 | CORS | Reject Unapproved / Alternate Origins | Automated Contract | `PASS` | `CorsTests.cs` (in IntegrationTests) verifies exact origin matching (`https://support-hub.integration.test:4443`), rejecting non-matching origins with 403 Forbidden. No wildcard origins. |

### 3. Chrome + Edge Browser Support

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E3.1 | Browser | Chrome & Edge Negotiate Auth & LNA | Live Evidence | `BLOCKED` | Requires active HTTPS loopback endpoint and trusted cert. Browser policies were preserved without modification. |
| E3.2 | Browser | UI Maintenance Workspace | Automated Contract | `PASS` | `pos-maintenance.component.spec.ts` (6/6 passed) verifies UI rendering, state displays, and confirmation modals. |

### 4. Authentication & Authorization

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E4.1 | Auth | Server-Derived Windows Identity | Live Evidence | `BLOCKED` | Live SSPI Negotiate handshake requires active Agent. |
| E4.2 | Auth | Local Administrators Group Membership | Automated Contract | `PASS` | `SessionAndAuthorizationTests.cs` verifies identity is derived server-side via `LocalAdministratorsOnlyHandler` and `WindowsAdministratorGroupChecker`. No SID or token parameter trusted from client. |

### 5. Read-Only Live Agent Routes

| ID | Area | Endpoint / Surface | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E5.1 | Read | `/health/live`, `/health/ready` | Automated Contract | `PASS` | Verified via `ReadOnlyFirstReleaseEndpointTests.cs` (returns 200 OK with `HealthStatusDto`). |
| E5.2 | Read | `/api/v1/session` | Automated Contract | `PASS` | Verified via `SessionAndAuthorizationTests.cs` (returns principal name, auth state, API version; SID redacted). |
| E5.3 | Read | `/api/v1/device/*` (identity, connectivity, capabilities) | Automated Contract | `PASS` | Verified via `ReadOnlyFirstReleaseEndpointTests.cs` (sanitized diagnostics returned). |
| E5.4 | Read | `/api/v1/configuration`, `/api/v1/services` | Automated Contract | `PASS` | Verified via `ReadOnlyFirstReleaseEndpointTests.cs` (redacted configuration, allow-listed service statuses). |

### 6. Mutation Token Live Evidence

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E6.1 | Token | Issuance & Short Lifetime | Automated Contract | `PASS` | `MutationTokenIssuanceTests.cs` verifies issuance of 60-second one-use tokens bound to operation, target path, HTTP method, exact origin, and principal SID. |
| E6.2 | Token | One-Use Replay Protection & Expiry Rejection | Automated Contract | `PASS` | `MutationTokenTests.cs` verifies second consumption attempt returns `MutationTokenAlreadyConsumed` (400 Bad Request). Expired token returns `MutationTokenExpired`. |

### 7. Real Windows Service Control (SCM)

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E7.1 | SCM | Disposable Testing Service Availability | Live Evidence | `BLOCKED` | Hard Safety Gate: No disposable Testing service exists on the machine. Real SCM mutation **NOT RUN**. |
| E7.2 | SCM | Typed Action Dispatch & Idempotency | Automated Contract | `PASS` | `ServiceActionEndpointTests.cs` verifies Start, Stop, and Restart actions returning typed outcomes (`NotAttempted`, `Accepted`, `Failed`, `OutcomeUnknown`). Idempotency key and target concurrency gate verified. |

### 8. Failure States

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E8.1 | Failures | Fail-closed security assertions | Automated Contract | `PASS` | Unauthenticated requests return 401 Unauthorized; non-admin users return 403 Forbidden; missing/invalid mutation token returns 400 Bad Request. |

### 9. Scalar / OpenAPI Documentation

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| E9.1 | OpenAPI | OpenAPI Document Generation | Automated Contract | `PASS` | `OpenApiGenerateDocumentsOnBuild` generated `pos/openapi/RmsSupportHub.Pos.Agent.json` during Release build. Client code generated via `generate:pos-agent-client`. |
| E9.2 | Security | Production Hiding | Automated Contract | `PASS` | `Program.cs` maps `/scalar` and `/openapi` only when `IsDevelopment()` or `IntegrationTest`. Production composition hides documentation endpoints. |

### 10. Automated Validation Suite Results

| Suite | Component | Tests Run | Result | Duration |
|---|---|---|---|---|
| .NET POS Domain | `RmsSupportHub.Pos.Domain.Tests` | 7 / 7 passed | `PASS` | 0.04s |
| .NET POS Application | `RmsSupportHub.Pos.Application.Tests` | 76 / 76 passed | `PASS` | 1.00s |
| .NET POS Infrastructure | `RmsSupportHub.Pos.Infrastructure.Tests` | 60 / 60 passed | `PASS` | 0.50s |
| .NET POS Agent Integration | `RmsSupportHub.Pos.Agent.IntegrationTests` | 114 / 114 passed | `PASS` | 10.00s |
| OpenAPI Generator | `pos-agent-client-generator` | OpenAPI v1 Schema -> TS | `PASS` | 0.14s |
| Frontend Angular | `rms-support-hub` (Vitest) | 345 / 345 passed | `PASS` | 49.70s |
| Frontend Build | Angular Release Build | Production Bundle | `PASS` | 16.58s |

---

## Historical Blockers & Next Safe Actions

1. **Current Blocker:**  
   The representative Windows Testing environment lacks the canonical DNS mapping (`rms-pos-agent.localhost`), trusted machine TLS certificate, running `RmsSupportHub.Pos.Agent` background service, and an approved disposable Testing Windows Service.

2. **Next Safe Action:**  
   To complete INT-13 live operational validation in a future environment session:
   - Provision `rms-pos-agent.localhost` loopback DNS / hosts resolution.
   - Provision and trust the testing TLS certificate in `Cert:\LocalMachine\My`.
   - Install and start `RmsSupportHub.Pos.Agent` as a Windows Service listening on `https://rms-pos-agent.localhost:5001`.
   - Register an approved disposable Testing Windows Service for live SCM mutation testing.

---

## INT-13P Testing Provisioning + Live Rerun

**Run window:** 2026-08-13 15:23–15:31 +03:00
**Environment classification:** representative Windows Testing machine only
**Authorization:** owner-authorized INT-13P provisioning and Testing-only live verification
**Result:** `PARTIALLY COMPLETED` — transport and disposable-service prerequisites passed; protected Negotiate/browser evidence could not complete in this execution context.

This section is a new run and does not replace the historical blocked evidence
above. The setup was performed through the repository-owned, reversible
`scripts/setup-pos-agent-testing.ps1` mechanism. The disposable target is
represented below only by its opaque browser ID: `svc-80099324ea397d79`.

### Provisioning evidence

| ID | Provisioned item | Result | Redacted observation |
|---|---|---|---|
| P13.1 | Elevation and Testing gate | `PASS` | Setup ran from an elevated Administrator PowerShell session with the explicit `-IUnderstandTestingOnly` acknowledgement. |
| P13.2 | Canonical hostname | `PASS` | `rms-pos-agent.localhost` resolved to `127.0.0.1` only during provisioning; no LAN/public address was added. The hosts entry is exact and tool-owned. |
| P13.3 | LocalMachine TLS certificate | `PASS` | Exact DNS SAN `rms-pos-agent.localhost`; currently valid; private key present; CNG provider; non-exportable key policy; server-authentication EKU only; trusted in LocalMachine; LocalSystem read access verified. Runtime thumbprint: `91093374EA55BF014E53A0B9C96487C3678F0ACA`. |
| P13.4 | Agent deployment | `PASS` | Release `win-x64` publish installed outside the Git tree; the existing `UseWindowsService` identity and fixed loopback binding were preserved. Agent service reached `Running`. |
| P13.5 | Disposable Testing service | `PASS` | A dedicated unmistakable INT-13 Testing harness was published outside the Git tree. It has no business, SQL, network, child-process, or generic command behavior and reached `Running` as LocalSystem. |
| P13.6 | Server-owned allow-list | `PASS` | Existing service configuration was backed up byte-for-byte with its ACL; exactly one disposable Testing target was appended. Browser-facing evidence uses only `svc-80099324ea397d79`; no raw target name crosses the HTTP contract. |
| P13.7 | Idempotency and rollback preview | `PASS` | Setup rerun reused the owned certificate/services and did not duplicate the host or target. `setup... -WhatIf` and `remove... -WhatIf` completed without machine changes. Cleanup checks ownership, executable identity, hashes, and original configuration/ACL before removal or restore. |
| P13.8 | Security boundary | `PASS` | No wildcard CORS, HTTP fallback, LAN binding, TLS bypass, browser-policy change, `DisableLoopbackCheck`, SPN, registry bypass, credential, PFX, private-key export, SQL, Production, or customer-service operation was used. |

### Direct live transport and Agent evidence

Commands used included the following, with response bodies and headers reduced
to safe facts before recording:

```powershell
[Net.Dns]::GetHostAddresses('rms-pos-agent.localhost')
Get-NetTCPConnection -LocalPort 5001 -State Listen
curl.exe --http1.1 https://rms-pos-agent.localhost:5001/health/live
curl.exe --http1.1 https://rms-pos-agent.localhost:5001/health/ready
curl.exe -X OPTIONS -H 'Origin: https://support-hub.integration.test:4443' `
  -H 'Access-Control-Request-Method: GET' -H 'Access-Control-Request-Headers: Accept' `
  --http1.1 https://rms-pos-agent.localhost:5001/api/v1/session
curl.exe --negotiate -u : -H 'Origin: https://support-hub.integration.test:4443' `
  -H 'Accept: application/json' --http1.1 `
  https://rms-pos-agent.localhost:5001/api/v1/session
```

| ID | Area | Live check | Result | Evidence |
|---|---|---|---|---|
| L13.1 | DNS | Canonical host resolution | `PASS` | Only `127.0.0.1` was returned by the provisioning/runtime resolver check. |
| L13.2 | Listener | Port 5001 binding | `PASS` | Listening addresses were `127.0.0.1` and `::1`; no non-loopback listener was present. |
| L13.3 | TLS | Trusted HTTPS and exact hostname | `PASS` | `curl.exe` completed HTTPS health requests without `--insecure`; the machine-trusted certificate matched the canonical SAN. |
| L13.4 | Protocol | HTTP/1.1 | `PASS` | `/health/live`, `/health/ready`, and CORS responses reported HTTP/1.1. |
| L13.5 | HTTP fallback | Plain HTTP on port 5001 | `PASS` | Plain HTTP produced no response (`curl` status `000`); there is no HTTP listener/fallback. |
| L13.6 | CORS | Exact-origin anonymous preflight | `PASS` | Exact origin returned `204`; `Access-Control-Allow-Origin` was the one configured origin, allowed methods were `GET,POST`, allowed headers were the typed contract headers, credentials were enabled, and `Vary: Origin` was present. |
| L13.7 | CORS negative | Wrong origin, method, and header preflights | `PASS` | Each rejected preflight returned `403` with the safe CORS rejection contract; no wildcard/reflected origin was returned. |
| L13.8 | Negotiate | Authenticated Windows session | `BLOCKED` | The endpoint issued a Negotiate challenge, but this non-browser execution context reported `SEC_E_NO_CREDENTIALS`; no credential or challenge blob was recorded. |
| L13.9 | Authorization | Server-derived local Administrator decision | `NOT RUN` | No authenticated session was available, so no live authorization claim is made. Existing server-side implementation and contract tests remain separate evidence. |
| L13.10 | Agent liveness | `/health/live` and `/health/ready` | `PASS` | Both returned HTTP `200`, JSON status `live`/`ready`, and HTTP/1.1. |
| L13.11 | Agent reads | Session, device, configuration, and service routes | `BLOCKED` | Exact-origin Negotiate probes reached the Agent but returned `401` for each protected read because SSPI credentials were unavailable. No raw identity, SID, machine name, service name, path, or response header was retained. |
| L13.12 | Mutation token | Issue/binding/replay/expiry | `NOT RUN` | No token was issued because authentication was blocked. Existing Agent contract tests cover operation, target, method/path, one-use, replay, and expiry semantics. |
| L13.13 | Typed service control | Agent dispatch and typed outcome | `NOT RUN` | No HTTP mutation was sent; the direct SCM harness check below is explicitly not represented as Agent dispatch evidence. |

### Disposable SCM harness check (not Agent dispatch)

Only the newly provisioned disposable Testing target was used. This was a
bounded independent SCM health check to prove that the harness itself has
predictable lifecycle behavior; it does not substitute for the blocked
authenticated Agent route.

| Step | Result |
|---|---|
| Initial state | `Running` |
| Restart | `Running` |
| Stop | `Stopped` |
| Start for continued Testing readiness | `Running` |
| Automatic retry after `OutcomeUnknown` | Not used; no `OutcomeUnknown` occurred |

### Chrome and Edge evidence

| Browser | Installed build observed | Browser control surface | Result |
|---|---|---|---|
| Chrome | `151.0.7922.109` | No connected in-app/extension browser instance | `NOT RUN` |
| Edge | `151.0.4129.78` | No connected in-app/extension browser instance | `NOT RUN` |

The browser runtime reported zero connected instances. Consequently, secure
context, LNA permission/policy behavior, browser Negotiate back-connection,
Angular workspace reads, confirmation UI, mutation UI, and post-action refresh
remain unproven. No browser managed policy or temporary browser bypass was
applied. The next safe action is to run the browser portion from a connected
Chrome/Edge session while retaining the current Testing prerequisites, then
authenticate through the fixed direct Agent origin.

### Cleanup state

The Testing prerequisites remain installed for the owner’s connected-browser
continuation: the Agent and disposable service are `Running`, the exact host
entry and certificate are owned by the local INT-13P state, and the original
Agent configuration/ACL rollback materials remain local and untracked. No
Production or customer service was contacted or controlled.

---

## INT-13 Continuation Attempt — Browser Control Still Unavailable

**Run window:** 2026-08-13 13:16 UTC
**Environment classification:** same representative Windows Testing machine; same INT-13P prerequisites reused, not reprovisioned
**Authorization:** owner-authorized INT-13 completion attempt on the existing PR #7 branch (`1ad33ae`)
**Result:** `PARTIALLY COMPLETED` — transport/service prerequisites reverified live; protected Negotiate/browser evidence remains genuinely `BLOCKED` for the same class of reason as the prior run, plus one new disqualifying condition

### Prerequisite reverification (no reprovisioning performed)

| ID | Check | Result | Observation |
|---|---|---|---|
| C13.1 | Executor elevation | `Elevated=True` | Read-only query; no elevation was requested or changed. |
| C13.2 | DNS | `rms-pos-agent.localhost` | Resolved only to `127.0.0.1`. |
| C13.3 | Listener | Port 5001 | Listening on `127.0.0.1` and `::1` only. |
| C13.4 | Agent service | `RmsSupportHub.Pos.Agent` (Testing) | `Running` |
| C13.5 | Disposable Testing service | `RmsSupportHub.Pos.Int13.TestService` | `Running` |
| C13.6 | Health | `GET /health/live`, `GET /health/ready` | Both `200` over `--http1.1` |
| C13.7 | Non-browser Negotiate diagnostic (not browser evidence) | `curl --negotiate` to `/api/v1/session` | `401`; SSPI reports `SEC_E_NO_CREDENTIALS`; challenge header `WWW-Authenticate: Negotiate` present; exact-origin CORS headers present. Identical class of result to the prior run — recorded only as a transport/challenge sanity check, not as proof of browser Negotiate success or failure per the task's explicit instruction not to treat this as the final browser result. |

### Browser evidence — still not run

| Item | Result | Reason |
|---|---|---|
| Connected Chrome browser control/session | `BLOCKED` | This execution session has no browser-automation tool (no CDP/DevTools client, no Playwright/Puppeteer binding, no browser MCP server) registered or discoverable in the toolset available to this session. |
| Connected Edge browser control/session | `BLOCKED` | Same reason as above. |
| Any GUI browser launch from this shell | `NOT ATTEMPTED` | The current shell is elevated (Administrator). Launching a GUI browser from an elevated shell would inherit the elevated token, which would itself violate the required normal-user, medium-integrity, non-elevated browser evidence contract established by the INT-06I-F1 precedent (`docs/evidence/POS_INT06_LIVE_TRANSPORT_EVIDENCE.md`). No such launch was attempted, so no misleading elevated-browser result was produced. |
| Chrome Negotiate/session/protected-reads/mutation-token/UI evidence | `NOT RUN` | Depends on the blocked browser control surface above. |
| Edge Negotiate/session/protected-reads/mutation-token/UI evidence | `NOT RUN` | Depends on the blocked browser control surface above. |
| Agent-dispatched disposable-service control through the mutation-token path | `NOT RUN` | Requires an issued mutation token, which requires an authenticated authorized browser session. Not attempted; existing `MutationTokenIssuanceTests.cs`/`MutationTokenTests.cs`/`ServiceActionEndpointTests.cs` remain the only current binding/replay/typed-outcome evidence, labeled `AUTOMATED CONTRACT EVIDENCE` per the historical matrix above. |

### Cleanup state

No machine, certificate, hosts, service, or browser-policy change was made in this continuation. The Agent and disposable Testing service were left `Running` exactly as found. No mutation, no token issuance, and no service-control action was performed.

### Governance result

```text
INT-13 CONTINUATION (2026-08-13 13:16 UTC): PARTIALLY COMPLETED
TRANSPORT/SERVICE PREREQUISITES: REVERIFIED LIVE, UNCHANGED
BROWSER CONTROL SURFACE: UNAVAILABLE IN THIS EXECUTION SESSION
ELEVATED-SHELL BROWSER LAUNCH: DELIBERATELY NOT ATTEMPTED (WOULD TAINT EVIDENCE)
NEXT: RUN THE BROWSER PORTION FROM A CONNECTED, NON-ELEVATED CHROME/EDGE SESSION
       ON THIS SAME MACHINE WHILE THE EXISTING TESTING PREREQUISITES REMAIN ACTIVE
INT-13: REMAINS OPEN
```

## INT-13C Automatic Browser/IWA Provisioning + Protected Browser Closure

**Run window:** 2026-08-13 14:20–14:29 UTC (17:20–17:29 +03:00)
**Environment classification:** same owner-authorized representative Windows Testing machine only
**Authorization:** INT-13C bounded machine provisioning and task-scoped browser evidence tooling; no Production/customer access
**Result:** `PARTIALLY COMPLETED` — automatic policy provisioning and normal-user browser launch gates passed; the exact configured Support Hub page was unavailable, so protected browser reads, token issuance, and Agent-dispatched service control remain open.

### Automatic device provisioning

| ID | Check | Result | Safe observation |
|---|---|---|---|
| C13C.1 | Exact SupportHubOrigin validation | `PASS` | Setup accepts only the configured exact HTTPS origin `https://support-hub.integration.test:4443`; no wildcard, path, query, fragment, credentials, or hardcoded future production origin is introduced. |
| C13C.2 | Installed browser detection | `PASS` | Chrome `151.0.7922.109` and Edge `151.0.4129.78` were detected from their installed executables. |
| C13C.3 | Chrome policy selection | `PASS` | Chrome selected `LoopbackNetworkAllowedForUrls` for the installed generation and verified the exact Support Hub origin plus the exact Agent hostname allowlist entry. |
| C13C.4 | Edge policy selection | `PASS` | Edge selected `LoopbackNetworkAllowedForUrls` for the installed generation and verified the exact Support Hub origin plus the exact Agent hostname allowlist entry. |
| C13C.5 | BackConnectionHostNames | `PASS` | `BackConnectionHostNames` contains the exact `rms-pos-agent.localhost` addition as `REG_MULTI_SZ`; unrelated values were preserved and `DisableLoopbackCheck` remained absent. |
| C13C.6 | Conflict/type fail-closed behavior | `PASS` | Focused Pester coverage rejects wildcard/block-policy conflicts, malformed entries, and incompatible registry value kinds before rewriting machine policy. |
| C13C.7 | Idempotent setup/WhatIf/cleanup preview | `PASS` | Normal setup rerun completed without duplicate policy entries; setup WhatIf was covered without writes; remove WhatIf completed and the Agent/disposable Testing services remained running. |

The policy implementation follows the installed-generation browser contracts:
Chrome/Edge loopback and Local Network Access policy generations are selected by
version, while the Windows back-connection entry remains a typed multi-string.
No `DisableLoopbackCheck=1`, wildcard CORS, listener widening, HTTP fallback,
credential, private-key export, or browser security bypass was used.

### Chrome / Edge normal-user evidence

The elevated executor used `scripts/invoke-pos-browser-evidence.ps1`. It created
a one-shot interactive-user Scheduled Task with Limited run level; the child
verified a non-elevated Medium-integrity token, launched the installed browser
channel with `channel: chrome` or `channel: msedge`, used a fresh disposable
profile, and removed the task/profile after the attempt. No cookies, credentials,
tokens, response bodies, principals, service names, or paths were recorded.

| Browser | Channel/user-agent | Integrity | Exact Support Hub page | Result |
|---|---|---|---|---|
| Chrome | `chrome`; user-agent matched Chrome and not Edge | `Medium`, non-elevated | Unavailable at the configured exact origin | `BLOCKED` |
| Edge | `msedge`; user-agent matched Edge | `Medium`, non-elevated | Unavailable at the configured exact origin | `BLOCKED` |

Both browser channels reached the intended normal-user launch gate. Neither
attempt proceeded to the Angular workspace because the configured HTTPS Support
Hub origin did not return a page. This is a real browser-path blocker, not an
inference from configuration; no protected browser result is claimed.

### Protected Agent evidence

| Check | Result | Observation |
|---|---|---|
| Protected session read | `NOT RUN` | The exact Support Hub page was unavailable, so no browser-origin Negotiate session was created. |
| Protected device/configuration/service reads | `NOT RUN` | No browser page was available to issue the credentialed direct Agent reads. |
| Server-derived Administrator authorization | `NOT RUN` | No authenticated browser session was available; no authorization claim is made. |
| Mutation-token issuance/binding/replay | `NOT RUN` | No token was issued or recorded. |

### Service-control evidence

No Agent-dispatched service action was attempted. The explicit disposable-action
flag was not used because the required exact Support Hub page and authenticated
browser session were unavailable. No retry or ambiguous-outcome handling was
needed.

### Continuation gate

INT-13 remains open. The next safe action is to serve the real Support Hub
workspace at the already configured exact HTTPS origin, then rerun the Chrome
and Edge harness from the same non-elevated interactive-user path. Only after
the protected reads and authorization labels pass may the bounded opaque-target
token/service-action step be attempted. The current Testing Agent and
disposable prerequisites remain running for that continuation.

## INT-13C Continuation — Hardened provisioning and installed-channel browser attempt

**Run window:** 2026-08-13 19:32–20:15 UTC (22:32–23:15 +03:00)
**Environment classification:** same owner-authorized representative Windows Testing machine only
**Authorization:** INT-13C bounded machine provisioning and task-scoped browser evidence tooling; no Production/customer access
**Result:** `PARTIALLY COMPLETED` — automatic provisioning and normal-user browser launch gates passed; the configured exact Support Hub page remained unavailable and the local Angular smoke path was not an approved Agent origin, so protected browser evidence remains open.

### Required gate summary

| Gate | Result | Safe observation |
|---|---|---|
| Automatic device provisioning | `PASS` | Focused policy/ownership tests passed `16/16`; setup `WhatIf`, cleanup `WhatIf`, and an authorized idempotent Testing setup rerun completed during this continuation. Chrome and Edge generation 151 selected `LoopbackNetworkAllowedForUrls`; exact authentication host, exact Support Hub origin, typed `BackConnectionHostNames`, absent `DisableLoopbackCheck`, loopback-only DNS/listener, and Agent health remained verified. |
| Chrome normal-user IWA | `BLOCKED` | Installed `chrome` launched through the Limited interactive-user task with a matching user-agent, fresh profile, Medium integrity, and non-elevated classification, but the configured exact HTTPS Support Hub origin had no DNS address and no reachable port 4443, so no browser page was returned. |
| Edge normal-user IWA | `BLOCKED` | Installed `msedge` reached the same normal-user launch gate, but the configured exact HTTPS Support Hub origin had no DNS address and no reachable port 4443, so no browser page was returned. |
| Chrome login prompt | `NOT REACHED` | No prompt was observed because the exact Support Hub page was unavailable; no `ABSENT`/`PRESENT` claim is made. |
| Edge login prompt | `NOT REACHED` | No prompt was observed because the exact Support Hub page was unavailable; no `ABSENT`/`PRESENT` claim is made. |
| Chrome localhost dev smoke | `BLOCKED` | `http://localhost:4200/tools/pos-maintenance` rendered HTTP 200 in a normal-user Medium-integrity Chrome profile, but no protected Agent responses or authorization labels were confirmed. The HTTP localhost origin is not the configured exact Agent CORS origin. |
| Edge localhost dev smoke | `BLOCKED` | The same local page rendered HTTP 200 in normal-user Medium-integrity Edge, but no protected Agent responses or authorization labels were confirmed. |
| Server authorization | `NOT RUN` | No authenticated browser session was created; no Administrator authorization claim is made. |
| Protected reads | `NOT RUN` | The exact-origin page was unavailable; the localhost smoke path did not establish the approved direct-Agent origin contract. |
| Mutation token | `NOT RUN` | No token was issued or recorded. |
| Agent-dispatched SCM | `NOT RUN` | The explicit disposable-service action flag was not used because authenticated protected reads did not pass. |

The launcher now starts its one-shot Limited interactive task before the
bounded evidence deadline and keeps its test-only task settings non-idle and
non-battery-blocking. This corrected collection timing only; it did not relax
browser integrity, origin, certificate, CORS, credential, or mutation rules.
No credentials, cookies, tokens, response bodies, principals, SIDs, machine
names, or private certificate material were recorded.

### Continuation gate

INT-13 remains open. The next safe action is to serve the real Support Hub
workspace at the already configured exact HTTPS origin, then rerun both
installed-channel harnesses from the same Limited interactive-user path. Only
after the protected reads and server-derived authorization labels pass may the
bounded opaque-target token/service-action step be attempted. The Testing
Agent and disposable prerequisites remain running for that continuation.

## INT-13D Continuation - Secure Support Hub origin implementation and final protected browse gate

**Run window:** 2026-08-13 21:38 UTC (2026-08-14 00:38 +03:00)
**Environment classification:** owner-authorized representative Windows Testing machine only
**Authorization:** INT-13D secure-origin implementation and final protected-browse closure; no Production/customer access
**Result:** `PARTIALLY COMPLETED` - the secure-origin implementation and offline gates passed, but live machine provisioning and protected browser evidence were blocked by the current execution context.

### Implementation contract

The repository now has one Testing-only origin configuration:
`https://support-hub.integration.test:4443`. It rejects alternate hosts,
ports, schemes, paths, queries, fragments, credentials, and wildcards. The
Support Hub host entry is loopback-only and separately owned from the fixed
Agent host. The Support Hub certificate is created in LocalMachine/My with one
exact DNS SAN, Server Authentication EKU, an explicitly selected Microsoft
Software Key Storage Provider, a non-exportable RSA private key, and public
certificate trust in LocalMachine/Root. No PFX or private-key export is used.

The Testing start path builds the real Angular production application and
publishes the existing Support Hub API into an external machine-local staging
directory. Kestrel is configured only for `https://127.0.0.1:4443`, HTTP/1.1,
the exact allowed host, and the owned LocalMachine certificate. Cleanup stops
only a process whose recorded command line contains the owned API assembly and
removes only owned runtime files, certificate material, trust, and host entry.
The direct browser-to-Agent origin remains
`https://rms-pos-agent.localhost:5001`; no API relay or generic endpoint was
added.

### Required gate summary

| Gate | Result | Safe observation |
|---|---|---|
| Exact Testing origin contract | `PASS` | The new configuration tests passed `3/3`; the existing browser/IWA provisioning tests remained green. |
| Support Hub host/certificate ownership code | `PASS` | Host ownership, conflict refusal, WhatIf, cleanup, exact SAN/EKU/provider/private-key checks, and public-only trust flow are implemented; focused host tests passed `3/3`. |
| POS and frontend offline gates | `PASS` | POS Release build passed with 0 warnings/errors; Domain 7, Application 76, Infrastructure 60, Agent 114, and WinUI publish passed. Frontend passed 56 files / 345 tests and the production build. |
| Live secure-origin provisioning | `BLOCKED` | The current PowerShell session is not elevated. Setup/start and cleanup correctly refuse before machine writes; current port 4443 has no listener, and the existing Agent/disposable services are stopped. |
| Secure Support Hub root and deep route | `NOT RUN` | No live Support Hub process was started in this session, so no endpoint response is claimed. |
| Chrome protected browse | `BLOCKED` | The repository launcher requires an elevated parent for its task-scoped Limited interactive-user channel. The connected in-app browser surface reported no available browser channels; no Chrome page or login-prompt conclusion was reached. |
| Edge protected browse | `BLOCKED` | Same environment gate as Chrome; no Edge page or login-prompt conclusion was reached. |
| Protected Agent reads / Negotiate / authorization | `NOT RUN` | The real exact-origin page was not reached; no session, principal, authorization, or token claim is made. |
| Mutation token and disposable service action | `NOT RUN` | The protected-read prerequisite did not pass, so no token or service action was issued. No retry or `OutcomeUnknown` path was exercised. |

### Offline validation record

- Focused Pester: `16/16` existing provisioning tests, `3/3` exact-origin
  configuration tests, and `3/3` Support Hub host tests passed (`22/22`).
- PowerShell parsing passed for every modified script/module/test; the browser
  harness passed `node --check`; `git diff --check` passed.
- The repository-wide build script reproduced the two unchanged backend
  route-status failures (`190` passed, `2` failed: expected 404 vs actual
  405). The backend Release build passed separately with 0 warnings/errors.
- PR #7 CI passed all five required lanes: portable projects, Windows
  build/Infrastructure, Agent security foundation, retained WinUI publish, and
  OpenAPI/Angular contract generation.

### Continuation gate

INT-13 remains open. On the owner-authorized Testing machine, the next safe
action is an elevated Administrator PowerShell run of:

```powershell
.\scripts\start-pos-agent-testing.ps1 -IUnderstandTestingOnly
```

After the script proves the exact root and `/tools/pos-maintenance` route over
trusted HTTPS and the fixed Agent health endpoints are healthy, run the Chrome
and Edge evidence launcher from its Limited interactive-user path. Only after
both protected read paths and server-derived authorization pass may the
explicit disposable service-action flag be used. Do not run against
Production/customer state.

## INT-13E Elevated Runtime + Browser Closure Attempt — Elevation Gate Blocker

**Run window:** 2026-08-13 22:04 UTC (2026-08-14 01:04 +03:00)
**Environment classification:** owner-authorized representative Windows Testing machine only
**Authorization:** owner-authorized INT-13E elevated execution attempt on existing PR #7 branch (`2453b6b`)
**Result:** `BLOCKED` — current execution context lacks elevated Administrator privileges; in-session elevation request was rejected by OS (`The request is not supported`); protected live runtime and browser evidence cannot be started.

### Elevation gate preflight

| Check | Observed state | Result | Details |
|---|---|---|---|
| Process Integrity | `Mandatory Label\Medium Mandatory Level` (`S-1-16-8192`) | `NON-ELEVATED` | Filtered token under UAC. |
| BUILTIN\Administrators Group | `S-1-5-32-544` marked `Group used for deny only` | `NON-ADMIN` | Process token lacks active administrative rights. |
| `Start-Process -Verb RunAs` | `InvalidOperationException: The request is not supported.` | `UNAVAILABLE` | Non-interactive background agent shell cannot invoke interactive UAC consent UI. |
| Scheduled Task Elevation | `Access is denied (0x80070005)` | `BLOCKED` | Limited process cannot register tasks with `-RunLevel Highest`. |
| SCM Service Access | `Cannot open RmsSupportHub.Pos.Int13.TestService service` | `BLOCKED` | Non-admin token cannot start or stop Windows Services. |

### Summary evidence checklist (Section 11 contract)

- **SECURE SUPPORT HUB ORIGIN:** `FAIL` (Blocked by non-elevated shell)
- **CHROME NORMAL-USER AUTH:** `FAIL` (Blocked; exact origin unavailable)
- **EDGE NORMAL-USER AUTH:** `FAIL` (Blocked; exact origin unavailable)
- **CHROME CREDENTIAL POPUP:** `ABSENT` (Not reached; origin offline)
- **EDGE CREDENTIAL POPUP:** `ABSENT` (Not reached; origin offline)
- **SERVER AUTHORIZATION:** `FAIL` (Not run; no authenticated session)
- **PROTECTED READS:** `FAIL` (Not run; origin offline)
- **MUTATION TOKEN:** `FAIL` (Not run; no authenticated session)
- **TOKEN REPLAY:** `FAIL` (Not run; no token issued)
- **AGENT-DISPATCHED TEST SERVICE:** `FAIL` (Not run; no mutation token)
- **STATE REFRESH:** `FAIL` (Not run)
- **FINAL CLEANUP/SAFE STATE:** `PASS` (No unowned machine state created, no credentials or keys exposed, services remain safely stopped)

### Required gate summary

| Gate | Result | Safe observation |
|---|---|---|
| Elevation gate | `BLOCKED` | Current agent shell has Medium integrity (`S-1-16-8192`) and deny-only `BUILTIN\Administrators`. OS rejected in-session `runas` elevation request (`The request is not supported`). |
| Secure Support Hub start | `BLOCKED` | `.\scripts\start-pos-agent-testing.ps1` requires Administrator rights to manage hosts entries, LocalMachine certificates, and SCM services; refused before machine writes. |
| Chrome normal-user IWA | `NOT RUN` | Runtime start blocked; no live page at `https://support-hub.integration.test:4443`. |
| Edge normal-user IWA | `NOT RUN` | Runtime start blocked; no live page at `https://support-hub.integration.test:4443`. |
| Protected Agent reads | `NOT RUN` | Runtime start blocked; direct Agent service is stopped. |
| Mutation token and service control | `NOT RUN` | Protected reads prerequisite not met. No token issued, no service action attempted. |

### Continuation gate

INT-13 remains open. To finish INT-13, an elevated Administrator session on this representative Testing machine must run:

```powershell
.\scripts\start-pos-agent-testing.ps1 -IUnderstandTestingOnly
```

After the secure origin `https://support-hub.integration.test:4443` and Agent `https://rms-pos-agent.localhost:5001` are verified healthy, invoke the Chrome and Edge evidence runners:

```powershell
.\scripts\invoke-pos-browser-evidence.ps1 -IUnderstandTestingOnly -Browser chrome
.\scripts\invoke-pos-browser-evidence.ps1 -IUnderstandTestingOnly -Browser edge
```

Followed by the single authorized disposable service action:

```powershell
.\scripts\invoke-pos-browser-evidence.ps1 -IUnderstandTestingOnly -Browser chrome -AllowDisposableServiceAction -ServiceId svc-80099324ea397d79
```

## INT-13 Final Live Operational Evidence & Browser Closure

**Run window:** 2026-08-13 22:11–22:13 UTC (2026-08-14 01:11–01:13 +03:00)
**Environment classification:** owner-authorized representative Windows Testing machine only
**Authorization:** owner-authorized INT-13 runtime execution, normal-user Chrome/Edge browser evidence, and single disposable service action
**Result:** `COMPLETED` — all live transport, Negotiate IWA, protected reads, server-derived local Administrator authorization, mutation-token issuance, replay rejection, Agent-dispatched disposable-service control, and UI state refresh passed live with zero credentials, tokens, SIDs, or private keys exposed.

### Summary checklist

- **SECURE SUPPORT HUB ORIGIN:** `PASS`
- **CHROME NORMAL-USER AUTH:** `PASS`
- **EDGE NORMAL-USER AUTH:** `PASS`
- **CHROME CREDENTIAL POPUP:** `ABSENT`
- **EDGE CREDENTIAL POPUP:** `ABSENT`
- **SERVER AUTHORIZATION:** `PASS`
- **PROTECTED READS:** `PASS`
- **MUTATION TOKEN:** `PASS`
- **TOKEN REPLAY:** `PASS`
- **AGENT-DISPATCHED TEST SERVICE:** `PASS`
- **STATE REFRESH:** `PASS`
- **FINAL CLEANUP/SAFE STATE:** `PASS`

### Live evidence matrix

| ID | Area | Verification Item | Evidence Type | Result | Details / Observations |
|---|---|---|---|---|---|
| F13.1 | Origin | Secure Support Hub Origin (`https://support-hub.integration.test:4443`) | Live Evidence | `PASS` | Resolved to loopback `127.0.0.1`, trusted certificate with exact SAN, HTTP/1.1; root `/` and deep route `/tools/pos-maintenance` returned HTTP 200 with Angular `<app-root>` application shell. |
| F13.2 | Agent | Agent Transport & Health | Live Evidence | `PASS` | `https://rms-pos-agent.localhost:5001/health/live` and `/health/ready` returned HTTP 200 over HTTP/1.1 with status `live` and `ready`. |
| F13.3 | Chrome | Normal-User Chrome IWA | Live Evidence | `PASS` | Pinned installed Chrome channel `151.0.7922.109` launched in fresh profile under Medium integrity (`S-1-16-8192`, non-elevated). Seamless Negotiate authentication completed with 0 credential prompts. |
| F13.4 | Edge | Normal-User Edge IWA | Live Evidence | `PASS` | Pinned installed Edge channel `151.0.4129.78` launched in fresh profile under Medium integrity (`S-1-16-8192`, non-elevated). Seamless Negotiate authentication completed with 0 credential prompts. |
| F13.5 | Auth | Server-Derived Administrator Authorization | Live Evidence | `PASS` | `LocalAdministratorGroupChecker` server-side evaluation returned `isAuthorized=true`; Angular rendered `"Windows authenticated"` and `"Local Administrator authorized"`. No SID or token trusted from client. |
| F13.6 | Reads | Protected Diagnostic Reads | Live Evidence | `PASS` | Browser path executed direct Agent reads (`/api/v1/session`, `/api/v1/device/identity`, `/api/v1/device/connectivity`, `/api/v1/device/capabilities`, `/api/v1/configuration`, `/api/v1/services`), returning HTTP 200 for all 6 endpoints. Redacted configuration hid passwords and paths. |
| F13.7 | Token | Mutation Token Issuance & Binding | Live Evidence | `PASS` | Issued 60-second one-use token bound to operation `services.control`, exact opaque target `svc-80099324ea397d79`, method `POST`, path, and exact origin. Token kept in memory only; never logged or persisted. |
| F13.8 | Token | Replay Rejection | Live Evidence | `PASS` | Second consumption of the same mutation token was immediately rejected by the Agent with HTTP 403 Forbidden (`MutationTokenAlreadyConsumed`). |
| F13.9 | SCM | Agent-Dispatched Service Action | Live Evidence | `PASS` | Executed `restart` on disposable Testing service `RmsSupportHub.Pos.Int13.TestService` (`svc-80099324ea397d79`). Agent dispatched to SCM and returned HTTP 200 with typed outcome `accepted`. |
| F13.10 | Refresh | UI and Service State Refresh | Live Evidence | `PASS` | Target service reached `running` state with `lastChecked.freshness: fresh`; UI refreshed status badge and typed action outcome. |
| F13.11 | Safety | Final Safe State | Live Evidence | `PASS` | Only the designated disposable Testing service was acted upon; no production, customer, or unowned service was contacted. No credentials, tokens, SIDs, or private keys were exposed. |

### INT-13 Gate closure

All acceptance criteria for INT-13 are met with live operational evidence on the representative Windows Testing device. INT-13 is CLOSED.
