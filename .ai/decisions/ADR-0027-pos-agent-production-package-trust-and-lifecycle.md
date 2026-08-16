# ADR-0027: POS Agent production package trust and transactional lifecycle

## Status

Accepted — 2026-08-17

## Decision

The permanent POS Agent package boundary has a real machine-owned trust and
activation path. The package manifest is not its own trust root: the effective
release mode comes from the exact canonical file
`%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`, and that path is
not configurable by `IConfiguration`, environment variables, command-line
arguments, appsettings, launch settings, package metadata, browser input, or
API input. The channel/environment select only the active signer thumbprint
from the immutable startup snapshot. Every valid trust file must contain both
Production and Testing pins as non-empty strings that normalize to distinct
40-hex values. The package's display signer metadata and the caller's requested
channel can never select or downgrade a certificate. Production always requires
a purpose-constrained Code Signing certificate with explicit online
chain/revocation validation. Testing may use an injected non-chain test seam
only when the protected operation mode is explicitly Testing.

Normal Agent startup resolves and validates this complete snapshot before the
host is usable. The obsolete `PosAgent:ReleaseChannel` and
`PosAgent:TrustConfigurationPath` keys are rejected by presence, including
empty values. The SDK OpenAPI document host is a metadata-only composition: it
does not create trust or usable package/SCM/certificate/rollback/repair
lifecycle services and uses poison-pill descriptors only where endpoint
metadata requires service parameter types. It never fabricates trust.

Security-control files (`package-trust.json`, `agent-certificate.json`, and
`lifecycle-state.json`) are fixed-name, bounded, non-reparse files whose own
owner/ACL and every ancestor back to the fixed service-owned root are verified
before their contents are consumed. The named test-fixture identity allowance
is confined to the explicit fixture root and cannot authorize the ProgramData
Production root.

The signed payload is a deterministic UTF-8 length-delimited envelope. It
binds the product/service identity, version and platform, channel/environment,
archive hash and size, sorted exact file paths/hashes/sizes/required flags,
ACL and certificate requirements, and rollback metadata. JSON property order
and serializer behavior are not part of the signature contract. The publisher
uses a LocalMachine private key directly and never exports or commits it.

The Windows platform owns one fixed SCM service, `RmsSupportAgent`, with the
exact display name, description, LocalSystem account, automatic start, quoted
single executable path, and bounded restart recovery actions. Install,
upgrade, repair, rollback, health, and uninstall are serialized by the
machine-wide Agent mutation lease. Staging is exact and re-verified; updates
retain a trusted previous payload, manifest, and archive; a durable atomic
checkpoint marks incomplete work as recovery-required; terminal success
requires both HTTPS loopback health endpoints. Uninstall removes only the
owned service/install boundary and retains audit, trust, browser policy, and
enterprise certificate state.

The HTTPS certificate prerequisite remains separate from package signing. It
requires the exact `rms-pos-agent.localhost` SAN in LocalMachine, Microsoft
Software Key Storage Provider, non-exportable private key, Server Authentication
EKU, explicit LocalSystem access to the actual CNG key file with bounded ACL
evidence, and an explicit local or enterprise ownership marker. Administrator
access alone or a broad key ACL is not proof. Local lifecycle code never
exports, issues, imports, replaces, or removes an enterprise certificate.

Terminal package audit and incident-timeline records use the generated opaque
operation instance ID and its correlation ID, rather than the static operation
descriptor. The accepted checkpoint/`PreviousVersion` rollback identity,
signed manifest/archive retention, fresh re-verification, health gate, explicit
recovery slot, and H-1/H-2/H-3 controls remain part of this decision.

## Evidence boundary

Repository builds, focused crypto/lifecycle tests, Pester, and static review
prove the implementation seams and fail-closed behavior. They do not prove a
Production signer, enterprise PKI issuance/renewal/revocation, an elevated
representative Windows machine, fleet policy enrollment, customer approval,
or Production execution. Those remain independent release gates.

## Consequences

- Trust rejection occurs before SCM or certificate mutation, while a
  provisioned approved package can use the same lifecycle boundary for real
  activation.
- Rollback and interrupted-operation recovery have explicit durable state
  instead of inferring ownership from directory presence.
- The package publication tool is a controlled release boundary, not a source
  of private-key material or a way to bypass Production trust.
- The next independent Terra review must inspect both the C# platform and the
  PowerShell boundary and classify external evidence separately from tests.
