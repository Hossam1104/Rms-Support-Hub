# POS Downloader / Deployment + Cleanup / Maintenance

## Role and scope

**Role:** `Implement`
**Recommended executor:** GPT-5.6 Luna Max or Gemini 3.7 Flash (large, mostly
mechanical wiring task across existing, already-tested Application/
Infrastructure code — well suited to a high-throughput implementation model).
**Repository:** `Hossam1104/Rms-Support-Hub`
**Base:** `main` at `615fda2` (PR #9 merged — RMS installation discovery and
typed Branch/Cashier database backup/restore are complete; do not modify
`RmsDatabaseEndpoints.cs`, `RmsDatabaseOperationRuntime.cs`,
`RmsDatabaseBackupCatalog.cs`, `RmsDatabaseBackupStorage.cs`, or the
Restore/Backup preflight logic unless a concrete defect is found there).

This is a real implementation task, not a planning task. Produce working
code, tests, and validation evidence directly on a new branch off `main`.

## Context (read before starting)

Two capabilities already exist as fully implemented, tested Application/
Infrastructure code but are **not wired to any Agent HTTP endpoint**:

1. **Downloader** — pulls RMS branch/cashier backup artifacts from the store
   network over SMB for the legacy WinUI multi-branch tool.
   - `pos/src/RmsSupportHub.Pos.Application/Services/DbDownloadService.cs`
   - `pos/src/RmsSupportHub.Pos.Infrastructure/Http/{BackupApiClient,BackupApiEndpointPolicy}.cs`
   - `pos/src/RmsSupportHub.Pos.Infrastructure/Smb/{SmbBackupRepository,SmbConnectionScope,SmbPathResolver}.cs`
   - Domain: `pos/src/RmsSupportHub.Pos.Domain/Models/{AgentDownloaderConfiguration,DbDownloaderSettings,DownloaderExecutionResult,DownloaderFailureCodes,DownloaderInputPolicy}.cs`, `Interfaces/{IBackupApiClient,IBackupRepository,IDownloaderDelay}.cs`
   - Contracts already modeled: `pos/src/RmsSupportHub.Pos.Contracts/V1/Downloader/*.cs`, `V1/Configuration/{DownloaderConfigurationUpdateRequestDto,RedactedDownloaderConfigurationDto}.cs`
   - Tests: `pos/tests/RmsSupportHub.Pos.Application.Tests/{DbDownloadServiceTests,DownloaderBackendTests}.cs`, `pos/tests/RmsSupportHub.Pos.Infrastructure.Tests/DownloaderSecurityTests.cs`

2. **Maintenance / Cleanup** — cleanup preview/execute and branch-reset
   preview/execute over local RMS installations.
   - `pos/src/RmsSupportHub.Pos.Application/Maintenance/{MaintenanceService.cs,MaintenancePathPolicy.cs,MaintenanceWorkflowModels.cs}`
   - `CleanupService.cs` is a compatibility facade whose doc comment already
     claims "the Agent uses `MaintenanceService` directly" — that statement is
     currently **aspirational, not true**; nothing in
     `pos/src/RmsSupportHub.Pos.Agent/**` references either class today. Fix
     the comment once the Agent actually owns this, or correct it if the
     final design differs.
   - Contracts already modeled: `pos/src/RmsSupportHub.Pos.Contracts/V1/Maintenance/*.cs` (Cleanup preview/execute, BranchReset preview/execute, `MaintenanceItemState`, outcome DTOs)

3. **Generic artifact download** — `ArtifactCatalog` (in-memory,
   `RuntimeRetentionPolicy.ArtifactLifetime` = 24h) is registered in
   `Program.cs` but deliberately has no mapped HTTP endpoint yet. See the
   comment at `pos/src/RmsSupportHub.Pos.Agent/Program.cs` near the
   `ArtifactCatalog` registration: "no HTTP artifact endpoint is mapped in
   this session." A browser-facing download route (e.g.
   `GET /api/v1/artifacts/{id}`) is the missing piece that lets Downloader
   results (and any other `ArtifactCatalog`-registered file) actually reach
   the operator. Do not reuse or modify `RmsDatabaseBackupCatalog` for this —
   that catalog is deliberately scoped only to database backups (see
   ADR-0022 consequences in `.ai/decisions/ADR-0022-typed-rms-database-recovery.md`).

Deployment/provisioning tooling (publish, install-as-service, idempotent
start/stop, ownership-checked cleanup of Testing manifest files) already
exists in root `scripts/` (`setup-pos-agent-testing.ps1`,
`start-pos-agent-testing.ps1`, `remove-pos-agent-testing.ps1`,
`PosAgentWindowsProvisioning.psm1`, `PosSupportHubProvisioning.psm1`,
`PosTestingConfiguration.psm1`, `publish-iis.ps1`). That is Testing-only
provisioning of the Agent process itself, distinct from this task's in-Agent
runtime artifact/download surface — do not conflate the two; only touch the
scripts if a genuine deployment-packaging gap blocks this slice.

`RmsSupportHub.Pos.Agent/Endpoints/` currently has `Configuration`, `Device`,
`RmsDatabase`, `Rms`, and `Service` endpoint files. There is no
`Downloader`/`Maintenance`/`Artifact` endpoints file. Use the existing
`RmsDatabaseEndpoints.cs` / `ServiceEndpoints.cs` as the reference pattern for
authorization, mutation tokens, idempotency, typed problem details, and
progress/outcome shape — this programme has been consistently typed,
opaque-artifact, fail-closed, and audit-evidenced across every prior POS
slice (see ADR-0015, ADR-0016, ADR-0018, ADR-0021, ADR-0022); do not introduce
a generic SQL/filesystem/command/credential surface to close this gap faster.

## Acceptance criteria

- Wire Downloader execution behind a typed Agent route: browser supplies only
  a bounded trigger request (no raw SMB path, credential, or connection
  string); the Agent owns `DbDownloadService` orchestration, SMB
  configuration, and backup-API endpoint policy. Redact
  `DownloaderConfigurationUpdateRequestDto`/`RedactedDownloaderConfigurationDto`
  the same way existing configuration reads are redacted.
- Wire Maintenance cleanup preview/execute and branch-reset preview/execute
  behind typed Agent routes, following the existing preview-then-confirm
  pattern used by Restore (exact confirmation text / explicit execute call
  after preview, one-use mutation token, bounded idempotency key).
- Add a bounded artifact-download endpoint for `ArtifactCatalog` entries
  (opaque ID only, principal-scoped or explicitly justified if not,
  Content-Disposition/Content-Type set safely, no path traversal, expired/
  missing artifacts return 404 not a stack trace).
- Principal-scoped REST progress (and SSE if consistent with the existing
  `RmsDatabaseOperationRuntime` pattern) for any long-running Downloader/
  Maintenance operation; ambiguous outcomes surface as `outcomeUnknown` and
  are never auto-retried, matching the Restore precedent.
- Preserve every existing security invariant: Windows Negotiate, exact-origin
  CORS, server-resolved local Administrators authorization, fail-closed SID
  handling, one-use target/path/method-bound mutation tokens, no credential/
  connection-string/raw-path crossing into the browser.
- Regenerate `pos/openapi/RmsSupportHub.Pos.Agent.json` and the Angular client
  (`npm run generate:pos-agent-client` in `frontend/`); add or extend the
  Support Hub `/tools/pos-maintenance` UI only as far as needed to exercise
  the new routes end-to-end (or state explicitly in the handoff if UI is
  deferred to a follow-up slice).
- Add focused tests mirroring the existing `RmsDatabaseEndpointTests.cs` /
  `ServiceActionEndpointTests.cs` style: success, unauthorized, forbidden,
  invalid/replayed/expired mutation token, idempotent replay, concurrent
  dispatch, ambiguous-outcome, and artifact-not-found/expired paths.
- Do not touch the just-merged RMS database backup/restore preflight,
  catalog, or retention logic; do not weaken filesystem ACLs; do not run
  against Production or any real customer RMS installation.

## Non-goals

- Do not redesign the RMS database backup/restore contract (ADR-0022) — it is
  complete and was just corrected for three recovery-blocking review defects.
- Do not change unrelated business/API/SQL contracts.
- Do not run against Production, control real customer services, or mutate
  real customer RMS databases.
- Do not commit, push, or merge without owner-equivalent validation evidence
  (build, targeted tests, full POS suite, frontend tests/build, OpenAPI/
  client drift check, `git diff --check`, secret scan) — same bar as the
  prior two POS slices.
- M-1 (managed browser policy) and M-2 (Production certificate lifecycle)
  remain explicit external gates; do not attempt to resolve them in this
  slice.

## Mandatory startup

1. Read `TASK.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HANDOFF.md` only if its status is `In Progress` or `Blocked`
   (currently `Empty`).
5. Read `DbDownloadService.cs`, `MaintenanceService.cs`,
   `MaintenancePathPolicy.cs`, `CleanupService.cs`, the existing Downloader/
   Maintenance contracts and their tests, `RmsDatabaseEndpoints.cs`,
   `RmsDatabaseOperationRuntime.cs`, `ServiceEndpoints.cs`, and
   `pos/src/RmsSupportHub.Pos.Agent/Program.cs` (DI registrations) before
   writing any endpoint code.
6. Read `.ai/decisions/ADR-0015-separate-pos-agent-trust-boundary.md`,
   `ADR-0016-pos-browser-transport-security-boundary.md`,
   `ADR-0021-pos-service-control-mutation-runtime.md`, and
   `ADR-0022-typed-rms-database-recovery.md` for the established security
   pattern this task must extend consistently.
7. Read `.ai/PROJECT.md` only if broader architectural context beyond the
   above is genuinely needed.

## Validation

- `dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo -warnaserror`
  (requires `PosAgentSecurity__SupportHubOrigin=https://support-hub.integration.test:4443`
  in the environment — see `.github/workflows/pos-ci.yml`).
- `dotnet test pos/RmsSupportHub.Pos.slnx --nologo` — full suite, no
  regressions against the current 306-test baseline.
- `npm test` and `npm run build -- --configuration production` in `frontend/`.
- OpenAPI/client regeneration with `git diff --exit-code` against the
  committed contract artifacts.
- `git diff --check`; scan the diff for hardcoded secrets/credentials/
  connection strings before committing.
- If live Testing verification is attempted, it requires an elevated
  Administrator session on the representative Testing machine (the current
  session's non-elevated token could not open Agent/RMS services via SCM —
  see `.ai/STATE.md`); do not attempt privilege-escalation workarounds.

## Completion response

Return only:

### Result
Completed, Partially Completed, or Blocked.

### Changes
Concise task-related changes, by file.

### Validation
Commands executed and results.

### Remaining
Only unresolved work, blockers, or risks.
