# P0-F — Production Mutation Gate / Draft PR #30 Acceptance and Finalization

MODEL: Implementation and validation executor
AUTHORITY: GPT-5.6 Sol remains Planner / Architect / Acceptance Authority
PROGRAMME: Staging-Safe Release Candidate v1
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/p0-f-production-mutation-gate`
PR: #30, `feat: protect Production order mutations and remediate Testing flows`
STATUS: Draft; awaiting independent Sol re-review and acceptance

## Current state

P0-F implements the server-enforced Production mutation gate for supported
UPC E-Commerce, GHC E-Commerce, and GHC Uni-Commerce send/cancel/resend
operations. Production Order Requests reads remain read-only without unlock;
Testing does not require the unlock ceremony. The gate uses a server-owned
secret, opaque in-memory tokens, exact module + Production + original-session
scope, bounded expiry, constant-time comparison, session/source/module
throttling, generic errors, and a fail-closed effective-HTTPS transport guard
for Production unlock/mutation requests. Production session cookies are
Secure, and trusted TLS-terminating proxy forwarding is empty-by-default and
allowlist-only. The browser stores no password or persistent unlock token.

PR #30 is not merged and must remain Draft until Sol independently accepts the
exact correction head. Do not create another PR, merge, or mark the PR ready.
Do not contact Production or execute a live Production mutation during this
acceptance/finalization task.

## Security and configuration boundaries

- Never reveal, print, log, screenshot, commit, or store the real Production
  unlock secret or any real downstream API key.
- Keep Production unlock/API-key values outside Git, packages, CI output,
  screenshots, and `.ai/` memory files. Only configuration-key names may be
  tracked.
- Preserve `SupportHub:DeploymentTier`, `Outbound:VerifyTls`, exact
  server-owned endpoint authority, CORS, capability gates, and disabled
  Production registrations in Testing deployments.
- Do not trust browser-selected endpoints, connection strings, SQL, headers,
  or arbitrary forwarding headers for security decisions.
- Do not send, cancel, resend, insert, update, delete, merge, or execute a
  side-effecting procedure against Production. Do not mutate customer orders,
  Main Server configuration, POS machines, or native RMS services.
- Treat missing configuration as unavailable and fail closed. Configuration
  presence alone never proves downstream health.
- Production HTTPS transport is mandatory before the real unlock secret may be
  provisioned; Production readiness remains **NO** until the controlled
  acceptance packet and all external readiness evidence are complete.

## Exact next executable work after PR #30 acceptance

1. Re-read the accepted exact head, `TASK.md`, `.ai/STATE.md`, and the empty
   handoff. Verify branch, `origin/main`, clean status, and all acceptance
   evidence without reconstructing prior chat.
2. Prepare the owner-controlled Production configuration outside the repository:
   the unlock secret, required server-owned Uni `X-Api-Key`, endpoint/database
   registrations, TLS policy, and disabled/allowed environment map. Validate
   JSON, tier, TLS, endpoint authority, secret presence, API-key presence,
   redaction, and restart persistence offline. Never copy values into tracked
   files or reports.
3. Keep Production readiness **NO** until the separate controlled acceptance
   packet is complete. That packet must identify environment, system,
   method/route/query, body/data, expected effect, reason, rollback/recovery,
   operator authorization, and read-only/mutating classification. Only after
   that gate may an authorized owner-led Production acceptance proceed.
4. Revalidate Testing using synthetic/read-only evidence. The Uni Testing
   gateway's HTTP:90 `502 Bad Gateway` remains an external blocker; do not
   reroute Testing to a suspected Production or directly observed HTTPS route.
5. Keep real release PKI/trust material open under Azure #12943 and keep the
   final integrated Online Order + POS smoke open under #12947. Do not claim
   Production or POS readiness from code/test evidence alone.
6. Update `.ai/STATE.md`, `.ai/HANDOFF.md`, and `.ai/HISTORY.md` with facts,
   exact validation evidence, unresolved external blockers, and the exact
   accepted head. Keep PR #30 Draft until Sol records acceptance.

## Validation and delivery rules

- Run focused backend/frontend remediation tests first, then the repository
  supported Release backend suite, frontend `npm test -- --no-watch`, frontend
  production build, `.\scripts\build.ps1`, any separate PowerShell quality
  gate, `python .ai/scripts/check_memory.py`, `python .ai/scripts/context.py`,
  and `git diff --check` as applicable.
- Do not use the unsupported Angular `--run` argument.
- Distinguish unavailable external dependencies from test/build failures.
- Review every changed file and run secret/configuration scans before delivery.
- Push only authorized corrections to this same branch, never force-push,
  merge, or create a new PR. Report the new exact-head Support Hub CI run and
  keep PR #30 Draft.

## Durable safety facts to preserve

- Uni Testing HTTP:90 gateway blocker remains unresolved.
- External Production unlock-secret/API-key provisioning remains required.
- POS release PKI/trust work item #12943 remains open.
- Integrated Online Order + POS smoke work item #12947 remains open.
- Production readiness remains **NO** until the controlled acceptance gates,
  configuration, PKI/trust, rollback, and integrated evidence are complete.

## Post-PR architecture checkpoint

After PR #30 final acceptance and merge, the owner has directed a hard
architecture checkpoint before any next implementation milestone.

WPF ARCHITECTURE DECISION POINT

No next implementation milestone begins after PR #30 until GPT-5.6 Sol and the
owner review the proposed migration from browser-local privileged integration
toward a WPF/Windows agent structure supervised by the Angular Support Hub
dashboard.

WPF is not implemented in this task.
