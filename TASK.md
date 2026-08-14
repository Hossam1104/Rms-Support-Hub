# POS Downloader / Deployment + Cleanup / Maintenance

## Role and scope

**Role:** `Plan`
**Executor:** GPT-5.6 Luna Max
**Repository:** `Hossam1104/Rms-Support-Hub`
**Scope:** Produce one executable implementation plan for the next POS
deployment/maintenance slice: downloader operational hardening, deployment
packaging/start-stop ownership, and safe cleanup of Support Hub/Agent runtime
artifacts. Keep the work inside the existing POS Agent, Testing provisioning,
and deployment scripts; do not redesign the RMS database backup/restore
contract that is now complete.

The plan must begin from current repository code, tests, Git status, and the
active `.ai/` state. Identify the smallest safe implementation boundary,
existing reusable utilities, validation commands, and any owner decisions
needed for Production/customer deployment. Do not implement code in this task
without an accepted plan and explicit owner sign-off.

## Acceptance criteria for the plan

- Establish the current downloader, publish, startup, cleanup, and runtime
  ownership behavior from source and task-related tests.
- Define exact changes for safe artifact retention, stale-process handling,
  idempotent start/stop, and Testing-only versus Production boundaries.
- Preserve the direct loopback Agent trust boundary, exact-origin browser
  policy, Windows authentication, server-owned secrets, and the typed RMS
  database backup/restore surface.
- Include targeted tests, broad validation gates, rollback/recovery, and
  runtime verification evidence.
- Leave M-1/M-2 production managed-fleet policy and certificate lifecycle
  decisions as explicit external gates unless the owner expands scope.

## Mandatory startup

1. Read `TASK.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HANDOFF.md` only if its status is `In Progress` or `Blocked`.
5. Read only downloader, deployment, cleanup, runtime scripts, tests, and
   documentation named by the task or discovered as directly task-related.
6. Read `.ai/PROJECT.md` or `.ai/DECISIONS.md` only when stable context or an
   affected lasting decision is required.

## Non-goals

- Do not change business/API/SQL contracts unrelated to POS deployment.
- Do not run against Production, control real services, or mutate real RMS
  databases.
- Do not commit, push, deploy, or delete material data while planning.

## Completion response

Return only:

### Result
Planning Completed or Blocked.

### Plan
Sequenced implementation plan with file scope, tests, validation, and
rollback/recovery.

### Decisions
Owner choices or access required before implementation.

### Remaining
Unresolved blockers or risks.
