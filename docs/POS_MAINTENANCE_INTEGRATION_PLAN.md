# POS Maintenance Cross-Project Integration Plan

## Status and authorization

This is the canonical destination-side architecture record for INT-00 through
INT-05F, INT-CI01, and the current INT-06/INT-06G/INT-06H evidence gate.

```text
INT-00: COMPLETE
CROSS-PROJECT INTEGRATION PLAN: COMPLETE
CLAUDE OPUS 5 ARCHITECTURE CHECKPOINT:
PASS - INT-01 AUTHORIZED AND COMPLETE

INT-00R TRANSPORT HARDENING:
COMPLETE

CROSS-PROJECT ARCHITECTURE:
CLOSED / HARDENED

CROSS-PROJECT INTEGRATION PLANNING:
AUTHORIZED

ORIGINAL INT-01/INT-02/INT-03 IMPORT PROVENANCE:
25922b499d33bd73f241ffc26c212dd000e81433

CORRECTED AGENT PROVENANCE CANDIDATE FOR INT-04+:
010abc52dc110cfde3dc2c53e057890ff6edaf97

INT-02:
COMPLETE - PORTABLE DOMAIN / APPLICATION / CONTRACTS IMPORTED

INT-03:
COMPLETE - WINDOWS INFRASTRUCTURE / TESTS / RETAINED WINUI IMPORTED

INT-03R:
COMPLETE - POS AGENT PROVENANCE SNAPSHOT INTEGRITY CORRECTED

INITIAL OPUS PRIVILEGED-BOUNDARY REVIEW:
HISTORICALLY BLOCKED - PROV-1

PROV-1:
CLOSED BY INT-03R

FOLLOW-UP OPUS REVIEW:
REQUIRED FOR LIVE / PRIVILEGED EVIDENCE

INT-04:
COMPLETE - AGENT HOST / RUNTIME COMPOSITION

INT-05:
COMPLETE / ACCEPTED AFTER INT-05F - BROWSER TRANSPORT / OPENAPI / CLIENT ADAPTER

INT-05F:
COMPLETE - OPENAPI GENERATOR PEER-DEPENDENCY ISOLATION

INT-CI01:
COMPLETE - PORTABLE APPLICATION LINUX CI BASELINE REMEDIATION

PORTABLE UBUNTU POS CI:
GREEN - ALL FIVE POS CI LANES

NEXT:
INT-06H - REAL BROWSER RUNTIME EVIDENCE / BLOCKED - PLANNER REVIEW
```

INT-00 closes the cross-project architecture decision. INT-01 created the
isolated destination skeleton and its build/CI boundary. INT-02 imported the
approved portable Domain/Application/Contracts source and the two portable test
boundaries. INT-03 then imported only the approved Windows Infrastructure,
Infrastructure test, and retained WinUI boundaries. INT-04 composed the
destination-owned headless Agent host from corrected POS provenance
`010abc52dc110cfde3dc2c53e057890ff6edaf97`: fixed HTTPS origin
`https://rms-pos-agent.localhost:5001`, HTTP/1.1 loopback binding, Windows
Service hosting, production Negotiate, local-Administrators/SID authorization,
exact-origin CORS/Origin enforcement, mutation-token and service-owned storage
foundations, and only health/live, health/ready, authenticated session, and
mutation-token foundation routes. INT-05 adds the destination-owned versioned
OpenAPI document, deterministic Support Hub generated types, and a dedicated
direct-Agent Angular transport; INT-05F isolates the generator's TypeScript 5
peer dependency from Angular's TypeScript 6 graph. Neither gate adds feature
operations or activates the POS UI.

INT-CI01 then made Windows maintenance and SMB path semantics deterministic in
portable Application code and resolved the nine pre-existing Ubuntu
Application failures. No Agent feature route or POS UI was activated.

The POS repository is a read-only provenance source. INT-04 started from the
Support Hub `main` head and `origin/main` at
`9ecaee934735993a81559427a6800291a7bc43d8` and used the clean POS source
snapshot `010abc52dc110cfde3dc2c53e057890ff6edaf97`; neither repository was
modified. INT-04 selectively adapted/copy-imported only the approved Agent
host/security foundations into destination-owned projects; no POS Angular,
history, or feature-operation source was imported. The old POS SHA above remains historical evidence for the INT-00R and
INT-01/INT-02/INT-03 import records. The corrected SHA is the INT-04 Agent
source baseline; the source repository remained read-only and unchanged.

## Decision summary

| Area | Canonical decision |
| --- | --- |
| Privileged boundary | A separate Windows `RmsSupportHub.Pos.Agent` process owns privileged POS work. |
| Browser transport | The final Support Hub Angular application talks directly over trusted HTTPS using HTTP/1.1 only to the Agent's loopback origin. `RmsSupportHub.Api` is not in this privileged request path. |
| Loopback security | The Agent binds only to loopback through a fixed hostname and fixed port. It never widens to LAN access, discovers services, listens on `0.0.0.0`, or relies on a certificate-warning bypass. |
| Browser permission | Local Network Access (LNA), a versioned Chrome/Edge policy matrix, managed exact-origin allowlisting, and first-run/revocation UX are part of the architecture. Denial makes POS unavailable. |
| Browser identity | Windows Negotiate is used directly. Kerberos is preferred; `.localhost` SPN behavior and NTLM loopback/back-connection behavior are open evidence items. Credential delegation is not required. |
| Cross-origin mutations | Use the selected authenticated principal + exact-origin-bound short-lived one-use request-token contract. Do not reuse the current remote-incompatible `SameSite=Strict` shell model or invent an unreviewed nonce. |
| CORS | Anonymous exact-origin transport preflight; subsequent application requests require Windows Negotiate, authorization, exact Origin, and the selected mutation-token contract. Exact methods/headers, credentials, `Vary: Origin`, and fail-closed negative cases are required. |
| TLS identity | Trusted machine certificate provisioning is mandatory. The certificate must cover the fixed Agent hostname, be owned in the machine certificate store, and be readable by the Agent service identity. Certificate lifecycle is part of deployment evidence. |
| Progress/artifacts | REST is state truth; SSE is authenticated read-only progress transport with no mutation token. Artifacts are fetched through typed authenticated HTTP and released as browser Blob/object URLs from opaque handles. |
| Device scope | The initial direct browser-to-loopback Agent is per-device local maintenance, not remote branch-fleet management. |
| Service identity | Preserve POS ADR-012: Windows service identity is `LocalSystem`, with loopback-only hosting, local Administrators authorization, server-owned allowlists, strict typed operations, explicit SQL credentials, and explicit SMB credential flow. |
| POS frontend | The standalone POS Angular workspace is frozen and reference-only. Support Hub owns the final route, UI, design system, package files, and typed generated consumer. |
| Desktop client | WinUI is retained and receives a destination-side publish-validation lane. It is not cut over by INT-00. |
| Source import | INT-02 and INT-03 use a clean tracked source snapshot from the approved SHA. Raw POS Git history merge is prohibited; project/lock metadata is reconciled into destination-owned projects. |
| CI | POS historic `.github/workflows/ci.yml` is reference-only; destination-owned `pos-ci.yml` restores/builds/tests the portable boundary on Ubuntu, runs Infrastructure tests and the solution build on Windows, and validates a retained WinUI publish artifact. |

The focused records are [ADR-0015](../.ai/decisions/ADR-0015-separate-pos-agent-trust-boundary.md),
[ADR-0016](../.ai/decisions/ADR-0016-pos-browser-transport-security-boundary.md),
[ADR-0017](../.ai/decisions/ADR-0017-pos-clean-snapshot-and-project-isolation.md),
and [ADR-0018](../.ai/decisions/ADR-0018-pos-contract-and-client-ownership.md).

## Canonical target topology

```text
Support Hub Angular
    |
    | HTTPS
    | Local Network Access permission/policy
    | Windows Negotiate
    | approved antiforgery/origin contract
    v
RmsSupportHub.Pos.Agent
    |
    +--> POS Application / Domain / Contracts
    |
    +--> Windows Infrastructure
          |
          +--> SQL
          +--> SCM
          +--> SMB/filesystem
          +--> backup trigger HTTP
```

The browser is the direct transport client. The general `RmsSupportHub.Api`
does not proxy, execute, or relay privileged POS operations in the initial
architecture. The portable Support Hub `Core` and `Data` projects do not
reference Windows POS Infrastructure. Online Orders and Prompt Studio remain
inside their current frozen contracts and behavior.

```text
DIRECT BROWSER -> LOOPBACK AGENT:
PER-DEVICE LOCAL MAINTENANCE ARCHITECTURE
```

The initial Agent is installed on and controlled from the same Windows device
as the browser. This is not a remote branch-fleet or central-management
architecture. Fleet, LAN, or remote-device requirements require a new
architecture/security programme; the Agent must not widen to LAN access or be
routed through the general Support Hub API as an ad-hoc workaround.

### Ownership and audit boundary

- Support Hub owns the final Angular feature, route, typed client generated from
  the Agent contract, user-facing state, and safe explanation of permission or
  reachability failures. It owns no SQL, SCM, SMB, filesystem, restore, or
  privileged process operation.
- The Agent owns Windows authentication, exact origin checks, antiforgery
  validation, operation allowlists, authorization, request correlation,
  timeouts, audit events for privileged actions, and the mapping from explicit
  contracts to Windows resources.
- Windows Infrastructure owns the service installation, LocalSystem service
  identity, machine certificate, private-key ACL, browser policy deployment,
  port reservation, and upgrade/rollback/uninstall behavior.
- POS Domain/Application/Contracts own portable business rules and typed
  operation contracts. POS Infrastructure owns SQL, SCM, SMB/filesystem, and
  backup-trigger adapters. The Agent is the Windows composition root.
- The Agent must not accept arbitrary PowerShell, command, SQL, script upload,
  executable path, or generic process-launch input.
- Hub identifiers, including `oot_sid`, are never Windows identities,
  authorization principals, POS operation owners, artifact owners,
  file-handle owners, idempotency identities, destructive audit principals, or
  mutation-token principals. POS ownership comes only from the authenticated
  Agent-side Windows principal and server-owned contracts. The existing
  Online Orders session/draft use remains unchanged.

### Trigger outcome truth

All future Agent/remote-trigger contracts use this semantic boundary:

```text
Proven pre-dispatch input/policy/DNS/SSRF/cancellation rejection
→ NotAttempted

Positive acknowledgement
→ Accepted

Dispatch/connection/transport/timeout/cancellation ambiguity
→ OutcomeUnknown

Any received non-success remote trigger response,
without an authoritative side-effect-free remote contract
→ OutcomeUnknown
```

`Failed` may represent a definitive remote rejection only if a future
authoritative contract proves that rejection means the remote operation was not
accepted. No generic HTTP status supplies that evidence. No automatic retry
follows `OutcomeUnknown`.

### Trigger finding verification

The required read-only source spot check was performed against POS provenance
SHA `25922b499d33bd73f241ffc26c212dd000e81433`. `BackupApiClient` returns
`OutcomeUnknown` for every received non-success response after dispatch when no
authoritative side-effect-free remote contract exists; the focused source tests
assert the same result. It preserves `NotAttempted`, `Accepted`, and
`OutcomeUnknown`, and does not authorize automatic retry after unknown.

```text
CLAUDE MEDIUM-8:
NOT APPLICABLE / ALREADY CLOSED BY SOURCE
```

No trigger documentation was weakened or changed to introduce `Failed`.

The two R2 non-blocking findings are dispositioned here: POS CI ownership is
decomposed into destination CI lanes, and trigger-truth prose uses the
conservative runtime semantics above.

## Browser and security boundary

### Local Network Access

Direct browser-to-loopback communication is not treated as ordinary CORS. The
Support Hub origin that initiates LNA must be HTTPS and a trusted secure
context; a plain HTTP intranet deployment is not assumed compatible. The
deployment and client contract account for browser LNA permission and policy,
the exact Support Hub origin, and the Agent origin.

LNA is a versioned deployment matrix, verified against current first-party
Chrome and Edge policy documentation on 2026-08-10:

| Browser generation | Local-network policies | Loopback-specific policies | Required treatment |
| --- | --- | --- | --- |
| Chrome 139-145 | `LocalNetworkAccessAllowedForUrls` / `LocalNetworkAccessBlockedForUrls` | Current policy precedence also names `LoopbackNetworkAccessAllowedForUrls` / `LoopbackNetworkAccessBlockedForUrls`; split `LoopbackNetworkAllowedForUrls` is not available until 146 | Inspect the actual policy surface and use any exposed loopback-specific control for the loopback Agent. |
| Chrome 146+ | `LocalNetworkAllowedForUrls` / `LocalNetworkBlockedForUrls`, with the `LocalNetworkAccess*` family also present | `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` | Use the applicable loopback-specific policy for the loopback Agent; it is not optional. |
| Edge 140-145 | `LocalNetworkAccessAllowedForUrls` / `LocalNetworkAccessBlockedForUrls` | Current policy precedence also names `LoopbackNetworkAccessAllowedForUrls` / `LoopbackNetworkAccessBlockedForUrls`; split `LoopbackNetworkAllowedForUrls` is not available until 146 | Inspect the actual policy surface and use any exposed loopback-specific control for the loopback Agent. |
| Edge 146+ | `LocalNetworkAllowedForUrls` / `LocalNetworkBlockedForUrls`, with the `LocalNetworkAccess*` family also present | `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` | Use the applicable loopback-specific policy for the loopback Agent; it is not optional. |

The matrix must record browser version, permission generation, target address
space, exact allow policy, exact block policy, policy precedence, and policy
scope independently for Chrome and Edge. Inspect pre-existing blocking and
higher-precedence policies before claiming an allowlist is effective. No
wildcard allowlist, global permanent LNA disablement, or permanent temporary
opt-out policy is permitted. The policy is a controlled deployment exception,
not a substitute for Agent binding, CORS, authentication, or antiforgery.

Unmanaged browsers receive an explicit first-run explanation and their normal
permission prompt where supported. The UI distinguishes at least:

1. Agent unreachable or stopped;
2. LNA permission denied or later revoked;
3. browser/version or managed policy unsupported; and
4. Agent authentication, certificate, CORS, or antiforgery rejection.

All states make POS unavailable and provide safe recovery guidance. None
causes a LAN fallback, network discovery, relaxed certificate validation, or
automatic retry of a privileged operation. The Agent remains loopback-only
even when LNA is denied.

### Windows Negotiate

The managed-browser deployment allowlists the exact approved Agent hostname in
`AuthServerAllowlist`. The client uses Windows Negotiate directly. Kerberos is
preferred, but NTLM fallback is accepted for the fixed loopback hostname in the
baseline architecture because the downstream SQL/SMB paths use explicit
server-owned credentials and do not require browser credential delegation.

INT-00 does not assert that an SPN exists or that a custom port has acceptable
Kerberos behavior. A Kerberos-only deployment must provide evidence for the
service principal, approved hostname, service identity, and port. If a
non-standard HTTPS port affects SPN resolution, the deployment must prove the
required behavior before enabling any browser/SPN-specific option. No browser
policy is enabled blindly.

`AuthNegotiateDelegateAllowlist` remains unset. It may be considered only by a
separately reviewed architecture that genuinely requires credential delegation.

The custom Agent hostname creates an explicit Windows authentication loopback /
back-connection evidence item. Kerberos is not assumed impossible for a
`.localhost` identity: SPN behavior for the approved hostname is open live
evidence, and explicit `HTTP/<approved-hostname>` SPN registration may be
required if Kerberos is selected. NTLM behavior for the approved local alias
must also be validated. Where NTLM and the Windows loopback check require it,
deployment may configure the exact approved hostname in:

```text
HKLM\SYSTEM\CurrentControlSet\Control\Lsa\MSV1_0\BackConnectionHostNames
```

`DisableLoopbackCheck = 1` is prohibited. The implementation must report
Windows-authentication/loopback rejection separately from LNA, TLS, CORS, and
antiforgery failures.

### Agent origin, port, and TLS

The preferred origin shape is:

```text
https://rms-pos-agent.localhost:5001
```

`rms-pos-agent.localhost:5001` is the fixed loopback-only origin selected by
INT-04. A deployment may substitute an equivalent fixed name only after proving
that it resolves exclusively to loopback and receives the same origin,
certificate, and browser-policy review. Port 5001 is reserved, documented,
non-dynamic, and owned by the Agent installer. It has no LAN hostname,
discovery protocol, wildcard listener, or `0.0.0.0` binding.

The initial browser endpoint is **HTTP/1.1 only**. The imported destination
Agent must configure the equivalent of `HttpProtocols.Http1`; browser HTTP/2
negotiation or a protocol downgrade must not be relied on for privileged
transport.

The certificate model is:

- HTTPS is mandatory; there is no certificate warning bypass.
- The certificate SAN contains the exact approved Agent hostname and no
  unneeded broad names.
- Trusted machine certificate provisioning is mandatory. No production design
  may depend on a publicly trusted CA issuing the `.localhost` certificate.
  Future models may use organization-managed enterprise PKI or secure
  per-device local certificate generation and trust provisioning.
- A per-device/local model requires per-device machine-generated keys, a
  non-exportable private key where supported, no shared private CA/root key
  across endpoints, and only the minimum private-key access needed by
  LocalSystem to serve TLS.
- The certificate belongs in the LocalMachine certificate store. The Agent
  service identity receives only the private-key read access needed to serve
  TLS; installation and renewal ownership remains with the machine/enterprise
  deployment owner.
- Renewal, rotation, upgrade, rollback, uninstall/trust cleanup, and
  expired-certificate UX must be tested. Expiry makes POS unavailable with an
  actionable state; it never prompts the user to bypass a warning.

### CORS and CSP

The future Agent CORS policy is precise and server-owned:

- allow only explicitly configured Support Hub origin(s), separately configured
  for development and deployed environments;
- allow credentials only for those origins;
- allow only the methods and request headers required by the typed Agent
  contract, including the approved request-token header;
- return `Vary: Origin` whenever the response varies by origin; and
- reject missing, unknown, wildcard, or reflected arbitrary origins.

The Support Hub API's existing CORS policy is unrelated and must not become the
Agent security policy. Future Support Hub security headers add only the
approved Agent origin to the POS feature's `connect-src` boundary when direct
transport is active. No global CSP weakening or CSP implementation is part of
INT-00.

The preflight/application split is mandatory:

```text
CORS PRE-FLIGHT:
ANONYMOUS TRANSPORT CHECK

APPLICATION REQUEST:
WINDOWS AUTHENTICATED + AUTHORIZED
```

The browser's CORS `OPTIONS` request does not carry Windows credentials. Agent
CORS processing must run before application authentication/authorization
rejection so a valid exact-origin preflight can be answered anonymously.
Preflight grants no operation authorization: the subsequent request must still
authenticate through Negotiate, pass local-Administrators authorization where
required, and pass exact Origin and mutation-token validation. Preflight is
fail-closed: exact configured Support Hub origin only, exact allowed methods
and headers, credentials declaration as required for the subsequent request,
and `Vary: Origin`. Future negative tests cover unknown origin,
missing/invalid origin where applicable, unauthorized requested method, and
unauthorized requested header.

### SSE and artifact transport

REST is the state truth; SSE is the progress transport. Native `EventSource`
cannot carry the mutation-token custom header, so the default SSE contract is
read-only and carries no mutation token. SSE still requires Windows Negotiate,
an authenticated Windows principal, local authorization as applicable, exact
trusted Origin/CORS, credentialed EventSource behavior, and principal-scoped
operation/event visibility. A custom-header stream requires a deliberately
reviewed `fetch`/`ReadableStream` transport. The mutation token is never put in
a query string, path, fragment, or loggable redirect.

The final Support Hub frontend retrieves Agent artifacts through authenticated
typed `fetch`/HTTP-client behavior, turns returned bytes into a browser
`Blob`/object URL only when a user download is needed, and does not make direct
Agent top-level navigation the default. Artifact access is authenticated,
principal-scoped, exact-origin controlled, opaque-handle based, and auditable
where applicable. There is no artifact token or persistent bearer-style
download URL in a query string.

### Antiforgery choice

The current Agent's cookie/header antiforgery model with `SameSite=Strict` was
designed for the former Agent-hosted Angular shell. It is not silently reused
for a remote Support Hub origin.

| Candidate | INT-00 disposition |
| --- | --- |
| Adapt ASP.NET Core antiforgery cookie/header | Rejected as the default. A remote origin would require a new cross-origin cookie policy and browser credential behavior, adding third-party-cookie and multi-tab complexity while still needing exact Origin and LNA controls. The existing `SameSite=Strict` settings do not describe this topology. |
| Authenticated principal + origin-bound short-lived request token | **Selected.** This is an explicit cross-origin contract, not an unreviewed silent nonce replacement. |
| Same-origin/local-hosted fallback | Retained as a future compatibility fallback for environments that cannot meet the direct LNA/browser baseline; it needs its own deployment validation and is not implemented by INT-00. |

The selected contract is a protected, opaque request token issued by the Agent
only after Windows authentication establishes the principal and the request
has the exact trusted `Origin`. Each token is bound to the authenticated
principal SID, exact origin, HTTP method, a server-defined operation/endpoint
ID from a server-owned allowlist, expiry, and a server-generated anti-replay
identifier. The browser never chooses an arbitrary privileged target string;
case, duplicate slash, escaping, trailing slash, and dot-segment differences
must not change the server-owned privilege binding. The Agent consumes it once
and rejects missing, expired, replayed, principal-mismatched,
origin-mismatched, or target-mismatched tokens.

The lifecycle is deterministic:

```text
ONE MUTATION
-> ONE TOKEN ISSUANCE
-> ONE IMMEDIATE CONSUMPTION
```

There is no reusable token pool, cross-tab sharing, persistence, or reuse of an
abandoned token. An unused token expires naturally. Each tab owns only its
transient in-memory issuance state. The client transport may serialize or
coordinate pending mutations without weakening principal, origin, or operation
binding. The token is sent only in a header, never a URL; server logging
redacts it; Agent restart invalidates outstanding tokens.

**The mutation token is not authentication.** The security layers are
loopback-only transport, trusted HTTPS, LNA/browser permission, exact
CORS/Origin, Windows Negotiate, local Administrators authorization, an
explicit typed operation allowlist, the single-use mutation token for
replay/target protection, and resource locks/idempotency/audit as applicable.
The token never substitutes for Windows authentication, authorization, or
Origin enforcement.

The future implementation must keep Windows principal binding and exact Origin
validation on every cross-origin mutation, allow the token header through the
anonymous CORS preflight, enforce expiry, and provide negative tests for
replay, wrong user, wrong origin, server-operation mismatch, URI normalization
variants, missing token, restart, multi-tab, abandoned-token expiry, and denied
LNA behavior. INT-00R implements none of this code.

## Future destination structure

The isolated destination concept is:

```text
/pos
  /src
    RmsSupportHub.Pos.Domain
    RmsSupportHub.Pos.Application
    RmsSupportHub.Pos.Contracts
    RmsSupportHub.Pos.Infrastructure
    RmsSupportHub.Pos.Agent
    PosAdminTool.WinUI
  /tests
    ...
```

Portable POS projects target `net10.0`. Windows Infrastructure, Agent, and
retained WinUI projects are Windows-targeted. Existing Support Hub backend
projects remain portable. INT-02 populated the Domain, Application, and
Contracts destination projects plus the Domain/Application test projects; INT-03
imported the Windows Infrastructure, Infrastructure test, and retained WinUI
source from the approved tracked snapshot. INT-04/INT-05 compose the
destination-owned Agent runtime and Support Hub contract/transport foundation;
the standalone POS Angular workspace and privileged feature execution source
remain unimported.

The future source boundary is:

```text
IMPORT STRATEGY: CLEAN TRACKED SOURCE SNAPSHOT
RAW POS GIT HISTORY MERGE: PROHIBITED
```

The source snapshot is based on the verified POS SHA above and excludes
generated output, build/runtime directories, local environment debris,
secrets/certificates, and raw repository history according to the approved
intake checklist. The original repository remains historical evidence
referenced by SHA. INT-03 imported 23 Infrastructure `.cs` files, 7
Infrastructure test `.cs` files, and 34 retained WinUI source/resource files;
source `.csproj` and `packages.lock.json` files remain excluded.

## Contract, frontend, and CI ownership

- The Agent owns the authoritative versioned OpenAPI contract and server-owned
  operation semantics at `/pos/openapi`.
- Support Hub owns the deterministic generated Angular types and dedicated
  `HttpBackend` transport. The exact `openapi-typescript@7.13.0` dependency
  and its TypeScript 5 peer-compatible lockfile are isolated in the
  destination-owned `tools/pos-agent-client-generator` workspace. Generated
  code is derived output and is not imported as POS application source.
- The standalone POS Angular workspace remains frozen/reference-only.
- Support Hub owns `package.json`, the lockfile, routes, UI primitives, design
  tokens, and the final POS feature implementation.
- The Agent contract must express explicit, typed, allow-listed operations and
  the `NotAttempted` / `Accepted` / `OutcomeUnknown` boundary above. No
  automatic retry follows `OutcomeUnknown`.

POS `.github/workflows/ci.yml` is:

```text
REFERENCE ONLY - RESPONSIBILITIES DECOMPOSED INTO DESTINATION WORKFLOWS
```

The destination CI plan has separate lanes for:

1. existing Support Hub portable/backend/frontend regression CI;
2. POS Windows build, test, and Agent security CI;
3. OpenAPI and generated-client validation;
4. retained WinUI publish validation;
5. cross-process/browser-to-Agent tests; and
6. protected representative-device evidence for LocalSystem/Session 0 SMB,
   SQL, SCM, restore/maintenance, downloader, and live transport behavior.

No workflow was created by INT-00. INT-01 created the destination-owned
`.github/workflows/pos-ci.yml`; INT-02 extended its portable lane with real
test-project restore/build/test steps; INT-03 added the Windows Infrastructure
test lane and retained WinUI publish validation; INT-05 added the pinned
OpenAPI/generated-client drift lane and frontend path triggers, and INT-05F
isolated its generator install/lockfile from the frontend graph. The R2 CI
finding is closed by ownership decomposition, not by copying the historical POS
workflow into the Hub.

## Deployment decisions and evidence gates

`TrustServerCertificate = true` remains visible as an unresolved SQL TLS
deployment decision. A future validated SQL certificate target is preferred,
but the current environment does not establish it. This does not block source
integration planning and must be resolved before deployment or any Production
claim.

The following are architecture/evidence boundaries, not claims that the
environment has already been proven:

```text
ADR-012 / Session 0 SMB:
OPEN - REPRESENTATIVE DEVICE EVIDENCE REQUIRED

LIVE AGENT / BROWSER TRANSPORT:
OPEN

LOCAL NETWORK ACCESS / MANAGED-BROWSER POLICY:
OPEN - VERSIONED CHROME/EDGE MATRIX AND LIVE EVIDENCE REQUIRED

SUPPORT HUB SECURE CONTEXT:
HTTPS / TRUSTED SECURE CONTEXT REQUIRED

AGENT BROWSER PROTOCOL:
HTTPS + HTTP/1.1

WINDOWS LOOPBACK AUTHENTICATION:
EXPLICIT BACK-CONNECTION / HOSTNAME EVIDENCE REQUIRED

AGENT MACHINE CERTIFICATE TRUST:
MANDATORY - PROVISIONING / LIFECYCLE EVIDENCE REQUIRED

CORS PREFLIGHT:
ANONYMOUS EXACT-ORIGIN TRANSPORT CHECK

SSE:
READ-ONLY / NO MUTATION TOKEN

ARTIFACT RETRIEVAL:
AUTHENTICATED FETCH / OPAQUE HANDLE

MUTATION TOKEN:
PER-MUTATION / SINGLE-USE / SERVER-OPERATION-BOUND

NEGOTIATE BROWSER POLICY:
OPEN - LIVE EVIDENCE REQUIRED

REAL SQL / SCM / RESTORE / MAINTENANCE / DOWNLOADER:
OPEN

REMOTE TRIGGER RECONCILIATION / REMOTE IDEMPOTENCY:
OPEN / UNVERIFIED

SQL TLS:
OPEN DEPLOYMENT DECISION

WINUI CUTOVER:
OPEN BY DESIGN

PORTABLE INT-02 SOURCE IMPORT:
COMPLETE

INT-03 WINDOWS INFRASTRUCTURE + RETAINED WINUI IMPORT:
COMPLETE - BUILD / TEST / PUBLISH VALIDATED

AGENT HOST / RUNTIME COMPOSITION:
NOT AUTHORIZED
```

## Next gate

INT-00R, INT-01, INT-02, and INT-03 are complete. The root task now stages the
next Windows-only owner gate:

```text
INT-01 - DESTINATION PROJECT / BUILD / CI SKELETON: COMPLETE

INT-02 - PORTABLE DOMAIN / APPLICATION / CONTRACTS IMPORT

STATUS:
COMPLETE

INT-03 - WINDOWS INFRASTRUCTURE + RETAINED WINUI IMPORT

STATUS:
COMPLETE

INT-03R - POS AGENT PROVENANCE SNAPSHOT INTEGRITY CORRECTION

STATUS:
COMPLETE

INITIAL OPUS PRIVILEGED-BOUNDARY REVIEW:
HISTORICALLY BLOCKED - PROV-1

PROV-1:
CLOSED BY INT-03R

FOLLOW-UP OPUS REVIEW:
REQUIRED FOR LIVE / PRIVILEGED EVIDENCE

INT-04 - AGENT HOST / RUNTIME COMPOSITION

STATUS:
COMPLETE

FIXED AGENT ORIGIN:
https://rms-pos-agent.localhost:5001

NEXT:
INT-06H - REAL BROWSER RUNTIME EVIDENCE

BLOCKED - BROWSER ADMINISTRATOR AUTHORIZATION / UAC FILTERED-TOKEN FINDING

MACHINE TRANSPORT EVIDENCE: COLLECTED AND CLEANED
REAL CHROME/EDGE LNA, NEGOTIATE, SESSION, AND DIRECT-AGENT EVIDENCE:
COLLECTED; NORMAL-BROWSER MUTATION AUTHORIZATION FAILED
RUNTIME REMEDIATION: NOT EXECUTED
NEXT: PLANNER REVIEW
```

INT-02 completion inventory from the approved provenance SHA is:

```text
DOMAIN: 46 tracked / 44 .cs imported
APPLICATION: 17 tracked / 15 .cs imported
CONTRACTS: 65 tracked / 63 .cs imported
DOMAIN TESTS: 6 tracked / 4 .cs imported
APPLICATION TESTS: 11 tracked / 9 .cs imported
EXCLUDED: 5 source .csproj files (destination identity owned by INT-01)
EXCLUDED: 5 packages.lock.json files (destination package graph is reconciled in .csproj)
```

INT-03 completion inventory from the approved provenance SHA is:

```text
INFRASTRUCTURE: 25 tracked / 23 .cs imported
INFRASTRUCTURE TESTS: 9 tracked / 7 .cs imported
RETAINED WINUI: 36 tracked / 34 source/resource files imported
EXCLUDED: 3 source .csproj files (destination identity/dependencies owned by INT-01/INT-03)
EXCLUDED: 3 packages.lock.json files (destination package graphs are reconciled in .csproj)
```

The destination validation passed: Infrastructure tests 60/60; POS Domain,
Application, and Agent integration tests 7/7, 76/76, and 69/69; solution Release
build with zero warnings/errors; and retained WinUI `win-x64` publish with
`PosAdminTool.WinUI.exe`, 44 `.xbf` resources, and 19 `.pri` resources. The
frontend suite passed 341/341 across 56 files, production build budgets were
clear, and OpenAPI/TypeScript generation was deterministic across two runs. No
Support Hub feature runtime or Production runtime was launched. INT-06G
collected and cleaned the temporary live Agent machine transport evidence;
INT-06H collected and cleaned actual Chrome/Edge LNA, Negotiate, session, and
direct browser-to-Agent evidence. The normal-browser local-Administrator
mutation authorization mismatch remains blocked for planner review; later
feature operations remain out of scope.

## Reference material

- [POS Maintenance Integration Readiness](POS_MAINTENANCE_INTEGRATION_READINESS.md)
- [POS Maintenance Migration Intake](POS_MAINTENANCE_MIGRATION_INTAKE.md)
- [Chrome LocalNetworkAccessAllowedForUrls policy](https://chromeenterprise.google/policies/local-network-access-allowed-for-urls/)
- [Chrome LocalNetworkAllowedForUrls policy](https://chromeenterprise.google/policies/local-network-allowed-for-urls/)
- [Chrome LoopbackNetworkAllowedForUrls policy](https://chromeenterprise.google/policies/loopback-network-allowed-for-urls/)
- [Chrome AuthServerAllowlist policy](https://chromeenterprise.google/policies/auth-server-allowlist/)
- [Chrome AuthNegotiateDelegateAllowlist policy](https://chromeenterprise.google/policies/auth-negotiate-delegate-allowlist/)
- [Edge LocalNetworkAccessAllowedForUrls policy](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkaccessallowedforurls)
- [Edge LocalNetworkAllowedForUrls policy](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkallowedforurls)
- [Edge LoopbackNetworkAllowedForUrls policy](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/loopbacknetworkallowedforurls)
- [Edge Local Network Access deployment guidance](https://learn.microsoft.com/en-us/deployedge/ms-edge-local-network-access)
