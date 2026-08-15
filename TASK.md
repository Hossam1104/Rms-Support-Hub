# Final POS Security Gate Verification

MODEL: GPT-5.6 Terra
EFFORT: HIGH
ROLE: REVIEW ONLY

## Objective

Independently verify the final POS Testing security remediation on the current
repository. This is a review gate, not implementation. Do not modify code,
tests, generated artifacts, docs, memory, Git state, services, certificates,
registry, hosts, RMS folders, databases, or runtime processes. Do not start
Slice C, perform the POS visual redesign, or claim Production readiness.

Verify closure of these preceding findings:

HIGH: (1) exactly one build-identity JSON record; (2) strict frontend identity
schema and expected/staged/served binding; (3) complete frontend suite green.

MEDIUM: (4) Agent private-key ACL broad-principal rejection; (5) runtime root,
API DLL, content root, host, port, certificate, PID, build ID, and commit
binding; (6) elevated two-process H-3 Global semaphore proof.

## Startup context

Read `TASK.md`, `.ai/STATE.md`, and run `python .ai/scripts/context.py`.
Read `.ai/HANDOFF.md` only if `In Progress` or `Blocked`; read only scoped
source/tests/docs and the task diff. Read `.ai/PROJECT.md` or the affected ADR
only when stable context is needed.

## Review contract

1. Inspect the real parser used by `scripts/start-pos-agent-testing.ps1` and
   its Pester tests. It must accept exactly one text success-stream record
   containing one JSON object, reject zero/blank/whitespace, two/three records,
   warning+JSON, JSON+warning, malformed, valid+malformed, array, scalar, null,
   and unexpected objects, and make `LAST_LINE_ACCEPTED` impossible. Confirm
   no `Select-Object -Last 1`, arbitrary filtering, or diagnostic contamination.

2. Inspect the PowerShell and Angular identity validators. Confirm strict types
   and values for schemaVersion, approved environment, full commit,
   commitShort prefix, sourceState (`clean`/`modified`), lowercase SHA-256
   build/index/main hashes, positive bounded assetCount, UTC builtAtUtc with
   startup/future bounds, and `main-<hash>.js`. Reject rooted/drive/UNC/ADS/
   URI/query/fragment/traversal/slash/backslash/nested/wrong-extension names,
   unknown fields, and byte/hash/count drift. Confirm exact expected commit and
   environment are required before `:4443` starts, and identity/diagnostics
   expose no path, credential, certificate, or secret. The development
   placeholder is allowed only in its exact documented sentinel shape.

3. Run the complete frontend suite twice and the production build:

       npm test --prefix frontend -- --watch=false --no-progress
       npm run build --prefix frontend -- --configuration production

   Require at least 361 tests on each full run plus new tests. No timeout
   increase, skip, xit, fdescribe, disabled teardown, or hidden failure. Check
   cleanup for fake timers, deferred promises, subscriptions, fixture
   destruction, route-scoped stores, and shared state, especially the former
   `pos-maintenance` and `flat-order` timeout specs.

4. Inspect Agent certificate provisioning and shared ACL logic. It must reject
   broad allow rules for Everyone, Authenticated Users, BUILTIN\Users,
   ANONYMOUS LOGON, Guests, and equivalent SIDs before and after LocalSystem
   read handling. Confirm CNG, expected provider, non-exportable policy, exact
   owned certificate, LocalSystem access, no PFX exposure, and no key logging.

5. Confirm the reusable runtime-state helper itself binds every value listed
   above. Review stale/reused/unrelated PID, unrelated dotnet, missing/corrupt
   state, unowned listener, and stale owned listener. Never adopt or kill an
   unrelated process or unowned listener.

6. From elevated Administrator PowerShell, run exactly once:

       .\scripts\test-pos-privileged-lease.ps1

   Confirm Process A acquired; B was busy while A held; A released; B then
   acquired; and a later process acquired after B termination. The proof may
   use only the named semaphore and temporary markers: no RMS/service/DB/
   installer/Main Server mutation. If this shell is not elevated, do not loop
   on UAC; report the exact command and leave H-3 unverified.

## Regression and validation

Check H-1 redaction/quarantine, H-2 fixed roots/path confinement, H-3
serialization, certificate policies, PowerShell safety/elevation confinement,
exact Agent `https://rms-pos-agent.localhost:5001`, exact Support Hub
`https://support-hub.integration.test:4443`, exact Origin, HTTPS, Negotiate,
derived local Administrator authorization, direct browser-to-Agent transport,
no API relay/proxy, no browser-selected Agent address, no localhost:4200 Agent
CORS, canonical wrong-origin routing, freshness, and CI coverage.

Use synthetic seams and existing tests. Testing is the only live environment;
never launch RMS products, installers, repair, rollback, package activation,
Production services, or Main Server state-changing requests. Where relevant:

    .\scripts\test-powershell-quality.ps1
    dotnet build pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo -warnaserror
    dotnet test pos/RmsSupportHub.Pos.slnx -c Release --no-restore --nologo
    git diff --check
    python .ai/scripts/check_memory.py

Do not run `npm audit fix` or claim a URL from configuration alone; report
actual responses and distinguish pre-existing failures.

## Outcome and return

Report findings Critical, High, Medium, Low, Informational with file/line
evidence, impact, and acceptance conditions for Critical/High findings.

    Critical > 0 -> BLOCKED
    High > 0     -> BLOCKED
    Critical = 0 AND High = 0 -> SLICE C APPROVED

Return the executive outcome, blocker status, findings, H-1/H-2/H-3 and
certificate/PowerShell/runtime/frontend/routing evidence, actual validation
results, unavailable elevated/live dependencies, residual Production gates,
and the explicit Slice C decision. Do not implement findings.
