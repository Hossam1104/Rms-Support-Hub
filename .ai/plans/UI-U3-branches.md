# UI-U3: Branch Discovery and Searchable Selection

## Objective

Finish the partially landed U3 work so all branch choices come from the verified `dbo.Branches` endpoint through one accessible shared selector.

## Preconditions

- Baseline is `4bf142bbd0adfd3265d9e009048e46193647c5ac`.
- U0's verified branch columns in `docs/database-schema.md` remain authoritative.
- U2's route-scoped draft store remains the order-header write path.
- Live acceptance requires UPC Testing SQL configuration and reachability; never substitute Production.

## Implementation Steps

1. Review the current U3 backend DTO, capability, repository, DI registration, endpoint, and tests. Fix contract/test mismatches without changing verified schema or exposing connection-string values in cache diagnostics.
2. Add typed frontend branch-option state/service usage for `GET modules/{key}/branches`, including loading, empty, error, environment changes, and explicit refresh behavior where needed.
3. Implement standalone `shared/ui/searchable-select` using Angular CDK overlay and virtual scrolling with label/code filtering, arrows, Enter, Escape, type-ahead, correct combobox/listbox ARIA, and focus restoration.
4. Export and exercise the selector in the dev-only kitchen sink, including loading, empty, error, long-list, light-theme, and dark-theme states; add focused component tests.
5. Replace the order-header free-text branch input and update Add Product guidance to display the selected branch name while persisting only its code through the U2 store.
6. Replace Order Requests filter/resend branch inputs and the obsolete frontend endpoint/model usage with branch options from the new endpoint.
7. Remove any now-dead history-derived branch DTO, repository/controller path, comments, and model imports; search the repository for stale route and `BranchSummary` references.
8. Run targeted backend/frontend tests, then `.\scripts\build.ps1`. With UPC Testing access, call the branch endpoint and perform the keyboard/name/code/draft browser gate; otherwise report the exact unavailable dependency.
9. Review only the U3 diff, update `.ai/STATE.md`, clear `.ai/HANDOFF.md`, and archive or delete this plan after completion.

## Validation Strategy

- Backend: branch capability, cache, refresh, repository failure, response contract, repository SQL-shape tests.
- Frontend: selector filtering/keyboard/ARIA/state tests and affected store/component tests.
- Broad: `dotnet test backend/OnlineOrderTool.slnx --nologo`; `.\scripts\build.ps1`.
- Live: sanitized UPC Testing branch count/sample and browser-only interaction checks.

## Risks

- Current frontend calls a removed history-derived endpoint.
- The current branch controller unit test bypasses MVC JSON options and fails on casing.
- Cache keys currently incorporate connection-string text; do not log or expose keys, and consider a non-secret environment/configuration identifier if behavior is changed.
- Live schema/network access may be unavailable even when local tests pass.

## Recovery

- Keep backend/frontend contract changes in one U3 diff. If selector integration cannot complete, do not restore the obsolete history route as a parallel source; record a handoff with the exact remaining consumer.
