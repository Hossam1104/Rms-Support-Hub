# RMS Support Hub — Next Task: Slice C Production/Fleet Security Review
MODEL: GPT-5.6 Terra
EFFORT: HIGH
ROLE: REVIEW ONLY
MODE: independent security, release-readiness, and runtime-evidence review
## Objective
Independently review the implemented POS Slice C foundation and decide whether
it is ready for Production/fleet approval. This is review only: do not modify
source, tests, generated artifacts, docs, `.ai/` memory, Git, services,
certificates, registry, hosts, RMS folders, databases, package stores, or
runtime processes. Do not claim any test, machine/fleet proof, customer
acceptance, or Production readiness without direct evidence; current code/tests
are primary truth.
## Startup and scoped reading
1. Read `TASK.md`, `.ai/STATE.md`, and run `python .ai/scripts/context.py`.
2. Read `.ai/HANDOFF.md` only if `In Progress`/`Blocked`; read `.ai/PROJECT.md`
   and ADR-0026 only when stable context is needed.
3. Read only these scoped docs/contracts and task diff:
   `docs/POS_SLICE_C_IMPLEMENTATION.md`, `docs/POS_SLICE_C_REQUIREMENTS.md`,
   `docs/POS_MAINTENANCE_INTEGRATION_READINESS.md`,
   `docs/POS_MAINTENANCE_MIGRATION_INTAKE.md`,
   `docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`,
   `docs/POS_SLICE_B_BOUNDARY.md`, `docs/api-spec.md`,
   `pos/openapi/README.md`, and generated OpenAPI JSON.
4. Inspect the deployment scripts/Pester tests, Slice C
   Domain/Application/Infrastructure/Contracts files/tests, Agent
   OpenAPI/client transport, POS feature files/tests/styles, and
   `docs/design-system.md`. Do not read `.ai/archive/`, old transcripts, full
    history, or unrelated source.
## Review gates
### Identity and migration
Verify the sole permanent product/service/display identity is
`RmsSupportAgent` / `RMS Support Agent`; historical
`RmsSupportHub.Pos.Agent` and `RmsSupportHub.Pos.Int13.TestService` are inputs
only. Ownership must be proved before removing a disposable legacy service;
unknown/conflicting services fail closed; migration is idempotent; RMS product
services, especially `RMSServiceManager`, are never controlled or deleted.
### Bootstrap and lifecycle
Review `Status`, `PlanOnly`, `Install`, `Upgrade`, `Repair`, `Uninstall`, and
`Rollback`. Parameters must be bounded, UAC handoff at most once, no recursive
elevation, silent mode noninteractive, and exit codes must distinguish trust,
conflict, elevation, and validation failures. Missing trusted package/SCM/
certificate authority must stop safely rather than simulate success. No
arbitrary SCM, registry, certificate, filesystem, RMS, or Production mutation
may be reachable through unvalidated input.
### Package trust and rollback
Verify exact manifest product, schema, service, OS/runtime, architecture,
channel/environment pairing, signer, signature/hash algorithm, package
hash/size, exact file set, file hashes/sizes, safe paths, ACL/certificate
requirements, and rollback metadata. Installed verification must detect missing,
extra, unexpected, reparse, hash, size, manifest, and control-metadata drift.
Production requires an approved signer; Testing is explicit and non-Production;
rollback cannot select an untrusted or ambiguous package.
### Browser and certificate policy
Confirm exact Support Hub/Agent origins, no wildcard or `DisableLoopbackCheck`,
unrelated policy preservation, and supported Chrome/Edge minimum-version
branches. Certificate evidence must require exact Agent DNS, LocalMachine,
Microsoft Software Key Storage Provider, non-exportable key, ServerAuth EKU,
and explicit local/enterprise ownership. Enterprise certificates may be used
but never removed by local lifecycle code; renewal and cleanup are bounded and
fail closed.
### H-1/H-2/H-3 and audit
Re-check Slice B H-1 bounded redaction/quarantine, H-2 fixed service-owned
roots, and H-3 machine-wide mutation serialization across Slice C paths.
Durable JSONL audit must be bounded, sanitized, restart-readable, and record
accepted/final/cancelled/unknown outcomes for diagnostics, maintenance,
download, repair, snapshot, package, service, and Support Bundle operations.
Correlation/idempotency data must not leak principals, paths, credentials, raw
logs, or attachment contents.
### RMS health and Support Bundle
Confirm diagnostics use only fixed RMS setup/download/repository,
Branch/Cashier data/log, ProgramData, and insurance roots; traversal is bounded,
skips reparse escapes, and returns aggregate counts/bytes/timestamps/release
state without raw paths, filenames, log contents, or attachment bytes. Update
state is descriptive and safe, insurance aggregation bounded, and Support
Bundle sections deterministic, manifest-driven, redacted, and export-safe.
### API, contracts, and architecture
Verify authenticated `GET /api/v1/rms/operational-health`, synchronized
OpenAPI/TypeScript generation, non-Production runtime OpenAPI, and direct
`HttpBackend` Agent transport. Dependencies remain Domain/Application ->
Infrastructure -> Agent composition; no privileged Hub API relay exists. SQL
and `docs/database-schema.md` remain the database contract.
### UI, accessibility, performance
Review `/tools/pos-maintenance` as an operations console: exact secure-origin
handoff, command hierarchy, signal-to-action layout, fixed-root/update/
insurance summaries, loading/empty/error states, responsive behavior, keyboard
focus, semantic headings, screen-reader labels, contrast, reduced motion, dark
theme, token-only styles, lazy loading, and bundle/test impact. Report concrete
maintainability issues (including whether a child/shared component is warranted)
without redesigning during review.
## Safe validation and evidence
Run only relevant safe checks; do not mutate Production, Main Server, RMS
databases/folders, registry, certificates, SCM, or customer data. At minimum:
```powershell
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
.\scripts\test-powershell-quality.ps1 -SkipScriptAnalyzer
Invoke-Pester -Path .\scripts\tests
$env:PosAgentSecurity__SupportHubOrigin='https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx -c Release --no-build --nologo
npm test --prefix frontend -- --watch=false --no-progress
npm run build --prefix frontend -- --configuration production
git diff --check
```
Run the frontend suite twice when the first passes and verify client generation
is byte-stable on a second pass. Compare CI where available; separate new and
pre-existing failures. Use `.\scripts\dev.ps1` only for authorized local smoke
checks and report only responding URLs. Do not start Testing provisioning,
elevate, or run live H-3 proof unless separately authorized for a Testing
machine.
## Required output and stop boundary
Return only a concise evidence-backed report containing:
1. `Result`: Review Completed, Partially Completed, or Blocked.
2. `Blocking findings`: severity, file/line or command evidence, impact, exact remediation/evidence needed; `None` when none exists.
3. `Accepted controls`: identity/migration, trust, certificate/browser policy, H-1/H-2/H-3, audit, RMS health/insurance, Support Bundle, API/client, UI.
4. `Validation`: exact commands/counts, CI state, and unavailable elevation, network, package, PKI, fleet, or customer evidence.
5. `Production decision`: Ready, Not ready, or Ready only after named gates.
6. `Remaining`: only unresolved work or evidence gates.
Do not modify the repository or leave a plan/prompt in the active tree; the task ends at the independent review decision.
