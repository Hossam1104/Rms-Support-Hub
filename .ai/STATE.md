# Current Project State

- **Updated:** 2026-08-17
- **Active branch:** `feat/staging-environment-safety` (P0-A implementation
  `08a54a8`; M-1 remediation `500a8b3`; draft PR #23; based on
  `f24e2fef1818f6aea3655fc7255dd894a4b53e71`).
- **Status:** P0-A server-owned Testing/Staging environment authority is
  implemented and validated on this branch, including the Opus-required M-1
  fail-closed deployment-tier remediation (see below). The change is not
  merged and no external deployment or customer/Production mutation was
  performed.
- **Next task:** see `TASK.md` for the complete `CLAUDE OPUS 5 HIGH` bounded
  M-1 remediation re-review prompt. P0-B remains blocked on that review and
  explicit Sol acceptance.

## P0-A staging environment safety and M-1 remediation

- `SupportHub:DeploymentTier` is typed and startup-validated, defaulting to
  `Testing`. The API composition root maps registered module/environment keys
  to server-owned endpoint and database configuration. Testing policy rejects
  Production resolution before send, cancel, resend, lookup, Order Requests,
  DB/endpoint diagnostics, and health probing; Production registrations stay
  available only to a future explicit Production-tier deployment.
- Missing endpoint mappings or secrets project an environment unavailable
  without crashing unrelated surfaces; no secret value is stored in tracked
  files or DTOs. Browser contracts carry only registered keys -- no raw
  connection strings, probe URLs, or custom endpoint overrides. GHC resend is
  false; OMS/Call Center/unconfigured GHC-Uni-Commerce are truthful
  unavailable. Errors use the existing `{error:{code,message,details}}`
  envelope; health is a separate policy-aware projection.
- **M-1 (Opus review at `04304ed`, 0 Crit/0 High/1 Med/3 Low; Sol required
  fix before merge):** `Enum.TryParse`/`Enum.Parse<DeploymentTier>` accepted
  the enum's numeric representation, so `DeploymentTier=1` passed validation
  and resolved to Production. Fixed in `500a8b3`: added
  `DeploymentTierParser.TryParseExact` (`RmsSupportHub.Core.Modules`) as the
  single strict textual-allowlist authority (`Testing`/`Production`,
  case-insensitive, no numeric/whitespace/compound coercion), used by both
  `SupportHubOptionsValidator` and the `Program.cs` `IEnvironmentPolicy`
  factory so they cannot diverge. Any other value now fails startup; no
  default-to-Production, silent default-to-Testing, name inference, or
  request override exists.
- Validation on `500a8b3`: backend `252 passed / 0 failed` (206 baseline +
  46 new parser/validator/host-startup tests); frontend `362 passed / 0
  failed` across 59 files (unchanged); production frontend build, broad
  `dotnet build -c Release`, and `.\scripts\build.ps1` all passed;
  `context.py`/`check_memory.py`/`git diff --check` clean. Prior P0-A runtime
  probes (`08a54a8`) returned HTTP 200 frontend/module-catalog/health/proxy
  and HTTP 403 `environment_not_allowed` for the live Testing-tier Production
  probe; no live Production/customer mutation was performed.
- L-1 (URL safety completeness for optional endpoint templates), L-2
  (policy-free legacy `IOrderModule.GetEnvironment`/
  `ModuleEnvironmentResolver` path), and L-3 (connection string in branch-
  cache key) remain deferred non-blocking debt, untouched by M-1.
- PR #23 remains open/draft/unmerged, awaiting the bounded Opus 5 HIGH M-1
  re-review in `TASK.md` and Sol's merge decision. P0-B has not started.

## PR #22 acceptance and merge

- Opus 5 HIGH independent review: Critical 0, High 0, Medium 0, Low 3;
  ACCEPTED / APPROVE MERGE. Sol authorized merge; PR #22 merged to `main`.
- L-1 (dead constructors removed), L-2 (OpenAPI metadata-only isolation
  proved), L-3 (terminal audit events added to early-return paths), L-4
  (test-only trust fixture proved unreachable, negative tests added) all
  closed. Non-blocking backlog debt: LOW-1 (audit event asymmetry on
  unresolved-checkpoint path), LOW-2 (Pester terminology), LOW-3 (six
  early-return paths lack dedicated Pester tests).

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
