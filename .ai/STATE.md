# Current Project State

- **Updated:** 2026-08-15
- **Repository baseline:** the POS Testing provisioning and secure frontend
  routing remediation is merged on synchronized `main`; the preceding baselines
  were PR #14 (`19f609b`), PR #13 (`8192141`), and POS Slice A PR #12 (`fb71d01`).
- **Slice B remediation:** H-1/H-2/H-3 and the adjacent M/L work stay merged.
- **Current outcome:** fixed service-owned roots, bounded fail-closed
  redaction, a machine-wide privileged mutation lease, typed POST previews,
  bounded Main Server transport, exact endpoint binding, configured snapshot
  identity, and the canonical secure POS entry are implemented, plus the
  Testing provisioning hardening below. No RMS executable, installer,
  uninstaller, repair, package activation, registry, RMS folder, database,
  Production, or Main Server mutation was run. Production readiness is not
  claimed.
- **Next executable task:** the independent POS security / Testing provisioning
  gate re-review specified in `TASK.md`. Slice C requirements, including the
  owner-approved POS visual redesign direction, are durable in
  `docs/POS_SLICE_C_REQUIREMENTS.md` and are not implemented.

## Durable Testing provisioning facts

- A multiline boolean in the Testing certificate check had no line
  continuation, so PowerShell parsed `-and ...` as a command. The script parsed
  cleanly and failed only at runtime with `The term '-and' is not recognized`,
  and the private-key provider half never contributed to the decision. Policy
  is now one boolean expression in `Test-PosSupportHubPrivateKeyPolicy`, and
  `scripts/test-powershell-quality.ps1` rejects the shape repository-wide.
- The Testing certificate must be CNG, from the Microsoft Software Key Storage
  Provider, non-exportable, with a private-key file granting no broad principal
  (Everyone, Authenticated Users, Users, Anonymous, Guests). Any failure
  terminates provisioning; no key material is printed.
- `scripts/start-pos-agent-testing.ps1` self-elevates through UAC by
  re-launching only itself, only from a PowerShell host inside `$PSHOME`, with
  only its known typed parameters, quoting spaced paths. `-NoSelfElevate` is the
  CI opt-out; the boundary is Testing tooling only, Production is Slice C.
- Startup stops only the runtime whose PID, image, and command line it owns,
  rebuilds the current Angular production frontend and API publish, stages the
  frontend into the current backend `wwwroot`, and re-verifies the launched PID.
  An unowned `:4443` listener is never killed or adopted; a stale owned one is
  replaced through the normal flow; state binds runtime root, PID, executable,
  content root, build identity, certificate, host, and port.
- A Windows Service reaches Running before its host binds a socket, so
  provisioning waits for the loopback-only listener with a bounded deadline
  (`Wait-PosLoopbackOnlyListener`) rather than sampling the port once. It stays
  fail-closed: a routable listener is rejected at once, an absent one throws.
- Frontend freshness is proved by identity, not by HTTP 200:
  `frontend/scripts/build-identity.mjs` emits a non-secret
  `/build-identity.json` (commit, source state, deterministic asset-manifest
  build id, asset count, timestamp, index and main-bundle hashes). Startup fails
  closed unless expected, staged, and served identities match and the served
  index and main bundle hash as recorded; neither it nor Advanced Diagnostics
  exposes a filesystem path.

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
  guard hands off wrong-origin direct loads to the same secure path instead of
  rendering Agent-unavailable errors, and the Agent still trusts only that
  exact Origin - `http://localhost:4200` is never allowed.
- Fixed RMS source roots and the sanitized uninstall registry allow-list are
  recorded in `docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`.
- Slice B boundary details and route inventory are recorded in
  `docs/POS_SLICE_B_BOUNDARY.md`.

## Validation evidence

- POS Release build with `PosAgentSecurity__SupportHubOrigin` set and
  `-warnaserror` passed: 0 warnings, 0 errors.
- Full POS solution tests passed: Domain 9, Application 76, Infrastructure 90,
  Agent integration 152 (327 total).
- Frontend tests passed: 58 files, 361 tests. Production frontend build passed.
- Pester suites passed: 94 tests across provisioning, cleanup, configuration,
  script quality, and runtime ownership/build identity. They bind no machine
  state, so a clean CI runner and the owner's machine agree.
- `scripts/test-powershell-quality.ps1` passed: 21 tracked PowerShell files
  parse with no operator-as-command or dangling-continuation findings.
  PSScriptAnalyzer is absent locally, so the native gate is the only local
  signal; CI runs the same gate.
- Client generation passed twice with a byte-stable second pass;
  `git diff --exit-code` on the generated OpenAPI and Angular client and
  `git diff --check` both passed.
- The memory checker reports only the pre-existing root `AGENTS.md` 146-line
  budget violation; all other checked memory files are within budget.

## Existing Main Server and runtime gates

- Main Server Swagger evidence remains read-only and environment-specific.
  Slice B adds an Agent-owned fixed profile/read boundary; Branch/POS
  install-state PUTs remain acknowledgements and are not invoked.
- Testing is the only live environment. Production/customer deployment remains
  blocked on M-1/M-2, durable audit, package/ACL ownership, representative
  proof, Whites comparison, and independent review.
