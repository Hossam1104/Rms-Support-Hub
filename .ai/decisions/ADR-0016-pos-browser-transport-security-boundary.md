# ADR-0016: POS browser transport and cross-origin security boundary

- Status: Accepted; live browser and deployment evidence remains open
- Affected area: LNA, Windows Negotiate, origin, CORS, antiforgery, CSP, HTTPS certificates

## Context

The former POS Agent hosted its Angular shell and used a same-origin
cookie/header antiforgery model with `SameSite=Strict`. The future Support Hub
owns the Angular shell, so the browser-to-Agent call becomes cross-origin.
Loopback reachability is also governed by modern Local Network Access (LNA)
permission and managed-browser policy. CORS alone is not the security model.

## Decision

The canonical Agent origin is:

```text
https://rms-pos-agent.localhost:<fixed-port>
```

`rms-pos-agent.localhost` is the preferred fixed name. A substitute requires
deployment evidence that it resolves only to loopback and must use the same
exact-origin, certificate, Negotiate, and browser-policy controls. The port is
fixed, reserved, documented, and owned by the Agent installer. The Agent has
no discovery protocol, LAN hostname, wildcard listener, or `0.0.0.0` binding.

The enterprise browser policy allowlists only the exact deployed Support Hub
origin. The deployment uses `LocalNetworkAccessAllowedForUrls` and, where the
browser/version supports it, `LoopbackNetworkAllowedForUrls`. Wildcard
allowlisting, global LNA disablement, and temporary opt-out policies are not
the permanent architecture. Unmanaged browsers use their normal permission
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

HTTPS uses a machine certificate whose SAN contains the exact Agent hostname.
Enterprise-managed PKI and trust are preferred but not established by this
ADR. The certificate is in the machine store, with private-key read access for
the Agent service identity. Renewal, rotation, upgrade, uninstall, and expiry
behavior are deployment responsibilities and evidence gates. Certificate
warning bypass is prohibited.

The future Agent CORS policy allows only explicitly configured Support Hub
origin(s), with credentials only for those origins, the minimum contract
methods/headers, and `Vary: Origin`. It rejects missing, unknown, wildcard, and
arbitrarily reflected origins. Development and deployed origin configurations
are separate. The Support Hub API's existing CORS policy is unrelated.

The future Support Hub CSP adds only the approved Agent origin to the POS
feature's `connect-src` boundary. INT-00 does not weaken or implement CSP.

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
   accepted. The token is bound to principal identity, exact origin, intended
   mutation method/path, expiry, and a server anti-replay identifier. It is
   consumed once; replay, expiry, target mismatch, user mismatch, origin
   mismatch, and missing-token requests fail closed.
3. **Same-origin/local-hosted fallback.** Retained only as a future deployment
   fallback for environments that cannot meet the direct LNA/browser baseline.
   It needs separate validation and is not implemented by INT-00.

The request token is sent only in a header, never a URL. It is not logged and
is held only in browser memory, never localStorage, sessionStorage, or another
persistent unsafe store. Agent restart invalidates outstanding tokens. Each tab
maintains its own in-memory state. CORS preflight explicitly permits the one
approved token header, and every mutation still validates exact Origin and the
Windows principal. This is an explicit cross-origin contract decision, not a
silent replacement of the former same-origin mechanism and not an unreviewed
generic nonce.

## Required future evidence

The implementation gate must test managed and unmanaged Chrome/Edge behavior,
first-run permission, denial, revocation, unsupported browsers, Agent
unreachable, certificate expiry, exact and negative origins, Negotiate policy,
Kerberos/NTLM behavior, token replay and expiry, wrong principal, wrong origin,
wrong target, restart, multi-tab, and preflight. No such implementation or
live test occurs in INT-00.
