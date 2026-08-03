# UI Rework Execution Prompts — Active Sessions U4-U8

Companion to [`UI_Rework_Plan.md`](UI_Rework_Plan.md). U0-U3 are complete
locally and recorded in [`.ai/HISTORY.md`](../.ai/HISTORY.md). U3's live/browser
gate is pending; execute U4-U8 in order, one session at a time.

---

## Rules that apply to every session

Repeat these verbatim if the agent drifts.

- Follow `AGENTS.md`, `TASK.md`, and the selected session's targeted read list.
  Do not treat archived plans as current instructions.
- **Never invent a SQL column name or a JSON key.** The two sources of truth
  are [`database-schema.md`](database-schema.md) (SQL) and
  `request_examples/**` (payload). If something is in neither, introspect
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
- **End every session with:** the required targeted checks and production
  build green. Commit only when the user explicitly requests it.
- Shell is **PowerShell on Windows**, repo root `d:\AI Tools\DBS\online_order_tool`.
  `.\scripts\dev.ps1` runs the API (`http://localhost:5200`) and `ng serve`
  (`http://localhost:4200`) together. `.\scripts\build.ps1` runs the full gate.

---

### Compact-context rule for U4-U8

For the compact prompts below, each session's targeted read list overrides the
earlier instruction to read the full plan. Run every required check, but report
successful commands as one-line results and include detailed output only for
failures or requested live evidence. All safety, scope, Testing-only,
credential, diff-review, and state-update rules still apply.

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
error. Report concise evidence, review the task diff, and update `.ai/STATE.md`.

**Suggested commit (only when requested):** `fix(u4): populate the item lookup result, adopt server totals, real send lifecycle`

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
and update `.ai/STATE.md`.

**Suggested commit (only when requested):** `feat(u5): dark-first token system, UI primitives, capped toast stack`

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
review the task diff, and update `.ai/STATE.md`.

**Suggested commit (only when requested):** `feat(u6): two-column order builder workspace with a sticky summary rail`

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
the task diff and update `.ai/STATE.md`.

**Suggested commit (only when requested):** `refactor(u7): migrate every remaining feature to the new primitives, delete the legacy glass classes`

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
not completion. Review the task diff and update `.ai/STATE.md`.

**Suggested commit (only when requested):** `docs(u8): verify the reworked tool end-to-end on UPC Testing and refresh the documentation`
