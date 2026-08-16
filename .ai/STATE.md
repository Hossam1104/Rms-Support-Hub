# Current Project State

- **Updated:** 2026-08-16
- **Active branch:** `feat/pos-production-agent-lifecycle`, based on merged
  Slice C baseline `0b380ef`.
- **Status:** Production-capable Agent package trust and lifecycle implementation
  is complete in the scoped repository. External Production, PKI, fleet, and
  customer evidence remains open.
- **Next task:** replace `TASK.md` with the full GPT-5.6 Terra HIGH independent
  Production/fleet security and release-readiness review prompt.

## Durable implementation facts

- The permanent product and SCM identity is `RmsSupportAgent`, display name
  `RMS Support Agent`, description `Local diagnostics, maintenance and repair
  agent for RMS Support Hub`, and account `LocalSystem`. Historical Testing
  services are migration inputs only; RMS product services are never adopted,
  removed, or controlled.
- Package publication is controlled by
  `scripts/publish-rms-support-agent-package.ps1`. It reads a bounded publish
  directory, signs a deterministic UTF-8 length-delimited envelope with a
  pinned `Cert:\LocalMachine\My` Code Signing certificate, and never exports
  private key material.
- C# and PowerShell trust paths independently bind channel/environment,
  machine-owned signer pins, archive hash/size, exact sorted file metadata,
  service identity, ACL/certificate requirements, and rollback metadata.
  Production cannot use the Testing signer or the injected no-chain seam.
- The typed Windows lifecycle uses the machine-wide
  `Global\RmsSupportHub.Pos.Agent.PrivilegedMutationLease`, fixed ACL roots,
  atomic lifecycle checkpoints, retained trusted rollback payload/archive,
  exact SCM configuration and bounded restart recovery actions. Terminal
  activation requires both `https://rms-pos-agent.localhost:5001/health/live`
  and `/health/ready` to return 200 over HTTPS without redirect following.
- The certificate prerequisite is read-only and requires the exact
  `rms-pos-agent.localhost` SAN, LocalMachine store, Microsoft Software Key
  Storage Provider, non-exportable key, Server Authentication EKU, private-key
  access, and a local or enterprise ownership marker. Enterprise certificates
  are never removed by the Agent lifecycle.
- `TASK.md` is intentionally left as the next independent Terra review prompt;
  this implementation turn does not claim representative-machine elevation,
  Production signing/PKI, fleet enrollment, or customer approval.

## Validation evidence

- POS Release build: 0 warnings, 0 errors with
  `PosAgentSecurity__SupportHubOrigin=https://support-hub.integration.test:4443`.
- POS solution tests: 345 passed, 0 failed (Domain 12, Application 80,
  Infrastructure 101, Agent integration 152).
- Focused package trust/lifecycle tests: 7 passed, 0 failed.
- PowerShell quality: 28 tracked scripts/modules parsed with no dangling
  operator continuations. Full Pester suite: 120 passed, 0 failed after the
  new package trust/lifecycle coverage.
- Existing backend/frontend baseline remains unchanged by this scoped task;
  no new frontend or backend source was modified.

## Runtime and delivery gates

- No Production, customer, RMS, Main Server, database, registry, certificate
  store, SCM, browser policy, or live package activation mutation was executed.
- No private key was exported or committed. A real representative-machine
  activation still requires separately authorized Testing evidence followed by
  independent Production/PKI/fleet/customer review.
