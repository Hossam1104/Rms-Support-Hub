# Current Project State

- **Updated:** 2026-08-06
- **Branch:** `main`, synchronized with `origin/main` at the pre-deployment
  documentation baseline `bf58fe918ea5ba63025d1d86c433053b1af37b34`
- **Release or milestone:** Application server deployment discovery

## Working State

- The repository contains the .NET 10 Web API, Angular 22 SPA, Dapper SQL
  Server data layer, and xUnit/Vitest tests. Existing order authoring,
  payment, detail, cancel, resend, payload, and Production-safety contracts
  remain unchanged.
- Production API calls use same-origin `/api`; the repository has local
  development and validation scripts but no documented hosting/deployment
  topology or server target.
- The active task is `APPLICATION-SERVER-DEPLOYMENT`. No application
  deployment, server mutation, SQL change, migration, or state-changing API
  verification has been performed.

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
- Read-only browser and Testing metadata evidence remains historical evidence;
  no state-changing workflow was run.

## Deployment Discovery Blocker

- README and `.ai/PROJECT.md` state that hosting/deployment topology is not
  documented.
- The repository contains no authoritative IIS, Docker, systemd, CI/CD,
  transfer, service, deployment-folder, target-server, or health-endpoint
  configuration.
- The exact target environment, server identity, deployment mechanism, secure
  access method, rollback location, and health checks are therefore unknown.

## Programme Status

- U0-U8, final project polish, Order Requests unification, and acceptance
  hardening are closed.
- The active deployment task is blocked only by missing authoritative target
  and mechanism details. The UPC fixture and Production index deferrals remain
  separate and non-blocking for application deployment.
