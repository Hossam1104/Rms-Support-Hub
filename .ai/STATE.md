# Current Project State

- **Updated:** 2026-08-16
- **Active branch:** `main`; final remediation is merged and synchronized.
- **Baseline:** PR #17 merge `b168f1c7cbef2db55c45fb681e46c4234f384855`;
  prior baselines were PR #14 `19f609b`, PR #13 `8192141`, and Slice A PR #12
  `fb71d01`.
- **Previous gate:** 0 Critical, 2 High blockers, and 3 Medium findings. No
  Production/customer/RMS mutation has been run; Production readiness is not
  claimed.
- **Next task:** `TASK.md` is now the GPT-5.6 Terra independent review-only
  prompt. Slice C and its visual redesign remain unimplemented.

## Final remediation facts

- Build-generator output now uses one shared exact-one parser; zero, extra,
  warning, malformed, non-object, and unexpected records fail closed. The
  owner-preserved `Invoke-Checked ... | Out-Host` fix remains in place.
- PowerShell and Angular identity validators strictly check fields/types,
  approved environment, full commit/short prefix, clean-or-modified source,
  bounded positive asset count, UTC timestamp bounds, lowercase SHA-256 hashes,
  safe main bundle filename, staged asset count, and index/main byte hashes.
  Expected/staged/served identity agreement is required before `:4443` starts.
- Agent certificate provisioning rejects broad private-key allow principals
  before and after LocalSystem read handling. CNG/provider/export policy,
  owned certificate, LocalSystem access, no PFX, and no key logging remain
  fail-closed requirements.
- Runtime state binding now covers root, API DLL, content root, host, port,
  certificate, PID, build ID, and commit. Unowned listeners and unrelated
  processes are never adopted or killed.
- Frontend route-scoped store subscriptions/debounce state and maintenance
  delays clean up on fixture/component destruction; formerly order-sensitive
  full-suite timeout specs have explicit fixture/timer cleanup.
- `scripts/test-pos-privileged-lease.ps1` is a Testing-only two-process proof
  for the named Global semaphore and termination release. It does not mutate
  RMS. Non-elevated `Unavailable` remains fail-closed and is not replaced.

## Durable POS boundaries

- H-1 bounded redaction/quarantine, H-2 fixed service-owned roots, and H-3
  machine-wide mutation serialization remain implemented.
- The canonical browser route is the exact
  `https://support-hub.integration.test:4443/tools/pos-maintenance`; the Agent
  is reached directly at its exact HTTPS loopback origin with Negotiate and
  derived local Administrator authorization. `http://localhost:4200` is not an
  Agent CORS origin and the Hub API is not a privileged relay/proxy.
- POS Agent OpenAPI/generated client artifacts are source/contract governed;
  current SQL and `docs/database-schema.md` remain the database contract.
- Main Server reads are GET/read-only; retained Agent previews are typed POST
  boundaries. No RMS installer, repair, rollback, package activation, DB,
  registry, RMS folder, Production, or Main Server mutation was executed.

## Validation evidence

- `scripts/test-powershell-quality.ps1` passed under both `-File` and
  `-Command`; 22 tracked PowerShell files parse cleanly. PSScriptAnalyzer is
  absent locally.
- Pester passed 108 tests (up from 94), including exact-one output, strict
  identity fields, runtime PID/build/commit binding, and ACL cases.
- Complete frontend suite passed 363/363 in two consecutive full runs across
  58 files; production build passed. Client generation passed twice with no
  generated diff.
- `npm ci --prefix frontend` passed; npm reported 5 dependency vulnerabilities
  (1 moderate, 4 high) and 4 blocked install scripts. No `npm audit fix` ran.
- POS Release build passed with `-warnaserror`, 0 warnings/errors. POS tests
  passed Domain 9, Application 76, Infrastructure 90, Agent integration 152
  (327 total). Non-admin infrastructure test records Global semaphore
  `Unavailable` as the required fail-closed result.
- `git diff --check` passed. Memory check passes every file except the
  pre-existing root `AGENTS.md` budget violation (146/140 lines).
- Secure Testing runtime and H-3 elevated proof are not yet evidenced because
  this shell is not Administrator. Owner command: `.\scripts\test-pos-privileged-lease.ps1`.

## Delivery/runtime gates

- PR #17 is merged non-draft with all six CI checks green. Local `main` equals
  `origin/main` after the follow-up durable-state commit, and the worktree was
  clean before runtime restart.
- Fresh `scripts/dev.ps1` processes are left running: `http://localhost:4200/`
  returned HTTP 200 and `http://localhost:5200/api/modules/health` returned
  HTTP 200. `/tools/pos-maintenance` returned the Angular shell at HTTP 200;
  the local browser controller was unavailable, while the full frontend guard
  tests verify the exact secure handoff target.
- Secure Testing Agent/Support Hub startup and elevated H-3 proof remain
  unverified because this shell is not Administrator. Owner commands are
  `.\scripts\start-pos-agent-testing.ps1 -IUnderstandTestingOnly` and
  `.\scripts\test-pos-privileged-lease.ps1`; no UAC loop was attempted.
- Remaining Production gates include independent Terra review, durable audit,
  package/ACL ownership, representative proof, Whites comparison, and all
  environment/customer approvals.
