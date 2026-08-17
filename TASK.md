# CLAUDE OPUS 5 HIGH — Independent Review — POS Agent Deferred Hardening L-1 through L-4

MODEL: Claude Opus 5, effort HIGH
EFFORT: HIGH
ROLE: Review
MODE: review only — do not modify, commit, push, merge, or mark the PR ready

## Background

PR #21 (Production POS Agent trust/lifecycle) previously passed independent
security review (0 Critical/High/Medium) and was merged to `main` at head
`548ff98`. That review deferred four non-blocking Low items (L-1 through
L-4). A follow-up branch, `fix/pos-agent-deferred-hardening` (based on
`main` at `548ff98`), closes all four items and is open as an UNMERGED PR
titled `fix(pos): close deferred L-1 through L-4 hardening items`. See
`.ai/STATE.md` section "Deferred non-blocking hardening (Low, from Opus
review) — closure status" and the matching `.ai/HISTORY.md` entry for the
implementer's own account of what changed and why. Treat those as claims to
verify, not as ground truth.

## Objective

Independently review the L-1 through L-4 closure diff (branch
`fix/pos-agent-deferred-hardening` vs. `main` at `548ff98`) for correctness,
completeness, and — most importantly — confirm it does not reopen or weaken
any previously accepted trust-boundary decision (ADR-0027) or any other
control PR #21 already closed. Do not repeat the full PR #21 review; focus
on this diff and its interaction with the existing accepted controls.

## Scope to verify

- **L-1**: the `MachineCertificatePackageSignatureVerifier` and
  `WindowsAgentPackageInstallationPlatform` parameterless constructors were
  removed. Confirm they were genuinely unreachable dead code (no DI
  registration/test/runtime path used them), confirm no replacement
  construction path independently reloads machine trust, and confirm the
  shipped composition still resolves the verifier from the immutable
  startup snapshot only.
- **L-2**: no production code changed. Confirm the added tests actually
  prove metadata-only OpenAPI composition has no trust/lifecycle authority
  and never reads `PosAgent:ReleaseChannel` / `PosAgent:TrustConfigurationPath`
  (including the empty-value Theory cases), and that the normal
  composition branch's obsolete-key rejection is untouched.
- **L-3**: every early-return path added in
  `Invoke-RmsSupportAgentLifecycle` (`scripts/PosSupportAgentDeployment.psm1`)
  now writes a terminal audit event before returning, reusing the
  operation's `OperationId`. Verify exactly one terminal outcome per
  started/attempted OperationId (no duplicates, no gaps), trust/integrity/
  ownership/certificate/lease failures each produce a terminal `failed` (or
  pre-existing `recovery_required`), pre-existing success/rollback-success
  terminals are unchanged, and no path writes a false `completed` over
  `recovery_required`.
- **L-4**: `TestOnlyTrustFixture` was retained rather than removed. Verify
  the normal entry point (`Invoke-RmsSupportAgentLifecycle` via
  `Get-RmsSupportAgentDeploymentContract`) truly cannot set, expose, or
  reach this property via any parameter/env var/config key, and that the
  new negative tests actually exercise that claim.

## Constraints

- Review only. Do not edit files, run `git commit`/`git push`, merge the PR,
  or mark it ready for review/auto-merge.
- Do not reopen or re-litigate the ADR-0027 trust boundary itself (canonical
  ProgramData trust path, mandatory distinct signer pins, immutable startup
  snapshot, metadata-only OpenAPI isolation) — only confirm this diff leaves
  it intact. If you find it does not, that is a finding, not a design
  discussion to resolve here.
- Do not claim Production/fleet/PKI/customer readiness; that remains
  out of scope per `.ai/STATE.md` external release gates.

## Suggested approach

1. Read `.ai/STATE.md` and `.ai/HISTORY.md` for the claimed closure summary.
2. Read every changed line via `git diff main...fix/pos-agent-deferred-hardening`
   (or the PR diff): `AgentPackageVerifier.cs`, `AgentPackageLifecycle.cs`,
   the three touched test files under `pos/tests/`,
   `scripts/PosSupportAgentDeployment.psm1`, and
   `scripts/tests/PosSupportAgentRollbackRecovery.Tests.ps1`.
3. For each of L-1 through L-4, independently confirm the claim (grep for
   other call sites, trace control flow, re-derive reachability) rather
   than trusting the summary.
4. Re-run validation and confirm the reported counts below still hold.
5. Regression-check adjacent accepted controls: canonical trust path
   exclusivity, dual mandatory signer pins, immutable snapshot binding, no
   synthetic OpenAPI trust, rollback/recovery checkpoint integrity,
   H-1/H-2/H-3, certificate prerequisite boundary.

## Validation to reproduce

```
git fetch && git checkout fix/pos-agent-deferred-hardening
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx -c Release --no-build --nologo
.\scripts\test-powershell-quality.ps1
Invoke-Pester -Path .\scripts\tests
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
```

Expected (implementer-reported, verify independently): POS 420 passed/0
failed (Domain 12, Application 82, Infrastructure 155, Agent integration
171); PowerShell quality 29 files clean; Pester 172 passed/0 failed.

## Return

### Result
Critical/High/Medium/Low counts and overall accept/request-changes verdict.
### L-1 / L-2 / L-3 / L-4
Per item: confirmed closed, closed with caveats, or not closed — with
concrete evidence (file:line).
### Regression check
Confirmation (or findings) on each adjacent accepted control listed above.
### Validation
Commands run and results, with any discrepancy from the reported counts.
### Remaining
Findings requiring a fix, and anything still open outside this diff's scope.
