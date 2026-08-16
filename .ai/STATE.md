# Current Project State

- **Updated:** 2026-08-16
- **Active branch:** `main`, synchronized with `origin/main`.
- **Status:** Baseline routing and POS test hygiene complete; Production/fleet
  lifecycle platform remains open for GPT-5.6 Luna Max HIGH.
- **Next task:** `TASK.md` remains the full GPT-5.6 Terra HIGH independent
  Production/fleet review prompt awaiting Luna Max HIGH execution.

## Durable implementation facts

- Routing hygiene: `backend/src/RmsSupportHub.Api/Program.cs` uses
  `app.MapFallback("{*path:nonfile}", ...)` without restrictive HTTP verb
  metadata so unknown or retired `/api/**` endpoints return HTTP 404 across all
  methods (GET, POST, PUT, DELETE, PATCH) rather than returning 405 Method Not
  Allowed from SPA fallback method constraints. Angular deep links continue to
  serve the SPA shell on GET/HEAD.
- Frontend test hygiene: `prompt-storage.service.spec.ts` wraps prototype spies
  in `try...finally` with `afterEach` restoration and `localStorage.clear()`;
  `pos-maintenance.component.spec.ts` invokes `TestBed.resetTestingModule()` in
  `afterEach` to guarantee deterministic Vitest worker isolation.
- The permanent product, SCM service, and ownership identity is
  `RmsSupportAgent`; display name is `RMS Support Agent`. The two historical
  Testing service names are migration inputs only. RMS product services,
  including `RMSServiceManager`, are never adopted or removed.
- Slice C code is verified; elevated Testing Agent startup, machine
  certificate/browser policy proof, fleet enrollment, enterprise PKI approval,
  Production signing, Whites comparison, and customer approval are not claimed
  and remain gated for Luna Max HIGH.

## Validation evidence

- Backend tests: 194 passed, 0 failed (`RmsSupportHub.Tests`).
- POS tests: Domain 12, Application 80, Infrastructure 94, Agent integration
  152; 338 passed, 0 failed.
- Broad build (`scripts/build.ps1`): passed with 0 warnings and 0 errors.
- Frontend tests: 58 files / 363 tests passed across three consecutive full runs
  with zero worker exits or test failures.
- Frontend production build: passed cleanly (`108.55 kB` raw POS lazy chunk).
- Repository memory: `check_memory.py` and `git diff --check` pass.

## Runtime and delivery gates

- Local development environment verified with `scripts/dev.ps1`.
- `http://localhost:4200/` and `http://localhost:5200/api/modules/health`
  probed and responding.
