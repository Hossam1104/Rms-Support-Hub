# Current Task

- **Task ID:** FINAL-LIVE-ACCEPTANCE
- **Status:** Completed Locally
- **Role:** Closed

## Objective

Close the final acceptance gaps around the approved Riyal asset, modern token
based form/grid styling, responsive layout, branch-selector dismissal, UPC
Testing safety evidence, and the documented Cash-on-Delivery contract. Keep
the work local and merge the completed hardening into `main` without pushing or
deploying.

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
  active row; the CDK backdrop closes it on an outside click without stealing
  focus back into the control. Visible amounts use the shared `app-riyal`
  renderer and the approved two-path SAMA vector at its asset path.
- Shared table/caption utilities, global accessibility helpers, and compact
  narrow-screen shell rules keep modern fields and grids within the viewport;
  component style budgets remain unchanged and no longer warn in production.
- The consumer-free Order Validation component tree, duplicate navigation, and
  stale active documentation were removed or reconciled. ADR-0008 records the
  lasting route/resend decision.
- Read-only UPC Testing metadata and branch discovery were exercised. The item
  lookup returned a successful no-data result for documented fixture codes, and
  no explicitly approved synthetic QA branch/item was available, so
  send/cancel/resend was not attempted. No Production action was attempted.
- The unfiltered Order Requests read reproduced the documented infrastructure
  gap: the API returned HTTP 500 after its approximately 15-second timeout;
  an exact-number read returned HTTP 200 with zero results. This is the
  documented missing-index condition on header/invoice join columns, not a
  newly introduced frontend defect.
- The local payment-free draft contract was exercised without an external send:
  zero payments, paid total zero, server balance, exported `COD`,
  `not_payment`, and an empty payment list.
- Edge headless screenshots verified dark-theme layout at 1920x1080,
  1280x720, 900x900, and 600x900. The connected interactive browser was
  unavailable, so pointer, keyboard, theme-toggle, and live detail workflow
  evidence remains blocked.
- The implementation is merged into local `main`; no deployment is performed.

## Validation

- Backend tests: 148/148 passed.
- Frontend tests: 107/107 passed across 22 spec files.
- `npm run test:riyal-asset`: passed; canonical SHA-1
  `02b0fe79a4c8f39f6344682e7ef4dcb5f21cf938`, two vector paths, no text or
  external references.
- Release solution build: 0 warnings, 0 errors.
- `scripts/build.ps1`: all checks passed; Angular production bundle generated
  at 426.93 kB with no style-budget warnings.
- Local Edge headless route checks loaded the landing, UPC order-builder, and
  Order Requests routes and captured the required desktop/tablet/mobile
  screenshots. The connected in-app browser runtime remained unavailable, so
  interactive browser evidence is still deferred.
- No Production send, cancel, or resend was attempted.

## Deferred

- No connected in-app browser was available for interactive responsive/theme,
  hover, keyboard, or outside-click verification; local headless screenshots
  cover layout only.
- No explicitly approved synthetic Testing order/branch/item was available for
  live send/cancel or same-number resend evidence. The UPC Testing item read
  returned no data, and the COD `COD` acceptance send is still deferred.

## Constraints Retained

- Do not invent SQL columns or payload keys; current SQL, fixtures, and schema
  documentation remain authoritative.
- Keep module behavior capability-gated and backend dependencies Core -> Data
  -> API.
- Keep credentials outside tracked files and use Testing for agent-run live
  verification. Never use Production.
- Do not edit generated/runtime paths or store secrets/customer data in `.ai/`.
