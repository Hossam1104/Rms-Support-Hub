# Documentation Index

Every document here is current and load-bearing. Historical planning documents
and executed session prompts are intentionally absent — Git history preserves
them, and `.ai/HISTORY.md` indexes the milestones.

## Contracts

| Document | What it governs |
| --- | --- |
| [api-spec.md](api-spec.md) | The REST surface, kept in sync with the controllers. Cited from frontend model and interceptor comments. |
| [database-schema.md](database-schema.md) | The verified SQL Server schema. Never invent a column; verify here and against the Dapper repositories. |
| [request_examples/](request_examples/) | Reference JSON payloads. These, and their mirrored backend test fixtures, are the executable payload contract. |
| [sql/order-requests-performance-indexes.sql](sql/order-requests-performance-indexes.sql) | Externally applied, database-owner-approved support indexes. Not an application migration. |

## Design and structure

| Document | What it governs |
| --- | --- |
| [design-system.md](design-system.md) | Token catalogue, the shared card contract, the "no raw hex outside token files" rule, theme and motion behavior, and the Hub scene. |
| [REPOSITORY_STRUCTURE.md](REPOSITORY_STRUCTURE.md) | Where code, styles, and documentation belong, and what to read first. |
| [UI_Rework_Plan.md](UI_Rework_Plan.md) | The closed U0-U8 programme. Retained because its D1-D13 decisions are cited by name from backend and frontend source comments. |

## Release and future work

| Document | What it governs |
| --- | --- |
| [QA_SUPPORT_HUB_RELEASE_READINESS.md](QA_SUPPORT_HUB_RELEASE_READINESS.md) | The release-candidate validation record, deferred items, and deployment preconditions. |
| [POS_MAINTENANCE_MIGRATION_INTAKE.md](POS_MAINTENANCE_MIGRATION_INTAKE.md) | The security boundary any future POS integration must satisfy. POS stays Coming Soon until then. |

## Agent context

`.ai/` holds the coding-agent working set: `PROJECT.md` (stable context),
`STATE.md` (durable current facts), `DECISIONS.md` plus `decisions/` (ADRs),
and `HANDOFF.md` (only when work stops mid-task). Start from `TASK.md` and
`AGENTS.md`.
