# ADR-0015: Separate Windows POS Agent and direct browser trust boundary

- Status: Accepted; INT-00R transport hardening complete; deployment evidence remains open
- Affected area: POS process isolation, privileged operations, Support Hub API boundary

## Context

The RMS+ Support Hub is a portable Angular/.NET application and currently
contains only an informational POS Coming Soon page. POS maintenance needs
machine-local SQL, service-control, SMB/filesystem, and backup-trigger access.
Putting those privileges in the general Support Hub API would collapse the
portable application boundary and make a remote web request a privileged server
operation.

## Decision

Privileged POS work runs in a separate Windows `RmsSupportHub.Pos.Agent`
process. The final Support Hub Angular application calls the Agent directly
over HTTPS on loopback. The general `RmsSupportHub.Api` is not a proxy, command
router, or execution path for initial POS operations. Support Hub `Core` and
`Data` remain portable and do not reference Windows POS Infrastructure.

The Agent is the Windows composition root. It owns Windows authentication,
origin and antiforgery enforcement, operation allowlists, authorization,
correlation, audit events, timeouts, and adapters to explicit POS contracts.
It may call only typed, allow-listed operations; generic PowerShell, command,
SQL, script-upload, executable-path, and process-launch surfaces are
prohibited.

The accepted POS ADR-012 service identity remains `LocalSystem`. Its
consequences are part of this decision: loopback-only hosting, local
Administrators authorization, server-owned allowlists, strict operation policy,
explicit SQL credentials, and explicit SMB credential flow. The
`LocalSystem`/Session 0 SMB result is still an evidence gate and is not inferred
from this ADR.

The Agent never widens to a LAN listener when browser permission, LNA policy,
certificate trust, or authentication fails. POS becomes unavailable instead.

The initial browser-to-Agent architecture is explicitly per-device local
maintenance:

```text
DIRECT BROWSER -> LOOPBACK AGENT:
PER-DEVICE LOCAL MAINTENANCE ARCHITECTURE
```

The Agent installed on the same Windows device as the browser is the only
initial target. This is not a remote branch-fleet or central-management
architecture. A future requirement for fleet, LAN, remote-device, or central
maintenance is a new architecture and security programme; the Agent must not
widen to LAN access or be routed through the general Support Hub API as an
ad-hoc workaround.

Support Hub identifiers are never POS security identities. In particular,
`oot_sid` and any other Hub session or draft identifier must never become a
Windows identity, authorization principal, POS operation owner, artifact
owner, file-handle owner, idempotency identity, destructive audit principal,
or mutation-token principal. Privileged ownership is derived exclusively from
the authenticated Agent-side Windows principal and server-owned POS contracts.
The Hub session identifier remains limited to its existing Online Orders
draft/session responsibilities; Online Orders behavior is unchanged.

## Consequences

- The privileged boundary is visible and separately deployable, auditable, and
  updateable.
- Support Hub keeps ownership of the final user experience without receiving
  machine credentials or privileged server access.
- Agent failure, LNA denial, certificate expiry, and authentication failure are
  distinct unavailable states rather than reasons to add a fallback proxy or
  LAN listener.
- Cross-process, representative-device, and live browser transport tests are
  required before implementation can be claimed complete.

## R2 disposition

The R2 non-blocking CI ownership finding is resolved by destination-owned CI
lanes. INT-00R hardens the browser transport and ownership wording in this ADR
and ADR-0016; it does not authorize implementation or runtime code changes.
The conservative trigger-truth wording is canonicalized in the integration
plan and intake record.
