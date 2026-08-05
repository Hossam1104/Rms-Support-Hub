# ADR-0010: Order Requests Clear All reset transaction

- **Status:** Accepted
- **Date:** 2026-08-05
- **Affected area:** Order Requests Angular store, filter bar, route state, and
  request lifecycle

## Context

Order Requests has draft controls, applied filters, pagination, URL state, and
refresh requests that can overlap. A partial Clear All implementation could
leave a draft value, active chip, URL alias, page number, previous rows, or a
late response visible after the operator had cleared the search.

## Decision

Clear All is treated as one state transition. It obtains a new default filter
object from `createDefaultOrderRequestFilters()`, resets the draft and applied
models to that shape, returns to page 1, clears the current result snapshot,
invalidates and unsubscribes the prior request, removes canonical and legacy
filter query parameters, and starts exactly one unfiltered request. Page size,
sort, and refresh preferences are not filter state and remain unchanged.

The store's request-generation token is the authority for applying results or
errors. A response from an invalidated generation cannot overwrite the cleared
state. A forced clear is allowed when only the draft differs, so the button
always has the same observable reset behavior when it is enabled.

## Consequences

- Reload, refresh, auto-refresh, retry, and browser history operate from the
  cleared route/model after Clear All.
- Optional query parameters are omitted rather than sent as empty strings.
- The frontend scenario suite must cover active filters, URL-restored state,
  page 2, refresh modes, in-flight requests, API errors, reload/history, and
  narrow layouts.
- Legacy query aliases remain parse-compatible for bookmarked routes, but are
  explicitly removed when the operator clears the search.
