# RMS+ Support Hub — INT-13 Representative Device + Live Operational Evidence

## Role and scope

**IMPLEMENT / EXECUTE only when the owner explicitly authorizes INT-13.** This
file is the next executable task after INT-08 and must not be executed merely
because it is staged here. Repository: `Hossam1104/Rms-Support-Hub`.

INT-08 is complete and validated in the destination repository. Keep this gate
limited to representative-device and live operational evidence for the
Testing environment. Do not broaden it into fleet management, deployment,
Production/customer service control, SQL changes, browser-policy changes, or
unrelated Support Hub work. Do not close INT-13 without evidence or fabricate
evidence when a live prerequisite is unavailable.

The owner-authorized INT-13P provisioning run has now provisioned the bounded
Testing prerequisites and recorded transport/SCM-harness evidence. INT-13
remains open while protected Negotiate/browser evidence, authenticated Agent
reads, server-derived Administrator authorization, mutation-token behavior,
and Agent-dispatched disposable-service control await a connected Chrome/Edge
session with usable Windows credentials.

The owner has now explicitly authorized INT-13C on the same Testing machine:
implement exact, idempotent Chrome/Edge IWA and LNA/loopback policy
provisioning, exact `BackConnectionHostNames` ownership, safe WhatIf/uninstall,
and a task-scoped non-elevated browser evidence harness. This exception is
limited to the configured Testing SupportHubOrigin and Agent hostname; it does
not authorize Production/customer changes, wildcard policies, loopback
disablement, listener widening, or any Agent architecture change.

Do not force-push, bypass CI, weaken security, trust `oot_sid`, add JWT/Bearer
authentication, add a generic command/process/PowerShell/SQL endpoint, route
privileged POS calls through `RmsSupportHub.Api`, or make machine changes to
force a green result.

## Mandatory startup

Read `AGENTS.md`, `TASK.md`, and `.ai/STATE.md`; run:

```powershell
python .ai/scripts/context.py
```

Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`. Read
only the source, tests, evidence documents, and documentation named by this
task, plus task-related changed files. Read `.ai/PROJECT.md` or a detailed ADR
only when stable context or an affected existing decision is non-obvious.

Verify from current code and Git state that INT-08 is merged, the Agent still
binds only to `https://rms-pos-agent.localhost:5001`, Production documentation
is hidden, the Hub uses direct `HttpBackend` transport, and INT-13 remains the
open gate. Reconcile only a concrete contradiction.

## Preconditions and allowed evidence

Use the Testing environment and an explicitly available representative Windows
device/browser session. Before any state-changing check, establish that the
device, disposable test service, account, and rollback path are identified and
approved for Testing. If a disposable service is not available, stop the live
mutation portion and record it as `BLOCKED` or `NOT RUN`; use existing fakes
for contract evidence. Never control a Production or customer service.

Collect evidence for the fixed direct path only:

```text
Support Hub Angular
    ↓
https://rms-pos-agent.localhost:5001
    ↓
HTTPS / HTTP/1.1 / exact Origin / Negotiate
    ↓
server-derived local Built-in Administrators authorization
    ↓
typed Agent reads and, only with an approved disposable Testing service,
opaque-target Start/Stop/Restart control
```

At minimum, verify and record:

- trusted certificate, hostname resolution, HTTPS secure context, loopback
  binding, and HTTP/1.1 behavior;
- exact-origin anonymous CORS preflight and rejection of wrong origins;
- Chrome and Edge behavior for the applicable LNA policy, secure context, and
  Windows Negotiate loopback back-connection;
- authenticated session and server-derived local Administrator authorization;
- direct Agent liveness, session, device identity, connectivity evidence,
  capabilities, redacted configuration, and allow-listed service reads;
- mutation-token issuance, target/method/path binding, replay/expiry behavior,
  and safe typed `NotAttempted`, `Accepted`, `Failed`, and `OutcomeUnknown`
  responses, using a disposable Testing service only for real SCM dispatch;
- unavailable-Agent, authentication, authorization, certificate, CORS, and
  transport failure states without adding a fallback relay.

Use only opaque target IDs in browser-facing evidence. Do not record mutation
tokens, Windows SIDs, credentials, raw service names, unrestricted paths,
connection strings, command text, or personal data. Redact browser profiles,
machine names, account names, certificate private material, and response
headers unless a safe non-secret fact is required to prove the gate.

## Prohibited changes and operations

Do not change certificates, hosts files, SPNs, registry policy, LNA policy,
browser managed policy, firewall rules, service startup configuration, machine
security settings, SQL/data, deployment state, or Production state to make the
evidence pass, except for the owner-authorized exact INT-13C Testing-machine
policy provisioning described above. Do not add `DisableLoopbackCheck`, wildcard CORS, LAN binding,
JWT/Bearer fallback, API relay, or a generic execution surface. Do not send,
cancel, resend, restart, or retry a real service action unless it is the
approved disposable Testing service and the live evidence plan explicitly
authorizes that single operation. Never automatically retry an
`OutcomeUnknown` result.

## Evidence and documentation deliverables

Create or update only the task-scoped evidence and memory records, normally:

- `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` with timestamped,
  redacted PASS/BLOCKED/NOT RUN rows, exact commands or browser observations,
  environment classification, and rollback/cleanup evidence;
- the relevant POS integration plan/readiness sections with links to the
  evidence and a clear separation between implementation proof and live proof;
- `.ai/STATE.md`, `.ai/HANDOFF.md`, `.ai/HISTORY.md`, and one ADR only if a
  lasting decision changes.

Do not claim live Agent, SCM, SQL, SMB, browser, certificate, or deployment
behavior from unit/integration fakes. Keep INT-13 open when a required live
prerequisite is blocked, and preserve the exact blocker and next safe action.

## Validation

Run focused live/contract checks first, then the relevant existing suites:

```powershell
dotnet restore pos/RmsSupportHub.Pos.slnx --nologo
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo --warnaserror
dotnet test pos/tests/RmsSupportHub.Pos.Domain.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Application.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Infrastructure.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Agent.IntegrationTests -c Release --no-restore --nologo
npm ci --prefix tools/pos-agent-client-generator
npm ci --prefix frontend
npm run generate:pos-agent-client --prefix frontend
npm test --prefix frontend -- --watch=false
npm run build --prefix frontend
```

Run `./scripts/build.ps1` as the broad informational regression gate. Distinguish
known unchanged backend route-status failures, unavailable live dependencies,
and environment locks from new regressions. Run `git diff --check` and a
task-scoped secret/temp-file scan before delivery. Never claim an unrun or
failed check passed.

## Delivery and completion

Before delivery inspect the task-only diff. Do not commit generated/runtime
directories or secrets. Commit intentionally, push the branch, open the PR,
verify CI and review, and merge normally only with green CI and no new
Critical/High finding when the owner has authorized that delivery. Reconcile
local `main` with `origin/main` and leave a clean tree. Report only responding
URLs among `http://localhost:4200`, `http://localhost:5200`, and
`https://rms-pos-agent.localhost:5001`; do not infer a URL from configuration.

INT-13 is complete only when the required representative-device/live evidence
is actually collected, redacted, linked from the readiness records, and any
approved Testing disposable-service action has a truthful outcome and cleanup
record. Otherwise return `Partially Completed` or `Blocked`, keep the handoff
below 40 lines with the exact next action, and do not mark the gate accepted.

## Completion response

Return only:

### Result
### Changes
### Validation
### Runtime URLs
### Remaining
