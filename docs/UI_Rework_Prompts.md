# UI Rework Execution Prompts — 9 Sessions (U0 → U8)

Companion to [`UI_Rework_Plan.md`](UI_Rework_Plan.md). One prompt per session,
executed in order, **one conversation each**. Copy the whole section — rules
block included — into a fresh session.

---

## Rules that apply to every session

Repeat these verbatim if the agent drifts.

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

---

## Session U0 — Ground truth: hosts and the `Branches` schema

The order tool's Testing lane points at the wrong host, the documented SQL
server is stale, and `dbo.Branches` — the table the next three sessions build
on — has never been introspected. This session establishes ground truth and
adds the guard tests that must **fail** so the following sessions have a real
gate. It changes **no behaviour**.

Read `UI_Rework_Plan.md` §2.1 (D5), §3.1 (the environment matrix) and §3
decision 2. This is session 1 of 9.

1. Correct the host literals. In
   `backend/src/OnlineOrderTool.Core/Modules/UpcEcommerceModule.cs`, the
   `UPC Testing` environment's `ApiUrl` and `CancelUrl` must be
   `http://10.10.10.181:8080/RmsMainServerApi/api/Order/CreateAndAssignOrder`
   and `…/CancelOrder`. `UPC Production` already reads `http://10.10.10.181/…`
   — confirm it, do not change it. Update
   `backend/src/OnlineOrderTool.Api/appsettings.json` `ModuleEndpoints:UpcTesting`
   to match.
2. Introspect `dbo.Branches` live. Write a throwaway script **in the scratchpad
   directory, not the repo**, that connects to `10.10.10.181` / `RmsMainTest2`
   using the `UpcEcommerceTest` connection string from user-secrets, and runs
   `SELECT TOP 1 * FROM dbo.Branches`. Print the full column list and one
   sample row with values redacted where they look sensitive. **This is the
   gate for U3** — report the exact column names, especially whichever column
   holds the human-readable branch name, and whether the table has an
   active/inactive flag worth filtering on.
3. Record the result. Add a `dbo.Branches` row to the verified-schema table in
   `docs/database-schema.md` §1, in the same format as the existing rows, with
   the same "confirmed live against …" provenance note. Correct the two stale
   `10.10.8.181` references in that file (§1 header and §5) to `10.10.10.181`,
   and the one in `README.md:133`. **Leave `docs/Prompts/**` untouched** — those
   are a historical record of what was true at the time.
4. Add guard tests to `backend/tests/OnlineOrderTool.Tests/ContractTests.cs`:
   - A test that reads the tracked source of `backend/src/**` and
     `backend/tests/**` and asserts **no** file contains `10.10.9.181` or
     `10.10.8.181`. Failure message must name the offending files.
   - A test asserting `new UpcEcommerceModule(…).GetEnvironment(null).Environment == "Testing"`.
     This is **expected to fail now** — U1 makes it pass. Do not add `Skip`.
5. Do not change `GetEnvironment`, any controller, or any frontend file. Step 4's
   second test failing is the correct end state for this session.

**Verify** (paste real output):
```powershell
dotnet build backend/OnlineOrderTool.slnx --nologo
dotnet test  backend/OnlineOrderTool.slnx --nologo
# expect: the stale-host test PASSES, the Testing-default test FAILS

git grep -n "10\.10\.9\.181" -- backend frontend docs/database-schema.md README.md   # expect empty
git grep -n "10\.10\.8\.181" -- backend frontend docs/database-schema.md README.md   # expect empty
```
Then paste the full `dbo.Branches` column list from step 2. **U3 cannot start
without it.** If `10.10.10.181` / `RmsMainTest2` is unreachable, stop and say
so — do not rewrite `docs/database-schema.md` on an unverified host.

**Commit:** `chore(u0): correct RMS host literals, introspect dbo.Branches, add environment guard tests`

---

### Kimi K3-256K context rule for U1–U8

For the compact prompts below, each session's targeted read list overrides the
earlier instruction to read the full plan. Run every required check, but report
successful commands as one-line results and include detailed output only for
failures or requested live evidence. All safety, scope, Testing-only,
credential, diff-review, state-update, and commit rules still apply.

---

## Session U1 — Environment safety: Testing by default, Production on purpose

Execute **U1 only** and follow `AGENTS.md`. To conserve context, read only
`UI_Rework_Plan.md` D3, D4 and §3 decision 4; then inspect the current diff,
the files named below, and their direct tests/dependencies. Do not load the
full plan or repository. Use existing patterns and keep tool/final output
concise (summaries plus failure details, never full successful logs).

Required outcome:

1. Add an explicit `IsDefault`-style flag to `ModuleEnvironment`; expose it on
   `EnvironmentDto`; flag Testing for every module. Each `GetEnvironment`
   resolves: requested available key → flagged default → first available
   non-Production → first available.
2. Add `EnvironmentKey` to `CancelOrderRequest`. `CancelOrder` must resolve
   that environment and return a clear 400 when it has no `CancelUrl`.
3. Audit `LookupController`, `OrderRequestsController`, `SendRequest`, and
   `ExportJson`; thread the caller's environment key everywhere. Report only
   actual fixes/findings.
4. In `frontend/src/app/core/services/module.service.ts`, select the flagged
   default, persist a validated choice per module in namespaced `localStorage`,
   and restore it on deep link/refresh.
5. Add standalone `shared/ui/env-badge` (`TEST` neutral, `PROD` danger) and a
   navbar environment switcher. A switch clears environment-scoped cache and
   reloads the draft.
6. Gate Production send/cancel with the existing `confirm-dialog`; require the
   typed environment name. Do not create another dialog.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\build.ps1
```

The U0 default test must pass. Add a stubbed-`IApiClient` test proving
`environmentKey = "UPC Testing"` cancels through the Testing `CancelOrder`
URL, never Production/CreateAndAssignOrder. Browser-check direct deep link →
`TEST`; refresh → `TEST`; switch/refresh → `PROD`; Send opens typed
confirmation. Cancel it—never send to Production. Record concise results.

Review only the task diff, update `.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `feat(u1): default to the Testing lane, persist and surface the environment, gate Production`

---

## Session U2 — Draft state: end the write race

Execute **U2 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` D1
and §3 decision 3, the current diff/state, and direct implementation/tests for
the draft write path. Do not reread unrelated project material. Keep command
output to summaries and failures.

Fix the concurrent draft lost-update/file-lock race:

1. In `DraftManager`, guard the entire load-modify-write with a per
   `(sessionId,moduleKey)` `SemaphoreSlim` stored in a
   `ConcurrentDictionary`. Write `<file>.tmp`, then atomic overwrite-move.
   Retry `IOException` briefly and finitely; preserve tolerant reads.
2. Add `PATCH modules/{key}/order-data` accepting
   `{ fields: Dictionary<string, object?> }` and applying the batch in one
   transaction. Retain `PUT order-field` only as a one-entry adapter for U4.
3. Add signal-backed `features/flat-order/draft.store.ts`: optimistic local
   patches, ~300 ms debounce/coalescing, one PATCH, no response echo assigned
   into local state. Only explicit full reloads replace state; overlapping
   requests must preserve later field values.
4. Rewire the flat-order UI. Consumer lookup sends one batch containing first,
   middle, last, phone, email, birthdate, gender, address, and address code.
   Its toast distinguishes populated from source-empty fields.
5. Tests: 20 concurrent distinct-field patches retain all fields; controller
   integration proves one multi-field PATCH.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\build.ps1
.\scripts\dev.ps1
```

In `TEST`, look up `0556028080`; all returned fields must appear with zero
error toasts. Confirm the session draft JSON matches the UI. Report concise
evidence (screenshot if available), inspect the task diff, update
`.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `fix(u2): serialise and batch draft writes, stop the client clobbering its own state`

---

## Session U3 — Branches: real table, real endpoint, searchable picker

Execute **U3 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` D6,
D7 and §3 decision 2, U0's verified `dbo.Branches` row in
`docs/database-schema.md`, current state/diff, and direct files/tests. Never
guess schema: use U0's exact code/name columns and active flag only if verified.
Keep successful tool output summarized.

Required outcome:

1. Add `IBranchRepository`/`BranchRepository.ListBranchesAsync(connectionString)`
   returning `BranchOptionDto(Code, Name)`, ordered by name, parameterized, and
   optionally filtered only by a verified active column.
2. Extend `LookupController` with
   `GET /api/modules/{key}/branches?envKey=`. Reuse its environment/connection
   handling; capability-gate it (no module-key comparison). Cache per
   connection-string key for 5 minutes and support explicit bypass.
3. Register the repository using existing DI lifetime conventions.
4. Add standalone `shared/ui/searchable-select` using existing CDK
   Overlay/scrolling patterns: label+code filtering, arrows/Enter/Escape/
   type-ahead/focus return, correct combobox ARIA, virtual scrolling, and
   loading/empty/error states in both themes. Export it and demo it in the
   kitchen sink.
5. Replace branch inputs in order info, add-product hint, request filter, and
   resend dialog. Display `Name (Code)`, persist only code through U2's store,
   and source all options from the new endpoint.
6. When unused, remove the order-history branch repository method/route and
   dead `BranchSummaryDto`.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\build.ps1
.\scripts\dev.ps1
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/branches?envKey=UPC%20Testing"
```

Report the real branch count and a short sanitized sample. In the browser,
filter by name and code, keyboard-select, and confirm the draft stores the
code. Review the task diff, update `.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `feat(u3): branch repository over dbo.Branches with a shared searchable picker`

---

## Session U4 — Order builder workflow correctness

Execute **U4 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` D2,
D8 and D13, current state/diff, `TotalsCalculator`'s response shape, and direct
builder files/tests. U2/U3 are dependencies. Do not modify
`FlatOrderPayloadBuilder`, `FlatOrderValidator`, or `TotalsCalculator`. Keep
logs and the final report concise.

Required outcome:

1. Item lookup must populate the add-product form: code, available English/
   Arabic names, unit price, VAT%, and computed net price. Mark DB-filled
   fields but keep them editable. Missing branch blocks with a picker-directed
   message; distinguish not-found from database failure.
2. Remove client `recalculate()`. Debounce `GET calculate-totals` after draft
   mutations through U2's store; type the exact server `TotalsSummary`; show
   its full breakdown in quick stats.
3. Replace the 2.5-second fake spinner with request lifecycle/finalize state.
   Show active environment `ApiUrl` read-only; only an explicit custom-endpoint
   toggle unlocks it.
4. Map `{success:false, errors:[...]}` to inline field errors, scroll/focus the
   first, and retain a summary issue count.
5. Remove U2's `PUT order-field` adapter and all callers.

**Verify:**
```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
.\scripts\build.ps1
.\scripts\dev.ps1
```

In `TEST`, select a real branch/item and confirm database fields populate.
Compare the UI totals to:
```powershell
curl.exe -s "http://localhost:5200/api/modules/upc_ecommerce/calculate-totals"
```
They must match to the cent. Clear a required field; Send must show its inline
error. Report concise evidence, review the task diff, update
`.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `fix(u4): populate the item lookup result, adopt server totals, real send lifecycle`

---

## Session U5 — Design system: dark-first rebuild

Execute **U5 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` D9,
D10, D12 and §3 decisions 5–6, current state/diff, and direct style/UI files.
Do not inspect feature pages reserved for U6/U7 unless needed to confirm a
token consumer. Keep output compact.

Required outcome:

1. Make `_tokens.css` dark-first with distinct page/panel/raised/overlay
   surfaces, readable text ramp, one accent, semantic colors, borders, and
   focus rings. Keep light overrides complete; preserve radius/shadow/motion
   scales and reduced-motion; retain only still-used compatibility aliases.
2. In `_gradients.css`, keep nine status gradients and mesh hero; stop using
   gradients as default surfaces. Leave `.glass-*` until U7.
3. Add/export standalone signal-based primitives: `ui-card`, collapsible
   `ui-section` (title/completion/actions), `ui-field` (label/required/hint/
   error), `ui-input`, `ui-select`, `ui-button` (four variants, sm/md/loading),
   `ui-table` (dense/sticky/zebra), and `ui-toolbar`. Compose with U3's picker;
   all controls need visible focus and true disabled states.
4. Toasts: max 3 visible, queue overflow, collapse identical consecutive
   messages with `×N`, pause timed dismissal on hover, preserve four variants,
   and position bottom-right without covering primary actions/U6 rail.
5. Publish/persist sidebar collapsed state; module-shell offset must track the
   real width via a CSS custom property, eliminating the dead gutter.
6. Update `docs/design-system.md` (tokens, no-raw-hex rule, primitives,
   U6/U7 migration note). Kitchen sink must cover all primitive states, nine
   pills, capped toast stack, and searchable select.

**Verify:**
```powershell
.\scripts\build.ps1
.\scripts\dev.ps1
```

Build must be under budget with zero warnings. Check kitchen sink in both
themes, focus rings, four identical errors → one `×4` toast, reduced motion,
and sidebar collapse/reflow. Record concise evidence, review the task diff,
update `.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `feat(u5): dark-first token system, UI primitives, capped toast stack`

---

## Session U6 — Order builder layout rebuild

Execute **U6 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` D11,
current state/diff, and direct flat-order/UI files. Reuse U5 primitives, U3
picker, U4 totals/data flow, and U2 store. Keep output concise.

Required outcome:

1. Two-column builder on `ui-section`: fluid collapsible Order/Customer/
   Products/Payments/Payload sections with sticky scroll-spy nav; sticky
   ~340px summary rail. Completion derives from server validation—never a
   duplicated hardcoded required-field list.
2. Add `order-summary-rail`: item count; subtotal, VAT, discount, delivery,
   total, paid, balance from server totals using `shared/ui/riyal`; clickable
   validation issues; environment badge; Validate/Send. Invalid Send is
   disabled with a reason.
3. Products/payments use dense sticky `ui-table`, correct `row_net_total`,
   inline quantity/discount via the draft store, explicit delete, and proper
   empty states.
4. Use `ui-field` and deliberate spans: phone pair, name trio, GPS pair,
   full-width address; consistent required markers. Put consumer/item lookup
   actions in section-header toolbars.
5. Below 1200px replace rail with sticky bottom total/Send bar; below 768px use
   one column. Prevent page-level horizontal scroll; tables may scroll inside.
6. Show skeletons until draft load completes; never flash blank inputs.

**Verify:**
```powershell
.\scripts\build.ps1
.\scripts\dev.ps1
```

In `TEST`, complete branch → item → consumer → payment at 1920/1280px; check
rail, overlap, and scrolling. At 900/600px check bottom bar/clipping. Keyboard
tab order must remain sensible. Capture concise evidence at 1920/1280/600,
review the task diff, update `.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `feat(u6): two-column order builder workspace with a sticky summary rail`

---

## Session U7 — Migrate the rest of the app, delete the legacy classes

Execute **U7 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` D10,
D14 and §3 decision 5, current state/diff, and the listed layout/features.
Reuse U5/U6 primitives; do not redesign working shared components. Keep logs
and final reporting compact.

Required outcome:

1. Migrate navbar to toolbar/button while preserving environment switch/badge
   and theme toggle. Replace the documentation `alert()` with a meaningful
   link; if none exists, remove it.
2. Migrate sidebar/breadcrumb; preserve persisted collapse. Mark Order
   Validation as superseded by Order Requests but keep its page and route.
3. Restyle landing/module cards; make default Testing obvious and mark
   Production.
4. Migrate remaining glass usage in all `order-requests` components. Preserve
   route-driven drawer, six tabs, ExceptionMessage danger card,
   cancel-blocked statuses, and U3 resend branch picker; retain already-shared
   stat/status/json-tree/drawer components.
5. Migrate `unicommerce` and `order-validation` to primitives with the same
   section rhythm; no forced two-column layout.
6. Delete all legacy `.glass-card/input/button/panel` rules and their
   introductory comment. Remove only now-unused token aliases and document
   that removal.

**Verify:**
```powershell
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src
.\scripts\build.ps1
.\scripts\dev.ps1
```

The grep must be empty and build warning-free/under budget. Check both themes
on `/`, UPC order/requests/drawer/validation, GHC unicommerce, and kitchen
sink; report only failures/unreachable routes plus concise evidence. Review
the task diff, update `.ai/CURRENT_STATE.md`, and commit once:

**Commit:** `refactor(u7): migrate every remaining feature to the new primitives, delete the legacy glass classes`

---

## Session U8 — End-to-end verification, documentation, cleanup

Execute **U8 only** and follow `AGENTS.md`. Read only `UI_Rework_Plan.md` §4's
U8 row and §5 risks, current state/diff, and the documentation named below.
This is verification/cleanup: add no features. Keep successful logs summarized,
but retain exact E2E facts, failures, and command statuses.

1. Run `.\scripts\build.ps1`; fix only regressions within this programme.
2. Run this browser flow against **UPC Testing only** (`TEST` badge):
   direct-open order → choose branch by name → look up/add real item with
   branch price → look up consumer `0556028080` with all returned fields and
   zero error toasts → add valid COD `not_payment` or full card payment →
   compare summary to `GET calculate-totals` to the cent → Send and record HTTP
   status/order number → find it in Order Requests and verify Request plus
   populated `ResponseJson` → cancel with reason and verify updated status plus
   Testing `CancelOrder` URL. Never call Production.
3. Update:
   - `README.md`: hosts/environment matrix, Testing default, branch picker,
     design system, and `UI_Rework_Plan.md` link.
   - `docs/api-spec.md`: PATCH order-data, branches, cancel
     `environmentKey`, removed PUT order-field/old branches route.
   - `docs/design-system.md`: final tokens/primitives.
   - `docs/database-schema.md`: verified Branches row/query.
4. Confirm `var/`/drafts remain untracked, tracked files contain no
   credentials, and remove programme-created scratch files only.
5. Append a short shipped/deferred/discrepancies closing section to
   `UI_Rework_Plan.md`.

**Verify** (run all; report one-line results and any matches/failures):
```powershell
.\scripts\build.ps1

git grep -n "10\.10\.9\.181\|10\.10\.8\.181" -- backend frontend README.md docs/api-spec.md docs/database-schema.md docs/design-system.md
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src
git ls-files | Select-String "var/drafts"
git status --short
```

All greps must be empty. Report a compact numbered E2E transcript including
the real order number; name any blocked step exactly. A green build alone is
not completion. Review the task diff, update `.ai/CURRENT_STATE.md`, and commit
once:

**Commit:** `docs(u8): verify the reworked tool end-to-end on UPC Testing and refresh the documentation`
