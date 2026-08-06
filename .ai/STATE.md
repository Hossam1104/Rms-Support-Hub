# Current Project State

- **Updated:** 2026-08-06
- **Branch:** `main`
- **Release or milestone:** QA Support Hub — Session 00 baseline

## Working State

- The repository contains the .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests. Existing order authoring,
  payment, detail, cancel, resend, payload, and Production-safety contracts
  remain unchanged.
- The QA Support Hub programme started with Session 00 (repository baseline
  and architecture decision), which is complete. Implementation plan:
  `docs/QA_SUPPORT_HUB_IMPLEMENTATION_PLAN.md`; session prompts:
  `docs/QA_SUPPORT_HUB_SESSION_PROMPTS.md`; Prompt Studio migration source:
  `prompt_generator/index.html`.
- Baseline architecture decisions are recorded in
  `.ai/decisions/ADR-0011-qa-support-hub-baseline.md`; the repository map is
  section 23 of the implementation plan. Session 00 changed documentation and
  project memory only; no application behavior changed.
- The active task is QA Support Hub **Session 01 — Shared Route Skeleton and
  Product Rename** (see `TASK.md`). POS Sessions 11-13 remain blocked until
  the standalone POS source project is supplied.

## Deferred Acceptance and Database Scope

- UPC Testing fixture acceptance: **Deferred - Testing approval required**.
  It does not block application deployment, but it blocks any live COD
  acceptance claim, send, resend, or cancellation.
- Production index work: **Deferred by user**. It is a separate future,
  database-owner-approved task, does not block application deployment, and no
  Production database change is authorized in this session.
- No false fixture approval, COD acceptance, or Production index deployment is
  recorded.

## Local Verification

- Focused frontend Order Requests/searchable-select tests: 43 passed across
  four spec files; full frontend suite: 141 passed across 24 files.
- Focused backend Order Requests tests: 35 passed; full backend suite: 161
  passed with no skipped tests.
- `scripts/build.ps1` passed the backend test, Release build, and Angular
  production-build gates. The production bundle is 438.35 kB with no
  style-budget warning.
- `npm run test:riyal-asset` passed with the provenance-verified asset.
- Session 00 required documentation validation only: documentation links and
  referenced paths verified, `git diff --check` clean, and the working tree
  limited to documentation and project-memory changes.

## Deployment Discovery Blocker

- README and `.ai/PROJECT.md` state that hosting/deployment topology is not
  documented.
- The repository contains no authoritative IIS, Docker, systemd, CI/CD,
  transfer, service, deployment-folder, target-server, or health-endpoint
  configuration.
- The exact target environment, server identity, deployment mechanism, secure
  access method, rollback location, and health checks are therefore unknown.
- Application deployment stays deferred until the final QA Support Hub release
  session; the plan scopes deployment outside feature sessions.

## Programme Status

- U0-U8, final project polish, Order Requests unification, and acceptance
  hardening are closed.
- QA Support Hub: Session 00 completed; Sessions 01-10 and 14-16 not started;
  Sessions 11-13 blocked until the POS source is supplied.
