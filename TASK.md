# RMS Support Hub — POS Agent Deferred Hardening Pass (L-1 through L-4)

MODEL: (assign at session start)
EFFORT: MEDIUM
ROLE: Implement
MODE: in-repo hardening only — no Production/PKI/fleet/customer scope

## Background

PR #21 (Production POS Agent trust/lifecycle) passed independent security
review (0 Critical/High/Medium) and was merged to `main`. The review recorded
four non-blocking Low items as deferred backlog. See `.ai/STATE.md` section
"Deferred non-blocking hardening (Low, from Opus review)" and
`.ai/HISTORY.md` for full acceptance/merge evidence. Do not repeat that
review or reopen accepted trust-boundary decisions (ADR-0027); only close the
four items below.

## Objective

Resolve L-1 through L-4 in the POS Agent lifecycle/trust code, or explicitly
document why a given item is intentionally left as-is, with tests proving the
resolved behavior.

## Scope

- **L-1** — `pos/src/RmsSupportHub.Pos.Infrastructure/Packages/AgentPackageVerifier.cs`,
  `MachineCertificatePackageSignatureVerifier` parameterless constructor
  (currently performs a canonical deferred trust reload via
  `new MachineAgentTrustConfigurationLoader().Load()`, while shipped DI uses
  the immutable-snapshot constructor). Confirm no DI registration or call
  site uses the parameterless constructor; if none does, remove it. If one
  does, bind it to the same immutable startup snapshot instead of a deferred
  reload.
- **L-2** — obsolete-key rejection (`PosAgent:ReleaseChannel`,
  `PosAgent:TrustConfigurationPath`) is skipped inside the metadata-only
  OpenAPI composition host. Confirm metadata mode genuinely has no
  trust/lifecycle authority and does not consume either key; if confirmed,
  add a regression test asserting metadata-only composition never reads
  those keys, rather than changing composition behavior.
- **L-3** — some early-return PowerShell lifecycle failure paths in
  `scripts/PosSupportAgentDeployment.psm1` write started/attempted audit
  events without a terminal failed event (operation-ID correlation itself is
  correct). Identify each early-return path and add the missing terminal
  failed audit event so every started/attempted operation ID has a terminal
  outcome.
- **L-4** — the PowerShell `TestOnlyTrustFixture` contract property
  (`scripts/PosSupportAgentDeployment.psm1`,
  `scripts/tests/PosSupportAgentRollbackRecovery.Tests.ps1`) can represent an
  alternate test trust path, but the normal lifecycle entry point does not
  expose or select it. Confirm this is dead/test-only surface with no
  production entry point; if confirmed, either remove the unused property or
  add an explicit test proving the normal entry point cannot select it.

## Constraints

- Do not touch the accepted trust-boundary design (ADR-0027): canonical
  ProgramData trust path, mandatory distinct signer pins, immutable startup
  snapshot, metadata-only OpenAPI isolation must remain exactly as they are.
- Do not add configuration/environment/API surface for trust path selection.
- Stay within `pos/` and `scripts/` — no backend/frontend changes expected.
- If any item turns out to require Production/PKI/fleet evidence to close,
  stop on that item and record it back into `.ai/STATE.md` external gates
  instead of guessing.

## Validation

Run for the changed scope first:

```
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx -c Release --no-build --nologo
.\scripts\test-powershell-quality.ps1
Invoke-Pester -Path .\scripts\tests
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
git diff --check
```

Run `.\scripts\build.ps1` only if the change footprint grows beyond `pos/`
and `scripts/`.

## Delivery

New branch off `main` (e.g. `fix/pos-agent-deferred-hardening`). Open a PR
titled `fix(pos): close deferred L-1 through L-4 hardening items` with a
summary of which items were closed, which were left as documented no-ops,
and exact validation counts.

## Return

### Result
### L-1 / L-2 / L-3 / L-4 — outcome and evidence for each
### Validation
Exact counts (POS Domain/Application/Infrastructure/Agent integration/total,
PowerShell quality, Pester, memory, diff).
### Git
Branch, HEAD, PR (if opened).
### Remaining
