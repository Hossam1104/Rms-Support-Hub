# POS Slice C implementation status

Updated: 2026-08-16

Slice C is implemented as a production-oriented foundation in the destination
`Rms-Support-Hub` repository. This document is the current operational summary;
the earlier Slice C requirements document remains the acceptance checklist and
is no longer a statement that the code is absent.

## Delivered architecture

The permanent per-device Windows service identity is fixed and immutable:

| Field | Value |
| --- | --- |
| Product ID / SCM name | `RmsSupportAgent` |
| Display name | `RMS Support Agent` |
| Description | `Local diagnostics, maintenance and repair agent for RMS Support Hub` |
| Service account | `LocalSystem` |
| Agent origin | `https://rms-pos-agent.localhost:5001` |

`RmsSupportHub.Pos.Agent` remains a direct browser-to-Agent loopback boundary:
HTTPS, HTTP/1.1, Windows Negotiate, exact Support Hub Origin, server-derived
local Administrator authorization, typed operations, opaque target IDs,
one-use mutation tokens, principal-scoped idempotency, and the global
`Global\RmsSupportHub.Pos.Agent.PrivilegedMutationLease` remain in force. The
Support Hub API is not a privileged relay.

The deployment foundation is deliberately fail-closed where this repository
does not contain machine-owned Production trust material:

- `scripts/PosSupportAgentDeployment.psm1` is the shared contract for identity,
  package validation, ownership/migration assessment, browser policy, certificate
  policy, lifecycle modes, and deterministic silent exit codes.
- `scripts/bootstrap-rms-support-agent.ps1` provides one bounded UAC handoff for
  unmanaged onboarding. It never recursively elevates or hides a declined UAC
  request.
- `scripts/install-rms-support-agent.ps1` provides `Install`, `Upgrade`,
  `Repair`, `Uninstall`, `Rollback`, and `Status` modes. `-PlanOnly` and
  `-Status` are non-mutating; lifecycle mutation exits with trust failure until
  an approved package source and signer are provisioned.
- Known historical services (`RmsSupportHub.Pos.Agent` and
  `RmsSupportHub.Pos.Int13.TestService`) can be considered only with matching
  path, display identity, package/marker evidence, and known Testing marker.
  Unknown or conflicting ownership is never adopted, removed, or overwritten.
  `RMS.BranchService`, `RMS.CashierService`, and `RMSServiceManager` remain
  RMS product services and are outside the migration catalog.
- Package policy requires schema 1, product identity, x64/arm64 architecture,
  matching channel/environment, Windows/net10.0-windows, LocalSystem, fixed
  service identity, SHA-256 metadata, bounded file manifests, and trusted
  signing evidence. Installed verification compares the complete manifest and
  exact owned file set, rejects reserved control files and reparse points, and
  re-hashes every installed file.
- The certificate policy requires the exact `rms-pos-agent.localhost` SAN,
  LocalMachine scope, Microsoft Software Key Storage Provider, non-exportable
  private key, Server Authentication EKU, bounded renewal awareness, and a
  proven local or enterprise ownership marker. Enterprise-owned certificates
  cannot be removed by the local Agent.
- Browser policy output is an exact-origin Chrome/Edge managed-policy plan. It
  rejects wildcards, `DisableLoopbackCheck`, broad CORS, and unsupported browser
  generations. The Slice C plan does not write Production registry policy.

## RMS evidence and Support Bundle

`GET /api/v1/rms/operational-health` is an authenticated, read-only projection
of fixed server-owned roots. It reports bounded existence, accessibility,
counts, aggregate bytes, timestamps, capacity, release-file state, update lane,
and insurance-attachment aggregate metadata. The browser cannot submit a path;
reparse points are skipped/rejected; filenames, attachment contents, patient
or insurance identifiers, and arbitrary filesystem enumeration never cross the
Agent boundary.

The fixed catalog includes the verified RMS setup/download/repository roots,
Branch and Cashier data, native Branch/Cashier logs, and
`C:\ProgramData\DBS\POS` as aggregate-only insurance attachment storage.
Existing native log diagnostics remain bounded and redacted before timeline,
artifact, transport, snapshot, or bundle retention.

Support Bundles are manifest-driven and bounded. They contain typed health,
installation, connectivity, database, services, failure-analysis, timeline,
RMS-storage, update, attachment-aggregate, and recent audit summaries. They do
not contain raw paths or filenames, credentials, tokens, private keys/PFX,
connection strings, arbitrary registry/filesystem exports, raw attachment data,
or unrestricted logs.

Sensitive operation outcomes are appended to the service-owned bounded JSONL
audit stream beneath `C:\ProgramData\DBS\RmsSupportAgent\Audit`. The stream is
redacted, size/entry bounded, ACL-provisioned, and readable only through a
bounded Support Bundle projection. Audit coverage includes service control,
database recovery, downloader, maintenance, diagnostic console, safety
snapshots, package/repair lifecycle, support-bundle generation, and guided
repair outcomes.

## UI delivery

The POS Maintenance workspace is now an operations console rather than an
informational-only surface. The command-center header makes the path from signal to
action explicit; fixed-root cards, update/attachment aggregates, health rings,
status chips, recovery shelves, typed action cards, and the bounded incident
timeline preserve technical detail while keeping dangerous actions visibly
separate. Design-token gradients, layered surfaces, controlled depth, tactile
states, responsive stacking, keyboard-visible focus, dark-mode tokens, and
reduced-motion fallbacks are used without a continuous canvas or heavy effects.
The direct secure-origin handoff remains unchanged.

## Evidence classification and remaining release gates

The implementation, automated tests, and local builds are not equivalent to
fleet or customer approval:

| Evidence class | Current state |
| --- | --- |
| Implemented | Slice C contracts, policies, fixed-root health, durable audit, Support Bundle, UI, tests, and generated client are in the repository. |
| CI/local validated | POS Release build/tests, frontend tests/build, PowerShell parse/Pester, and generated OpenAPI/client checks pass when the documented Testing origin is supplied. |
| Testing live-proven | Existing historical INT-13 evidence remains valid for the earlier Testing service model; this session did not claim a new elevated run. |
| Production representative-machine proven | Not claimed. The current shell is not Administrator and no Production registry, certificate, SCM, RMS, Main Server, or database mutation was performed. |
| Customer/fleet approved | Not claimed. Enterprise package signing, package publication, managed policy deployment, PKI issuance/renewal/revocation, and final independent Slice C review remain release gates. |

The next task is the full independent Production/Fleet Security and
Release-Readiness Review described in the root `TASK.md`. It must inspect the
delivered foundation and classify each remaining machine, CI, fleet, and
customer gate without starting Production rollout.
