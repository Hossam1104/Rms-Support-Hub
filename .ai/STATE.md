# Current Project State

- **Updated:** 2026-08-15
- **Repository baseline:** PR #14 is merged as `19f609b` on synchronized
  `main`; PR #13 was `8192141` and POS Slice A PR #12 was `fb71d01`.
- **Slice B remediation:** The task-scoped H-1/H-2/H-3 and adjacent M/L
  remediation is merged and delivered. `TASK.md` is now the bounded
  independent Claude Opus 5 security-review handoff.
- **Current outcome:** Fixed service-owned roots, bounded fail-closed
  redaction, a machine-wide privileged mutation lease, typed POST previews,
  bounded Main Server transport, exact endpoint binding, configured snapshot
  identity, and the canonical secure POS entry are implemented. No RMS
  executable, installer/uninstaller, repair, package activation, registry or
  RMS folder mutation, database mutation, Production action, or Main Server
  mutation was run.
- **Next executable task:** perform the independent security review specified
  in `TASK.md`. Slice C requirements are durable in
  `docs/POS_SLICE_C_REQUIREMENTS.md` and are not implemented.

## Durable Slice A facts

- Product Release is read only from
  `C:\ProgramData\RMS_Plus\ReleaseNumber.txt`; missing, invalid, unreadable,
  or control-bearing content is unavailable and never falls back to a build.
- Client is read from `RMS.CashierUI\appsettings.json` at
  `Settings:TheClient`. Component BuildNumbers remain separate drift evidence.
- Canonical SCM names are `RMS.BranchService`, `RMS.CashierService`, and
  `RMSServiceManager`. Friendly display labels remain separate from SCM names.
- Agent-owned typed contracts now expose read-only Health Check, database and
  backup/capacity health, bounded redacted service-failure analysis, and a
  principal-scoped bounded Incident Timeline. Timeline writes occur only after
  authorized POST operation boundaries; read-only GETs do not mutate state.
- Support Bundle generation is a protected one-use-token POST. It contains a
  bounded typed JSON projection in an opaque authenticated artifact and does
  not proxy raw configuration, credentials, paths, SQL, or unbounded logs.
- `WindowsRmsDiagnosticEvidenceReader` reads only fixed canonical log roots and
  allow-listed SCM/.NET/Application Error/WER event IDs, with file, byte, line,
  event, time, stack, and redaction bounds. The analyzer classifies and
  recommends only; it never launches a process or changes service state.
- The Angular POS workspace has the final compact header/peer-status rail,
  paired Branch/Cashier database cards, canonical three-service table,
  Health/Timeline/Bundle actions, bounded evidence panels, preserved PR #10
  controls, and typed Slice B boundary panels. It uses design tokens and responsive
  desktop/tablet/narrow breakpoints.
- Main Server state reads remain GET-only and read-only; retained Agent previews
  are typed POST operations. Secret-bearing inventory DTOs and the discovered
  Branch/POS PUT contracts are not proxied or invoked.
- The Hub POS card hands the browser to the exact canonical Testing route
  `https://support-hub.integration.test:4443/tools/pos-maintenance`; the route
  guard rejects wrong-origin direct loads while preserving the direct
  browser-to-Agent boundary.
- Fixed RMS source roots and the sanitized uninstall registry allow-list are
  recorded in `docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`.
- Slice B boundary details and route inventory are recorded in
  `docs/POS_SLICE_B_BOUNDARY.md`.

## Validation evidence

- POS Release build with `PosAgentSecurity__SupportHubOrigin` set and
  `-warnaserror` passed.
- Full POS solution tests passed: Domain 9, Application 76, Infrastructure 90,
  Agent integration 152 (327 total).
- POS Release solution build passed with warnings treated as errors: 0
  warnings, 0 errors.
- Frontend tests passed: 56 files, 345 tests. Production frontend build passed.
- POS OpenAPI contract tests passed (9 checks); client generation passed twice
  with a byte-stable second pass. `git diff --check` passed.
- The memory checker reports only the pre-existing root `AGENTS.md` 146-line
  budget violation; all other checked memory files are within budget. The POS
  solution checks and frontend checks passed independently. The prescribed
  `scripts/build.ps1` gate reached backend tests and reported 190/192: its two
  failures are legacy-route assertions expecting 404 while the current API
  correctly returns 405. Final merged-main runtime probes returned HTTP 200
  for `http://localhost:4200/tools/pos-maintenance` and
  `http://localhost:5200/api/modules/health`; the Testing-only launcher then
  stopped at its required elevated-Administrator gate. The successful API and
  frontend development processes remain running.

## Existing Main Server and runtime gates

- Main Server Swagger evidence remains read-only and environment-specific.
  Slice B adds an Agent-owned fixed profile/read boundary; Branch/POS
  install-state PUTs remain acknowledgements and are not invoked.
- Testing is the only live environment. Production/customer deployment remains
  blocked on M-1/M-2, durable audit, package/ACL ownership, representative
  proof, Whites comparison, and independent review.
