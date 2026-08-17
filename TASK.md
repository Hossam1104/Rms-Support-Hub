# CLAUDE OPUS 5 HIGH — Independent Review — P0-A Staging Environment Safety
MODEL: Claude Opus 5 HIGH
ROLE: Review
MODE: REVIEW ONLY. Do not modify files, create/delete files, commit, push,
merge, deploy, install, or mutate Testing, Production, customer, database,
IIS, POS Agent, certificate, registry, SCM, or browser-policy state.
Repository: https://github.com/Hossam1104/Rms-Support-Hub
Branch: `feat/staging-environment-safety`; base: `main`
Programme: Staging-Safe Release Candidate v1; milestone: P0-A only.
## Objective

Independently decide whether this branch establishes a trustworthy,
server-owned Testing/Staging boundary. Do not begin P0-B packaging, artifact
generation, signing, IIS deployment, or any new feature.
## Review setup
Read `AGENTS.md`, `.ai/STATE.md`, `.ai/HISTORY.md`, `.ai/DECISIONS.md`,
`README.md`, `docs/api-spec.md`, and
`docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md`. Run:

```powershell
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
git status --short --branch
git diff main...HEAD --stat
git diff --check
```
Inspect the complete task diff and its tests. Treat current code/tests,
repository SQL, `docs/database-schema.md`, `docs/request_examples/**`, and
mirrored fixtures as authoritative. Never reset legitimate work or invent
schemas, credentials, gateway behavior, or Production approval.
## Required review evidence
1. Configuration: `SupportHub:DeploymentTier` is typed, startup-validated,
   server-owned, and defaults to Testing. API composition binds configuration;
   Core has no arbitrary `IConfiguration`. Registration covers module/env
   availability, endpoint/cancel endpoint keys, connection-string names,
   custom-endpoint policy, health enablement, and bounded timeout. Invalid tier,
   unsafe URI, missing enabled mapping/key, unsafe database override, or
   impossible custom policy fails structurally. Missing optional secrets do not
   crash unrelated surfaces; only that environment becomes unavailable.
2. Testing denial: trace direct handcrafted API requests and prove Production
   send, ad-hoc cancel, Order Requests cancel, resend, item/consumer/branch
   lookup, DB diagnostic, endpoint resolution/diagnostic, and health probing are
   rejected or disabled before DB/downstream side effects. A request payload,
   query, header, or guessed key cannot change the tier. Production entries may
   remain as future server registrations but must be unavailable in Testing.
3. Browser authority: search controllers, DTOs, services, modules, Angular,
   tests, and docs. No raw `connectionString`, server/catalog/raw SQL, or
   provider config is accepted. `/test-db` uses only module + environment keys.
   No caller URL/host/port/scheme/path can become a probe target. Send/cancel/
   resend cannot accept endpoint redirects, `CustomApiUrl`, custom URLs, or
   equivalent overrides; compatibility inputs must be ignored. No secret or
   connection-name leak; endpoint topology is disclosed only by the existing
   approved read-only contract.
4. Truth: `GET /api/modules` and health reflect effective policy. UPC Testing
   remains available with valid server configuration; UPC Production is off in
   Testing. GHC unconfigured lanes are unavailable and `Resend=false`; GHC
   Uni-Commerce is unavailable without a verified contract; OMS and Call Center
   are registered unavailable stubs with false unsupported capabilities.
   Angular renders only server-provided available environments, rejects stale
   Production selections, sends keys only, has no raw DB/URL controls, and
   renders safe policy errors.
5. Errors: new policy/downstream failures use `{error:{code,message,details}}`
   with safe deterministic values for `invalid_environment`,
   `environment_not_allowed`, `environment_unconfigured`,
   `capability_unavailable`, `downstream_unreachable`, and timeout where
   applicable. No raw SQL/connection string, downstream exception, stack,
   filesystem path, credential, or arbitrary target reaches the browser/log
   response. Do not broaden this into a P1 error refactor.
6. Health: `/api/modules` remains fast/deterministic and does no live work;
   `/api/modules/health` is a separate registered-environment, policy-aware,
   bounded projection, never a generic scanner/SSRF surface. Disabled or
   unconfigured entries are truthful and Testing never probes Production.
7. Regression/boundary: UPC Testing item, consumer, branch, builder, Order
   Requests, supported cancel/resend, DB key, and gateway contracts remain
   fake/fixture-tested. Confirm no `pos/` source, Agent trust/lifecycle,
   certificate, PowerShell, H-1/H-2/H-3, browser-to-Agent, SCM, or deployment
architecture changed. Do not add GHC/OMS/Call Center behavior.
## Validation (read-only, exact evidence)

Run without changing code or weakening gates:

```powershell
dotnet test backend/RmsSupportHub.slnx -c Release --nologo
Push-Location frontend
npx ng test --watch=false --progress=false --reporters=default
npx ng build --configuration production --no-progress
Pop-Location
.\scripts\build.ps1
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
git diff --check
```

Check applicable CI for the exact PR head if access exists. Report exact
passed/failed/skipped counts, warnings/errors, and distinguish pre-existing
failures. Do not send/cancel/resend, probe live gateways, write databases,
change indexes, mutate Testing data, deploy IIS, or change POS/host state.

## Required report

Return only a self-contained review report:

- `Result`: ACCEPTED, ACCEPTED WITH NON-BLOCKING FINDINGS, REQUEST CHANGES, or
  BLOCKED.
- `Evidence`: reviewed branch/commit; configuration; Production denial for
  send/cancel/resend/DB/endpoint/health; browser search; module truth; errors;
  UPC regression; POS boundary.
- `Validation`: exact commands, counts, warnings/errors, and CI statuses.
- `Findings`: Critical/High/Medium/Low with file/line and concrete evidence;
  explicitly state zero findings per severity where applicable.
- `Decision boundary`: state whether P0-A is independently accepted. Keep
  P0-B, Production approval, customer approval, PKI/fleet gates, live Testing
  acceptance, and deployment execution open unless evidence closes them.

Stop after the review. Do not run Opus recursively, modify the repository,
commit, push, mark a PR ready, or merge.
