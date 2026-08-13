# ADR-0020: POS authorization uses Windows account group membership

- Status: Accepted
- Affected area: POS Agent authorization, browser transport, future mutations
- Gate: INT-06I / INT-07

## Context

The normal Chrome/Edge Negotiate session can receive a UAC-filtered browser
token even when the signed-in Windows account is a local Administrator. Using
the browser token's elevation state as the POS authorization decision therefore
produced a false negative in INT-06H.

## Decision

Production POS authorization is based on the authenticated Windows account's
membership in the local machine's Built-in Administrators group. The Agent
resolves that membership server-side through Windows local-group membership,
including indirect membership, and compares returned group SIDs with the
well-known Built-in Administrators SID. Resolution failures fail closed.

Browser token elevation, browser role claims, usernames, `oot_sid`, JWT/Bearer
tokens, and client-provided SID values are not authorization sources. The
session diagnostics and every protected read/mutation operation use the same
server-derived boundary. Synthetic claims are permitted only in the dedicated
IntegrationTest host.

## Consequences

Normal non-elevated browser sessions can authorize when the signed-in account
is a local Administrator, while non-Administrators remain forbidden. Group
enumeration determinism and localized/domain-joined group-name-to-SID coverage
remain INT-13 representative-device hardening work. The decision was accepted
by the independent INT-06I security review (`PASS`, no Critical/High findings)
and is preserved for INT-08 mutation integration.
