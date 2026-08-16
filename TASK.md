# RMS Support Hub - Final Production Agent Lifecycle Security & Release-Readiness Review
MODEL: GPT-5.6 Terra
EFFORT: HIGH
ROLE: REVIEW ONLY
MODE: independent security, release-readiness, and runtime-evidence review

## Objective
Independently review the implemented Production POS Agent trust/lifecycle for Production/fleet approval. Code and tests are primary truth; do not claim machine, fleet, customer, PKI, signing, or Production evidence without direct evidence.
Review-only boundary: do not modify source/tests/artifacts/docs, `.ai/`, Git, services, certificates, registry, hosts, RMS folders, databases, package stores, or runtime processes.

## Mandatory startup
1. Read `TASK.md` and `.ai/STATE.md`.
2. Run `python .ai/scripts/context.py`.
3. Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`.
4. Read `.ai/PROJECT.md`, `.ai/DECISIONS.md`, and ADR-0027 only when affected architecture or decision rationale is required.
5. Inspect only the scoped implementation, tests, docs, and task diff. Do not read `.ai/archive/`, old transcripts, full Git history, or unrelated source.

## Scoped review set
- Docs: `docs/POS_SLICE_C_IMPLEMENTATION.md`, `docs/POS_SLICE_C_REQUIREMENTS.md`, `docs/POS_MAINTENANCE_INTEGRATION_READINESS.md`, `docs/POS_MAINTENANCE_MIGRATION_INTAKE.md`, `docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`, `docs/POS_SLICE_B_BOUNDARY.md`, `docs/api-spec.md`, `pos/openapi/`.
- Scripts/tests: `scripts/PosSupportAgentDeployment.psm1`, `scripts/publish-rms-support-agent-package.ps1`, `scripts/install-rms-support-agent.ps1`, `scripts/bootstrap-rms-support-agent.ps1`, and `scripts/tests/`.
- POS Domain/App: `AgentPackageModels.cs`, `AgentProductIdentity.cs`, `AgentPackagePolicy.cs`, `AgentPackageCanonicalizer.cs`, `AgentPackageVersion.cs`.
- POS Infra: `AgentMachineTrustConfiguration.cs`, `MachineAgentTrustConfigurationLoader.cs`, `AgentPackageVerifier.cs`, `AgentPackageLifecycle.cs`, `AgentPackageOptions.cs`, `AgentPackageTrustOptions.cs`, `AgentCertificatePrerequisite.cs`, `AgentPackageLifecycleStateStore.cs`, `WindowsAgentServiceController.cs`, and focused tests.

## Review gates
### 1. Permanent identity and migration
Verify sole permanent identity `RmsSupportAgent` / `RMS Support Agent`, fixed description, `LocalSystem`, automatic start. Historical services are inputs only; ownership proved before adoption/removal, unknown conflicts fail closed, migration idempotent, and `RMS.BranchService`, `RMS.CashierService`, `RMSServiceManager` never controlled/deleted.

### 2. Signer trust and canonical envelope
Verify package cannot select signer. Production/Testing use separate machine-owned pins; Production requires Code Signing EKU, valid dates, strict chain, Online revocation. Testing cannot satisfy Production; no no-chain seam can downgrade Production. C#/PowerShell envelopes are deterministic, length-delimited, and bind metadata/hash/files/ACLs/channel. Publication never exports/commits private keys.

### 3. Install, upgrade, repair, rollback, health, uninstall
Review `Status`, `PlanOnly`, `Install`, `Upgrade`, `Repair`, `Rollback`, `Uninstall`: bounded roots/parameters; single UAC elevation; silent operation; distinct outcomes; no trust/ACL bypass. Exact extraction, reparse/traversal rejection, staging re-verification, atomic checkpoints, recovery-required on interruption, previous payload/archive retention, trusted rollback, health-gated commit, safe idempotency, and uninstall retaining audit/trust/policy.

### 4. Windows service and certificate platform
SCM access fixed to `RmsSupportAgent`; binary path is single quoted approved executable; display/account/recovery verified. HTTPS prerequisite requires `rms-pos-agent.localhost` SAN in LocalMachine, Microsoft KSP, non-exportable key, Server Auth EKU, LocalSystem key-file ACL proof, local/enterprise marker.

### 5. Health, audit, and H-1/H-2/H-3
Re-check H-1 redaction, H-2 fixed roots, H-3 mutation lease across API/C#/PowerShell. Terminal success requires HTTPS `/health/live` and `/health/ready` loopback 200 without redirects. Audit is bounded, sanitized JSONL distinguishing attempted/completed/failed/recovery outcomes without secrets.

### 6. Existing Agent/API and repository boundaries
Direct browser-to-Agent transport remains exact loopback HTTPS/HTTP/1.1/Negotiate with server-derived auth; Hub API is not a relay; dependencies Core -> Data -> API / Domain/App -> Infra -> Agent; OpenAPI/client synchronized; contracts verified against fixtures.

### 7. Browser, RMS, UI, and release evidence
Exact origins, no wildcard, supported browsers, fixed RMS health/insurance boundaries, Support Bundle redaction, accessible token-only UI, unchanged direct handoff. Separate repo tests from unavailable Production signer, PKI, elevated Windows activation, fleet policy, and customer approval.

## PR #21 remediation re-review points

PR #21 remediation addresses Terra findings without changing accepted rollback.
The next independent Terra HIGH review must explicitly re-check these points:

1. Production and Testing signer pins are distinct after canonical
   normalization, and equal/case/whitespace variants fail closed.
2. Machine-owned release/deployment mode (`package-trust.json` `deploymentMode`)
   is the only lifecycle authority in both C# and PowerShell
   (`AgentMachineTrustConfiguration`, `MachineAgentTrustConfigurationLoader`,
   `Get-RmsSupportAgentMachineTrustConfiguration`); caller-selected `-Channel Testing`
   or process config is never a Production downgrade.
3. Missing, malformed, or invalid mode cannot fall back to Testing, a caller
   assertion, process config, an environment variable, or package metadata.
   Obsolete `PosAgent:ReleaseChannel` is rejected on Agent startup.
4. Every security-control file has fixed-path, bounded-size, no-reparse,
   trusted-owner, protected-ACL, and no-unsafe-allow validation.
5. Every ancestor from the control file to the defined service-owned security
   root has the required ownership, protected ACL, and no-reparse proof.
6. The temporary test-fixture identity escape hatch is bounded to the named
   fixture root and cannot authorize Production control files or runtime paths.
7. The HTTPS certificate prerequisite proves actual LocalSystem access to the
   non-exportable CNG key file and rejects admin-only or broad ACL evidence.
8. Terminal package audit and incident-timeline records use the generated
   lifecycle operation instance ID and preserve the operation correlation ID.
9. Previous-version rollback identity, signed manifest/archive retention,
   fresh re-extraction/re-verification, health-gated recovery, explicit
   rollback recovery, durable checkpoints, H-1, H-2, and H-3 remain intact.

## Safe validation
Run relevant read-only checks only. Do not start Testing provisioning, elevate, touch Production, mutate Main Server/RMS/database/registry/certificates/SCM, or run customer operations.
```powershell
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
.\scripts\test-powershell-quality.ps1 -SkipScriptAnalyzer
Invoke-Pester -Path .\scripts\tests
$env:PosAgentSecurity__SupportHubOrigin='https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx -c Release --no-build --nologo
git diff --check
```
If broader checks run, report exact commands and distinguish new/pre-existing failures. Report only endpoints that responded.

## Required output and stop boundary
Return only:
### Result
`Review Completed`, `Partially Completed`, or `Blocked`.
### Blocking findings
For each Critical/High finding give severity, file/line or command evidence, impact, and exact remediation/external evidence required. Write `None` when absent; list Medium/Low separately.
### Accepted controls
Summarize identity/migration, trust/envelope, lifecycle/rollback, service, certificate/browser policy, H-1/H-2/H-3, audit, health/insurance, Support Bundle, API/client, UI, and evidence classification.
### Validation
List exact commands, counts, warnings/errors, CI state if available, and unavailable elevation/network/package/PKI/fleet/customer evidence.
### Production decision
Return `Ready`, `Not ready`, or `Ready only after named gates`.
### Remaining
List only unresolved code findings or external evidence gates. Do not modify the repository or create another active plan/prompt.
