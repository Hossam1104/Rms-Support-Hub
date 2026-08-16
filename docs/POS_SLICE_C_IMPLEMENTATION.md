# POS Slice C implementation status

Updated: 2026-08-17

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

The deployment implementation is fail-closed at each machine-owned boundary.
Repository code does not contain private keys or customer/Production evidence;
the real lifecycle becomes executable only when the operator provisions the
fixed LocalMachine trust and certificate material described below.

The sole normal C# package-trust authority is exactly
`%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`. The path is not
configurable: `IConfiguration`, environment variables, command-line arguments,
appsettings, launch settings, package metadata, browser input, and API input
cannot select a sibling, nested, temporary, or alternate trust file. Tests use
only the internal test DI replacement seam.

- `scripts/PosSupportAgentDeployment.psm1` is the shared contract for identity,
  package validation, ownership/migration assessment, browser policy, certificate
  policy, lifecycle modes, and deterministic silent exit codes.
- `scripts/bootstrap-rms-support-agent.ps1` provides one bounded UAC handoff for
  unmanaged onboarding. It never recursively elevates or hides a declined UAC
  request.
- `scripts/publish-rms-support-agent-package.ps1` is the package-publication
  boundary. It packages only a bounded publish directory, computes the archive
  hash/size and exact file list, signs the deterministic canonical envelope
  with a pinned `Cert:\LocalMachine\My` code-signing certificate, and never
  exports or writes a private key.
- `scripts/install-rms-support-agent.ps1` provides `Install`, `Upgrade`,
  `Repair`, `Uninstall`, `Rollback`, and read-only `Status` modes. `-PlanOnly`
  and `-Status` are non-mutating. Mutation verifies the channel-specific
  signer thumbprint, Code Signing EKU, online chain/revocation policy,
  archive hash/size, exact archive file set, service ownership, certificate
  prerequisite, and both fixed health endpoints before reporting completion;
  absent or invalid machine trust still fails closed before SCM mutation.
- The typed C# platform mirrors the same boundary: LocalMachine-pinned
  signer resolution, deterministic length-delimited envelope signing,
  bounded staging, durable atomic checkpoints, retained rollback payload and
  archive, exact `RmsSupportAgent` SCM configuration, bounded restart recovery
  actions, fixed ACLs, certificate policy, and terminal live/ready health.
- Known historical services (`RmsSupportHub.Pos.Agent` and
  `RmsSupportHub.Pos.Int13.TestService`) can be considered only with matching
  path, display identity, package/marker evidence, and known Testing marker.
  Unknown or conflicting ownership is never adopted, removed, or overwritten.
  `RMS.BranchService`, `RMS.CashierService`, and `RMSServiceManager` remain
  RMS product services and are outside the migration catalog.
- Package policy requires schema 1, product identity, x64/arm64 architecture,
  matching channel/environment, Windows/net10.0-windows, LocalSystem, fixed
  service identity/description, SHA-256 metadata, bounded file manifests, and
  trusted signing evidence. The signed canonical envelope binds package
  metadata, archive hash/size, exact sorted files, ACL requirements,
  certificate requirements, release channel, and rollback metadata; JSON
  property order is not a trust input. Installed verification compares the complete manifest and
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

## PR #21 production trust-boundary remediation

The follow-up remediation keeps the accepted checkpoint-based rollback model
and closes the remaining trust-boundary findings in both the typed platform and
the PowerShell lifecycle:

- Production and Testing signer pins are mandatory in every valid trust
  document, regardless of deployment mode. Both must be JSON strings, non-empty,
  valid normalized 40-hex thumbprints, and cryptographically distinct. Equal
  pins, malformed pins, a Testing signer on a Production package, and any
  missing pin fail closed before trust authority is selected.
- Lifecycle mutations in both PowerShell and C# use `deploymentMode` from the
  protected machine-owned trust control file (exactly
  `%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`) as the release
  authority (`AgentMachineTrustConfiguration`, `MachineAgentTrustConfigurationLoader`,
  `Get-RmsSupportAgentMachineTrustConfiguration`). `deploymentMode` selects the
  active signer only. Process configuration (`PosAgent:ReleaseChannel` and
  `PosAgent:TrustConfigurationPath`), `IConfiguration`, environment variables,
  appsettings, launch settings, and the host environment cannot select, alter,
  or downgrade trust mode. Both obsolete keys are rejected by presence,
  including empty values, on normal Agent startup.
- The SDK OpenAPI document host is metadata-only. It builds endpoint metadata
  without loading the canonical trust file and without composing usable package
  trust, package activation, SCM, certificate lifecycle, rollback/recovery, or
  repair services; deliberate poison-pill descriptors prevent lifecycle service
  resolution in that host. Normal Agent startup without the canonical file still
  fails closed, and no synthetic trust snapshot is fabricated.
- `package-trust.json`, `agent-certificate.json`, and `lifecycle-state.json`
  are bounded, fixed-name, non-reparse control files. The file owner and ACL,
  plus every non-reparse ancestor back to the fixed service-owned security root,
  must be trusted, protected, and free of unsafe broad allow rules. The
  temporary test identity is limited to the explicit named fixture root and
  cannot authorize the ProgramData production root.
- Certificate readiness now includes typed evidence for the actual CNG key file:
  machine key/provider identity, non-exportable policy, fixed Microsoft CNG key
  root, trusted owner/protected ACL, and explicit `S-1-5-18` LocalSystem read
  access. Administrator-only access or broad key ACLs fail closed; the key is
  never exported.
- Package completion audit and incident-timeline records use the generated
  opaque operation instance ID, with the same correlation ID as the accepted
  operation. PowerShell lifecycle audit events carry one operation ID through
  started, attempted, accepted, completion/failure, rollback, and recovery
  outcomes. The static operation descriptor is not used as terminal identity.

The accepted rollback controls remain unchanged: recovery identity comes from
the durable checkpoint `PreviousVersion`; retained slots contain only signed
manifest/archive material; recovery always fresh-extracts and re-verifies;
health gates success; explicit rollback preserves a bounded recovery slot; and
H-1/H-2/H-3 remain enforced.

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
| Implemented | Slice C contracts, canonical package trust, machine-owned release mode in C# and PowerShell, control-file and LocalSystem key ACL boundaries, typed Windows lifecycle, fixed-root health, durable audit, Support Bundle, UI, tests, and generated client are in the repository. |
| Automated tested | C# focused trust/signer/lifecycle tests (55) and Agent trust/composition/OpenAPI tests (14), POS solution (410: Domain 12, Application 82, Infrastructure 153, Agent integration 163), PowerShell quality (29 files), full Pester (159), backend (194), and `build.ps1` all pass; the POS origin is supplied for Release validation. |
| Testing live-proven | Existing historical INT-13 evidence remains valid for the earlier Testing service model; this session did not claim a new elevated run. |
| Production signed | Not claimed. No representative Production signer, package publication, PKI issuance/renewal/revocation, or Production execution evidence is in this session. |
| Representative-machine proven | Not claimed. The current shell is not Administrator and no Production registry, certificate, SCM, RMS, Main Server, or database mutation was performed. |
| Fleet proven | Not claimed. Managed browser policy, fleet enrollment, inventory, and rollout evidence remain external gates. |
| Customer approved | Not claimed. Customer approval and the final independent Slice C review remain release gates. |
The next task is the full independent Production/Fleet Security and
Release-Readiness Review described in the root `TASK.md`. It must inspect the
delivered foundation and classify each remaining machine, CI, fleet, and
customer gate without starting Production rollout.
