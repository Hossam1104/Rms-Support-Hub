SOL_ACCEPTED_WPF06_SHA=8267e7de7e698c6d31be0e682c96a1c1ceca98fd
# WPF-08 — Safety Snapshots & Incident Timeline
ROLE: Implement
PRIMARY STORY: #13036 - Safety Snapshots and incident timeline
STATUS: Future prompt only. WPF-07 is implemented on `feat/wpf-07-guarded-rms-service-control` and awaits GPT-5.6 Sol review, exact-head CI, and merge. Do not execute this prompt now.
## Hard stop and automated WPF-07 merge boundary
WPF-08 may not start until GPT-5.6 Sol accepts WPF-07. The first execution action after acceptance must verify the exact WPF-07 Draft PR head and required CI:
1. Set `SOL_ACCEPTED_WPF07_SHA` to the exact Sol-accepted WPF-07 head SHA.
2. Verify the Draft PR branch is `feat/wpf-07-guarded-rms-service-control` and its head is exactly that SHA.
3. Verify exact-head POS CI and Support Hub CI are green; stop if either is missing/failing or the head moved.
4. Run `gh pr ready <WPF07_PR_NUMBER>` only after those checks, then run `gh pr merge <WPF07_PR_NUMBER> --squash --match-head-commit <SOL_ACCEPTED_WPF07_SHA>`.
5. Verify merge, record the merge SHA, check out `main`, run `git pull --ff-only origin main`, verify `HEAD == origin/main`, and create `feat/wpf-08-safety-snapshots-incident-timeline`.
Do not mark WPF-08 ready or merge it without separate Sol acceptance.
## Current truth and preserved boundaries
- WPF-01 through WPF-06 are accepted/merged. WPF-07 owns the fixed RMS service catalog, typed Start/Stop/Restart, local Administrator mutation, operator read-only behavior, Agent self-protection, confirmation, bounded coordination, and truthful audit/outcome state.
- WPF-06 backup creation, principal-scoped inventory, and caller-bound local artifact export remain intact. Restore is separately governed by #13129 under #13034; do not implement restore here.
- Use Testing only for live verification. No Production/customer-data execution, real RMS service mutation, machine/operator-group provisioning, fleet/remote work, or Online Order work is authorized.
## WPF-08 objective
Implement a bounded local safety/diagnostic slice through `WPF -> typed LocalIpcClient -> Agent Local IPC -> transport-neutral Application -> Agent-owned snapshot/audit sources`.
Add fixed, server-owned Safety Snapshot metadata and a bounded Incident Timeline so an operator can understand what changed around an incident without raw customer data, machine paths, credentials, arbitrary log files, or a generic command console.
## Required scope
- Define a fixed snapshot catalog containing only bounded, typed, redacted Agent-owned evidence: service health/action outcomes, Agent/WPF version and protocol state, installation identity, database-health summary, storage-health summary, and bounded diagnostic references.
- Store snapshots under an Agent-owned fixed root with bounded count, size, retention, serialization, checksum, and cleanup rules. Never return physical paths or accept caller paths, filenames, commands, executables, SQL, or log sources.
- Expose typed create/list/inspect/expire only. Creation/expiry require local Administrator where they change state; LocalOperator may inspect allowed evidence. RemoteHub and unauthenticated callers remain denied.
- Build the Incident Timeline over durable Agent audit and bounded typed diagnostic events. It must be ordered, capped/paged, correlated, redacted, and safe for missing, malformed, duplicate, or out-of-order events. WPF must not read arbitrary filesystem logs.
- Bind requests to authenticated principal, correlation ID, protocol version, fixed catalog, and bounded limits. Reject unknown fields, arbitrary identifiers, invalid ranges, traversal, unbounded pages, and unsupported event types at the Agent boundary.
- Preserve WPF-07 outcome truth, WPF-06 export impersonation/SQOS and backup isolation, WPF-05 redaction, and Local IPC trust checks. Application stays transport-neutral; Domain stays Contracts-free.
- Keep WPF timeline inspection read-only with token-owned loading/empty/degraded/error states and safe recovery guidance. Do not add restore, rollback, cleanup/reset, package lifecycle, repair, configuration, fleet, SignalR, or remote mutation UI.
## Required tests
Add deterministic tests for fixed catalog and arbitrary payload/path/command/event rejection; Operator inspection versus Administrator create/expire authorization; RemoteHub/unauthenticated denial; bounded count/size/retention/checksum/serialization and cleanup; duplicate requests and principal/correlation binding; timeline order, caps, redaction, malformed/missing/duplicate events and audit failure; Local IPC identity/SQOS/version failures; malformed responses, shutdown cancellation, timeout, Agent unavailable, partial truth; WPF loading/empty/degraded/unauthorized/recovery states; WPF-05/WPF-06/WPF-07 regressions; and architecture searches proving WPF has no Agent/Infrastructure/SQL/HTTP/process/PowerShell/Named Pipe/filesystem-reader dependency beyond typed Local IPC and approved native output dialogs.
## Validation and delivery
Run focused tests, every POS test project, strict Testing-origin Release `--warnaserror`, `.\scripts\test-powershell-quality.ps1`, `Invoke-Pester -Path .\scripts\tests -PassThru`, `python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`, `git diff --check`, and prohibited-reference/security searches. Review affected Markdown/Azure evidence. Commit, push without force, create a Draft PR for #13036, wait for exact-head POS/Support Hub CI, and perform authorized runtime verification. Do not merge WPF-08.
## Hard stop
DO NOT EXECUTE WPF-08 until GPT-5.6 Sol reviews/accepts WPF-07 and the exact merge sequence above succeeds. DO NOT IMPLEMENT DATABASE RESTORE IN WPF-08. DO NOT START WPF-09, fleet/remote supervision, or Safety Snapshot expansion.
