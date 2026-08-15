# RMS+ Support Hub — Release Readiness

Date: 2026-08-16

## 1. Release scope

Available:

- QA Prompt Studio
- Online Order Tool

Available with an evidence gate:

- POS Maintenance Tool — secure Testing Agent foundation

The POS route is an operations console backed by the separate permanent
`RmsSupportAgent` service. Repository contracts and local validation do not
prove elevated Testing runtime, fleet enrollment, Production signer/PKI,
customer approval, or Production readiness. See
[POS_SLICE_C_IMPLEMENTATION.md](POS_SLICE_C_IMPLEMENTATION.md).

## 2. Validation summary

Measured on the Slice C implementation validation session, 2026-08-16. The
older release-candidate values below are retained only where a check was not
rerun; current Slice C evidence is summarized in
[POS_SLICE_C_IMPLEMENTATION.md](POS_SLICE_C_IMPLEMENTATION.md).

| Gate | Result |
| --- | --- |
| Frontend tests | 363 passed across 58 files in two consecutive runs; 0 skipped |
| Backend tests | 190 passed, 2 unchanged legacy route-test failures (`NotFound` expected, `MethodNotAllowed` actual) |
| Backend Release build | Passed; 0 warnings; 0 errors |
| POS Release build | Passed; 0 warnings; 0 errors |
| Standard production build | Passed; 483.64 kB raw / 107.52 kB estimated transfer initial bundle; no budget warnings |
| Lazy `three-module` chunk | 734.66 kB raw / 153.78 kB estimated transfer; Hub-only, dynamically imported |
| Offline production build | Not rerun in Slice C validation |
| Riyal asset verification | Passed; SHA-1 `02b0fe79…`, 924 bytes, 2 paths, no text or external references |
| Security review | No credential, token, or connection-string value in the release diff |
| Rendered browser matrix | Not re-run this session — see below |

The full rendered route matrix was last executed during the branding programme:
all 9 routes at 1440, 1024, 900, 768, and 390 px widths in light and dark plus
reduced motion, each with one H1, one main landmark, no shell overflow, no
unlabeled control, and no broken image; Three.js appeared only on the Hub in
full motion and was released on navigation away. The owner waived the repeat
smoke because the application source had not materially changed since. This
document does not claim a browser run that did not occur.

## 3. Included routes

The current route topology is in
[REPOSITORY_STRUCTURE.md](REPOSITORY_STRUCTURE.md). The pre-hub
`/modules/:key/...` mount remains a supported compatibility path and shares the
same route factory — including the capability guard on Order Requests — as the
canonical `/tools/online-orders/...` mount.

## 4. Behavior preservation

- Prompt Studio builders retain their canonical section contracts: Bug 11,
  Story 7, Test Case 9. Prompt Quality stays local, deterministic, advisory, and
  non-blocking. History stays local-only and capped at ten records. Drafts,
  Open/Copy/Delete/Clear, Markdown and plain-text export, and
  `Ctrl`/`Cmd`+`Enter` are unchanged and covered by unit tests. No external AI
  or API transmission path exists.
- Online Order API endpoints, DTOs, JSON contracts, payload mappings, module
  keys, capabilities, payment values, statuses, filtering, sorting, paging,
  totals, and send/cancel/resend behavior are unchanged. No state-changing
  Online Order action was executed.
- UPC Testing and UPC Production are supported through the existing environment
  architecture. UPC Production read-side routing uses the server-owned
  `RmsMainProd` catalog derived from the existing UPC Testing connection
  details; Production database checks remain read-only and no Production order
  mutation is validated here.
- The Online Order landing now renders a neutral bounded empty state when no
  modules are available. It is presentation only: no API change, no service
  contract change, no retry behavior, and no claimed reason, because
  `ModuleService` cannot distinguish an empty response from a failed load.
- POS Slice C exposes the typed Agent-backed operational surface and plan-first
  deployment contracts. No Production, Main Server, RMS database/folder,
  registry, SCM, certificate, or customer mutation was executed; live/fleet
  evidence remains a separate gate.
- All eight persisted storage keys remain byte-exact. They are listed in
  `.ai/STATE.md` and must not be "cleaned" because their prefixes reflect
  earlier product names.

## 5. Release blockers

Two pre-existing backend legacy-route test failures remain as noted above; no
new Slice C-specific blocker was identified by the targeted validation.

This is a release-candidate recommendation only. It is not Production approval,
WCAG certification, or a global security certification.

## 6. Deferred items

- POS integration, after the independent POS project is complete and a source
  assessment is authorized.
- `ConnectionStrings:UpcEcommerceTest` is not configured in the local Testing
  environment, so Testing-only UPC order and order-request calls return HTTP
  500. Deferred environment setup, not an application defect.
- UPC Testing fixture and live state-changing acceptance. No COD acceptance,
  send, resend, or cancellation claim is made here.
- UPC Production database runtime validation was not executed in this local
  session; resolver and repository-boundary tests use fakes and do not contact
  the Production database or order API.
- Production database performance indexes, pending database-owner approval.
- Hosting and deployment topology, SPA fallback, target-server validation, and
  Production acceptance.
- Timer and lifecycle observations from the hardening session remain accepted
  low-risk items; no reproducible defect was found.

## 7. Deployment preconditions

Deployment was not executed. Before any deployment decision, the owning team
must document and validate the actual target environment and approved access
path, then confirm:

- approved deployment window and release owner;
- target configuration and secret injection without committing credentials;
- hosting and SPA fallback behavior for canonical and legacy routes;
- health-check and smoke-test procedure;
- package backup and rollback location;
- the Production database index decision;
- the UPC Testing acceptance decision, before making any live COD or
  state-changing workflow claim.

The repository does not define an authoritative IIS, transfer, server, or
health-endpoint topology, so those details are intentionally not invented here.

## 8. Rollback expectations

Retain the previous deployed package and preserve its compatible configuration.
If release health checks fail, stop the rollout, restore the previous
application package using the approved deployment mechanism, verify the
documented health check and route smoke tests, and record the result. No
rollback was executed.

## 9. Final release recommendation

**READY WITH DEFERRED ITEMS**

The codebase is ready for a release candidate based on local validation.
Deployment, Production acceptance, database-owner decisions, and future POS
integration remain pending and outside this scope.
