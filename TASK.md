# Focused POS Slice B Security Remediation Re-review

**Reviewer:** Claude Opus 5
**Effort:** HIGH
**Role:** REVIEW ONLY

## Objective

Independently review the current synchronized POS Slice B security
remediation. Verify H-1, H-2, and H-3 and the directly adjacent M-1 through M-4
and L-1 through L-3 controls, plus the canonical POS entry path. This is a
focused security gate, not a broad product review and not a new implementation
task.

The review must use the current repository, task-scoped diff, tests, and
durable project memory. The fixed RMS source catalog and sanitized registry
discovery are in
`docs/POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`. The bounded future work
contract is in `docs/POS_SLICE_C_REQUIREMENTS.md`; Slice C requirements are
context only and must not be implemented during this review.

## Required review areas

- H-1: bounded fail-closed redaction before diagnostic stdout/stderr,
  exceptions, timeline summaries, artifacts, snapshots, generated clients,
  and Support Bundle evidence; quarantine behavior on sanitization failure.
- H-2: fixed service-owned roots, provisioning, ownership/ACL checks, reparse
  and traversal confinement, package staging, installation-root verification,
  manifest/checksum/size validation, and no unsafe inherited write boundary.
- H-3: one machine-wide privileged mutation lease across package, Repair
  Installation, and Guided Repair state-changing checkpoints; principal,
  scope, and idempotency bypass resistance; lease held through terminal truth.
- Adjacent M-1 through M-4 and L-1 through L-3: bounded transport and
  redirects, timeout cancellation, exact Main Server endpoint binding,
  snapshot environment/profile identity, retention/corruption handling,
  truthful lifecycle/rollback/recovery states, and typed POST retained
  previews with GET remaining read-only.
- Canonical POS entry: exact external secure origin
  `https://support-hub.integration.test:4443/tools/pos-maintenance`, Hub
  external handoff, wrong-origin route guard, preserved exact Origin policy,
  Windows Negotiate identity, derived Administrator authorization, and direct
  browser-to-Agent transport with no Support Hub API relay or generic proxy.
- RMS evidence: fixed Branch/Cashier/update/download/log/setup/ReleaseRepo
  sources, metadata-only POS insurance evidence, exact SCM/source identity,
  sanitized uninstall registry allow-list, and read-only Testing discovery.

## Review constraints

- Review only. Do not modify implementation, tests, generated artifacts,
  documentation, or project memory, and do not broaden the review.
- Use synthetic seams and existing tests for validation. Testing is the only
  permitted live environment.
- Do not launch RMS Branch, Cashier, Service Manager, Cashier UI, installer,
  uninstaller, repair, rollback, or package activation executables.
- Do not issue Main Server state-changing requests, mutate a registry or RMS
  folder, modify a database, or use Production/customer data.
- Preserve and verify the exact Origin, Negotiate, Administrator, and direct
  browser-to-Agent boundaries; do not treat a successful HTTP acceptance as
  proof of a completed privileged action.
- Do not implement Slice C requirements during this review.

## Evidence and outcome

Inspect the complete task-scoped diff and relevant source/tests. Run only
read-only or synthetic checks appropriate to the review. Report findings in
severity order: Critical, High, Medium, Low, or Informational. Any Critical or
High finding blocks the Slice C gate and must include file/line evidence,
impact, and a concrete acceptance condition.

Return a concise report containing:

1. Executive outcome and blocker status.
2. Findings with file/line evidence and acceptance conditions.
3. Boundary-by-boundary coverage against H-1/H-2/H-3, M-1..M-4, and L-1..L-3.
4. Canonical POS entry and exact transport/authentication coverage.
5. RMS source/registry discovery and safety-boundary coverage.
6. Validation evidence, unrelated failures, residual assumptions, and the
   explicit Production/Slice C gates that remain.

Do not execute this review during the implementation/delivery session.
