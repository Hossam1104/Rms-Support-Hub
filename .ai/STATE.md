# Current Project State

- **Updated:** 2026-08-17
- **Active branch at last session:** `feat/pos-production-agent-lifecycle`,
  merged to `main` via PR #21 (head `21256c9`). See PR #21 acceptance below.
- **Status:** Production-capable Agent package trust/lifecycle implementation
  (Slice C, ADR-0026/ADR-0027) is complete in scope and merged to `main`.
  External Production, PKI, fleet, and customer evidence remains open (see
  External release gates).
- **Next task:** see `TASK.md` for the next executable session.

## PR #21 acceptance and merge

- Claude Opus 5 HIGH completed the final independent security review of PR #21
  at head `21256c9d59d5e0295e51fdc7f3704a60665eea4` (base `main`,
  `02d6e1f62fd8aa4c463cbe42d946f449360f2de`): Critical 0, High 0, Medium 0.
  Code/security accepted.
- GPT-5.6 Sol accepted that review and authorized merge. Production/fleet
  rollout remains gated on the external items below; repository merge is not
  Production rollout approval.
- Required CI verified green on the accepted head: POS OpenAPI and Angular
  contract generation, POS Windows build and Infrastructure tests, POS
  portable projects, Retained WinUI publish validation, Support Hub PowerShell
  quality gate, Windows Agent security foundation.
- PR #21 was merged to `main` using the repository's normal merge strategy
  after a documentation/state-only closure commit recording this acceptance.

## Deferred non-blocking hardening (Low, from Opus review)

- **L-1:** Parameterless `MachineCertificatePackageSignatureVerifier`
  constructor performs a deferred trust reload; shipped DI uses the
  immutable-snapshot constructor instead. No production impact.
- **L-2:** Obsolete-key rejection is skipped inside metadata-only OpenAPI
  composition; metadata mode has no trust/lifecycle authority and consumes
  neither key.
- **L-3:** Some early-return PowerShell lifecycle failures write
  started/attempted audit events without a terminal failed event.
  Operation-ID correlation itself is correct.
- **L-4:** PowerShell `TestOnlyTrustFixture` contract property can represent
  an alternate test trust path; the normal lifecycle entry point does not
  expose or select it.

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
  machine-key CNG storage, and actual LocalSystem private-key-file access
  evidence.
- Package publication (`scripts/publish-rms-support-agent-package.ps1`) signs
  a deterministic envelope with a pinned `Cert:\LocalMachine\My` Code Signing
  certificate; no private key is ever exported.

## Validation evidence (PR #21 final head)

- POS Release build: 0 warnings/errors with
  `PosAgentSecurity__SupportHubOrigin=https://support-hub.integration.test:4443`.
- POS solution tests: 410 passed (Domain 12, Application 82, Infrastructure
  153, Agent integration 163); focused trust/composition tests 55 + 14 passed.
- PowerShell quality: 29 tracked scripts/modules parsed; full Pester 159
  passed, 0 failed, 0 skipped.
- Backend: `dotnet test backend/tests/RmsSupportHub.Tests` 194 passed;
  `.\scripts\build.ps1` and frontend production build passed; backend Release
  build 0 warnings/errors.

## Runtime and delivery gates

- No Production, customer, RMS, Main Server, database, registry, certificate
  store, SCM, browser policy, or live package activation mutation was executed.
- No private key was exported or committed. A real representative-machine
  activation still requires separately authorized Testing evidence followed by
  independent Production/PKI/fleet/customer review.

## External release gates (unresolved, outside repository scope)

1. Real Production Code Signing signer.
2. Enterprise PKI issuance/renewal/revocation process.
3. Representative elevated Windows lifecycle execution.
4. Representative LocalSystem CNG key ACL evidence.
5. Multiprocess H-3 contention evidence.
6. Managed Chrome/Edge and BackConnectionHostNames policy evidence.
7. Fleet deployment/enrollment plan.
8. Customer/environment approval.
9. Authorized Production execution window.

Repository merge of PR #21 does not authorize Production signer installation,
enterprise PKI changes, certificate-store mutation, SCM Agent activation on
customer machines, fleet browser-policy deployment, Production package
install/upgrade, RMS service changes, or Main Server/customer environment
mutation. Those remain separate evidence/approval gates.
