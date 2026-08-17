# Current Project State

- **Updated:** 2026-08-17
- **Active branch:** `feat/staging-environment-safety` (P0-A implementation;
  based on `f24e2fef1818f6aea3655fc7255dd894a4b53e71`).
- **Status:** P0-A server-owned Testing/Staging environment authority is
  implemented and validated on this branch. The change is not merged and no
  external deployment or customer/Production mutation was performed.
- **Next task:** see `TASK.md` for the complete `CLAUDE OPUS 5 HIGH`
  independent review prompt. P0-B remains blocked on that review and explicit
  acceptance.

## P0-A staging environment safety

- `SupportHub:DeploymentTier` is typed and startup-validated, defaulting to
  `Testing`; the API composition root maps registered module/environment keys to
  server-owned endpoint and database configuration.
- Testing policy rejects Production environment resolution before send, cancel,
  resend, lookup, Order Requests, database diagnostics, endpoint diagnostics,
  or health probing. Production registrations remain available only to a future
  explicitly configured Production-tier deployment.
- Missing endpoint mappings or server secrets project an environment as
  unavailable without preventing unrelated application surfaces from starting.
  Secrets remain external configuration; no secret value is stored in tracked
  files or returned by DTOs.
- Browser request contracts carry only registered module/environment keys.
  Raw connection strings, arbitrary probe URLs, and custom endpoint overrides
  are not accepted as authority. GHC resend is false; OMS, Call Center, and
  unconfigured GHC/Uni-Commerce environments remain truthful unavailable
  registrations.
- Safe environment/downstream codes are handled through the existing
  `{ error: { code, message, details } }` envelope. Health keeps a separate
  policy-aware projection and never probes a Testing-tier Production target.
- Validation on this branch: backend `206 passed / 0 failed`; frontend `362
  passed / 0 failed` across 59 files; production build passed; broad build and
  final runtime evidence remain to be recorded before handoff.

## PR #22 acceptance and merge

- Claude Opus 5 HIGH completed independent security review of PR #22: Critical 0,
  High 0, Medium 0, Low 3. Decision: ACCEPTED / APPROVE MERGE.
- GPT-5.6 Sol accepted that review and authorized merge. PR #22 merged to `main`.
- L-1 through L-4 hardening results:
  - **L-1 (Closed):** Removed dead parameterless constructors in
    `MachineCertificatePackageSignatureVerifier` and
    `WindowsAgentPackageInstallationPlatform`. Shipped verifier stays bound to
    immutable startup snapshot.
  - **L-2 (Closed):** Proved metadata-only OpenAPI composition has no trust
    authority and ignores obsolete config keys.
  - **L-3 (Closed with accepted caveat):** Terminal audit events added to all
    early-return paths under existing `OperationId`.
  - **L-4 (Safely retained):** Proved normal lifecycle cannot reach test-only
    `TestOnlyTrustFixture`; negative tests added.
- Non-blocking backlog debt (Low findings from Opus review, not merge blockers):
  - **LOW-1:** Audit event asymmetry on unresolved-checkpoint path
    (`recovery_required` vs `recovery_required` + `failed`).
  - **LOW-2:** Terminal-outcome terminology in Pester test helper vs accepted
    timeline event semantics.
  - **LOW-3:** Six newly audited early-return paths lack dedicated Pester unit
    tests (coverage debt only).

## Durable implementation facts

- Sole normal C# package-trust authority is
  `%ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json`, non-configurable
  from config/env/CLI/API/browser. Mandatory distinct 40-hex Production/Testing
  signer pins in C# and PowerShell.
- OpenAPI host is metadata-only with no trust/lifecycle authority; normal startup
  without canonical trust fails closed.
- Rollback/recovery resolves target identity from checkpoint `PreviousVersion`;
  retained slots hold signed manifest+archive only, re-extracted and
  re-verified before activation, health-gated before success. Explicit rollback
  preserves current install to `recovery/`. Security-control files and ancestors
  are ACL/ownership-verified.
- SCM identity is `RmsSupportAgent` (`LocalSystem`); typed Windows lifecycle uses
  one mutation lease, fixed ACL roots, atomic checkpoints, HTTPS `/health/live`
  and `/health/ready` gates. Certificate prerequisite requires exact
  `rms-pos-agent.localhost` SAN, non-exportable CNG machine key, and LocalSystem
  private key ACL evidence.

## Validation baseline

- Merged `main` validation: POS 420 passed/0 failed (Domain 12, Application 82,
  Infrastructure 155, Agent integration 171); PowerShell quality 29 files clean;
  Pester 172 passed/0 failed; Backend 194 passed/0 failed; `context.py` and
  `check_memory.py` clean.
- All six CI workflows passed on PR #22 head `1c401b409c0f986336a9a676d4c95fd79bf0c7a6`.

## External release gates (unresolved, outside repository scope)

Real Production Code Signing signer; enterprise PKI issuance/renewal/
revocation; representative elevated Windows lifecycle execution;
representative LocalSystem CNG key ACL evidence; multiprocess H-3
contention evidence; managed Chrome/Edge and BackConnectionHostNames policy
evidence; fleet deployment/enrollment plan; customer/environment approval;
authorized Production execution window.

Repository merge does not authorize Production signer installation, PKI
mutation, customer SCM activation, fleet browser-policy deployment, or RMS/
Main Server live mutation.
