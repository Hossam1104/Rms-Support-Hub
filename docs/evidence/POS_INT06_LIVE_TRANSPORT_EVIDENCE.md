# POS INT-06 Live Transport Security Evidence

**Result:** `BLOCKED / FAILED — INT-06F elevation unavailable`

**Execution date:** 2026-08-12

**Scope:** INT-06 and the authorized INT-06F continuation. No POS feature operation, POS Maintenance UI
activation, Support Hub API relay, runtime Agent remediation, SQL, SCM, SMB,
backup, restore, maintenance, downloader, or artifact request was executed.

## Decision

The owner-authorized INT-06 run stopped before machine mutation because the
executor was not elevated and the single controlled elevation attempt could
not complete in this environment. The mandatory LocalMachine certificate,
machine trust, LocalSystem service, temporary hosts mapping, and live browser
transport chain were therefore not available for evidence collection.

This is a truthful evidence block, not simulated evidence. Runtime
remediation was not executed. The prior blocked session identified planner review and an authorized INT-06F
continuation as the next action. INT-06F was attempted here and remains blocked.
INT-07 remains unauthorized and was not executed.

## INT-06F continuation

INT-06:
BLOCKED - NO ELEVATION

INT-06F:
ELEVATED CONTINUATION - BLOCKED

The one controlled UAC attempt authorized by INT-06F returned exit code 1 and
produced no elevated child/result. The current shell remained non-elevated.
No certificate, trust entry, service, hosts mapping, browser policy, browser
profile, or other machine-security change was made.

No live transport or browser evidence was collected. Runtime source remained
unchanged. INT-07 was not executed.

## Repository

| Item | Evidence |
|---|---|
| Starting Support Hub SHA | `f04f88d8e41ce54f89e7f7a820eaeedf58636129` |
| Final Support Hub SHA | Same as starting SHA; no commit was created because INT-06F did not pass |
| PR | `N/A` — no commit or push was authorized after the blocked run |
| POS provenance | Corrected Agent baseline `010abc52dc110cfde3dc2c53e057890ff6edaf97` |
| POS source repository | Not modified; no source repository operation was performed |
| Runtime source changes | None |
| Test-only origin | `https://support-hub.integration.test:4443` was not installed or served because elevation blocked setup |

## Machine preflight

| Item | Result | Notes |
|---|---|---|
| Windows version | `Windows 11 Pro 10.0.26200`, build `26200`, x64 | Read-only OS query |
| Executor elevation | `BLOCKED` | Current process was not elevated; the single controlled UAC attempt returned no elevated child/result (exit code 1) |
| Existing Agent service | `ABSENT` | The canonical service name was absent before and after the attempt |
| Existing port 5001 listener | `ABSENT` | No process was listening before or after the attempt |
| Existing INT-06 certificates | `ABSENT` | No matching evidence certificate was present before or after the attempt |
| Existing test-origin hosts entry | `ABSENT` | No hosts-file change was made |
| Browser policy keys | `ABSENT` | Relevant Chrome and Edge machine/user policy roots were absent at preflight |
| BackConnectionHostNames | `ABSENT` | No exact entry was added |
| DisableLoopbackCheck | `ABSENT` | No bypass value was added |
| Canonical resolver support | `INFERRED` | Windows `Resolve-DnsName` returned `127.0.0.1`; no live TLS listener was present |
| IPv6 capability | `INFERRED` | Active IPv6 interfaces were detected; no live Agent IPv6 listener was present |
| IPv4 port probe | `INFERRED` | `127.0.0.1:5001` was closed because the Agent was not running |
| IPv6 port probe | `INFERRED` | `[::1]:5001` was closed because the Agent was not running |
| LAN port probes | `INFERRED` | Two active non-loopback IPv4 addresses were probed; none accepted port 5001, but no live Agent was running |

The read-only SPN query for `HTTP/rms-pos-agent.localhost` was unavailable in
the executor. The Kerberos cache contained no matching HTTP service ticket,
but no live Agent authentication request occurred, so this is not mechanism
evidence.

## Required live evidence matrix

`BLOCKED` means the environment prevented a direct live test. `INFERRED`
means supporting source or preflight evidence only and is not a pass.

| ID | Area | Test | Result | Evidence type | Notes |
|---|---|---|---|---|---|
| E1 | Host | Canonical hostname and origin behavior | `INFERRED` | Supporting | Windows resolver returned the canonical host to generic IPv4 loopback; no Agent response was available |
| E2 | Binding | IPv4 loopback listener | `BLOCKED` | Not tested | Requires the live Agent |
| E3 | Binding | IPv6 loopback listener | `BLOCKED` | Not tested | IPv6 was available, but the live Agent could not be started |
| E4 | Binding | No LAN listener | `BLOCKED` | Not tested | Preflight found no port 5001 listener; this cannot prove the intended live binding |
| E5 | Certificate | LocalMachine certificate selection | `BLOCKED` | Not tested | LocalMachine access/evidence certificate provisioning required elevation |
| E6 | Certificate | Machine trust and served certificate | `BLOCKED` | Not tested | No certificate or Agent listener was provisioned |
| E7 | Certificate | Invalid thumbprint fail-closed startup | `BLOCKED` | Not tested | Temporary service could not be created |
| E8 | Certificate | A-to-B rollover mechanics | `BLOCKED` | Not tested | Two dedicated certificates could not be provisioned |
| E9 | Protocol | HTTPS-only and HTTP/1.1 | `BLOCKED` | Not tested | No live TLS endpoint was available; no validation bypass was used |
| E10 | Host | Canonical Host acceptance and wrong Host rejection | `BLOCKED` | Not tested | Requires a valid TLS connection to the live endpoint |
| E11 | CORS | Exact configured origin | `BLOCKED` | Not tested | No live Agent configured with the test origin |
| E12 | CORS | Anonymous valid preflight | `BLOCKED` | Not tested | No live Agent endpoint |
| E13 | CORS | Wrong-origin preflight and application Origin gate | `BLOCKED` | Not tested | No live Agent endpoint |
| E14 | Authentication | Windows Negotiate | `BLOCKED` | Not tested | No live service/browser authentication flow |
| E15 | Authentication | Server-side Windows SID resolution | `BLOCKED` | Not tested | No authenticated live session |
| E16 | Authorization | Local Administrators behavior | `BLOCKED` | Not tested | No live session or mutation-token request |
| E17 | Authentication | Kerberos/NTLM classification | `BLOCKED` | Not tested | SPN query unavailable and no live Negotiate exchange occurred |
| E18 | Loopback auth | BackConnectionHostNames behavior | `BLOCKED` | Not tested | Registry entry absent, but no live auth attempt proves neither requirement nor non-requirement |
| E19 | Browser | Chrome LNA default/permission behavior | `BLOCKED` | Not tested | No evidence page or live Agent was available |
| E20 | Browser | Edge LNA default/permission behavior | `BLOCKED` | Not tested | No evidence page or live Agent was available |
| E21 | Browser policy | Exact-origin managed allow/block behavior | `BLOCKED` | Not tested | Pre-existing policy roots were absent; no temporary policy was applied |
| E22 | Architecture | Direct browser-to-Agent path | `BLOCKED` | Not tested | No browser request reached the Agent; no backend relay was created |
| E23 | Browser auth | Browser Windows credentials and authenticated session | `BLOCKED` | Not tested | No browser session was run |
| E24 | Mutation foundation | Unknown-operation result | `BLOCKED` | Not tested | No request was sent; no feature operation was registered |
| E25 | Route surface | Production live route recheck | `INFERRED` | Source/tests | Destination source and Agent integration tests define only the four foundation route categories; no Production Agent was launched |
| E26 | Relay boundary | Support Hub API relay absent | `INFERRED` | Static | Repository scan found no Agent reference in `backend/**`; no relay was added |
| E27 | Security bypass | TLS/LNA/loopback bypass absent | `PASS` | Direct | No certificate bypass, insecure flag, wildcard policy, LAN widening, or `DisableLoopbackCheck` value was used |

## Browser vendor evidence

Installed stable browser versions were read from the installed binaries:

```text
CHROME VERSION: 151.0.7922.77
EDGE VERSION: 151.0.4129.59
```

The installed generations are within the current first-party policy range
that supports the exact-origin policy families required for this test. The
run did not apply or exercise them because the Agent could not be provisioned.

- [Chrome LocalNetworkAccessAllowedForUrls](https://chromeenterprise.google/policies/local-network-access-allowed-for-urls/) — supported from Chrome 139.
- [Chrome LocalNetworkAllowedForUrls](https://chromeenterprise.google/policies/local-network-allowed-for-urls/) — supported from Chrome 146; exact local-network precedence is documented there.
- [Chrome LoopbackNetworkAllowedForUrls](https://chromeenterprise.google/policies/loopback-network-allowed-for-urls/) — supported from Chrome 146.
- [Chrome AuthServerAllowlist](https://chromeenterprise.google/policies/auth-server-allowlist/) — integrated authentication allowlisting.
- [Edge LocalNetworkAccessAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkaccessallowedforurls) — supported from Edge 140.
- [Edge LocalNetworkAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkallowedforurls) — supported from Edge 146.
- [Edge LoopbackNetworkAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/loopbacknetworkallowedforurls) — supported from Edge 146.
- [Edge Local Network Access testing guidance](https://learn.microsoft.com/en-us/deployedge/ms-edge-local-network-access) — documents the narrow `--ip-address-space-overrides` testing mechanism; it was not used because no browser evidence page was run.

## Regression validation

All required pre-change repository checks passed:

| Check | Result |
|---|---|
| `dotnet restore pos/RmsSupportHub.Pos.slnx --nologo` | Passed |
| POS Release build with `--warnaserror` | Passed; 0 warnings, 0 errors |
| Domain tests | Passed; 7/7, 0 skipped |
| Application tests | Passed; 76/76, 0 skipped |
| Infrastructure tests | Passed; 60/60, 0 skipped |
| Agent integration tests | Passed; 69/69, 0 skipped |
| Static backend relay scan | Passed; no Agent references in `backend/**` |
| Static security scan | Passed; no newly introduced disallowed bypass pattern in scanned runtime/test paths |

No post-evidence runtime regression was needed because no runtime source file,
browser policy, certificate, service, or hosts entry was changed.

## Cleanup

| Item | Result |
|---|---|
| Temporary Agent service | Not created; no service cleanup required |
| Evidence certificates/trust | Not created; no certificate cleanup required |
| Hosts file | Unchanged; no restoration required |
| Browser policies | Unchanged; no restoration required |
| Browser profiles | Not created |
| Temporary evidence workspace | Removed after the report was prepared |
| Private key/PFX/auth trace committed | No; none was created or retained in Git |

## Governance

```text
INT-06:
BLOCKED / FAILED

INT-06F:
BLOCKED / FAILED

BLOCKER:
Single controlled UAC attempt returned no elevated child/result; mandatory
LocalMachine certificate, machine trust, LocalSystem service, and live
browser/Agent evidence remain unproven.

RUNTIME REMEDIATION:
NOT EXECUTED

BACKCONNECTIONHOSTNAMES:
BLOCKED - could not safely determine

KERBEROS / NTLM:
NEGOTIATE NOT EXECUTED

DISABLELOOPBACKCHECK:
NOT USED / NOT INTRODUCED

INT-13:
REPRESENTATIVE-DEVICE VALIDATION STILL OPEN

NEXT:
PLANNER REVIEW
```

`TASK.md` and `.ai/STATE.md` record the blocked gate. The exact task-related
Git diff is limited to this sanitized evidence document, the durable blocked
state, and the active handoff. INT-07 was not staged as executable.

**INT-07 WAS NOT EXECUTED.**

## INT-06I - UAC-SAFE ADMINISTRATOR AUTHORIZATION REMEDIATION + SCALAR DOCUMENTATION

### Scope and cause

INT-06H isolated an authorization mismatch rather than a transport failure:
the normal Chrome/Edge Negotiate session authenticated, but the session
reported `isAuthorized=false` and mutation-token issuance returned 403, while
the equivalent elevated Windows request reached the safe
`operation_not_supported` result. The prior implementation used the Windows
access token's `IsInRole` elevation-sensitive view as the Administrator
membership check.

INT-06I was limited to that Agent authorization seam and the permanent
documentation gate. No POS feature operation, POS UI activation, Support Hub
backend relay, persistence/schema change, deployment, or INT-07 work was
performed.

### Authorization implementation

The production Agent now:

- starts from the authenticated `WindowsIdentity.User` SID;
- resolves the account name through the operating system;
- calls `NetUserGetLocalGroups` with `LG_INCLUDE_INDIRECT` so direct and
  indirect local-group membership are considered;
- resolves returned localized group names back to SIDs and compares them with
  `WellKnownSidType.BuiltinAdministratorsSid`; and
- fails closed for account, local-group, SID, native-API, or unexpected
  resolver failures, logging only a categorical message with the request
  correlation identifier and no raw identity/group data.

Session `isAuthorized` and the `LocalAdministratorsOnly` mutation-token policy
share this server-derived membership decision. Windows SID ownership for
mutation tokens is also identity-only in production; synthetic SID claims are
accepted only by the dedicated `IntegrationTest` authentication substitute.

### Scalar/OpenAPI implementation

`Scalar.AspNetCore` is pinned to exact stable version `2.16.18`. Runtime
OpenAPI and Scalar are mapped only in `Development` and `IntegrationTest`:
`/openapi/{documentName}.json` is the source used by Scalar and `/scalar`
redirects to the local `/scalar/` reference. Scalar's AI Agent and default
external font loading are disabled. The four current operations have stable
operation IDs, tags, summaries, descriptions, security metadata, semantic
responses, and problem-details media. All reachable contract schemas and
properties have descriptions, and the generated OpenAPI JSON and Support Hub
TypeScript client were regenerated.

### Automated validation

| Check | Result |
|---|---|
| POS Release build (`--warnaserror`) | PASS; 0 warnings, 0 errors |
| POS tests | PASS; Domain 7/7, Application 76/76, Infrastructure 60/60, Agent 85/85; 0 skipped |
| UAC-safe resolver tests | PASS; direct, indirect, non-Administrator, identity/SID, lookup/API failure, native smoke, and shared-checker coverage included in Agent 85/85 |
| OpenAPI/Scalar contract tests | PASS; operation/schema completeness, IntegrationTest Scalar reachability, and Production route inventory |
| OpenAPI/client generation | PASS; two-pass file hashes stable |
| Frontend tests | PASS; 56 files / 341 tests |
| Agent package audit | PASS; no vulnerable packages reported by the configured NuGet sources |
| Retained WinUI publish | PASS; executable plus 23 `.pri` and 55 `.xbf` resources |

The repository-wide `scripts/build.ps1` was also attempted. Its unrelated
Support Hub backend test lane remains red on two pre-existing route-status
assertions (`NotFound` expected, `MethodNotAllowed` returned); no backend files
were changed by INT-06I. The POS validation above is independent and green.

### Post-remediation browser gate

The browser-control runtime reported no connected browser backends:
`browsers.list()` returned an empty list, and the requested Chrome and Edge
bindings were unavailable. Consequently no post-remediation interactive
Chrome/Edge session, LNA result, session response, unknown-operation request,
or browser-visible Scalar page is claimed here. The installed-browser versions
and successful pre-remediation transport evidence remain the INT-06H baseline;
the required post-remediation normal-browser check is still open for an
authorized connected harness. No browser policy, host entry, certificate,
profile, elevation, or bypass workaround was created during this blocked
attempt.

The automated Production endpoint inventory contains neither `/scalar` nor
`/openapi/`; because TestServer cannot execute the production Negotiate
handler, this record does not claim a Production Kestrel HTTP 404 probe. That
live route check remains part of the authorized post-remediation evidence.

### Governance result

Every future POS Agent HTTP operation must carry complete OpenAPI metadata and
be fully described in Scalar before its integration gate closes. The focused
INT-06I PR is not merged and requires independent security review.

**INT-07 WAS NOT EXECUTED.**

## INT-06H - REAL BROWSER RUNTIME EVIDENCE

**Result:** `BLOCKED / FAILED - browser Administrator authorization mismatch`

**Execution date:** 2026-08-12

INT-06H completed the browser-only continuation authorized by the owner. It did
not repeat the INT-06G machine matrix, did not alter Agent or Support Hub
runtime source, and did not register or execute a POS operation. The existing
six-file INT-06G checkpoint was preserved before testing.

### Browser and evidence source

| Item | Result |
|---|---|
| Actual stable Google Chrome | `151.0.7922.77` |
| Actual stable Microsoft Edge | `151.0.4129.78` |
| Browser profiles | Fresh disposable profiles; owner profiles were not used |
| Browser execution identity | Normal interactive Windows user with a limited browser token |
| Evidence page | `https://support-hub.integration.test:4443` |
| Public-source classification | Temporary HTTPS page response included CSP `treat-as-public-address` |
| Direct Agent target | `https://rms-pos-agent.localhost:5001` |
| Backend relay | `ABSENT`; browser network targets were the Agent origin directly |
| Operation registry | Empty; no fake or real feature operation was added |

The evidence page self-recorded sanitized status, error, and response-shape
results for `/health/live`, `/health/ready`, `/api/v1/session`, and
`/api/v1/security/mutation-token`. It did not retain principal names, SIDs,
cookies, credentials, authorization headers, or Negotiate material. The
public-source mechanism was selected from the first-party Local Network Access
guidance and the page was served over HTTPS from the exact requested origin.

The installed browser versions support the exact policy families used here.
The current vendor references checked at execution time were [Chrome
LoopbackNetworkAllowedForUrls](https://chromeenterprise.google/policies/loopback-network-allowed-for-urls/),
[Chrome LoopbackNetworkBlockedForUrls](https://chromeenterprise.google/policies/loopback-network-blocked-for-urls/),
[Chrome AuthServerAllowlist](https://chromeenterprise.google/policies/auth-server-allowlist/),
[Edge LoopbackNetworkAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/LoopbackNetworkAllowedForUrls),
[Edge LoopbackNetworkBlockedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/LoopbackNetworkBlockedForUrls),
and [Edge AuthServerAllowlist](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/AuthServerAllowlist).

### LNA allow/block matrix

The only requesting origin used in the matrix was
`https://support-hub.integration.test:4443`. The Agent itself was never
classified as public. No wildcard, global LNA opt-out, all-localhost rule, or
all-HTTPS-origin rule was used.

| Browser | Exact allow result | Exact block result |
|---|---|---|
| Chrome 151.0.7922.77 | `PROVEN`: direct `/health/live` and `/health/ready` returned 200; direct session/mutation requests reached the Agent after the exact auth prerequisite | `PROVEN`: direct requests failed; the mutation preflight recorded `ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS` and no Agent response was reached |
| Edge 151.0.4129.78 | `PROVEN`: direct `/health/live` and `/health/ready` returned 200; direct session/mutation requests reached the Agent after the exact auth prerequisite | `PROVEN`: direct requests failed; the mutation preflight recorded `ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS` and no Agent response was reached |

The allow runs first tested browser authentication without
`AuthServerAllowlist`. Chrome and Edge did not complete the browser session
request in that state. The exact value `rms-pos-agent.localhost` was then
configured temporarily for each browser, with no wildcard, and both browsers
completed the Negotiate session. This exact policy is therefore a tested
deployment prerequisite on this device; it was removed during cleanup.

### Browser Negotiate and session

| Browser | Authenticated session | Sanitized session result |
|---|---|---|
| Chrome | `PASS` after exact `AuthServerAllowlist` and exact loopback back-connection prerequisite | HTTP 200; `isAuthorized=false`; no `sid` property; API version metadata present |
| Edge | `PASS` after exact `AuthServerAllowlist` and exact loopback back-connection prerequisite | HTTP 200; `isAuthorized=false`; no `sid` property; API version metadata present |

Both browser runs reached the canonical Agent origin directly. The response
DTO did not expose a Windows SID. The exact temporary `BackConnectionHostNames`
entry required by the prior INT-06G evidence was recreated only for the
canonical Agent hostname and was removed afterward; `DisableLoopbackCheck` was
never set.

### Mutation authorization stop condition

The normal browser called:

```json
{"operationId":"int06h.unknown"}
```

| Validation client | Session | Mutation-token result |
|---|---:|---|
| Chrome normal interactive browser | 200, `isAuthorized=false` | 403 authorization failure; no token; no operation executed |
| Edge normal interactive browser | 200, `isAuthorized=false` | 403 authorization failure; no token; no operation executed |
| Equivalent elevated Windows validation | 200, `isAuthorized=true` | 400 `operation_not_supported`; no token; no operation executed |

This satisfies the required stop condition for a potential **browser
Administrator authorization / UAC filtered-token defect**: normal browser
Negotiate succeeds, but the browser's server-side Windows identity is not
recognized as a local Administrator, while the equivalent elevated validation
is authorized and reaches the safe empty-registry response. No runtime fix was
attempted. Planner/security review is required before any mutation feature is
implemented or enabled.

### INT-06H pass-gate reconciliation

| Gate | Result |
|---|---|
| Chrome LNA secure allow path | `PROVEN` |
| Chrome LNA secure block path | `PROVEN` |
| Edge LNA secure allow path | `PROVEN` |
| Edge LNA secure block path | `PROVEN` |
| Chrome browser Negotiate/session | `PASS` with exact auth prerequisite |
| Edge browser Negotiate/session | `PASS` with exact auth prerequisite |
| Direct browser -> Agent | `PROVEN` |
| Browser SID exposure | `NONE` |
| Browser mutation authorization | `BLOCKED / 403` |
| Unknown-operation response | `PROVEN` only through equivalent elevated validation |
| No browser elevation requirement | `FAILED / NOT PROVEN`; stop condition triggered |
| No backend relay | `PROVEN` |
| No TLS/LNA/auth bypass | `PASS`; only exact temporary prerequisites were used |
| Cleanup | `PASS` |

### Cleanup

Cleanup completed and was verified after the temporary workspace was removed:

```text
temporary Agent process/service: stopped/absent
port 5001: no listener
temporary certificates and LocalMachine trust: removed
Chrome policies: restored/absent
Edge policies: restored/absent
BackConnectionHostNames: restored/absent
DisableLoopbackCheck: absent
hosts mappings: restored/absent
temporary browser profiles: removed
PFX, certificate exports, auth traces, cookies, and browser profiles: none retained
temporary evidence workspace and repository harness: removed
```

### Regression validation

| Check | Result |
|---|---|
| Domain tests | Passed; 7/7, 0 skipped |
| Application tests | Passed; 76/76, 0 skipped |
| Infrastructure tests | Passed; 60/60, 0 skipped |
| Agent integration tests | Passed; 69/69, 0 skipped |
| `dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --warnaserror` | Passed; 0 warnings, 0 errors |
| Runtime source diff | `NONE` |

### INT-06H governance result

```text
INT-06: BLOCKED
INT-06F: BLOCKED HISTORICAL ATTEMPT
INT-06G: MACHINE-SIDE EVIDENCE COMPLETE
INT-06H: BLOCKED / FAILED AT BROWSER ADMINISTRATOR AUTHORIZATION

BLOCKER:
Normal Chrome/Edge browser Negotiate sessions authenticate but are not
authorized as local Administrators; equivalent elevated validation is
authorized and reaches operation_not_supported. Potential UAC filtered-token
defect requires planner/security review.

RUNTIME SOURCE CHANGES: NONE
RUNTIME REMEDIATION: NOT EXECUTED
NEXT: PLANNER REVIEW
INT-07: NOT AUTHORIZED / NOT EXECUTED
```

**INT-07 WAS NOT EXECUTED.**

## INT-06G - PRE-ELEVATED CONTINUATION

**Result:** `BLOCKED / FAILED - browser evidence unavailable`

**Execution date:** 2026-08-12

This section extends the preserved INT-06 and INT-06F blocked history. INT-06G
started from the expected Support Hub `main` SHA `190741fbee459b14b5124c26395c7c1bf64670b1`,
which matched `origin/main`. The executor's first machine check returned
`Elevated=True`. No UAC or privilege-escalation attempt was made.

### Scope and source integrity

| Item | Result |
|---|---|
| Runtime Agent source changes | `NONE` |
| Support Hub backend/frontend changes | `NONE` |
| POS provenance repository | Read-only; not modified |
| Temporary evidence service | Unique `RmsSupportHub.Pos.Agent.INT06G`; no existing Agent service overwritten |
| Business infrastructure | Not executed: no SQL, SCM, SMB, backup, restore, maintenance, downloader, artifact, or POS feature operation |
| Backend relay | `ABSENT` - no Agent reference found under `backend/**` |
| Agent runtime prohibited patterns | `ABSENT` in `pos/src/RmsSupportHub.Pos.Agent/**` |

### Certificate, trust, and LocalSystem transport

The dedicated evidence certificates were short-lived, machine-store
certificates with SHA-256 signatures, private keys, Server Authentication EKU,
and the exact DNS SAN `rms-pos-agent.localhost`. Thumbprints, private keys, and
raw service/auth material were kept only in the temporary evidence workspace
and were not written to the repository.

| Evidence | Result | Direct observation |
|---|---|---|
| Pre-elevated executor | `PASS` | First machine check returned `Elevated=True` |
| Certificate A in `LocalMachine\\My` | `PASS` | Dedicated cert; exact SAN, private key, SHA-256, and Server Authentication EKU |
| Machine trust for A | `PASS` | Public certificate trusted in `LocalMachine\\Root`; default Schannel/SslStream validation succeeded |
| Invalid certificate startup | `PASS - FAILED CLOSED` | Deliberately nonexistent thumbprint left the service stopped and port 5001 closed |
| Certificate B provisioning | `PASS` | Same exact SAN and security properties; separately trusted |
| A-to-B rollover | `PASS` | Service restart served B; default TLS client received HTTP 200; A was then removed without breaking B |
| Trust-negative test | `PASS` | Removing B trust caused TLS validation failure; restoring trust returned HTTP 200 |
| Temporary service | `PASS` | Service ran as `LocalSystem`; mapped listener process owner resolved to `NT AUTHORITY\\SYSTEM` |
| IPv4 loopback | `PASS` | Live listener on `127.0.0.1:5001`; health returned HTTP 200 |
| IPv6 loopback | `PASS` | Live listener on `[::1]:5001`; health returned HTTP 200 |
| LAN listener | `ABSENT` | Live listener inspection showed only loopback; 3 IPv4 and 3 IPv6 non-loopback probes were closed |
| HTTPS | `PASS` | Canonical HTTPS health request succeeded without a certificate bypass |
| HTTP fallback/redirect | `ABSENT` | Plain HTTP received no response; no redirect listener exists |
| Negotiated protocol | `PASS` | Live curl/SChannel evidence reported HTTP/1.1 |
| Canonical Host | `PASS` | `rms-pos-agent.localhost:5001` accepted over valid TLS |
| Wrong Host | `PASS - REJECTED` | Valid TLS targeting with an incorrect HTTP Host returned `400 host_rejected` |

### CORS, Origin, Negotiate, and identity

| Evidence | Result | Direct observation |
|---|---|---|
| Exact configured Support Hub origin | `PASS` | `https://support-hub.integration.test:4443` only |
| Anonymous valid preflight | `PASS` | OPTIONS returned 204 without a Windows-auth challenge |
| Preflight grant | `PASS` | Exact allow-origin, credentials, `GET,POST`, approved headers, and `Vary: Origin`; no wildcard |
| Wrong-origin preflight | `PASS - REJECTED` | Wrong Origin returned 403 `cors_preflight_rejected` with no grant |
| Authenticated wrong Origin | `PASS - REJECTED` | Actual Negotiate request returned 403 `origin_rejected` |
| Negotiate | `PASS` | Actual Windows Negotiate succeeded after the exact hostname loopback exception was tested |
| Windows SID | `RESOLVED - VALUE REDACTED` | `/api/v1/session` returned 200; that route returns 403 when server-side SID resolution fails |
| SID exposed to browser DTO | `NO` | Session response property set contained no `sid` property |
| Local Administrator status | `INFERRED / NEGATIVE AUTHORIZATION` | Elevated local OS check was Administrator; the live Agent session reported `isAdministrator=false` |
| Mutation authorization | `BLOCKED` | The real curl Negotiate client did not replay the POST body after the handshake; the token request ended as an empty-body 400, so no token or feature operation was issued |
| Unknown operation | `NOT PROVEN` | The production operation registry is empty; no feature operation was enabled or executed |
| SPN query | `QUERY UNAVAILABLE` | Read-only `HTTP/rms-pos-agent.localhost` query could not reach LDAP; no SPN was registered or changed |
| Kerberos/NTLM classification | `NEGOTIATE PROVEN / UNDERLYING MECHANISM UNPROVEN` | No canonical HTTP ticket was present in `klist`; the mechanism was not guessed |
| BackConnectionHostNames | `REQUIRED - EXACT HOSTNAME PROVEN` | Authentication failed before the exact `rms-pos-agent.localhost` entry; succeeded after it; the entry was removed during cleanup |
| DisableLoopbackCheck | `NOT USED` | Value was absent before and after; the broad bypass was never set |

### Production route surface

While the temporary service ran with `DOTNET_ENVIRONMENT=Production`, the
approved foundation surface was checked without executing a POS operation:

| Route | Live result |
|---|---|
| `GET /health/live` | 200 |
| `GET /health/ready` | 200 |
| `GET /api/v1/session` without credentials | 401 |
| `POST /api/v1/security/mutation-token` | Foundation route present; no token issued and no operation registered |
| `/openapi/v1.json` | 404; runtime OpenAPI absent in Production |
| Feature paths (`backup`, `restore`, `maintenance`, `downloader`, `service`, `configuration`, `artifacts`) | 404 with the valid test Origin; feature routes remain unmapped |

### Browser evidence gate

Installed stable browser binaries were present and recorded as:

```text
Chrome: 151.0.7922.77
Edge: 151.0.4129.78
```

Current first-party policy names and version support were checked against the
official vendor documentation before the live attempt:

- [Chrome LocalNetworkAccessAllowedForUrls](https://chromeenterprise.google/policies/local-network-access-allowed-for-urls/)
- [Chrome LocalNetworkAllowedForUrls](https://chromeenterprise.google/policies/local-network-allowed-for-urls/)
- [Chrome LoopbackNetworkAllowedForUrls](https://chromeenterprise.google/policies/loopback-network-allowed-for-urls/)
- [Chrome AuthServerAllowlist](https://chromeenterprise.google/policies/auth-server-allowlist/)
- [Edge LocalNetworkAccessAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkaccessallowedforurls)
- [Edge LocalNetworkAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkallowedforurls)
- [Edge LoopbackNetworkAllowedForUrls](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/loopbacknetworkallowedforurls)
- [Edge AuthServerAllowlist](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/authserverallowlist)

The browser-control runtime had no connected in-app Browser, Chrome, or Edge
session (`browsers.list()` was empty). Therefore the following evidence is
`BLOCKED`, not `PASS` or `INFERRED`:

| Evidence | Result |
|---|---|
| Secure Support Hub page at the exact test origin | `BLOCKED - no browser session` |
| Chrome LNA default/allow/block matrix | `BLOCKED - not tested` |
| Edge LNA default/allow/block matrix | `BLOCKED - not tested` |
| Chrome authenticated browser session | `BLOCKED - not tested` |
| Edge authenticated browser session | `BLOCKED - not tested` |
| Direct browser -> Agent network evidence | `BLOCKED - not tested` |
| Browser conservative failure classification | `NOT APPLICABLE - no browser request was run` |
| Browser policy changes | `NONE`; pre-existing policy roots were absent and no temporary policy was installed |

No hosts entry for either test origin or Agent hostname was added, no browser
profile was created, and no LNA wildcard or global opt-out was used. The lack
of a connected browser surface is the sole remaining INT-06G evidence gate;
it prevents claiming Chrome/Edge LNA, browser authentication, or direct
browser-to-Agent proof.

### Cleanup

Cleanup completed and was verified after the temporary workspace was removed:

```text
temporary service: deleted
port 5001: no listener
machine environment: restored
evidence certificates/trust: removed
hosts file: unchanged/restored
browser policies: unchanged
BackConnectionHostNames: restored
DisableLoopbackCheck: absent
temporary browser profiles/auth traces/PFX: none retained
temporary evidence workspace: removed
```

### Regression validation

| Check | Result |
|---|---|
| `dotnet restore pos/RmsSupportHub.Pos.slnx --nologo` | Passed |
| POS Release build with `--warnaserror` | Passed; 0 warnings, 0 errors |
| Domain tests | Passed; 7/7, 0 skipped |
| Application tests | Passed; 76/76, 0 skipped |
| Infrastructure tests | Passed; 60/60, 0 skipped |
| Agent integration tests | Passed; 69/69, 0 skipped |
| Repository runtime source diff | None |

### INT-06G governance result

```text
INT-06: BLOCKED
INT-06F: BLOCKED HISTORICAL ATTEMPT
INT-06G: BLOCKED

BLOCKER:
Connected Chrome/Edge browser control was unavailable; LNA, secure test-page,
browser-authenticated session, and direct browser -> Agent evidence remain
unproven.

RUNTIME REMEDIATION: NOT EXECUTED
NEXT: PLANNER REVIEW / RERUN ONLY THE BROWSER EVIDENCE ON A CONNECTED DEVICE
INT-07: NOT AUTHORIZED / NOT EXECUTED
```

**INT-07 WAS NOT EXECUTED.**
