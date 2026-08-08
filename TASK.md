# Active Task

## Current mode

**Post-release iterative maintenance and enhancement.**

The QA Support Hub implementation programme is complete. There is no numbered
implementation session, no active plan under `.ai/plans/`, and no roadmap to
resume. Work arrives as individually scoped maintenance, enhancement, or
defect tasks against the current repository state.

## Status

Release Candidate Ready / Awaiting Deployment Decision. Deployment and
Production acceptance were not executed and remain outside implementation
work. The validation evidence for the release is
`docs/QA_SUPPORT_HUB_RELEASE_READINESS.md`.

## Product boundaries

| Tool | Route | State |
| --- | --- | --- |
| QA Prompt Studio | `/tools/prompt-studio` | Available |
| Online Order Tool | `/tools/online-orders` | Available |
| POS Maintenance Tool | `/tools/pos-maintenance` | Coming Soon, informational only |

The POS Maintenance Tool is developed in a separate project. Do not implement
POS operations, backends, or controls here; keep the route informational until
a dedicated integration task is authorized. See
`docs/POS_MAINTENANCE_MIGRATION_INTAKE.md` for the security boundary that any
future integration must satisfy.

## Standing constraints

- Preserve the Prompt Studio canonical Bug, Story, and Test Case output
  contracts, the ten-record local history cap, draft persistence, and the
  local-only deterministic generation path.
- Preserve Online Order business behavior: API calls, DTOs, payloads, filters,
  paging, statuses, capability guarding, order actions, and route meaning.
- Never send, cancel, or resend against Production. Testing is the default
  environment for any agent-run live verification.
- Keep connection strings and credentials out of tracked files.
- Follow `AGENTS.md` for role, scope, validation, and memory rules.

## Entry points

- Repository layout and where new work belongs: `docs/REPOSITORY_STRUCTURE.md`
- Documentation index: `docs/README.md`
- Current durable state: `.ai/STATE.md`
- Full validation gate: `.\scripts\build.ps1`
