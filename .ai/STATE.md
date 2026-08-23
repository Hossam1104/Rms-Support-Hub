# Current Project State

- **Updated:** 2026-08-23
- **Repository baseline:** WPF-05 started from accepted WPF-04 main
  `0b9d0b678cfb33a3828876fb0a980fa8fdeb7676` (PR #35).
- **Working branch:** `feat/wpf-05-logs-support-bundle`.
- **Status:** WPF-05 Logs, safe diagnostic evidence, and Support Bundle
  metadata is implemented, pushed in Draft PR #36, and exact-head CI is green.
  Sol acceptance remains pending.
- **Authority:** CR-001 and ADR-0029 remain accepted; GPT-5.6 Sol is the
  acceptance authority. WPF-06 must not start before Sol accepts WPF-05.

## WPF-05 durable facts

- `LogEvidenceQueryHandler` is the shared, transport-independent read seam.
  It authorizes LocalWpf LocalOperator/LocalAdministrator callers, uses the
  fixed three RMS service identities and existing analyzer/evidence reader,
  bounds each query to 15 seconds, caps records/unknown reasons/
  recommendations, and validates safe aggregate state before returning a
  typed projection.
- The typed `rms.logs.evidence` Local IPC operation accepts no payload. The
  existing Local IPC framing, request/correlation matching, protocol v1,
  response bounds, Windows caller identity, server identity/PID verification,
  ACL, impersonation boundary, and fail-closed authorization remain intact.
- `SupportBundleExecutor` is the shared typed execution seam for local
  Support Bundle generation. It requires local administrator authority,
  verifies the principal/correlation boundary, invokes the existing fixed-root
  redacted/bounded generator, audits once, revokes an artifact if audit is
  unavailable, and records the existing timeline only after successful audit.
- The typed `support.bundle.generate` Local IPC operation accepts a
  correlation ID only and returns validated artifact metadata, creation time,
  expiry, checksum, included sections, and correlation ID. WPF never receives
  ZIP bytes, server paths, arbitrary paths, or an export destination.
- Detailed Logs evidence is collected only when the Logs workspace opens or
  the user presses Refresh. The 30-second automatic health poll does not
  collect detailed evidence. Support Bundle generation is explicit and is not
  automatic.
- WPF owns typed IPC adapters, safe view models, bounded client-side service
  and severity filters, and metadata-only rendering. It has no Agent or
  Infrastructure reference, SQL connection, filesystem reader, process launch,
  HTTPS client, service mutation, database mutation, or provisioning surface.
- The WPF-05 UI shows the fixed service cards, safe evidence records, unknown
  source reasons, bounded recommendations, safe error codes, and Support
  Bundle metadata (artifact, size, checksum, created, expiry, sections,
  correlation, opaque artifact ID). Artifact export/download is explicitly
  deferred to WPF-06.

## Validation evidence

Targeted WPF-05 validation completed:

- Application `LogEvidenceQueryTests`: 6/6.
- WPF `LogsAndSupportBundleViewModelTests`: 5/5.
- WPF `LocalAgentLogsAndSupportBundleClientTests`: 4/4.
- Agent Integration optional-handler fail-closed test: 1/1.
- Existing HTTP Support Bundle/audit-unavailable regression tests: 5/5.
- WPF Debug build and relevant Release project builds: 0 warnings, 0 errors.
- `git diff --check`: passed.

Final validation on the task state passed: strict Release solution build with
Testing-only origin was 0 warnings/0 errors; Domain 12/12, Application 116/116,
Infrastructure 155/155, Agent Integration 238/238, and WPF 57/57 (578/578);
PowerShell quality 37/37; Pester 172 passed, 0 failed, 0 skipped, 0 pending;
`git diff --check` passed; and both context/memory checks passed. The security
classification found only the intended typed Named Pipe boundary, Agent-side
diagnostics/HTTP composition, and redaction keyword checks/tests; no prohibited
WPF machine-access path was introduced.
Implementation and evidence commits are pushed in Draft PR #36; all seven
required checks passed for the latest verified branch head. The PR remains
Draft/open for Sol acceptance.

## Azure and backlog

- Live 2026-08-23 reconciliation: E16 #13017 Active/P2; E17 #13018
  Active/P1; E18 #13019 New/P2; E19 #13020 New/P1.
- WPF children now match live states/priorities: #13022/#13024/#13031 are
  Closed/P1; #13023/#13029/#13030 are Active/P2; #13032/#13033/#13035 are
  Active/P1; #13034 is New/P2; remaining WPF local/future children retain
  their live New priorities.
- #13072-#13076 remain New in the Online Order integrated-testing backlog;
  #12900-#12902 remain New/P3 conditional and #12949 remains New/P3 deferred
  Production acceptance.
- Azure #13035 has implementation and Draft PR #36 evidence for this branch;
  it remains Active/P1 until Sol acceptance and merge.

## Runtime and environment boundary

- Final Release WPF runtime verification passed at
  `pos/src/RmsSupportHub.Pos.Desktop.Wpf/bin/Release/net10.0-windows10.0.19041.0/RmsSupportHub.Pos.Desktop.Wpf.exe`:
  PID 9028, title `RMS Support Hub`, `Responding=True`, exactly one process at
  the exact path. The process required the process-local `WINDIR=C:\WINDOWS`
  environment normalization for WPF font initialization and is left running.
- The computer-use native pipe was unavailable after two required attempts;
  no visible Logs/filter or Support Bundle screenshot/click evidence is claimed.
- No Agent service, operator group, Production endpoint, Production database,
  native RMS state, machine provisioning, or service mutation is authorized or
  claimed.

`.ai/HANDOFF.md` remains `Empty`; no incomplete implementation handoff is
needed while this execution continues.
