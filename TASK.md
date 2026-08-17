# GPT-5.6 LUNA MAX HIGH — P0-B Release Candidate Pipeline
MODEL: GPT-5.6 Luna Max HIGH
ROLE: Implement
BRANCH: `feat/staging-release-candidate-pipeline`; BASE: `main`
PROGRAMME: Staging-Safe Release Candidate v1; MILESTONE: P0-B Release Candidate Pipeline

## Objective
Implement a deterministic, offline-verifiable Testing/Staging IIS Release Candidate pipeline and integrated Support Hub CI.

## Requirements
1. Add integrated Support Hub CI workflow for `backend/**` and `frontend/**` running backend tests/build, frontend tests, production frontend build, Riyal asset verifier, and required quality checks.
2. Produce deterministic Testing/Staging IIS Release Candidate package.
3. Finalize build identity using source commit and Testing environment.
4. Add release manifest including schema version, source commit, build ID, configuration schema identity, and runtime prerequisites.
5. Produce file-integrity manifest/hashes and ZIP SHA-256 sidecar.
6. Unpack generated ZIP into a fresh directory and verify package integrity.
7. Add packaged-runtime smoke testing: application starts, `/`, API liveness/health, module catalogue, SPA fallback/deep link, build identity, static assets.
8. Establish offline/runtime independence: remove public Google Fonts/runtime CDN dependencies, bundle required local assets, scan HTML/CSS/JS for unexpected public runtime URLs.
9. Preserve approved internal RMS gateway dependencies as explicit configured dependencies rather than treating them as public Internet dependencies.
10. Ensure package contains backend publish output, Angular assets under `wwwroot`, `web.config`, build identity, release manifest, and deployment/config schema documentation.
11. Ensure package excludes secrets, `.env`, certificates/private keys, runtime `var`, local development state, source maps (unless approved), and generated junk.
12. Add configuration template using names/placeholders only.
13. Add deployment, rollback, and smoke instructions (DO NOT deploy IIS).
14. Account for framework-dependent .NET 10 Hosting Bundle prerequisite and writable `var/drafts` ACL/runtime storage requirement.
15. Add automated regression proving omitted `SupportHub:DeploymentTier` defaults to Testing (N-2).
16. N-1 (redundant Program.cs fallback test) may be cleaned up only if trivial and naturally adjacent; do not expand scope.
17. P0-A L-1/L-2/L-3 remain deferred unless directly touched.
18. Preserve P0-A: server-owned tier, Production denial in Testing, no browser raw connection strings, no arbitrary endpoint probes, no browser endpoint redirects.

## Guardrails
- Do NOT deploy IIS, contact Production, mutate Testing data, send/cancel/resend orders, change customer environments, begin OMS/Call Center implementation, or reopen POS architecture.

## Execution Sequence
Implementation → focused validation → full validation → artifact verification → CI exact-head verification → draft PR → Opus independent review → STOP.
