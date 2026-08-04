# Current Task

- **Task ID:** ORDER-REQUESTS-UNIFICATION
- **Status:** Completed Locally
- **Role:** Closed

## Objective

Unify the Order Requests and superseded validation experiences, make the
canonical detail view a stable full page, enforce the final resend rule using
the selected stored request and original number, stabilize branch selection,
reconcile Riyal rendering and project documentation, and prepare a clean local
merge into `main`.

## Closeout

- `/modules/:key/order-requests` is the single guarded list/detail route;
  `/requests` and `/validation` remain compatibility redirects.
- The detail page presents Items, Order Info, Transactions, Request JSON, and
  Response JSON in that order. Request/response sections are collapsed by
  default and missing data has explicit safe states.
- Resend blocks New (1), With_Delegate (4), and unknown statuses. For every
  other known status, the API verifies the stored `order_code`, reuses the
  original number, changes only `branch_code`, preserves unknown fields, and
  leaves the historical row unchanged. Production confirmation and duplicate
  submit guards remain active.
- Branch selection keeps fixed option geometry and keyboard ownership of the
  active row. Visible amounts use the shared `app-riyal` renderer and its
  approved asset path; the checked-in asset itself is still an unapproved
  placeholder awaiting replacement.
- The consumer-free Order Validation component tree, duplicate navigation, and
  stale active documentation were removed or reconciled. ADR-0008 records the
  lasting route/resend decision.
- The implementation is merged into local `main` with the requested
  no-fast-forward merge. Nothing is pushed or deployed.

## Validation

- Backend tests: 145/145 passed.
- Frontend tests: 105/105 passed across 21 spec files.
- Release solution build: 0 warnings, 0 errors.
- `scripts/build.ps1`: all checks passed; Angular production bundle generated
  with two non-blocking style-budget warnings.
- No Production send, cancel, or resend was attempted.

## Deferred

- No browser instance was available for responsive/theme visual verification.
- No approved safe Testing order was available for live Order Requests,
  send/cancel, or same-number resend evidence.
- The approved `frontend/public/assets/Saudi_Riyal.svg` replacement is still
  required; no symbol was fabricated, redrawn, or downloaded.

## Constraints Retained

- Do not invent SQL columns or payload keys; current SQL, fixtures, and schema
  documentation remain authoritative.
- Keep module behavior capability-gated and backend dependencies Core -> Data
  -> API.
- Keep credentials outside tracked files and use Testing for agent-run live
  verification. Never use Production.
- Do not edit generated/runtime paths or store secrets/customer data in `.ai/`.
