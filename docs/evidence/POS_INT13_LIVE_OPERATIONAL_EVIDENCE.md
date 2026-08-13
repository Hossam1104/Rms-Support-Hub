# POS INT-13 Live Operational Evidence

**Latest Execution Date:** 2026-08-13
**Gate Status:** `INT-13 OPEN (PARTIALLY COMPLETED; PROTECTED LIVE PATH BLOCKED)`
**Latest Result:** `PARTIALLY COMPLETED`
**Latest Repository Baseline:** `b7a11fb409641bd7c9b7fbdf8abfefddddbda98d`

---

## Executive Summary

Owner authorization was granted to execute INT-13 verification on this machine.
In accordance with repository governance and strict safety guidelines:
1. Live operational tests on `https://rms-pos-agent.localhost:5001` were evaluated.
2. Canonical DNS resolution (`rms-pos-agent.localhost`), machine TLS certificate, loopback port 5001 listener, and disposable Testing Windows Service are **ABSENT** on this machine.
3. System configuration (hosts file, certificate stores, firewall, SPNs, registry, browser policies) was **NOT modified**, preserving machine integrity.
4. Per Section 7 Safety Gate and Section 14 Completion Rule, real SCM mutation was **NOT RUN / BLOCKED** because no approved disposable Testing service exists on the machine.
5. All automated contract tests (Domain, Application, Infrastructure, Agent Integration, Frontend, OpenAPI Generation) passed 100%.

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
| E2.2 | CORS | Reject Unapproved / Alternate Origins | Automated Contract | `PASS` | `CorsTests.cs` (in IntegrationTests) verifies exact origin matching (`https://support-hub.integration.test`), rejecting non-matching origins with 403 Forbidden. No wildcard origins. |

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
