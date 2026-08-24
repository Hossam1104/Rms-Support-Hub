SOL_ACCEPTED_WPF06_SHA=<provided by Sol>

# WPF-07 - Guarded RMS Service Control

ROLE: Implement
PRIMARY STORY: #13032 - Agent/RMS service health and approved service control
STATUS: Future prompt only. WPF-06 security/correctness remediation is implemented on `feat/wpf-06-database-backup-artifact-delivery` but remains pending formal GPT-5.6 Sol acceptance. Do not execute this prompt in the current session.

## Current truth and hard boundaries

- WPF-05 was accepted at `ee2d62c030a2266ed410f91604ce8524df61e50b`, merged through PR #36 as `e0c82cdefaac47c1ab9d0649d249cbf85f4d8837`, and #13035 is Closed/P1.
- WPF-06 implements fixed Branch/Cashier backup creation, principal-scoped bounded inventory, and one shared Agent-owned local artifact-delivery path for database backups and Support Bundles. Its final security remediation includes caller-bound ProfileList Desktop/Documents/Downloads roots, export-only caller impersonation, accepted/completed/failed/cancelled audit truth with rollback compensation, explicit principal isolation and legacy compatibility, bounded destination coordination, and strict `.bak`/`.zip` request validation.
- WPF-06 final Opus remediation additionally preserves Identification for normal Local IPC, uses Impersonation only for `artifact.export.local`, enforces exact server-side SQOS per operation with Delegation rejected, independently binds the export Windows token SID to the authenticated principal, records local database backup Requested/Accepted/Started/Dispatch/terminal events in the shared privileged audit stream with pre-mutation fail-closed behavior, and requires a valid principal SID for every new backup registration. A real Windows Named Pipe test proves caller-token Impersonation and rejects Identification before destination mutation. ADR-0030 remains Proposed pending GPT-5.6 Sol acceptance.
- WPF-06 does not implement database restore. #13034 remains Active/P1. Azure child #13129, `Implement guarded RMS database restore in WPF`, is New/P2 under #13034.
- Preserve #13018 Active/P1, #13032 Active/P1, #13033 Closed/P1, #13035 Closed/P1, #13116 New/P2, #13019 New/P2, #13020 New/P2, and Online Order #13072-#13076 without unrelated changes.
- The next recommendation is guarded RMS service control under #13032, not Safety Snapshots. Do not start #13036 or any WPF-08 slice.
- No Production access, machine provisioning, Agent/operator-group provisioning, real RMS backup/restore, service mutation, fleet/remote work, or Online Order work is authorized until explicitly authorized for that separate execution.

## Mandatory startup

Read `TASK.md`, `.ai/STATE.md`, run `python .ai/scripts/context.py`, read `.ai/HANDOFF.md` only if its status is `In Progress` or `Blocked`, then read only task-relevant sources, tests, and documentation. Inspect current code and tests before challenging accepted work. Do not restart WPF-06 discovery.

## Required delivery sequence before implementation

The first action after Sol supplies the exact accepted SHA is to verify the delivery boundary. Stop immediately if any check fails or the PR head moved:

1. Verify PR #37 is still Draft, its head branch is `feat/wpf-06-database-backup-artifact-delivery`, and its head commit is exactly `SOL_ACCEPTED_WPF06_SHA`.
2. Verify exact-head POS CI and Support Hub CI are green.
3. Do not continue if the head moved, the branch is dirty unexpectedly, or either required CI lane is not green.
4. Run `gh pr ready 37` only after the exact accepted head and CI are verified.
5. Merge only with `gh pr merge 37 --squash --match-head-commit <accepted SHA>`.
6. Verify the PR reports `merged=true` and capture the resulting WPF-06 merge SHA.
7. Check out `main`.
8. Run `git pull --ff-only origin main`.
9. Verify the worktree is clean and `HEAD == origin/main == <WPF-06 merge SHA>`.
10. Update Azure WPF-06 evidence with the accepted SHA, PR #37, merge SHA, and validation evidence; keep #13034 Active/P1 because guarded restore remains outstanding.
11. Set or retain #13032 as Active/P1 and use it as the WPF-07 story.
12. Create branch `feat/wpf-07-guarded-rms-service-control` from the verified `main` head.

Do not merge, mark ready, or start WPF-07 before all preceding acceptance-boundary checks succeed.

## WPF-07 objective

Implement only the guarded RMS service-control capability through:

`WPF -> typed LocalIpcClient -> Agent Local IPC -> transport-neutral Application/Agent seam -> allow-listed Windows service manager`.

The capability must use one fixed, server-owned service catalog and preserve the existing WPF-06 backup/export path and WPF-05 logs/evidence/Support Bundle behavior.

## Required scope

- Expose only fixed catalog entries for the approved Agent/RMS services and only typed `Start`, `Stop`, and `Restart` actions. Reject arbitrary service names, arbitrary SCM targets, generic service-control payloads, and caller-supplied executables.
- LocalAdministrator may mutate. LocalOperator is read-only. RemoteHub and unauthenticated callers are denied for local WPF service mutation.
- Require explicit confirmation for every Stop and Restart request. Bind confirmation, target, action, principal, correlation, one-use mutation authorization, idempotency, and the fixed mutation lease at the Agent boundary.
- Record durable `requested`, `accepted`, `completed`, `failed`, and `cancelled` audit outcomes. Never report success when the final audit is unavailable or service state is unknown.
- Use one bounded, reference-counted mutation coordinator for service actions, with bounded status/progress, timeout/cancellation handling, truthful ambiguous outcomes, and no unbounded in-memory key growth.
- Enforce dependency safety and deterministic preflight. Never stop or restart the Agent service itself unless a separately accepted architecture explicitly defines safe recovery; default behavior must reject that target.
- Preserve backup/export capabilities and their security boundaries. Do not implement restore, snapshots, cleanup, branch reset, package install/upgrade/repair/uninstall, rollback/recovery, configuration changes, or remote/fleet service control in this slice.
- Do not invoke PowerShell, `sc.exe`, arbitrary command shells, or a generic SCM console. Use the existing typed service-manager port and allow-list patterns.
- Build the focused WPF workspace with design tokens, read-only operator presentation, administrator confirmation UX, bounded state/error handling, cancellation on shutdown, and no hidden service targets.

## Required tests

Add deterministic tests for:

- Fixed catalog and rejection of arbitrary service IDs, names, commands, and payload fields.
- Operator read-only behavior; administrator Start/Stop/Restart authorization; RemoteHub and unauthenticated denial.
- Stop/Restart confirmation, one-use authorization, principal/correlation binding, idempotency, same-target conflict, different-target concurrency, and coordinator cleanup.
- Dependency safety, Agent-self protection, service-manager unavailable/timeout/error states, cancellation, unknown post-action state, and recovery truth.
- Durable audit ordering and final-outcome failure behavior for requested/accepted/completed/failed/cancelled paths.
- Bounded progress/status responses, malformed-response handling, WPF shutdown cancellation, and preservation of WPF-05/WPF-06 regression surfaces.
- Architecture boundaries: WPF has no Agent/Infrastructure/SQL/HTTP/process/PowerShell/Named Pipe/filesystem-reader/service-controller dependency beyond typed Local IPC and approved native output dialogs; Application remains transport-neutral and Domain remains Contracts-free.

## Validation and delivery

Run focused tests first, then every POS test project, strict Testing-origin Release build with `--warnaserror`, `.\scripts\test-powershell-quality.ps1`, `Invoke-Pester -Path .\scripts\tests -PassThru`, `python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`, `git diff --check`, and prohibited-reference/security searches. Report actual counts and distinguish unavailable live dependencies.

Update only affected tracked Markdown and Azure evidence. Review the final task-related diff, commit, push without force, create a Draft PR for #13032, wait for exact-head POS and Support Hub CI, and after authorized runtime verification leave the final current-head WPF process running. Do not provision Agent/operator groups, contact Production, run a real RMS backup/restore, or mutate services during this prompt unless separately authorized.

After WPF-07 is complete, record concise history/state evidence, set `.ai/HANDOFF.md` to `Empty`, and stop. Do not begin WPF-08, Safety Snapshots, or another WPF slice in the same execution.

HARD STOP - DO NOT EXECUTE WPF-07 UNTIL GPT-5.6 SOL ACCEPTS WPF-06 AND THE EXACT MERGE SEQUENCE ABOVE HAS SUCCEEDED.
DO NOT IMPLEMENT DATABASE RESTORE IN WPF-07.
DO NOT START WPF-08 OR SAFETY SNAPSHOTS.
