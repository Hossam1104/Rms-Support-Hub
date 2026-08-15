# Current Project State

- **Updated:** 2026-08-16
- **Active branch:** `main`, synchronized with `origin/main` after PR #18.
- **Status:** POS Slice C implementation, validation, Git delivery, and local
  runtime smoke checks are complete; Production/fleet evidence remains gated.
- **Next task:** `TASK.md` is the full GPT-5.6 Terra HIGH independent
  Production/fleet security and release-readiness review prompt.

## Durable implementation facts

- The permanent product, SCM service, and ownership identity is
  `RmsSupportAgent`; display name is `RMS Support Agent`. The two historical
  Testing service names are migration inputs only. RMS product services,
  including `RMSServiceManager`, are never adopted or removed.
- Slice C adds bounded package identity/trust and installed-manifest
  verification, one-handoff plan-first bootstrap/lifecycle scripts, explicit
  browser and certificate plans, durable sanitized JSONL audit, fixed RMS-root
  operational health, update state, insurance-attachment aggregate, and the
  redesigned typed POS operations console.
- The Support Hub remains a direct secure Agent client. The general Hub API is
  not a privileged POS relay; runtime OpenAPI is non-Production only; generated
  OpenAPI/TypeScript artifacts are contract governed.
- H-1 redaction/quarantine, H-2 fixed service-owned roots, H-3 mutation lease,
  exact secure-origin handoff, Testing-default safety, and no Production/RMS/
  Main Server/SCM/registry/certificate mutation remain in force.
- Fixed health and Support Bundle outputs are bounded summaries. They do not
  expose raw paths, filenames, log contents, credentials, or attachment bytes.
- Current shell is not Administrator. Elevated Testing Agent startup, machine
  certificate/browser policy proof, fleet enrollment, enterprise PKI approval,
  Production signing, Whites comparison, and customer approval are not claimed.

## Validation evidence

- PowerShell quality: 26 tracked scripts parse cleanly with
  `test-powershell-quality.ps1 -SkipScriptAnalyzer`.
- Pester: 117 passed, 0 failed, including 9 Slice C deployment tests.
- POS Release build: passed with `-warnaserror`, 0 warnings and 0 errors.
- POS tests: Domain 12, Application 80, Infrastructure 94, Agent integration
  152; 338 passed, 0 failed.
- Frontend: 58 files / 363 tests passed in two consecutive full runs;
  production build passed. The POS lazy chunk is 108.55 kB raw.
- OpenAPI/client generation passed twice; the generated client remained stable
  on the second pass. `git diff --check` and `check_memory.py` pass.
- Backend Release build passed with 0 warnings/errors. `scripts/build.ps1`
  first encountered a verified project-owned API DLL lock; after that process
  was stopped, it reached backend tests and exposed two unchanged legacy route
  expectations (`NotFound` expected, `MethodNotAllowed` actual). No backend
  source was changed for Slice C.

## Runtime and delivery gates

- `scripts/dev.ps1` was restarted after verified project-owned stale processes
  were stopped. Actual `http://localhost:4200/`,
  `http://localhost:4200/tools/pos-maintenance`, and
  `http://localhost:5200/api/modules/health` each returned HTTP 200; the
  successful project-owned frontend/backend processes remain running.
- No Testing or Production Agent provisioning, service control, certificate,
  registry, RMS folder/database, Main Server, customer, or fleet mutation was
  executed.
- PR #18 merged as `0b380ef19eb17e9b911bd89aa62609c6d49c5ec2`; all six CI jobs
  passed. The implementation commit is `a698fe0` and local `main` is aligned
  with `origin/main`.
