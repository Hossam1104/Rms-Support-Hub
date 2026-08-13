# Independent POS First-Release Security & Readiness Review

## Role and scope

**Role:** `Review`
**Reviewer:** Claude Opus 5
**Effort:** HIGH
**Repository:** `Hossam1104/Rms-Support-Hub`
**Scope:** INT-06I + INT-07 + INT-08 + INT-13 comprehensive security, architecture, contract, and live operational readiness review.

You are the designated independent security and readiness reviewer for the completed RMS+ Support Hub Point of Sale (POS) first-release integration milestone.

Do not implement new features, rewrite architectures, or open unnecessary PRs.
Inspect code, contracts, tests, and live operational evidence under `.ai/`, `docs/`, `pos/`, `frontend/`, and `scripts/`.
Report any Critical, High, Medium, or Low findings with concrete file links, line references, and remediation guidance.

## Mandatory startup

1. Read `TASK.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HISTORY.md`.
5. Read `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` and `docs/evidence/POS_INT06_LIVE_TRANSPORT_EVIDENCE.md`.
6. Read `docs/POS_MAINTENANCE_INTEGRATION_PLAN.md` and `docs/POS_MAINTENANCE_INTEGRATION_READINESS.md`.
7. Review the codebase across the four reviewed milestones:
   - **INT-06I:** Server-side local Built-in Administrator group resolution (`LocalAdministratorGroupChecker`), fail-closed SID/token handling, non-production Scalar/OpenAPI gating.
   - **INT-07:** Protected read surface (`/session`, `/device/identity`, `/device/connectivity`, `/device/capabilities`, `/configuration`, `/services`), redacted configuration, direct Angular `HttpBackend` transport, no API relay.
   - **INT-08:** Typed `services.control` mutation surface, opaque allow-listed service IDs, target/method/path-bound one-use mutation tokens, bounded memory store, concurrency/idempotency protection, truthful typed outcomes (`Accepted`, `Failed`, `OutcomeUnknown`, `NotAttempted`).
   - **INT-13:** Representative Windows device provisioning, exact Support Hub origin (`https://support-hub.integration.test:4443`), LocalMachine certificates, exact-origin Chrome/Edge IWA policies, non-elevated Medium-integrity browser harness, live disposable service control and state refresh evidence.

## Review criteria

Evaluate the codebase against these non-negotiable security and readiness boundaries:

1. **Authentication & Authorization:**
   - Is Windows Negotiate IWA strictly enforced on all protected endpoints?
   - Is local Administrator authorization derived strictly server-side via `LocalAdministratorGroupChecker` using `S-1-5-32-544` and local group enumeration?
   - Is any client-supplied SID, header, cookie, or token rejected as an authorization bypass?

2. **Network & Transport Isolation:**
   - Does the Agent bind strictly to loopback (`127.0.0.1`, port 5001) over HTTPS and HTTP/1.1?
   - Is CORS restricted to the exact configured Support Hub origin without wildcards, regex matches, or header spoofing?
   - Does the Support Hub frontend communicate directly with the local Agent via `HttpBackend`, ensuring the Hub backend (`RmsSupportHub.Api`) never relays or proxies privileged POS traffic?

3. **Mutation Token & Service Control:**
   - Are mutation tokens strictly one-use, short-lived (60s), memory-only, and cryptographically bound to caller principal, exact origin, HTTP method (`POST`), path (`/api/v1/services/{opaqueId}/actions`), operation (`services.control`), and target opaque service ID?
   - Is token replay definitively rejected (HTTP 403)?
   - Are service IDs opaque allow-listed hashes, preventing arbitrary service control?
   - Is there any generic process execution, cmd/PowerShell invocation, or unrestricted file/registry access endpoint?

4. **Information Disclosure & Privacy:**
   - Are passwords, connection strings, private keys, tokens, and machine/account SIDs redacted from API responses and evidence logs?
   - Is OpenAPI / Scalar documentation completely disabled in Production mode?

5. **Operational Evidence & Rollback:**
   - Is the live operational evidence in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` truthful, complete, and reproducible on representative hardware?
   - Are provisioning and cleanup scripts (`setup-pos-agent-testing.ps1`, `remove-pos-agent-testing.ps1`, `PosAgentWindowsProvisioning.psm1`, `PosSupportHubProvisioning.psm1`) idempotent, scoped, and non-destructive?

## Validation commands

```powershell
python .ai/scripts/context.py
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo --warnaserror
dotnet test pos/tests/RmsSupportHub.Pos.Domain.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Application.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Infrastructure.Tests -c Release --no-restore --nologo
dotnet test pos/tests/RmsSupportHub.Pos.Agent.IntegrationTests -c Release --no-restore --nologo
npm test --prefix frontend -- --watch=false
Invoke-Pester -Script scripts/tests/*.Tests.ps1
```

## Completion response

Return only:

### Result
Review Completed (or Blocked).

### Findings
Grouped by severity (Critical, High, Medium, Low, Informational), or `None (Clean Bill of Health)`.

### Gate Assessment
Verdict on POS First-Release production readiness across INT-06I, INT-07, INT-08, and INT-13.

### Remaining
Any residual operational recommendations or post-review actions.
