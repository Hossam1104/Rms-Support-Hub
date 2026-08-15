# ADR-0024: POS Slice B security and entry boundary

Status: Accepted
Date: 2026-08-15

## Decision

Privileged POS package and Repair Installation workflows share one
machine-wide, non-blocking mutation lease and hold it through terminal
operation/timeline truth. Diagnostic, timeline, artifact, and snapshot text
passes through one bounded fail-closed structured-secret redaction pipeline;
sanitization failure quarantines the output. Agent-owned package, staging,
installation, and diagnostic roots are provisioned and verified for ownership,
ACL safety, and reparse escape before use.

Retained privileged previews are typed `POST` operations with bounded
principal-scoped idempotency. GET endpoints remain read-only. Main Server
transport is bounded, no-redirect, and bound to the normalized
scheme/host/effective-port/base-path owned by the Agent. Snapshot evidence
persists the configured environment/profile identity.

The Support Hub exposes one canonical POS entry at
`https://support-hub.integration.test:4443/tools/pos-maintenance`. The Hub
opens that exact external route, and its route guard redirects wrong-origin
direct loads. The browser remains a direct trusted browser-to-Agent client;
the Support Hub API is not a relay or generic proxy.

## Consequences

- Testing remains synthetic/read-only for RMS and Main Server. This decision
  does not authorize RMS executables, installers, uninstallers, repair,
  rollback, package activation, Main Server mutation, registry mutation, or
  RMS folder mutation.
- A default installation under an untrusted or broadly writable machine
  parent fails closed until an authorized service/deployment step provisions
  the service-owned boundary. Existing machine-wide parents are not silently
  rewritten by the Agent.
- Slice C must define the permanent `RmsSupportAgent` service, managed browser
  and certificate lifecycle, upgrade/repair/uninstall/rollback proof, fixed RMS
  source health, safe Support Bundles, and fleet/Production audit. The
  requirements are recorded in `docs/POS_SLICE_C_REQUIREMENTS.md`.
