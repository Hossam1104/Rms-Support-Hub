# RMS Support Hub - Final Production Agent Lifecycle Security & Release-Readiness Review
MODEL: GPT-5.6 Terra
EFFORT: HIGH
ROLE: REVIEW ONLY
MODE: independent security, release-readiness, and runtime-evidence review

## Objective
Independently review the implemented Production-capable POS Agent package trust and lifecycle and decide whether it is ready for Production/fleet approval. Current code and tests are primary truth. Do not claim machine, fleet, customer, PKI, signing, or Production evidence without direct evidence.
Review-only boundary: do not modify source, tests, generated artifacts, docs, `.ai/` memory, Git, services, certificates, registry, hosts, RMS folders, databases, package stores, or runtime processes.

## Mandatory startup
1. Read `TASK.md` and `.ai/STATE.md`.
2. Run `python .ai/scripts/context.py`.
3. Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`.
4. Read `.ai/PROJECT.md`, `.ai/DECISIONS.md`, and ADR-0027 only when affected architecture or decision rationale is required.
5. Inspect only the scoped implementation, tests, documentation, and task diff below. Do not read `.ai/archive/`, old transcripts, full Git history, or unrelated source.

## Scoped review set
- Docs: `docs/POS_SLICE_C_IMPLEMENTATION.md`, `docs/POS_SLICE_C_REQUIREMENTS.md`, `docs/POS_MAINTENANCE_INTEGRATION_READINESS.md`, `docs/POS_MAINTENANCE_MIGRATION_INTAKE.md`, `docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`, `docs/POS_SLICE_B_BOUNDARY.md`, `docs/api-spec.md`, and `pos/openapi/`.
- Scripts/tests: `scripts/PosSupportAgentDeployment.psm1`, `scripts/publish-rms-support-agent-package.ps1`, `scripts/install-rms-support-agent.ps1`, `scripts/bootstrap-rms-support-agent.ps1`, and relevant `scripts/tests/`.
- Domain/application: `AgentPackageModels.cs`, `AgentProductIdentity.cs`, `AgentPackagePolicy.cs`, `AgentPackageCanonicalizer.cs`, and `AgentPackageVersion.cs`.
- Infrastructure: `AgentPackageVerifier.cs`, `AgentPackageLifecycle.cs`, `AgentPackageOptions.cs`, `AgentPackageTrustOptions.cs`, `AgentCertificatePrerequisite.cs`, `AgentPackageLifecycleStateStore.cs`, `WindowsAgentServiceController.cs`, Agent package/audit DI/service files, and focused trust/lifecycle tests.

## Review gates
### 1. Permanent identity and migration
Verify the sole permanent identity is `RmsSupportAgent` / `RMS Support Agent`, with the exact fixed description, `LocalSystem`, and automatic start. Historical services are inputs only; ownership is independently proved before adoption/removal, unknown same-name conflicts fail closed, migration is idempotent, and `RMS.BranchService`, `RMS.CashierService`, and `RMSServiceManager` are never controlled or deleted.

### 2. Signer trust and canonical envelope
Verify a package cannot select its signer. Production and Testing use separate machine-owned pins; Production always requires Code Signing EKU, Digital Signature usage where present, valid dates, strict chain validation, and Online revocation. Testing cannot satisfy Production and no no-chain seam can downgrade Production. Check LocalMachine source, fixed trust config, ACL/reparse handling, safe failures, and that signer metadata is not trust authority.
Verify C# and PowerShell canonical envelopes are deterministic and parity-compatible. They must bind product/schema/service metadata, version/platform, channel/environment, archive hash/size, sorted exact file paths/logical names/sizes/hashes/required flags, ACL/certificate requirements, and rollback fields; JSON order and culture must not affect signatures. Publication is bounded and atomic and never exports or commits a private key.

### 3. Install, upgrade, repair, rollback, health, uninstall
Review `Status`, `PlanOnly`, `Install`, `Upgrade`, `Repair`, `Rollback`, and `Uninstall`: fixed bounded roots/parameters; at most one UAC elevation; silent noninteractive operation; distinct trust/conflict/elevation/validation/busy/recovery outcomes; and no bypass of trust, ownership, certificate, or ACL gates. Confirm exact archive/file-set extraction, reparse/traversal rejection, staging re-verification, atomic checkpoints, recovery-required interruption behavior, previous payload/manifest/archive retention, semantic-version rules, trusted rollback, health-gated commit, safe idempotency, and uninstall limited to the owned service/install root while retaining audit, trust, browser policy, and enterprise certificate state. No arbitrary SCM, registry, certificate, RMS, Main Server, database, Production, or customer mutation may be input-reachable.

### 4. Windows service and certificate platform
Confirm typed SCM access is fixed to `RmsSupportAgent`; binary path is one quoted approved executable with no arguments; display/description/account/start type/state/restart recovery are verified; and no generic service/path control exists. Confirm the read-only HTTPS prerequisite requires exact `rms-pos-agent.localhost` in LocalMachine, Microsoft Software Key Storage Provider, non-exportable key, Server Authentication EKU, valid dates, private-key access, and explicit local or enterprise ownership. Uninstall/rollback must not remove enterprise certificates.

### 5. Health, audit, and H-1/H-2/H-3
Re-check H-1 bounded redaction/quarantine, H-2 fixed service-owned roots, and H-3 machine-wide mutation serialization across API, C#, and PowerShell. Terminal success requires HTTPS `/health/live` and `/health/ready` on the exact loopback origin with no redirect or TLS bypass. Audit is bounded, sanitized, restart-readable JSONL distinguishing attempted, accepted, completed, failed, rollback-succeeded, rollback-failed, unknown, and recovery-required outcomes, without paths, credentials, raw logs, or private material.

### 6. Existing Agent/API and repository boundaries
Confirm direct browser-to-Agent transport remains exact-origin HTTPS/HTTP/1.1/Negotiate with server-derived authorization and typed one-use mutation controls; the Hub API is not a privileged relay; dependencies remain Domain/Application -> Infrastructure -> Agent composition; OpenAPI/client artifacts are synchronized; and SQL/payload contracts were not guessed or changed by lifecycle work.

### 7. Browser, RMS, UI, and release evidence
Re-check exact Hub/Agent origins, no wildcard or `DisableLoopbackCheck`, supported Chrome/Edge branches, unrelated policy preservation, fixed RMS health/insurance boundaries, Support Bundle redaction, operations-console accessibility, token-only styles, reduced-motion behavior, and unchanged direct Agent handoff. Separate repository implementation/tests from unavailable Production signer, enterprise PKI, elevated Windows activation, fleet/managed policy, Whites comparison, customer approval, and Production/customer execution evidence.

## Safe validation
Run only relevant read-only checks. Do not start Testing provisioning, elevate, touch Production, mutate Main Server/RMS/database/registry/certificates/SCM, or run customer operations.
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
If broader checks run, report exact commands and distinguish new from pre-existing failures. Report only endpoints that actually responded.

## Required output and stop boundary
Return only:
### Result
`Review Completed`, `Partially Completed`, or `Blocked`.
### Blocking findings
For each Critical/High finding give severity, file/line or command evidence, impact, and exact remediation or external evidence required. Write `None` when there are no such findings; list Medium/Low separately.
### Accepted controls
Summarize identity/migration, trust/envelope, lifecycle/rollback, service, certificate/browser policy, H-1/H-2/H-3, audit, health/insurance, Support Bundle, API/client, UI, and evidence classification.
### Validation
List exact commands, counts, warnings/errors, CI state if available, and unavailable elevation/network/package/PKI/fleet/customer evidence.
### Production decision
Return `Ready`, `Not ready`, or `Ready only after named gates`.
### Remaining
List only unresolved code findings or external evidence gates. Do not modify the repository or create another active plan/prompt during the review.
