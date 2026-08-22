# ADR-0016: POS browser transport and cross-origin security boundary

- Status: Accepted (Historical delivered baseline; superseded by ADR-0029 as future target architecture only)
- Affected area: LNA, Windows Negotiate, origin, CORS, antiforgery, CSP, HTTPS certificates
- Note: Superseded by ADR-0029 as the future target architecture. This document remains the authoritative historical record of the delivered browser-to-loopback transport and cross-origin security model (E07-E09).

## Context

The former POS Agent hosted its Angular shell and used a same-origin
cookie/header antiforgery model with `SameSite=Strict`. The future Support Hub
owns the Angular shell, so the browser-to-Agent call becomes cross-origin.
Loopback reachability is also governed by modern Local Network Access (LNA)
permission and managed-browser policy. CORS alone is not the security model.
The Support Hub page that initiates the request must itself be served from a
trusted HTTPS secure context. A plain HTTP intranet deployment is not assumed
to be compatible with this architecture.

## Decision

The canonical Agent origin selected and composed by INT-04 is:

```text
https://rms-pos-agent.localhost:5001
```

`rms-pos-agent.localhost:5001` is the fixed loopback-only name and port. A
substitute requires deployment evidence that it resolves only to loopback and
must use the same exact-origin, certificate, Negotiate, and browser-policy
controls. Port 5001 is reserved, documented, and owned by the Agent installer.
The Agent has no discovery protocol, LAN hostname, wildcard listener, or
`0.0.0.0` binding.

INT-04 composes a headless ASP.NET Core Windows Service-capable host at this
origin with HTTPS-only HTTP/1.1 Kestrel, production Windows Negotiate, local
Administrators/SID authorization, exact-origin CORS and Origin enforcement,
single-use mutation-token ports, service-owned storage, and only the
`/health/live`, `/health/ready`, and `/api/v1/session` foundation routes. The
feature operation contract, OpenAPI/client adapter, Support Hub UI, and live
browser/device evidence remain later owner-gated work.

The initial browser transport is **HTTP/1.1 only**. The imported destination
Agent must configure the equivalent of `HttpProtocols.Http1` for this
Negotiate-authenticated endpoint. The design must not depend on browser HTTP/2
negotiation or an HTTP/2-to-HTTP/1.1 downgrade for a privileged request.

The enterprise browser policy allowlists only the exact deployed Support Hub
origin. LNA is a versioned deployment matrix, not one timeless policy
assumption. The names and first supported versions below were verified against
the current first-party policy references on 2026-08-10:

| Browser generation | Local-network policy family | Loopback-specific policy family | Deployment rule |
| --- | --- | --- | --- |
| Chrome 139-145 | `LocalNetworkAccessAllowedForUrls` / `LocalNetworkAccessBlockedForUrls` ([allow](https://chromeenterprise.google/policies/local-network-access-allowed-for-urls/)) | The loopback-specific `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` family is not available until 146 | Use the exact Support Hub origin with the documented Local Network Access policy; inspect higher-precedence block policies. |
| Chrome 146+ | `LocalNetworkAllowedForUrls` / `LocalNetworkBlockedForUrls` plus the `LocalNetworkAccess*` family ([allow](https://chromeenterprise.google/policies/local-network-allowed-for-urls/)) | `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` ([allow](https://chromeenterprise.google/policies/loopback-network-allowed-for-urls/)) | A loopback Agent must use the applicable loopback-specific policy; do not treat it as optional. |
| Edge 140-145 | `LocalNetworkAccessAllowedForUrls` / `LocalNetworkAccessBlockedForUrls` ([allow](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkaccessallowedforurls)) | The loopback-specific `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` family is not available until 146 | Use the exact Support Hub origin with the documented Local Network Access policy; inspect higher-precedence block policies. |
| Edge 146+ | `LocalNetworkAllowedForUrls` / `LocalNetworkBlockedForUrls` plus the `LocalNetworkAccess*` family ([allow](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/localnetworkallowedforurls)) | `LoopbackNetworkAllowedForUrls` / `LoopbackNetworkBlockedForUrls` ([allow](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/loopbacknetworkallowedforurls)) | A loopback Agent must use the applicable loopback-specific policy; do not treat it as optional. |

The corresponding `*BlockedForUrls` policies and any higher-precedence URL
blocking policies must be inspected before an allowlist is declared
effective. The deployment record must keep Chrome and Edge rows independent,
including browser build, permission generation, target address space,
governing allow policy, governing block policy, and policy precedence. No
wildcard allowlist, global permanent LNA disablement, or permanent temporary
opt-out policy is permitted. Unmanaged browsers use their normal permission
flow where supported. Denial or revocation makes POS unavailable; it never
causes a LAN fallback.

Windows Negotiate is used directly. Kerberos is preferred, but NTLM fallback is
acceptable for the approved loopback hostname unless a security baseline later
requires Kerberos-only deployment. INT-00 does not guess SPN behavior. A
Kerberos-only deployment must prove the service principal, hostname, service
identity, and port, especially when the HTTPS port is non-standard.
`AuthServerAllowlist` contains the exact Agent hostname. The browser user's
credentials are not delegated: `AuthNegotiateDelegateAllowlist` remains unset
unless a separate security review authorizes a genuine delegation requirement.
The Agent uses explicit SQL/SMB credentials where applicable.

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

The deployment must never use `DisableLoopbackCheck = 1` as a workaround. The
Agent/client must report Windows-authentication or loopback rejection
separately from LNA, TLS, CORS, and antiforgery failures.

HTTPS uses a machine certificate whose SAN contains the exact Agent hostname.

```text
TRUSTED MACHINE CERTIFICATE PROVISIONING:
MANDATORY
```

No certificate-warning bypass is permitted, and no production design may rely
on a publicly trusted CA issuing a `.localhost` certificate. Acceptable future
models include organization-managed enterprise PKI or secure per-device local
certificate generation and trust provisioning. A per-device model requires
machine-generated per-device keys, a non-exportable private key where
supported, no shared private CA/root key across endpoints, and only the
minimum private-key access needed by LocalSystem to serve TLS. Renewal,
rotation, upgrade, rollback, uninstall/trust cleanup, and expiry behavior are
deployment responsibilities and evidence gates.

The future Agent CORS policy allows only explicitly configured Support Hub
origin(s), with credentials only for those origins, the minimum contract
methods/headers, and `Vary: Origin`. It rejects missing, unknown, wildcard, and
arbitrarily reflected origins. Development and deployed origin configurations
are separate. The Support Hub API's existing CORS policy is unrelated.

The preflight/application split is mandatory:

```text
CORS PRE-FLIGHT:
ANONYMOUS TRANSPORT CHECK

APPLICATION REQUEST:
WINDOWS AUTHENTICATED + AUTHORIZED
```

The browser's CORS `OPTIONS` request does not carry Windows credentials. Agent
CORS processing must therefore run before application authentication or
authorization rejection so a valid exact-origin preflight can be answered
without Negotiate. Preflight grants no operation authorization: the subsequent
request must authenticate with Negotiate, pass local-Administrators
authorization where required, and pass exact Origin and mutation-token checks.
Preflight remains fail-closed: exact configured Support Hub origin only, exact
allowed methods and headers, the credentials declaration required by the
subsequent request, and `Vary: Origin`. Future negative tests must cover an
unknown origin, missing/invalid origin where applicable, an unauthorized
requested method, and an unauthorized requested header.

The future Support Hub CSP adds only the approved Agent origin to the POS
feature's `connect-src` boundary. INT-00 does not weaken or implement CSP.

### SSE and artifact transport

REST is the state truth; SSE is the progress transport. Native `EventSource`
cannot carry the mutation-token custom header, so the default SSE contract is
read-only and carries no mutation token. SSE still requires Windows Negotiate,
an authenticated Windows principal, local authorization as applicable, exact
trusted Origin/CORS, credentialed EventSource behavior, and principal-scoped
operation/event visibility. A custom-header stream would require a separately
reviewed `fetch`/`ReadableStream` transport. The mutation token must never be
placed in a query string, path, fragment, or loggable redirect.

Artifact retrieval is browser-fetch based. The final Support Hub frontend uses
typed authenticated `fetch`/HTTP-client behavior, receives bytes through an
opaque handle, and creates a browser `Blob`/object URL only when a user
download is needed. Direct Agent top-level navigation, `window.open`,
`location.href`, query-string tokens, persistent bearer-style URLs, and
unscoped artifact handles are not the default or an allowed token workaround.
Artifact access remains authenticated, principal-scoped, exact-origin
controlled, and auditable where applicable.

## Antiforgery decision

Three choices were evaluated:

1. **Adapt the existing ASP.NET Core antiforgery cookie/header model.** This is
   not the default. The remote origin would need a new cross-origin cookie and
   credential policy, and `SameSite=Strict` no longer describes the topology.
   That adds browser-cookie and multi-tab complexity without removing the need
   for exact Origin and LNA checks.
2. **Authenticated principal plus origin-bound short-lived request token.**
   **Selected.** The Agent issues a protected opaque token only after Windows
   authentication establishes the principal and the exact trusted `Origin` is
   accepted. The token is bound to the authenticated Windows principal SID,
   exact origin, HTTP method, a server-defined operation/endpoint ID from a
   server-owned allowlist, expiry, and a server anti-replay identifier. The
   browser never chooses an arbitrary privileged target string. URI textual
   normalization differences (case, duplicate slash, escaping, trailing slash,
   and dot segments) must not change the server-owned privilege binding. It is
   consumed once; replay, expiry, target mismatch, user mismatch, origin
   mismatch, and missing-token requests fail closed.
3. **Same-origin/local-hosted fallback.** Retained only as a future deployment
   fallback for environments that cannot meet the direct LNA/browser baseline.
   It needs separate validation and is not implemented by INT-00.

The request token is sent only in a header, never a URL. It is not logged and
is held only in browser memory, never localStorage, sessionStorage, or another
persistent unsafe store. The lifecycle is deterministic:

```text
ONE MUTATION
-> ONE TOKEN ISSUANCE
-> ONE IMMEDIATE CONSUMPTION
```

There is no reusable token pool, cross-tab token sharing, token persistence, or
reuse of an abandoned token. An unused token expires naturally. Each tab owns
only its transient in-memory issuance state; the client transport may
serialize or coordinate its own pending mutations without weakening principal,
origin, or operation binding. Agent restart invalidates outstanding tokens.
CORS preflight explicitly permits the one approved token header, and every
mutation still validates exact Origin and the Windows principal.

**The mutation token is not authentication.** The security layers remain:

1. loopback-only transport;
2. trusted HTTPS;
3. LNA/browser permission;
4. exact CORS/Origin;
5. Windows Negotiate;
6. local Administrators authorization;
7. explicit typed operation allowlist;
8. mutation request token for replay/target protection; and
9. resource locks, idempotency, and audit as applicable.

The token never substitutes for Windows authentication, authorization, or
Origin enforcement. This is an explicit cross-origin contract decision, not a
silent replacement of the former same-origin mechanism and not an unreviewed
generic nonce.

## Required future evidence

The implementation gate must test managed and unmanaged Chrome and Edge
independently, with the version/policy matrix, policy precedence, pre-existing
blocking policy, first-run permission, denial, revocation, unsupported browser
generation, and Agent-unreachable behavior. It must also test trusted secure
context, HTTP/1.1-only transport, certificate provisioning/expiry, exact and
negative origins, anonymous preflight, unauthorized methods/headers,
Negotiate policy, custom-hostname loopback/back-connection behavior,
Kerberos/NTLM and any required SPN, separate error classification, token
replay/expiry, wrong principal, wrong origin, server-operation mismatch, URI
normalization variants, restart, multi-tab, abandoned-token expiry, read-only
SSE, artifact fetch/handle ownership, and per-device scope. Optional
`Sec-Fetch-Site`/`Sec-Fetch-Mode` checks may be defense in depth only; they
must not replace Origin, Windows authentication, authorization, or token
validation. No such implementation or live test occurs in INT-00R.
