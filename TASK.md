# WPF-05 - Logs, Safe Diagnostic Evidence, and Support Bundle

MODEL: Implementation and validation executor
ROLE: Implement
AUTHORITY: GPT-5.6 Sol remains Planner, Architect, and Acceptance Authority
PROGRAMME: POS Dual Control-Surface Architecture (CR-001 / ADR-0029)
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/wpf-05-logs-support-bundle`
EPIC: E17 - WPF Standalone Local Operations (#13018)
PRIMARY STORY: US-E17-05 - Logs and safe Support Bundle (#13035)
STATUS: Implemented and locally validated; Draft PR delivery and Sol acceptance remain pending

## Baseline and implementation truth

WPF-05 started from accepted WPF-04 main `0b9d0b678cfb33a3828876fb0a980fa8fdeb7676` (PR #35). WPF-01/#32, WPF-02/#33, WPF-03/#34, and WPF-04/#35 are merged; WPF-03 main was `803dc85c60bc3662f78b041c8c99499195656e08`.
Logs flow: `WPF -> rms.logs.evidence` typed Local IPC -> shared `LogEvidenceQueryHandler` -> existing `ServiceFailureAnalyzer`/evidence reader.
The Agent owns three fixed RMS services, safe mapping, authorization, timeout, bounds, and response validation. Local operators/admins may read; remote and unauthenticated callers fail closed.
Each service is capped at 12 records, 8 unknown-source reasons, and 4 recommendations; timeout is 15 seconds. Sensitive text, paths, credentials, connection strings, raw log payloads, and caller-selected files never cross IPC.
Logs are collected only on Logs workspace open or explicit Refresh; 30-second health polling does not collect detailed evidence. WPF provides safe service/severity filters.
Support Bundle flow: `WPF -> support.bundle.generate` typed Local IPC -> `SupportBundleExecutor` -> existing `SupportBundleService`/`ArtifactCatalog`/audit.
Generation is local-administrator-only; WPF does not initiate UAC. The executor verifies principal/correlation, uses the existing fixed-root redacted/bounded generator, audits once, revokes on audit failure, and then records the timeline.
WPF receives only validated opaque metadata: artifact ID/display name, size, checksum/unavailable, created, expiry, correlation ID, and included sections. ZIP bytes, server paths, and export/download destinations are not exposed; the UI says export is deferred.
WPF has no Agent or Infrastructure reference, filesystem reader, SQL connection, HTTPS client, process launch, service/database mutation, backup/restore, cleanup/reset, package, repair, Hub/SignalR, fleet, Production, provisioning, or Online Order work.

## Validation

- Restore: `dotnet restore pos/RmsSupportHub.Pos.slnx` passed.
- Strict Release solution build with Testing-only `PosAgentSecurity__SupportHubOrigin=https://localhost:4443`: 0 warnings, 0 errors.
- Release tests: Domain 12/12, Application 116/116, Infrastructure 155/155, Agent Integration 238/238, WPF 57/57; total 578/578.
- Focused WPF-05: Application 6/6, WPF view-model 5/5, WPF IPC 4/4, optional-handler fail-closed 1/1, existing HTTP Support Bundle regressions 5/5.
- PowerShell quality 37/37; Pester 172/172 passed, 0 failed, 0 skipped, 0 pending.
- `git diff --check` passed. `python .ai/scripts/context.py` and
  `python .ai/scripts/check_memory.py` passed after the final task-state edits.

## Live Azure truth - read 2026-08-23

- E16 #13017 Active/P2: #13022/#13024 Closed/P1; #13023/#13029/#13030 Active/P2; #13021/#13025/#13026/#13028 New/P1; #13027 New/P2.
- E17 #13018 Active/P1: #13031 Closed/P1; #13032/#13033/#13035 Active/P1; #13034 New/P2; #13036/#13037/#13038/#13040 New/P2; #13039/#13041/#13042/#13043 New/P1.
- E18 #13019 remains New/P2 and E19 #13020 New/P1. Their future children retain live New states/priorities.
- #13072/#13073/#13074/#13076 are New/P1, #13075 New/P2; #12900/#12901/#12902/#12949 remain New/P3. #13035 has an implementation-started branch/evidence comment and stays open until Sol acceptance and Draft PR merge.

## Runtime and security truth

Final local runtime: the current Release executable at `pos\src\RmsSupportHub.Pos.Desktop.Wpf\bin\Release\net10.0-windows10.0.19041.0\RmsSupportHub.Pos.Desktop.Wpf.exe` is running as PID 9028 with title `RMS Support Hub`, `Responding=True`, and exactly one process at that path. The computer-use native pipe was unavailable after two required attempts, so no visible Logs/filter or Support Bundle screenshot/click evidence is claimed. Leave this one current-head WPF process running for Sol. No Agent service/operator-group provisioning or Production contact is allowed.
The implementation and tests cover the Logs/filter behavior, metadata-only Support Bundle UI, deferred export text, and one-process ownership. No Agent service/operator-group provisioning or Production contact is allowed.
No Production endpoint/database, raw log, secret, connection string, customer data, arbitrary path, stack trace, ZIP bytes, or server path is exposed; no service/database/filesystem/native RMS/machine mutation occurs. Authorization, fixed roots, redaction, bounds, audit, revocation, cancellation, and fail-closed validation are covered by code/tests. Online Order and WPF-06 are not started.

## Delivery guardrails

Keep the PR Draft; do not run `gh pr ready`, merge, deploy, provision, or alter native RMS state. Push this branch, create a Draft PR linked to AB#13035, verify exact-head CI, and stop for Sol acceptance.

## Full next prompt - WPF-06

### Mandatory Sol-accepted merge placeholder

When Sol provides `SOL_ACCEPTED_WPF05_SHA=<exact SHA>`, Luna must first:
1. Verify PR #36 head exactly matches that SHA.
2. Verify exact-head CI is green.
3. Run `gh pr ready 36`.
4. Run `gh pr merge 36 --squash --match-head-commit <accepted SHA>`.
5. Verify `merged=true`.
6. `git checkout main`.
7. `git pull --ff-only`.
8. Verify clean synchronized main.
9. Update Azure #13035 Closed only after the verified merge.
10. Create the WPF-06 branch.
If the head differs, STOP. Do not merge a SHA Sol has not accepted. Do not execute these steps during WPF-05.

### WPF-06 - DATABASE BACKUP & LOCAL ARTIFACT DELIVERY

Primary Azure: #13034 - Database backup/download and guarded restore.
Prioritize approved fixed RMS database backup creation, safe bounded artifact inventory/metadata inspection, bounded local artifact delivery/export, and Support Bundle export through the same artifact mechanism.
Database RESTORE is separately gated and remains out of scope unless Sol explicitly includes it after WPF-05 review.
Preserve Agent-owned fixed roots, opaque IDs, principal scope, checksum/expiry, audit, size/time limits, cancellation, redaction, and fail-closed validation. No arbitrary path, shell, PowerShell, SQL, or process execution.
Acceptance must cover authorization, fixed targets, inventory, delivery/export, checksum/expiry, retry/cancel, retention/audit, malformed responses, operator/admin behavior, and WPF-05 regression.

**HARD STOP - DO NOT EXECUTE WPF-06 until GPT-5.6 Sol reviews and accepts WPF-05.** Do not start WPF-06 now. Do not merge WPF-05 now. Leave the final WPF application running.
