# RMS+ Support Hub — Release Readiness

Date: 2026-08-09

## 1. Release scope

Available:

- QA Prompt Studio
- Online Order Tool

Coming Soon:

- POS Maintenance Tool

POS remains informational and non-operational while the independent POS project
is developed outside this repository. See
[POS_MAINTENANCE_INTEGRATION_READINESS.md](POS_MAINTENANCE_INTEGRATION_READINESS.md).

## 2. Validation summary

Measured on the final cleanup session, 2026-08-09.

| Gate | Result |
| --- | --- |
| Frontend tests | 262 passed across 51 files; 0 skipped |
| Backend tests | 161 passed; 0 failed; 0 skipped |
| Backend Release build | Passed; 0 warnings; 0 errors |
| Standard production build | Passed; 441.43 kB raw / 101.85 kB estimated transfer initial bundle; no budget warnings |
| Lazy `three-module` chunk | 734.66 kB raw / 153.78 kB estimated transfer; Hub-only, dynamically imported |
| Offline production build | Passed; 427.11 kB raw / 101.30 kB estimated transfer initial bundle |
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
- The Online Order landing now renders a neutral bounded empty state when no
  modules are available. It is presentation only: no API change, no service
  contract change, no retry behavior, and no claimed reason, because
  `ModuleService` cannot distinguish an empty response from a failed load.
- POS remains Coming Soon with informational capabilities only. No SQL,
  PowerShell, command, backup/restore, service-control, or operational surface
  is implemented or exposed; negative tests assert that boundary.
- All eight persisted storage keys remain byte-exact. They are listed in
  `.ai/STATE.md` and must not be "cleaned" because their prefixes reflect
  earlier product names.

## 5. Release blockers

None identified in local validation.

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
