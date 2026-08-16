# ADR-0027: POS Agent production package trust and transactional lifecycle

## Status

Accepted — 2026-08-16

## Decision

The permanent POS Agent package boundary has a real machine-owned trust and
activation path. The package manifest is not its own trust root: the channel
and environment select only a signer thumbprint pinned in LocalMachine-owned
configuration, and the package's display signer metadata can never select a
certificate. Production always requires a purpose-constrained Code Signing
certificate with explicit online chain/revocation validation. Testing may use
an injected non-chain test seam only when the operation is explicitly Testing.

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
EKU, private-key access, and an explicit local or enterprise ownership marker.
Local lifecycle code never issues, imports, replaces, or removes an enterprise
certificate.

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
