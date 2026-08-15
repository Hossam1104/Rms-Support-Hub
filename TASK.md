# Independent POS Security / Testing Provisioning Gate Re-review

**Preferred reviewer:** GPT-5.6 Terra
**Effort:** HIGH
**Role:** REVIEW ONLY

## Objective

Independently re-review the current synchronized POS security and Testing
provisioning surface after the "Testing provisioning, PowerShell and secure
routing remediation". Confirm that the previously closed Slice B controls
remain closed, and that the new provisioning, elevation, runtime-ownership,
build-identity, and routing boundaries are actually sound.

The implementer of that remediation must not perform this review.

Use the current repository, the task-scoped diff, tests, and durable project
memory. Context lives in `.ai/STATE.md`,
`.ai/decisions/ADR-0025-testing-runtime-ownership-and-build-identity.md`,
`docs/POS_SLICE_B_BOUNDARY.md`,
`docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`, and
`docs/POS_SLICE_C_REQUIREMENTS.md`. Slice C requirements, including the
owner-approved POS visual redesign direction, are context only and must not be
implemented.

## Required review areas

1. **Slice B regression.** H-1 bounded fail-closed redaction and quarantine,
   H-2 fixed service-owned roots and traversal/ownership confinement, and H-3
   the single machine-wide privileged mutation lease remain closed. The
   adjacent M-1..M-4 and L-1..L-3 remediation remains sound.
2. **Certificate validation.** `Test-PosSupportHubPrivateKeyPolicy` evaluates
   CNG-ness, key storage provider, and export policy as one boolean expression;
   a wrong provider or an exportable key fails closed; the private-key ACL
   rejects broad principals; failure terminates provisioning; no certificate or
   private-key material is printed.
3. **PowerShell surface.** Every tracked `.ps1`/`.psm1` parses; no operator is
   parsed as a command; no dangling continuation, unsafe partial state, path
   quoting, `Start-Process` argument, or working-directory defect remains.
   Confirm `scripts/test-powershell-quality.ps1` runs under both `-File` and
   `-Command` invocation and that the CI lane actually gates.
4. **Elevation confinement.** Self-elevation re-launches only the known startup
   script, only from a PowerShell host inside `$PSHOME`, with only the known
   typed parameters, correct quoting for paths containing spaces, and a working
   opt-out. Prove no arbitrary command, executable, path, or argument can be
   injected, including through the forwarded origin.
5. **Secure `:4443` runtime ownership.** An unowned listener is neither killed
   nor adopted; a stale owned listener is replaced only through the normal
   flow; unrelated `dotnet` processes are never stopped; a stale or corrupt
   state file fails closed; state binds runtime root, PID, executable, content
   root, build identity, certificate, host, and port.
6. **Frontend freshness.** The expected, staged, and served build identities
   must agree, including the index document and main bundle hashes. A stale
   build must be a typed startup failure. HTTP 200 and the presence of
   `<app-root>` must not be accepted as proof. The identity document and the
   Advanced Diagnostics panel must expose no filesystem path or secret.
7. **Canonical POS routing and transport.** The dashboard card and a direct
   wrong-origin `/tools/pos-maintenance` load both hand off to exactly
   `https://support-hub.integration.test:4443/tools/pos-maintenance` without
   first rendering Agent-unavailable errors. The Agent still requires the exact
   Origin, HTTPS, Windows Negotiate, and derived local Administrator
   authorization; the browser still reaches the Agent directly; there is no
   `RmsSupportHub.Api` relay, generic proxy, browser-selectable Agent endpoint,
   or `http://localhost:4200` allowance.

## Review constraints

- Review only. Do not modify implementation, tests, generated artifacts,
  documentation, or project memory, and do not broaden the review.
- Use synthetic seams and existing tests. Testing is the only permitted live
  environment; never act against Production.
- Do not launch RMS Branch, Cashier, Service Manager, Cashier UI, installer,
  uninstaller, repair, rollback, or package activation executables.
- Do not perform Branch Reset, Cleanup, Restore, database mutation, registry or
  RMS folder mutation, or any Main Server state-changing request.
- Do not treat a successful HTTP acceptance as proof of a completed privileged
  action.
- Do not implement Slice C.

## Evidence and outcome

Inspect the complete task-scoped diff and the relevant source and tests. Run
only read-only or synthetic checks. Report findings in severity order:
Critical, High, Medium, Low, Informational. Each Critical or High finding must
carry file/line evidence, impact, and a concrete acceptance condition, and
blocks the Slice C gate.

Return a concise report containing:

1. Executive outcome and blocker status.
2. Findings with file/line evidence and acceptance conditions.
3. Coverage of H-1/H-2/H-3 and the adjacent M/L controls.
4. Coverage of certificate validation, PowerShell parse health, elevation
   confinement, `:4443` runtime ownership, and frontend build identity.
5. Coverage of canonical POS routing, exact Origin, Negotiate, Administrator
   authorization, and the direct browser-to-Agent boundary.
6. Validation evidence, unrelated pre-existing failures, residual assumptions,
   and the explicit Production/Slice C gates that remain.
