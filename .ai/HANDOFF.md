# Active Handoff

- **Status:** Blocked
- **Task ID:** APPLICATION-SERVER-DEPLOYMENT
- **From:** Codex
- **To:** Next owner after the existing target and deployment mechanism are
  identified
- **Checkpoint commit:** `9f1e561ace01898875a421030a69d4ea591026a1`

## Scope

- Application deployment is authorized only through an existing, verified
  mechanism.
- UPC Testing fixture acceptance is deferred for missing completed approval;
  it does not block deployment and remains in `.ai/STATE.md`.
- Production index work is deferred by the user; no database action is
  authorized and it is not a deployment blocker.

## Blocker

- No authoritative deployment documentation, target configuration, CI/CD
  workflow, transfer script, or IIS/Docker/systemd/service definition exists in
  the repository.
- Missing target details: environment, server identity, deployment folder,
  service/site/container, URL/port, health endpoint, secure access method, and
  rollback procedure.
- The hard-coded UPC RMS addresses are upstream API endpoints, not an
  application-server deployment target and must not be used as one.
- Local validation completed after the documentation push: backend/frontend
  tests, Release build, Angular production build, Riyal verifier, wrapper,
  memory checks, and diff checks passed.

## Exact next action

Provide or restore the authoritative existing deployment target and mechanism.
Then verify secure access and the current server baseline before building or
transferring an artifact. Do not guess, deploy, run SQL, or perform live API
actions while these details are absent.
