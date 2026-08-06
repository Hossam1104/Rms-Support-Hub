# Current Task

- **Task ID:** APPLICATION-SERVER-DEPLOYMENT
- **Status:** Blocked - deployment target and mechanism are not documented
- **Role:** Release

## Objective

Deploy the currently validated Online Order Tool application to the existing
configured application server through its authoritative deployment mechanism.
Preserve server-owned configuration and secrets, create a rollback copy, run
read-only health and smoke checks, and record the deployed commit only after
acceptance succeeds.

## Scope and safety gate

- Application deployment only; no SQL, schema migration, index script, or
  state-changing API operation is authorized.
- UPC Testing fixture acceptance remains deferred because completed approval is
  missing. It does not block application deployment, but it blocks any live COD
  acceptance claim, send, resend, or cancellation.
- Production index work remains deferred by the user, does not block
  application deployment, and is not authorized in this task.
- Never guess a server, service/site, port, deployment folder, health endpoint,
  credential, or deployment architecture.

## Current blocker

The repository contains no authoritative deployment documentation, target
configuration, CI/CD workflow, transfer script, IIS/Docker/systemd definition,
or server-management procedure. README and `.ai/PROJECT.md` explicitly state
that hosting/deployment topology is not documented. Deployment cannot proceed
until the existing target and mechanism are identified.

## Current evidence

- Baseline: `main` at `bf58fe918ea5ba63025d1d86c433053b1af37b34`, synchronized
  with `origin/main` before the pending documentation commit.
- No application source, SQL, payload, fixture, generated, or secret changes
  are pending.
- No application deployment or server mutation has been performed.
