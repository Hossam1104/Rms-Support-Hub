# Current Project State

- **Updated:** 2026-08-17
- **Active branch at last session:** `fix/pos-agent-deferred-hardening`,
  based on `main` at PR #21's merge head `548ff98`. Not yet merged; open
  for independent review (see Deferred hardening section below).
- **Status:** Production-capable Agent package trust/lifecycle implementation
  (Slice C, ADR-0026/ADR-0027) is complete in scope and merged via PR #21.
  The four Low items PR #21 deferred (L-1 through L-4) are now closed in a
  follow-up branch/PR, pending independent Opus 5 HIGH review. External
  Production, PKI, fleet, and customer evidence remains open (see External
  release gates). This closure does not change the ADR-0027 trust boundary
  or claim Production/fleet readiness.
- **Next task:** see `TASK.md` for the next executable session (independent
  review of the L-1 through L-4 closure).

## PR #21 acceptance and merge

- Claude Opus 5 HIGH completed the final independent security review of PR #21
  at head `21256c9d` (base `main` `02d6e1f6`): Critical 0, High 0, Medium 0.
  Code/security accepted.
- GPT-5.6 Sol accepted that review and authorized merge; repository merge is
  not Production rollout approval (gated on external items below).
- Required CI (POS OpenAPI/Angular contracts, POS Windows build/Infra tests,
  portable projects, WinUI publish, PowerShell quality, Agent security
  foundation) verified green on the accepted head before merge.

## Deferred non-blocking hardening (Low, from Opus review) — closure status

Closure evidence is in branch `fix/pos-agent-deferred-hardening` (unmerged),
pending independent Opus 5 HIGH review per the current `TASK.md`.

- **L-1 (Closed):** removed the dead parameterless
  `MachineCertificatePackageSignatureVerifier` constructor (deferred trust
  reload) and its sole caller, the parameterless
  `WindowsAgentPackageInstallationPlatform` constructor. Added tests proving
  no public constructor can independently reload machine trust, and that
  the shipped verifier stays bound to the immutable startup snapshot even
  if `package-trust.json` is rewritten after startup.
- **L-2 (Closed, no code change):** confirmed metadata-only OpenAPI
  composition has no trust/lifecycle authority and never reads
  `PosAgent:ReleaseChannel`/`PosAgent:TrustConfigurationPath` — those keys
  appear only in the normal composition branch, and
  `AddMetadataOnlyLifecycleGuards` takes no `IConfiguration` parameter.
  Added regression tests instead of changing behavior.
- **L-3 (Closed):** added a terminal audit event (same `OperationId`) before
  every early-return path in `Invoke-RmsSupportAgentLifecycle` that
  previously lacked one (checkpoint, trust-rejected, not-installed,
  integrity, ownership, service-ownership, version-mismatch, certificate,
  and lease-busy paths). 10 new Pester tests prove one terminal outcome per
  OperationId; existing success/rollback terminals unchanged.
- **L-4 (Intentionally retained, with evidence):** confirmed
  `TestOnlyTrustFixture` is read only inside the non-exported
  `Get-RmsSupportAgentMachineTrustConfiguration` and set only via Pester
  `InModuleScope`; the normal contract-builder
  `Get-RmsSupportAgentDeploymentContract` never sets it. Retained (needed
  for trust unit tests without touching real `%ProgramData%`); added 3
  negative tests proving the normal entry point can't reach it.

## Durable implementation facts

- Sole normal C# package-trust authority is exactly
  `%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`, non-configurable
  from any config/env/CLI/API/browser input; deployment mode (from the same
  file) selects the mandatory, distinct, 40-hex Production/Testing signer pins
  in both C# and PowerShell. OpenAPI generation uses a metadata-only host with
  no trust/lifecycle authority; normal startup without canonical trust fails
  closed.
- Rollback/recovery resolves target identity from the durable checkpoint's
  `PreviousVersion`; retained slots hold only a signed manifest+archive,
  always re-extracted and re-verified before activation, and are health-gated
  before success. Explicit rollback preserves the current install into a
  bounded `recovery/` slot first. Trust-control files and their full ancestor
  path are ACL/ownership-verified before any sensitive value is consumed.
- Permanent product/SCM identity is `RmsSupportAgent` (`LocalSystem`); typed
  Windows lifecycle uses one machine-wide mutation lease, fixed ACL roots,
  atomic checkpoints, and requires both `/health/live` and `/health/ready`
  over HTTPS for terminal activation. Certificate prerequisite is read-only,
  requiring the exact `rms-pos-agent.localhost` SAN, non-exportable
  machine-key CNG storage, and actual LocalSystem private-key-file evidence.
- Package publication (`scripts/publish-rms-support-agent-package.ps1`) signs
  a deterministic envelope with a pinned `Cert:\LocalMachine\My` Code Signing
  certificate; no private key is ever exported.

## Validation evidence

- PR #21 final head (historical baseline): POS 410 passed (Domain 12,
  Application 82, Infrastructure 153, Agent integration 163); PowerShell
  Pester 159 passed/0 failed; backend 194 passed; `.\scripts\build.ps1` and
  frontend build passed; 0 warnings/errors throughout.
- L-1 through L-4 hardening pass (branch head, footprint bounded to `pos/`
  and `scripts/`, 7 files — broad `.\scripts\build.ps1` not re-run since
  nothing outside those paths changed): POS 420 passed/0 failed (Domain 12,
  Application 82, Infrastructure 155 [+2 L-1], Agent integration 171 [+2
  L-1, +6 L-2]); PowerShell Pester 172 passed/0 failed (+13: 10 L-3 + 3 L-4
  in `PosSupportAgentRollbackRecovery.Tests.ps1`); `context.py`/
  `check_memory.py`/`git diff --check` all clean.

## Runtime and delivery gates

- No Production, customer, RMS, Main Server, database, registry, certificate
  store, SCM, browser policy, or live package activation mutation was executed.
- No private key was exported or committed. A real representative-machine
  activation still requires separately authorized Testing evidence followed by
  independent Production/PKI/fleet/customer review.

## External release gates (unresolved, outside repository scope)

Real Production Code Signing signer; enterprise PKI issuance/renewal/
revocation; representative elevated Windows lifecycle execution;
representative LocalSystem CNG key ACL evidence; multiprocess H-3
contention evidence; managed Chrome/Edge and BackConnectionHostNames policy
evidence; fleet deployment/enrollment plan; customer/environment approval;
authorized Production execution window.

Repository merge of PR #21 does not authorize Production signer installation,
enterprise PKI changes, certificate-store mutation, SCM Agent activation on
customer machines, fleet browser-policy deployment, Production package
install/upgrade, RMS service changes, or Main Server/customer environment
mutation. Those remain separate evidence/approval gates.
