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


## Session U2 — Draft state: end the write race

This is the bug in the user's screenshot. A consumer lookup reports
`Found consumer: Mohamed Elbanna` while the Last Name, Phone and Address
inputs sit empty behind six stacked
`The process cannot access the file … because it is being used by another process`
errors.

The cause is a lost-update race in three parts:
`flat-order.component.ts::onFieldChange` fires one `PUT modules/{key}/order-field`
per field with no debounce; `OrderController.UpdateOrderField` does an
unsynchronised load-modify-write against a single JSON file; and the client
then assigns each response back into its own state (`this.draft.set(res.state)`).
`onLookupConsumer` fires **eight** of these back to back, so late responses
built from stale reads overwrite fields written by earlier ones, while the
concurrent writes collide on the file handle.

Read `UI_Rework_Plan.md` D1 and §3 decision 3. Session 3 of 9.

1. Serialise and make the write atomic. In
   `backend/src/OnlineOrderTool.Core/Services/DraftManager.cs`: hold a
   `SemaphoreSlim` per `(sessionId, moduleKey)` in a `ConcurrentDictionary` and
   take it around the whole load-modify-write, not just the write. Write to
   `<file>.tmp` then `File.Move(tmp, path, overwrite: true)` so a reader never
   observes a partial file. Retry a small bounded number of times on
   `IOException` with a short backoff, then surface a real error rather than
   swallowing it. Keep `LoadDraftAsync`'s existing tolerant behaviour.
2. Batch the write path. Add `PATCH modules/{key}/order-data` to
   `OrderController` taking `{ fields: Dictionary<string, object?> }` and
   applying every field inside **one** load-modify-write. Keep
   `PUT order-field` as a thin adapter that forwards a single-entry dictionary,
   so nothing breaks mid-programme — U4 deletes it.
3. Stop the client clobbering itself. Add
   `frontend/src/app/features/flat-order/draft.store.ts`: a signal-backed store
   owning the draft, exposing `patch(fields)` which updates local state
   immediately, debounces ~300 ms, coalesces all pending fields into one
   `PATCH`, and **does not** assign the response back into local state. The
   server echo is authoritative only on an explicit full reload
   (`GET state`, `load-default`, `clear-all`). Handle overlapping in-flight
   patches so a later one always wins for the fields it carries.
4. Rewire `flat-order.component.ts` onto the store. `onLookupConsumer` must
   send **one** `patch({...})` carrying every prefilled field — first, middle,
   last name, phone, email, birthdate, gender, address, address code — not
   eight sequential calls. Report in the toast which fields were actually
   prefilled and which came back empty from the lookup, so an empty Last Name
   is visibly the data's fault rather than the tool's.
5. Add tests: a `DraftManagerTests` case firing 20 concurrent patches of
   distinct fields at one `(sessionId, moduleKey)` and asserting all 20 are
   present afterwards; and a `ControllerIntegrationTests` case asserting
   `PATCH order-data` applies a multi-field body in one call.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\dev.ps1
```
Then, at `http://localhost:4200/modules/upc_ecommerce/order` with the badge
reading `TEST`: look up consumer `0556028080`. **Every** field the lookup
returns must be populated, and there must be **zero** error toasts. Paste a
screenshot. Then check the draft file under
`backend/src/OnlineOrderTool.Api/var/drafts/<session>/upc_ecommerce.json` and
confirm it holds the same values the form shows.

**Commit:** `fix(u2): serialise and batch draft writes, stop the client clobbering its own state`
