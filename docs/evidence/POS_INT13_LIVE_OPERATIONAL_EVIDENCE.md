# POS INT-13 Live Operational Evidence

**Execution Date:** 2026-08-13  
**Gate Status:** `INT-13 OPEN (BLOCKED ON REPRESENTATIVE-DEVICE LIVE PREREQUISITES)`  
**Overall Result:** `BLOCKED`  
**Repository Baseline:** `369ed345cde177cba5bdccedb9330538a55c2e08`  

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

## Machine Environment Preflight

| Environment Component | Observed State | Classification | Result | Notes |
|---|---|---|---|---|
| OS Version | Windows 11 Pro 10.0.26200 | Environment | Query Passed | x64 Architecture |
| Host Resolution (`rms-pos-agent.localhost`) | `No such host is known` | Live Evidence | `BLOCKED` | Canonical loopback host entry absent |
| TLS Certificate (`rms-pos-agent`) | `ABSENT` | Live Evidence | `BLOCKED` | Not present in LocalMachine/CurrentUser store |
| HTTPS Port 5001 Listener | `NOT LISTENING` | Live Evidence | `BLOCKED` | POS Agent service process not running |
| Disposable Testing Service | `ABSENT` | Live Evidence | `BLOCKED` | Hard safety gate prevented live SCM mutation |
| Machine Security Policy | Unmodified | Safety Gate | Enforced | No hosts, cert, registry, or SPN mutations made |

---

## Evidence Matrix

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

## Blockers & Next Safe Actions

1. **Current Blocker:**  
   The representative Windows Testing environment lacks the canonical DNS mapping (`rms-pos-agent.localhost`), trusted machine TLS certificate, running `RmsSupportHub.Pos.Agent` background service, and an approved disposable Testing Windows Service.

2. **Next Safe Action:**  
   To complete INT-13 live operational validation in a future environment session:
   - Provision `rms-pos-agent.localhost` loopback DNS / hosts resolution.
   - Provision and trust the testing TLS certificate in `Cert:\LocalMachine\My`.
   - Install and start `RmsSupportHub.Pos.Agent` as a Windows Service listening on `https://rms-pos-agent.localhost:5001`.
   - Register an approved disposable Testing Windows Service (e.g., `RmsPosTestDummyService`) for live SCM mutation testing.
