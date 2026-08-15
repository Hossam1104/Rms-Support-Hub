# Independent POS Privileged Repair / Installation Security Review

**Reviewer:** Claude Opus 5
**Effort:** HIGH
**Role:** REVIEW ONLY

## Objective

Perform an independent security and readiness review of the delivered POS Slice
B privileged repair, installation, diagnostic, Main Server, Safety Snapshot,
package, and Guided Repair boundaries. Review the current repository, tests,
task-scoped diff, and durable project memory. Do not reconstruct the project
from chat history.

Slice B is delivered as a typed Agent-backed boundary. Testing is the only
permitted live environment. Slice C remains out of scope. Critical or High
findings block Slice C and must be reported with evidence, affected boundary,
impact, and a concrete acceptance condition.

## Required review areas

Review arbitrary process-launch prevention; executable-manifest confinement;
argument, environment, and working-directory confinement; process-tree timeout
and termination; output bounds and redaction; Main Server profile allow-listing;
absence of a generic API proxy; Branch/POS identity binding; API credential
handling; and mutation isolation from GET/read endpoints.

Review Safety Snapshot integrity, principal/environment/package binding, expiry,
fixed-root storage, atomicity, corruption handling, retention, and fail-closed
verification. Review package signature and checksum validation, compatibility,
archive traversal, duplicate and absolute paths, reparse/symlink escape,
package ownership, ACL and service ownership, certificate/private-key handling,
installer and uninstaller confinement, rollback material, and recovery truth.

Review Repair confirmation, one-use mutation tokens, idempotency, concurrency,
fresh-snapshot enforcement, state-changing step isolation, rollback behavior,
and Guided Repair confinement. Confirm that recommendations cannot become
arbitrary generated commands or actions, that only server-owned typed repair
checkpoints exist, and that no health check performs automatic repair.

Review Testing/Production separation, secret leakage through Angular contracts,
logs, artifacts, bundles, generated clients, persisted evidence, and error
responses. Confirm there is no arbitrary SQL, filesystem, shell, process,
service, URL, credential, or browser-supplied target capability.

## Review constraints

- Review only. Do not remediate findings, modify implementation, or broaden
  Slice B in this review.
- Do not launch RMS Branch, Cashier, Service Manager, Cashier UI, installer,
  uninstaller, repair, rollback, or package activation executables.
- Do not issue Main Server mutations or use Production/customer data.
- Use synthetic seams and existing tests for boundary validation.
- Do not mark a boundary safe merely because an HTTP request was accepted;
  distinguish accepted, running, completed, partial, failed, rollback-failed,
  recovery-required, and unknown outcomes.
- Do not execute this Opus review during the Slice B delivery session.

## Evidence and outcome

Inspect the complete Slice B diff and relevant current code/tests. Run only
read-only or synthetic checks appropriate to review. Record concrete findings
with severity: Critical, High, Medium, Low, or Informational. Critical and High
findings are release blockers for Slice C. If no blocker is found, state the
remaining assumptions, unavailable proof, and explicit gates still required
before Production or Slice C.

Return a concise review report with:

1. Executive outcome and blocker status.
2. Findings ordered by severity, with file/line evidence.
3. Boundary-by-boundary coverage against this task.
4. Validation evidence and any known unrelated failures.
5. Residual risks, assumptions, and required follow-up.
