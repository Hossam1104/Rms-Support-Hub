# POS Maintenance Cross-Project Integration Plan

## Status and authorization

This is the canonical destination-side architecture record for INT-00.

```text
INT-00: COMPLETE
CROSS-PROJECT INTEGRATION PLAN: COMPLETE
CLAUDE OPUS 5 R2:
PASS WITH NON-BLOCKING FINDINGS

CROSS-PROJECT INTEGRATION PLANNING:
AUTHORIZED

POS SOURCE PROVENANCE:
25922b499d33bd73f241ffc26c212dd000e81433
```

INT-00 closes the cross-project architecture decision. It does not import POS
source, create projects, change application code, create workflows, or change
the POS repository. INT-01 remains blocked until the architecture checkpoint
below passes and the owner explicitly authorizes implementation.

The POS repository is a read-only provenance source for this session. At the
start of INT-00, the verified Support Hub remote head was
`a042b253f8621f9096ac9b989edcb9767f2e2527`.
The only Support Hub change since the planner baseline `a63a8834ef14c7f961b8d079b55c34aa98cd6f6e`
is an Online Orders payment-status correction in two frontend files. It does
not alter the POS boundary, browser transport, deployment topology, or
repository architecture.

## Decision summary

| Area | Canonical decision |
| --- | --- |
| Privileged boundary | A separate Windows `RmsSupportHub.Pos.Agent` process owns privileged POS work. |
| Browser transport | The final Support Hub Angular application talks directly over HTTPS to the Agent's loopback origin. `RmsSupportHub.Api` is not in this privileged request path. |
| Loopback security | The Agent binds only to loopback through a fixed hostname and fixed port. It never widens to LAN access, discovers services, listens on `0.0.0.0`, or relies on a certificate-warning bypass. |
| Browser permission | Local Network Access (LNA), managed exact-origin allowlisting, and first-run/revocation UX are part of the architecture. Denial makes POS unavailable. |
| Browser identity | Windows Negotiate is used directly. Kerberos is preferred; NTLM fallback is acceptable for the approved loopback hostname unless a deployment security baseline requires Kerberos-only evidence. Credential delegation is not required. |
| Cross-origin mutations | Use the selected authenticated principal + exact-origin-bound short-lived one-use request-token contract. Do not reuse the current remote-incompatible `SameSite=Strict` shell model or invent an unreviewed nonce. |
| CORS | Exact configured Support Hub origin(s) only; credentials only for those origins; only contract-required methods and headers; `Vary: Origin`; no wildcard or reflected arbitrary origin. |
| TLS identity | Enterprise-managed machine certificate trust is preferred. The certificate must cover the fixed Agent hostname, be owned in the machine certificate store, and be readable by the Agent service identity. Certificate lifecycle is part of deployment evidence. |
| Service identity | Preserve POS ADR-012: Windows service identity is `LocalSystem`, with loopback-only hosting, local Administrators authorization, server-owned allowlists, strict typed operations, explicit SQL credentials, and explicit SMB credential flow. |
| POS frontend | The standalone POS Angular workspace is frozen and reference-only. Support Hub owns the final route, UI, design system, package files, and typed generated consumer. |
| Desktop client | WinUI is retained and receives a destination-side publish-validation lane. It is not cut over by INT-00. |
| Source import | A clean tracked source snapshot is the future import shape. Raw POS Git history merge is prohibited. No source is imported by INT-00. |
| CI | POS historic `.github/workflows/ci.yml` is reference-only; its responsibilities are decomposed into destination-owned lanes. No workflow is created by INT-00. |

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

The two R2 non-blocking findings are dispositioned here: POS CI ownership is
decomposed into destination CI lanes, and trigger-truth prose uses the
conservative runtime semantics above.

## Browser and security boundary

### Local Network Access

Direct browser-to-loopback communication is not treated as ordinary CORS. The
deployment and client contract account for browser LNA permission and policy,
TLS secure-context requirements, the exact Support Hub origin, and the Agent
origin. The enterprise default is an exact Support Hub origin allowlist using
the narrowest supported Chrome/Edge policy:

- `LocalNetworkAccessAllowedForUrls` for the exact deployed Support Hub origin;
- `LoopbackNetworkAllowedForUrls` where the browser/version provides the
  loopback-specific policy; and
- no wildcard allowlist, global LNA disablement, or temporary opt-out policy as
  the permanent architecture.

The deployment matrix must record the managed Chrome and Edge versions,
policy scope, exact origin entries, and whether the loopback-specific policy is
available. The policy is a controlled deployment exception, not a substitute
for Agent binding, CORS, authentication, or antiforgery.

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

### Agent origin, port, and TLS

The preferred origin shape is:

```text
https://rms-pos-agent.localhost:<fixed-port>
```

`rms-pos-agent.localhost` is the preferred fixed loopback-only name. A
deployment may substitute an equivalent fixed name only after proving that it
resolves exclusively to loopback and receives the same origin, certificate, and
browser-policy review. The Agent uses one reserved, documented, non-dynamic
port owned by the installer. It has no LAN hostname, discovery protocol,
wildcard listener, or `0.0.0.0` binding.

The certificate model is:

- HTTPS is mandatory; there is no certificate warning bypass.
- The certificate SAN contains the exact approved Agent hostname and no
  unneeded broad names.
- Enterprise PKI and managed trust in the machine certificate store are
  preferred. Enterprise PKI availability is not established by INT-00 and is a
  deployment decision/evidence gate.
- The certificate belongs in the LocalMachine certificate store. The Agent
  service identity receives only the private-key read access needed to serve
  TLS; installation and renewal ownership remains with the machine/enterprise
  deployment owner.
- Renewal, rotation, upgrade, rollback, uninstall, and expired-certificate UX
  must be tested. Expiry makes POS unavailable with an actionable state; it
  never prompts the user to bypass a warning.

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
has the exact trusted `Origin`. Each token is bound to the principal identity,
exact origin, intended mutation method/path, expiry, and a server-generated
anti-replay identifier. The Agent consumes it once and rejects missing,
expired, replayed, principal-mismatched, origin-mismatched, or target-mismatched
tokens. The token is sent in a request header, never a URL; server logging
redacts it; the browser keeps it only in in-memory application state, never
localStorage, sessionStorage, or another persistent unsafe store. Agent
restart invalidates outstanding tokens. Each tab obtains its own token state,
and concurrent mutations coordinate through the client transport service rather
than sharing persisted secrets.

The future implementation must keep Windows principal binding and exact Origin
validation on every cross-origin mutation, allow the token header through CORS
preflight, enforce expiry, and provide negative tests for replay, wrong user,
wrong origin, wrong target, missing token, restart, multi-tab, and denied LNA
behavior. INT-00 implements none of this code.

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

Portable POS projects target `net10.0`. Windows Infrastructure and Agent
projects are Windows-targeted. Existing Support Hub backend projects remain
portable. These directories and projects are intentionally not created by
INT-00.

The future source boundary is:

```text
IMPORT STRATEGY: CLEAN TRACKED SOURCE SNAPSHOT
RAW POS GIT HISTORY MERGE: PROHIBITED
```

The source snapshot is based on the verified POS SHA above and must exclude
generated output, build/runtime directories, local environment debris,
secrets/certificates, and raw repository history according to the approved
intake checklist. The original repository remains historical evidence
referenced by SHA. No source is imported now.

## Contract, frontend, and CI ownership

- The Agent owns the authoritative POS OpenAPI contract and operation
  semantics.
- Support Hub owns the final generated/typed Angular consumer. Generated code
  is derived output and is not imported as POS application source.
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

No workflow is created by INT-00. The R2 CI finding is closed by ownership
decomposition, not by copying the historical POS workflow into the Hub.

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
OPEN - ARCHITECTURE DEFINED, LIVE EVIDENCE REQUIRED

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

INTEGRATION IMPLEMENTATION:
NOT AUTHORIZED
```

## Next gate

The root task now stages:

```text
CLAUDE OPUS 5
POS INTEGRATION ARCHITECTURE CHECKPOINT

STATUS:
REVIEW REQUIRED / NO EXECUTION AUTHORIZED
```

That checkpoint is narrowly limited to process isolation; browser-to-loopback
transport; LNA; managed Chrome/Edge policy; Negotiate/browser policy;
hostname/port/certificate; CORS; antiforgery; identity; audit/resource
ownership; destination project isolation; and the clean source-import
boundary. It is not another full R2 review. INT-01 remains blocked until the
checkpoint passes and the owner explicitly authorizes it.

## Reference material

- [POS Maintenance Integration Readiness](POS_MAINTENANCE_INTEGRATION_READINESS.md)
- [POS Maintenance Migration Intake](POS_MAINTENANCE_MIGRATION_INTAKE.md)
- [Chrome LocalNetworkAccessAllowedForUrls policy](https://chromeenterprise.google/policies/local-network-access-allowed-for-urls/)
- [Chrome AuthServerAllowlist policy](https://chromeenterprise.google/policies/auth-server-allowlist/)
- [Chrome AuthNegotiateDelegateAllowlist policy](https://chromeenterprise.google/policies/auth-negotiate-delegate-allowlist/)
- [Edge LocalNetworkAccessAllowedForUrls policy](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkaccessallowedforurls)
- [Edge Local Network Access deployment guidance](https://learn.microsoft.com/en-us/deployedge/ms-edge-local-network-access)
