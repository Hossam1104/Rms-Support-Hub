# ADR-0026: POS Slice C deployment and evidence boundary

## Status

Accepted — 2026-08-16

## Decision

The POS product has one permanent Windows identity:

- product: `RmsSupportAgent`
- service name: `RmsSupportAgent`
- display name: `RMS Support Agent`
- package schema: `rms-support-agent.package.v1`

`RmsSupportHub.Pos.Agent` and `RmsSupportHub.Pos.Int13.TestService` are
historical Testing migration inputs. Migration is idempotent and ownership
aware: an owned disposable legacy service may be removed through an explicit
plan, while an unowned service, conflicting display name, RMS product service,
or ambiguous marker is a hard conflict. The Agent never claims ownership of
`RMS.BranchService`, `RMS.CashierService`, or `RMSServiceManager`.

Deployment is plan-first and fail-closed. Package verification binds the exact
product, service identity, OS/runtime, architecture, release channel,
environment, signer, signature/hash algorithm, package hash/size, file set,
ACL requirements, certificate requirements, and rollback metadata. Production
requires an approved trusted signer; Testing is explicit and non-Production.
Bootstrap performs at most one bounded elevation handoff. If the trusted
package or authorized elevated platform is unavailable, Install/Upgrade/
Repair/Rollback stop with a typed trust/elevation result and do not write SCM,
registry, certificate, RMS, or Production state.

The certificate plan requires a LocalMachine CNG key, non-exportable private
key, Server Authentication EKU, exact `rms-pos-agent.localhost` DNS identity,
and explicit local or enterprise ownership. Enterprise-owned certificates are
accepted but never removed by local lifecycle code. Browser policy is exact
origin and version-gated; wildcard origins, `DisableLoopbackCheck`, and broad
CORS/loopback relaxations are rejected.

Agent audit is durable JSONL under the fixed service-owned ProgramData root,
bounded by entries and bytes, sanitized to safe event fields, and exposed only
as bounded summaries. Fixed RMS-root health exposes aggregate counts, bytes,
and timestamps without raw paths, filenames, log contents, credentials,
attachments, or arbitrary filesystem enumeration. Support Bundles use fixed
manifest-driven sections and the same redaction/bounding rules.

## Evidence boundary

Repository tests, OpenAPI/client generation, and local builds prove the
contract and fail-closed seams. They do not prove a representative machine,
fleet enrollment, enterprise PKI approval, Production signer, elevated
Testing runtime, customer acceptance, or Main Server/RMS mutation. Those are
independent review and release gates and must be recorded as evidence before
claiming Production readiness.

## Consequences

- Installer, bootstrap, migration, certificate, and browser behavior can be
  reviewed and exercised in plan-only mode without side effects.
- The service identity cannot drift back to a historical INT-13 name.
- The Support Hub remains a direct secure Agent client; privileged work does
  not leak into the general API.
- Release documentation must distinguish implemented repository foundations
  from machine, fleet, PKI, and customer evidence.
