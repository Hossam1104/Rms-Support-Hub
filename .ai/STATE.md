# Current Project State

- **Updated:** 2026-08-15
- **Repository baseline:** PR #11 merged as `2bc8dfc`; POS Slice A PR #12
  merged as `fb71d01`.
- **Current branch:** `main`, fast-forwarded to `origin/main` at `fb71d01` with
  a clean worktree after the authorized delivery merges.
- **Current outcome:** POS Slice A implementation and validation are complete;
  no RMS executable, installer/uninstaller, database mutation, Production
  action, or Main Server mutation was run.
- **Next executable task:** after Slice A delivery, root `TASK.md` contains the
  full Slice B implementation task for Main Server profiles, Safe Diagnostic
  Console Run, Safety Snapshot, Repair/Guided Repair, and the real Agent
  package boundary. Do not execute Slice B in the Slice A session.

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
  controls, and Slice B planned states. It uses design tokens and responsive
  desktop/tablet/narrow breakpoints.
- Main Server integration remains GET-only and read-only. Secret-bearing
  inventory DTOs and the discovered Branch/POS PUT contracts are not proxied
  or invoked.

## Validation evidence

- POS Release build with `PosAgentSecurity__SupportHubOrigin` set and
  `-warnaserror` passed.
- Full POS solution tests passed: Domain 9, Application 76, Infrastructure 83,
  Agent integration 149 (317 total).
- Frontend tests passed: 56 files, 345 tests. Production frontend build passed.
- POS OpenAPI/client generation passed twice; the second generated-client pass
  was byte-stable. `git diff --check` passed.
- The memory checker reports the pre-existing `AGENTS.md` 146-line budget
  violation; all other checked memory files are within budget after the Slice B
  task replacement. No generated/runtime paths are part of the source change.

## Existing Main Server and runtime gates

- Main Server Swagger evidence is read-only and environment-specific. Branch/POS
  install-state PUTs have no proven package, operation, polling, cancellation,
  idempotency, or rollback contract; Slice B must add an Agent-owned profile
  boundary before any mutation is considered.
- Testing is the only live environment. Production/customer deployment remains
  blocked on M-1/M-2, durable audit, package/ACL ownership, representative
  proof, Whites comparison, and independent review.
