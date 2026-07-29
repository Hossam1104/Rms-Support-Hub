# Task Execution

Before starting:

1. Read `AGENTS.md` completely.
2. Read `.ai/CURRENT_STATE.md`.
3. Read only the additional project files required by `AGENTS.md` and this task.
4. Inspect the current Git status and task-related diff.
5. Execute the task below through completion.
6. Run targeted validation.
7. Review the final task-related Git diff.
8. Update `.ai/CURRENT_STATE.md`.
9. Update `.ai/CONTEXT.md` or `.ai/DECISIONS.md` only when materially required.

Do not stop after summarizing or planning unless the task explicitly requests planning only.


# TASK AND Objective

- **Read [`UI_Rework_Plan.md`](UI_Rework_Plan.md) in full before touching
  anything**, especially §2 (defect register D1–D14) and §3 (guiding
  decisions). Read [`docs/Prompts/remediation_plan.md`](docs/Prompts/remediation_plan.md)
  §3 for the constraints that still bind (no invented columns or keys, no
  committed credentials, no module-key string comparisons).
- **Never invent a SQL column name or a JSON key.** The two sources of truth
  are [`docs/database-schema.md`](docs/database-schema.md) (SQL) and
  `docs/request_examples/**` (payload). If something is in neither, introspect
  it live and write it down, or stop and ask. Do not guess a column because a
  similarly-named one exists on another table.
- **Every live call goes to UPC Testing.** `RmsMainTest2`,
  `http://10.10.10.181:8080/RmsMainServerApi/…`. **Never** send, cancel or
  resend against Production. If you need to prove Production wiring, prove it
  with a stubbed `IApiClient` in a test, not a live call.
- **No credential in any tracked file.** Connection strings come from
  `dotnet user-secrets` (development) or `CONNECTIONSTRINGS__*` environment
  variables. Throwaway introspection scripts go in the scratchpad directory,
  never in the repo.
- **Work only inside the files listed for the session.** If the work genuinely
  requires a file that is not listed, say so and explain why before editing it.
- **Run the Verify block and paste its real output before declaring done.**
  A green build is not verification. If the database is unreachable, say so
  explicitly — do not report success.
- **End every session with:** `dotnet build` clean, `dotnet test` green,
  `ng build --configuration production` under budget with zero warnings, and
  **one** clean commit using the message given.
- Shell is **PowerShell on Windows**, repo root `d:\AI Tools\DBS\online_order_tool`.
  `.\scripts\dev.ps1` runs the API (`http://localhost:5200`) and `ng serve`
  (`http://localhost:4200`) together. `.\scripts\build.ps1` runs the full gate.


## Session U3 — Branches: real table, real endpoint, searchable picker

Branch code is a free-text input for a value that must match
`Branches.BranchCode` exactly and on which all UPC item pricing depends — a
typo reads as a missing item. The only branch list in the application derives
from order history (`GROUP BY H.BranchCode` over `RequestOrderHeaders`), so
branches with no orders do not exist and the name is a `MAX()` over whatever
was denormalised into past orders.

Read `UI_Rework_Plan.md` D6, D7 and §3 decision 2. **This session depends on
U0's `dbo.Branches` introspection — use those exact column names and nothing
else.** Session 4 of 9.

1. Add `backend/src/OnlineOrderTool.Data/Repositories/BranchRepository.cs` with
   an `IBranchRepository` in `Core/Repositories`. One method,
   `ListBranchesAsync(connectionString)`, selecting the branch code and name
   columns U0 verified from `dbo.Branches`, filtered by the active flag **only
   if U0 confirmed one exists**, ordered by name. Return a
   `BranchOptionDto(string Code, string Name)`. Parameterised throughout; no
   string interpolation of user values.
2. Expose `GET /api/modules/{key}/branches?envKey=` — extend `LookupController`
   rather than adding a controller, so environment resolution and the
   `Connect Timeout` handling in its existing `GetConnectionString` helper are
   reused. Gate it on a capability, not a module-key comparison. Cache the
   result in memory per connection-string key with a short TTL (5 minutes) and
   an explicit way to bypass; a branch list does not change during a session.
3. Register the repository in `Program.cs` alongside the existing singletons.
4. Build `frontend/src/app/shared/ui/searchable-select/`: a standalone
   component over Angular CDK `Overlay` (already a dependency — see the drawer
   and confirm-dialog components for the established pattern). Requirements:
   filters on **both** label and code as the operator types; full keyboard
   support (arrow keys, Enter, Escape, type-ahead, focus return on close);
   `role="combobox"` with correct `aria-*` wiring; virtual scroll via
   `@angular/cdk/scrolling` for long lists; loading, empty and error states;
   works in both themes. Export it from `shared/ui/index.ts` and add it to the
   kitchen sink.
5. Wire it in, replacing free-text and derived-list branch inputs:
   - `features/flat-order/components/order-info.component.ts` — `branch_code`,
     displaying `Name (Code)` and emitting the code. Route the change through
     U2's draft store.
   - `features/flat-order/components/add-product-dialog.component.ts` — show
     the resolved branch name in the "set a branch first" hint.
   - `features/order-requests/components/filter-bar.component.ts` and
     `features/order-requests/components/resend-request-dialog.component.ts` —
     switch both to the new endpoint.
6. Retire `ListBranchesAsync` from `OrderRequestRepository` and its
   `OrderRequestsController` route once nothing consumes them. Remove the now-dead
   `BranchSummaryDto` if it has no other caller.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\dev.ps1
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/branches?envKey=UPC%20Testing"
```
Paste the real branch list. Then in the browser: open the branch picker, filter
by a branch **name**, clear it, filter by a branch **code**, and select one
using only the keyboard. Confirm the selected code lands in the draft file.
Report the branch count returned.

**Commit:** `feat(u3): branch repository over dbo.Branches with a shared searchable picker`
