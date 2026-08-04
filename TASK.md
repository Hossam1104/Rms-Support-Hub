# Current Task

- **Task ID:** ORDER-REQUESTS-FILTERS
- **Status:** Completed Locally — Database Deployment Pending
- **Role:** Closed

## Objective

Correct Order Requests filtering end to end, keep list/count/stats consistent,
prevent stale or hanging searches, and modernize the operator search header
without changing payment, ordering, details, cancel, resend, payload, or
Production-safety contracts.

## Delivered

- API input is normalized and validated; exact order search is the default,
  partial search is escaped, phone searches use the last nine digits, branch
  and statuses use canonical values, and date-to is end-exclusive.
- The repository uses a canonical filter model for list/count/stats, distinct
  request counts, stable paging/sorting, reduced list projection, cancellation
  tokens, a bounded timeout, and sanitized retryable API errors.
- UPC Testing received the reviewed idempotent join-index script only. The
  script is tracked at `docs/sql/order-requests-performance-indexes.sql`;
  Production was not accessed or changed.
- The Angular workbench now uses explicit Apply with an order-number Enter
  shortcut, shared token-based controls, a dedicated status row, active chips,
  refresh/auto-refresh actions, retryable loading/error states, and responsive
  narrow-screen behavior. The branch selector closes through the CDK outside
  click path.

## Validation evidence

- Focused backend Order Requests tests: 47 passed.
- Focused frontend Order Requests/searchable-select tests: 20 passed.
- UPC Testing read-only filter matrix passed after the index script was
  applied; no send, cancel, resend, or Production operation was attempted.
- Edge headless screenshots covered the local Order Requests route at desktop,
  tablet, and 390px mobile widths. The connected interactive browser was not
  available, so pointer/keyboard/theme interaction evidence remains external.

## Closeout

The feature branch was committed as logical backend, UI state, UI design, and
documentation changes, then merged into local `main` with a no-fast-forward
merge. Repository synchronization and temporary-branch cleanup are the final
closeout steps. No deploy.
