# Current Project State

- **Updated:** 2026-08-17
- **Active branch:** `feat/pos-production-agent-lifecycle`, based on merged
  Slice C baseline `0b380ef`. PR #21 remains DRAFT/unmerged.
- **Status:** Production-capable Agent package trust/lifecycle implementation is
  complete in scope, including rollback/recovery hardening and the final PR #21
  trust remediation below. External Production, PKI, fleet, and customer
  evidence remains open.
- **Next task:** run the full GPT-5.6 Terra HIGH independent Production/fleet
  security and release-readiness review already in `TASK.md`.

## Rollback/recovery hardening (this pass)

- Automatic rollback/recovery now resolves the retained slot and target
  identity from the durable checkpoint's `PreviousVersion`, never from the
  failed operation's incoming manifest.
- Retained slots (`rollback/`, new `recovery/`) hold only a signed manifest
  and archive, never a raw copied installation directory. Restoration always
  re-extracts into a new unique staging directory and re-verifies
  signature/hash/traversal (`AgentPackageArchiveStaging`, `VerifyRecoveryAsync`)
  before activation, identically in C# and PowerShell
  (`Save-`/`Restore-RmsSupportAgentRetainedSlot`).
- Rollback/recovery success is health-gated (`VerifyHealthAsync` /
  `Test-RmsSupportAgentHealth`) before returning success and clearing the
  checkpoint; failure returns `RollbackFailed`/`RecoveryRequired` and keeps
  the checkpoint as recovery evidence.
- Explicit rollback preserves the CURRENT installation into a bounded
  one-operation `recovery/` slot before its destructive mutation, so a failed
  explicit rollback can restore the pre-rollback version.
- Trust-control files (`package-trust.json`, `agent-certificate.json`,
  `lifecycle-state.json`) are ACL/ownership-verified
  (`Test-RmsSupportAgentControlFileTrust` /
  `ServiceOwnedDirectoryProvisioner.IsTrustedControlFile`) before any
  security-sensitive value is consumed, in both C# and PowerShell.
- The control file itself and every ancestor back to the fixed service-owned
  root are bounded, non-reparse, owner-verified, protected, and free of unsafe
  broad allow rules. The temporary test identity is confined to the named test
  fixture root and cannot authorize the ProgramData production root.

## PR #21 production trust-boundary remediation

- The sole normal C# package-trust authority is exactly
  `%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`; the path is not
  configurable from configuration, environment, command line, appsettings,
  launch settings, package input, browser input, or API input. Tests replace
  the loader only through the test-only DI seam.
- Production and Testing signer pins are mandatory, string, non-empty,
  normalized 40-hex values and must be distinct in both C# and PowerShell.
  Equal, malformed, missing, or ambiguous trust values fail closed before
  either pin becomes authority; deployment mode selects the active signer only.
- Lifecycle mutation mode in both C# and PowerShell comes exclusively from the
   protected machine-owned canonical `package-trust.json` `deploymentMode` snapshot
  (`AgentMachineTrustConfiguration`, `MachineAgentTrustConfigurationLoader`).
  Process config (`PosAgent:ReleaseChannel`), `IConfiguration`, environment
  variables, appsettings, launch settings, and host environment cannot decide or
  alter trust mode; `PosAgent:ReleaseChannel` and
  `PosAgent:TrustConfigurationPath` are rejected by presence, including empty
  values, on startup.
- OpenAPI document generation uses a metadata-only host. It does not create
  synthetic trust or compose usable Agent package trust, activation, SCM,
  certificate, rollback/recovery, or repair lifecycle services; normal startup
  without canonical trust fails closed.
- Certificate readiness carries typed actual CNG key-file ACL evidence,
  including Microsoft provider, machine-key, non-exportable policy, fixed key
  root, protected owner/ACL, and explicit LocalSystem read access. Admin-only
  access and broad grants fail closed; no key is exported.
- Terminal package audit and incident timeline use the generated opaque
  operation instance ID and preserve the operation correlation ID.

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
  Storage Provider, non-exportable key, Server Authentication EKU, actual
  LocalSystem private-key-file access evidence, and a local or enterprise
  ownership marker. Enterprise certificates are never removed by the Agent
  lifecycle.
- `TASK.md` is the next independent Terra review prompt. This implementation
  turn does not claim representative-machine elevation, Production signing/PKI,
  fleet enrollment, or customer approval.
## Validation evidence
- POS Release build: 0 warnings, 0 errors with
  `PosAgentSecurity__SupportHubOrigin=https://support-hub.integration.test:4443`.
- POS solution tests: 410 passed, 0 failed, 0 skipped (Domain 12,
  Application 82, Infrastructure 153, Agent integration 163). Focused trust /
  composition tests: 55 infrastructure and 14 Agent integration tests passed.
- PowerShell quality: all 29 tracked scripts/modules parsed; full Pester 159
  passed, 0 failed, 0 skipped.
- Backend: `dotnet test backend/tests/RmsSupportHub.Tests` 194 passed, 0 failed;
  `.\scripts\build.ps1` and the explicit frontend production build passed;
  backend Release build was 0 warnings, 0 errors.

## Runtime and delivery gates

- No Production, customer, RMS, Main Server, database, registry, certificate
  store, SCM, browser policy, or live package activation mutation was executed.
- No private key was exported or committed. A real representative-machine
  activation still requires separately authorized Testing evidence followed by
  independent Production/PKI/fleet/customer review.
