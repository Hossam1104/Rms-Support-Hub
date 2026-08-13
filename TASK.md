# RMS+ Support Hub — INT-08 POS Service Control + Mutation Runtime

## Role and scope
**IMPLEMENT / EXECUTE only when the owner explicitly authorizes INT-08.**
Repository: `Hossam1104/Rms-Support-Hub`; this is the bounded task after INT-07. Do not execute merely because it is staged. Keep INT-13 open; exclude deployment, fleet management, and unrelated Hub work.
Do not force-push, bypass CI, weaken security, fabricate evidence, use `oot_sid`/JWT/Bearer, add generic command/SQL/process endpoints, or route POS privileged calls through `RmsSupportHub.Api`.

## Mandatory startup
Read `AGENTS.md`, `TASK.md`, `.ai/STATE.md`; run `python .ai/scripts/context.py`. Read `.ai/HANDOFF.md` only when In Progress/Blocked, and read the POS plan, readiness docs, and ADR-0020 when authorization is affected. Inspect current status/diff and only task-related source/tests/docs.

## Preconditions and contracts
Verify INT-06I COMPLETE/ACCEPTED, independent review PASS, PR #3 merged; INT-07 COMPLETE/ACCEPTED with protected read-only routes and direct `/tools/pos-maintenance`; no API relay or mutation route; INT-13 OPEN. If the repository contradicts this, reconcile only the smallest concrete contradiction.
Preserve these existing contracts:
```text
POST /api/v1/security/mutation-token
POST /api/v1/services/{serviceId}/actions
ServiceActionRequestDto(Action, IdempotencyKey)
ServiceActionKind = Start | Stop | Restart
IServiceManager.ControlAsync(serviceName, action, cancellationToken)
```

## Objective and Agent rules
Implement a typed Agent runtime allowing an authorized administrator to Start, Stop, or Restart one server-allow-listed Windows service. Keep `Support Hub Angular -> fixed trusted HTTPS loopback Agent -> typed SCM port` and all INT-07 read-only behavior/redaction unchanged.
- Register only explicit server-owned operations; resolve opaque `serviceId` through the Agent allow-list/configured names. Never accept raw service names, paths, executables, commands, SQL, scripts, or arbitrary SCM input.
- The browser requests only a logical operation. Bind the one-use token to the authenticated Windows SID, exact Support Hub Origin, operation, and resolved POST method/path; consume once immediately before dispatch.
- Reject missing/unknown/expired/replayed/principal/origin/target-mismatched tokens fail-closed. Preserve Negotiate and server-derived local Built-in Administrators membership from ADR-0020; tokens are never logged, in URLs, or exposed in UI.
- Accept only Start/Stop/Restart. Require a bounded non-empty idempotency key; never use a SID, username, or `oot_sid`. Prevent conflicting same-service actions when current seams require it.
- Call `IServiceManager.ControlAsync` only after every policy, identity, token, allow-list, and idempotency gate passes.
- Use typed outcome truth: pre-dispatch rejection=`NotAttempted`, positive dispatch acknowledgement=`Accepted`, ambiguity/timeout=`OutcomeUnknown`, definitive authoritative rejection=`Failed`. Never auto-retry Unknown. Return only safe correlation/detail data; never exceptions, credentials, SID, connection data, unrestricted paths, or command text.

## Direct Hub, OpenAPI, and generated artifacts
Extend the generated-client-backed `HttpBackend` transport; never add a relay. Keep tokens in memory and the approved header only. Show controls only for an authorized eligible service; require confirmation, prevent duplicates, and give typed outcome/retry guidance. Preserve visible read-only state, accessibility, keyboard/reduced-motion behavior, responsive states, and design tokens.
Document every operation with stable ID/tag, summary/description, Negotiate/Administrator/exact-Origin/token semantics, side-effect/outcome truth, safe responses/examples, and DTO/property descriptions. Regenerate (never hand-edit) `pos/openapi/RmsSupportHub.Pos.Agent.json` and `frontend/src/app/core/pos-agent/generated/pos-agent-api.generated.ts`.
Keep Scalar/OpenAPI Development/IntegrationTest-only and Production 404 for `/scalar`, `/scalar/`, and `/openapi/v1.json`.

## Tests and validation
Cover registry scope, token binding/fail-closed cases, non-admin/unresolved SID, raw targets/actions/bad keys, duplicate/concurrent actions, dispatch ordering, action mapping, safe NotAttempted/Accepted/Unknown/Failed responses, secret/path/command non-disclosure, route/OpenAPI parity, production docs absence, and UI authorization/confirmation/duplicate-click/typed-error/accessibility behavior.
Run targeted checks, then:
```powershell
dotnet restore pos/RmsSupportHub.Pos.slnx --nologo
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo --warnaserror
dotnet test pos/tests/RmsSupportHub.Pos.Domain.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Application.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Infrastructure.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Agent.IntegrationTests -c Release --no-restore --nologo
npm ci --prefix tools/pos-agent-client-generator; npm ci --prefix frontend
npm run generate:pos-agent-client --prefix frontend
npm test --prefix frontend -- --watch=false; npm run build --prefix frontend
```
Run `./scripts/build.ps1` as the broad informational gate; distinguish its known unchanged backend 404-vs-405 route-status failures from new regressions. Never claim an unrun or failed check passed.

## Runtime, safety, and delivery
Use Testing only; never control Production/customer services. Prefer fakes when no disposable Testing service exists and report live control unavailable. Do not modify SQL/data, certificates, browser policy, hosts, SPNs, or machine security settings to make tests pass. Report only responding URLs among `http://localhost:4200`, `http://localhost:5200`, and `https://rms-pos-agent.localhost:5001`.
Before delivery inspect the task diff, run `git diff --check`, scan secrets/temp files, update `.ai/STATE.md`, `.ai/HISTORY.md`, `.ai/HANDOFF.md` and affected POS docs, and add one concise ADR only for lasting decisions. Leave INT-13 open; set HANDOFF Empty only on completion. Commit/push intentionally, open the PR, verify CI/review, merge normally only with green CI and no new Critical/High, then reconcile local `main` with `origin/main` and leave a clean tree.

## Completion and response
Complete only when the typed allow-listed token-bound mutation, idempotency and outcome truth, direct accessible Hub UX, no generic execution/API relay, documented drift-free hidden production contract, and required tests are done; known baseline failures are explicit and INT-13 remains OPEN unless separately authorized and closed.
Return only:
### Result
### Changes
### Validation
### Runtime URLs
### Remaining
