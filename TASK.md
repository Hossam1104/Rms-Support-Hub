# POS Slice A — Final Operator Workspace and Diagnostic Evidence

**Executor:** GPT-5.6 Luna Max, High. **Role:** Implement / Validate / Deliver. One active agent; no replanning or delegation.

## Start and safety

Start from `main` after the reconnaissance PR. Read mandatory memory, `docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`, POS ADR-0015/16/18/21/22/23, and relevant Agent contracts/runtime/security/OpenAPI, RMS discovery/service/database code/tests, Angular transport/generated client/shared UI/POS component, and Testing boundaries.

Main Server GET is pre-authorized only when side-effect-free. Any POST/PUT/PATCH/DELETE or state-changing GET requires Owner approval before invocation. This slice needs no live mutation: do not call Branch/POS install/uninstall, setup, deploy, refresh, sync, cache-clear, package-download, or cancel. UPC alone is reachable; Whites is expected but unverified.

Testing is the only live environment. Never run real Restore, Cleanup, Branch Reset, service-failure generation, RMS executable, installer/uninstaller, DB mutation, or Production/customer action.

## Completed baseline and facts

Preserve PR #10 and earlier accepted secure Agent, exact loopback HTTPS/HTTP/1.1, Origin/CORS, Negotiate/local-Administrators authorization, fail-closed SID, generated client, typed service control, Branch/Cashier Backup/Restore/durable catalog, Downloader/Artifact Download, Cleanup/Branch Reset, one-use tokens/challenges, principal-scoped REST/SSE/artifacts, bounded idempotency/concurrency, and explicit not-attempted/partial/recovery/unknown truth.

Runtime facts: fixed `C:\ProgramData\RMS_Plus\ReleaseNumber.txt` is Product Release; Cashier UI `Settings:TheClient` is Client; component BuildNumbers are separate drift evidence. Actual SCM names are `RMS.BranchService`, `RMS.CashierService`, `RMSServiceManager` (current `RMSServicesManager` catalog spelling is defective). Fixed native logs plus SCM/.NET/Application Error/WER contain useful bounded exception/stack evidence. Safe Diagnostic Console Run, Main Server profiles/mutations, repair, and Agent packaging belong to Slice B.

## Objective

Deliver one vertical Slice A across Domain/Application/Infrastructure/Agent, OpenAPI/generated Angular types, UI, tests, and docs: authoritative Release/Client/drift, real Service Manager mapping, Health Check, DB/backup/capacity health, confined Failure Analyzer, Support Bundle, Incident Timeline, and final responsive workspace while preserving every completed operation.

## Backend and contract work

- Extend fixed-path `RmsInstallationDiscovery`; add `ReleaseNumberPath`. Trim/bound/reject controls; missing/unreadable/invalid means unavailable. Never substitute BuildNumber. Safely project Client; preserve identity/GUID/mode/endpoint labels and secret/path exclusion.
- Expose Product Release distinctly; add typed per-component drift with reason. Correct the server-owned catalog to `RMSServiceManager`, retain a friendly label, reconcile only conflicting legacy defaults, and prove configuration cannot widen the catalog or promote Testing services.
- Add Health states `healthy|warning|actionRequired|unknown`; unknown never becomes green/red. Return typed checks with safe code/state/summary/time/remediation for Agent/auth/authorization, identity/Client/Release/drift, Main/Branch connectivity, both DBs, three services, fixed-root capacity, backups/storage/catalog, consistency, and bounded recent critical errors.
- Reuse diagnostics/catalog. Fixed read-only SQL may return canonical DB/data-log allocation; no browser SQL/database, `DBCC`, integrity/repair/mutation. If Health records timeline state, use authorized exact-origin one-use-token POST with bounded idempotency, never state-changing GET.
- Failure Analyzer accepts only an opaque canonical service ID. Server catalogs resolve exact expected services, installed images, fixed configured log roots, and only SCM plus `.NET Runtime`, `Application Error`, WER providers. No browser path/file/provider/Event ID/XPath/regex/time range/command.
- Canonicalize roots, reject reparse escape, tolerate rotation, and bound time/files/records/bytes/lines/frames/retention. Redact connections, credentials/tokens/API keys/auth, DPAPI/PFX, UNC credentials, URL secrets, SIDs/users, and unbounded paths. Preserve useful exception/component/frame/time/service fields only when present.
- Analyzer returns typed category/severity/evidence/confidence/unknown reasons and non-executing recommendations (start/restart, backup, investigate drift, repair, bundle). It never launches a process or action.
- Build a bounded sanitized Timeline correlating service/crash/restart, errors, DB/network recovery, Health, Backup/Restore, Downloader/Artifact, Cleanup/Reset, and future repair/install kinds. Persistence, if used, is fixed-root/atomic/retained/bounded/corruption-fail-closed and not Production audit.
- Support Bundle is typed, authorized, one-use-token, opaque-artifact/authenticated-fetch. Include bounded sanitized identity/version/drift/DB/service/connectivity/health/log/event/analyzer/timeline/operation evidence; exclude raw config/connections/credentials/tokens/DPAPI/private keys/arbitrary paths/full events/unbounded logs.

## Mandatory UI rework

- Compact header: `POS Maintenance` and `Branch · POS · Release · Client`, plus Refresh. Compact peer statuses: POS Agent, Windows Auth/Authorization, Main Server, Configuration Consistency, Overall Health. Move Agent/API/OS metadata to Advanced Diagnostics.
- One installation panel: Branch, POS, Client, Product Release, Installation Mode, Installation GUID; component builds/detailed connectivity are advanced.
- Two equal primary DB cards: Branch/`RmsBranchSrv`, Cashier/`RmsCashierSrv`; show status, configured DB, safe server, backup count/latest/freshness/storage health. Authorized Backup, Restore, View Backups, Health are always visible; Restore is disabled with reason when no approved backup. Backups expand/collapse; remove “Recovery shelf”. Preserve exact confirmation/token/idempotency/progress/recovery/unknown behavior.
- Compact service table `Service | State | Diagnostics | Actions` for only the three real RMS services. Diagnose plus state/authorization-valid Start/Stop/Restart; preserve confirmation and outcome truth. Testing service appears only in Advanced Diagnostics in Testing.
- One maintenance area visibly retains functional Downloader/Artifact, Cleanup, Branch Reset and shows Deployment, Repair Installation, Guided Repair as `Planned — Slice B` (not fake controls). Add obvious Run Health Check, Diagnose Service Failure, Generate Support Bundle, View Incident Timeline.
- Accessible collapsible Advanced Diagnostics holds Agent/API/OS, component builds, detailed connectivity, safe config, legacy SQL evidence, Testing infrastructure. Replace secret-presence rows with safe statements such as `Branch DB configuration: Detected` and `Credentials: Available internally`.
- Validate 1440/1024/768/390: peer DB cards side-by-side desktop, stacked narrow; no overflow/blank cells/mismatched heights/buried actions. Preserve keyboard/focus/screen-reader/live-region/reduced-motion/theme/tokens/style budgets.

## Invariants and tests

Browser inputs remain bounded typed values/opaque IDs; never raw credentials, SQL, filesystem/log/process/service targets, commands/scripts/environment, Main Server URL/header/body, or arbitrary target identity. General Support Hub API is not a POS proxy. Preserve exact transport/auth/token/challenge/SSE/artifact/outcome boundaries. Long operations refresh from REST/SSE terminal truth; accepted is not complete. Downloads use authenticated fetch and sanitized filename; no path/token URL. Dependencies remain Domain/Core -> Application -> Infrastructure -> Agent. No raw colors outside token/gradient files.

Test Release/Client edge cases, drift, real/stale manager spelling, health/unknown precedence, DB/capacity/backups, root/reparse/rotation/event bounds, stack redaction, non-executing advice, timeline retention/corruption, bundle allowlist/secret/principal/expiry/replay/filename/outcome, authorized/partial UI, hierarchy/actions/confirmations/unknown/recovery/refresh/keyboard, and completed-operation regressions. Use synthetic seams only.

Retain full product scope in live docs: identity/Release/Client/drift/connectivity; DB diagnostics/Backup/Restore/catalog/Health/capacity/future integrity; service control/analyzer/future console; Downloader/Artifact/Cleanup/Reset/Deployment/Repair/Guided Repair/Safety Snapshot; Main Server profiles and Branch/POS state; Health/logs/events/timeline/bundle; peripherals/history/fleet; M-1/M-2/audit/package-ACL/live proof/Whites/Opus review.

## Validation and delivery

Run targeted checks, then:

```powershell
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx --nologo
Set-Location frontend
npm test -- --watch=false --no-progress
npm run build -- --configuration production
npm run generate:pos-agent-client
Set-Location ..
git diff --check
python .ai/scripts/check_memory.py
```

Prove a second generation pass byte-stable; run broad `scripts/build.ps1` when impact warrants. Inspect diff and scan secrets/private paths/logs/generated runtime artifacts. Verify real rendered responsive UI; report only responding endpoints. No live destructive or Main Server mutation evidence.

Commit intentional work on a feature branch, push/open normal PR, merge only after governance, sync `main --ff-only`, and leave clean. Runtime probing is last, Testing-only, using project-owned processes and `scripts/dev.ps1` when authorized. Update STATE/HISTORY, clear HANDOFF, preserve stable docs, and replace `TASK.md` with the full Slice B prompt after completion.

Return only `### Result`, `### Changes`, `### Validation`, `### Remaining` per `AGENTS.md`.
