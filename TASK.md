# RMS Support Hub — GPT-5.6 Terra HIGH
# Final Production Agent Lifecycle Security & Release-Readiness Review

MODEL: GPT-5.6 Terra
EFFORT: HIGH
ROLE: REVIEW ONLY
MODE: independent security, release-readiness, and runtime-evidence review

## Objective

Independently review the implemented Production POS Agent trust/lifecycle for
Production/fleet approval. Code and tests are primary truth; do not claim
machine, fleet, customer, PKI, signing, or Production evidence without direct
evidence. Do not modify source, tests, artifacts, docs, .ai, Git, services,
certificates, registry, hosts, RMS folders, databases, package stores, or
runtime processes during the review.

## Mandatory startup

1. Read TASK.md and .ai/STATE.md.
2. Run python .ai/scripts/context.py.
3. Read .ai/HANDOFF.md only when In Progress or Blocked.
4. Read .ai/PROJECT.md, .ai/DECISIONS.md, and ADR-0027 only when affected.
5. Inspect only scoped implementation, tests, docs, and task diff; do not read
   .ai/archive/, old transcripts, full Git history, or unrelated source.

## Explicit remediation re-review gates

Verify all of the following:

- exact canonical trust path only:
  %ProgramData%\DBS\RmsSupportAgent\Trust\package-trust.json
- no PosAgent:TrustConfigurationPath, PosAgent__TrustConfigurationPath, or
  other environment/config trust-path override; no Production custom-path API
- no package/browser/API trust-path input and no signer verifier trust-file reread
- missing canonical trust fails normal startup
- synthetic OpenAPI trust is removed; OpenAPI is metadata-only
- metadata host cannot resolve/use privileged lifecycle services or create a
  synthetic AgentMachineTrustConfiguration
- both signer pins are mandatory in C# and PowerShell, strings, non-empty,
  normalized 40-hex, and distinct; deploymentMode selects the active signer
- immutable startup snapshot is complete/non-null
- PowerShell operation ID is identical across all applicable started, attempted,
  accepted, completed, failed, rollback attempted/succeeded/failed, and recovery
  required audit events
- obsolete PosAgent:ReleaseChannel and PosAgent:TrustConfigurationPath keys are
  rejected by presence, including empty values
- previously accepted rollback, certificate, H-1, H-2, and H-3 controls remain
  intact

Production/fleet evidence remains external and incomplete unless directly
verified. DO NOT RUN TERRA THIS SESSION.

## Validation

Run:

python .ai/scripts/context.py
python .ai/scripts/check_memory.py
.\scripts\test-powershell-quality.ps1
Invoke-Pester -Path .\scripts\tests
$env:PosAgentSecurity__SupportHubOrigin='https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx -c Release --no-build --nologo
dotnet test backend/tests/RmsSupportHub.Tests/RmsSupportHub.Tests.csproj --nologo
.\scripts\build.ps1
npm run build --prefix frontend -- --configuration production
git diff --check

Report exact focused counts for canonical path, alternate path rejection,
configuration/environment injection, missing canonical trust, OpenAPI
metadata-only isolation, privileged lifecycle absence, complete C# signer
snapshot, complete PowerShell signer snapshot, PowerShell operation ID, and
obsolete empty config keys. No skips, timeout inflation, or weakened tests.

If RmsSupportHub.Api locks backend DLLs, identify the exact verified
project-owned PID. Never globally kill dotnet; stop only that verified API if
necessary. Restore normal project runtime last.

## Delivery

Stay on feat/pos-production-agent-lifecycle. Commit to existing PR #21 with
fix(pos): seal final Agent trust authority gaps, push, update the PR description
with current counts, and wait for GitHub CI. Require all six POS CI jobs green,
especially POS OpenAPI and Angular contract generation, without installing or
fabricating machine trust.

PR #21 must remain OPEN, DRAFT, and UNMERGED. Do not mark ready or merge.

## Return

Return:

### Result
### High 1 — canonical trust authority
### High 2 — OpenAPI metadata isolation
### High 3 — complete signer snapshot
### Medium — PowerShell audit correlation
### Low — obsolete config presence rejection
### Focused tests
Exact counts.
### Full validation
PowerShell quality, Pester, Domain, Application, Infrastructure, Agent
integration, POS total, backend, broad build, frontend production build,
memory, and diff.
### CI
All six jobs on the new head.
### Documentation
### Git
Commit, new PR HEAD, OPEN, DRAFT, UNMERGED.
### Remaining
