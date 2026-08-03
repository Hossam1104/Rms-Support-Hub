# UI Rework Execution Prompts - Active Sessions U7-U8

Companion to [`UI_Rework_Plan.md`](UI_Rework_Plan.md). U0-U6 are complete
locally and recorded in [`.ai/HISTORY.md`](../.ai/HISTORY.md). U3 branch data
and U4 item lookup still lack live evidence because the safe Testing dependency
is unavailable; execute U7-U8 in order, one session at a time.

---

## Rules for every active session

- Follow `AGENTS.md`, `TASK.md`, and the selected session's targeted read list.
  Do not treat archived plans as current instructions.
- Never invent a SQL column or JSON key. Use `docs/database-schema.md`,
  `docs/request_examples/**`, current code, and contract tests.
- Every live call is UPC Testing only. Never send, cancel, or resend against
  Production.
- Keep credentials and customer data out of tracked files, logs, and `.ai/`.
- Work only in the selected session's scope. Do not start a later session's
  layout, migration, or end-to-end work early.
- Run the required Verify block and report unavailable live/browser evidence
  honestly. A green build is not visual or live verification.
- Do not edit generated/runtime paths, add dependencies, push, deploy, reset,
  stash, rebase, or amend commits.
- Keep successful output concise, review the task diff, update project memory,
  and leave `.ai/HANDOFF.md` Empty when the session completes.

## Completed U5 closeout

U5 was completed in `3f6646d`: dark-first/light-complete tokens, preserved
status gradients and the `.glass-*` bridge, eight shared primitives, capped
and queued toasts, persisted sidebar reflow, and the development-only kitchen
sink. Backend 110/110, frontend 68/68, and the warning-free production build
passed with a 429.42 kB initial bundle. No in-app browser was available, and
UPC Testing item lookup remains an external HTTP 502 limitation.

---

## Completed U6 closeout

U6 was completed in `dac0cc4`: the flat-order workspace now uses ordered
collapsible sections, capability-aware section navigation, a server-value-only
summary rail, dense product/payment tables, real loading/empty/error states,
and a responsive bottom action bar. Backend 110/110, frontend 81/81, and the
warning-free production build passed with a 429.42 kB initial bundle. Browser
inspection was unavailable; the UPC Testing item lookup remained HTTP 502.

U6 Validate is intentionally a non-sending draft/preview/totals refresh because
the current API has no standalone validation endpoint. The existing
`send-request` path remains the server-authoritative validation/send action.

---

## Session U7 - Migrate the rest of the app and remove legacy classes

Execute **U7 only**. Read UI Rework Plan D10/D14 and decision 5, current
state/diff, and the listed layout/features. Reuse U5/U6 primitives; preserve
routes and behavior.

### Required outcome

- Migrate navbar, sidebar, breadcrumb, landing, Order Requests/drawer/filter
  bar, Uni-Commerce, and Order Validation to the shared primitives. Preserve
  environment controls, theme toggle, six drawer tabs, statuses, branch picker,
  and the superseded Order Validation route.
- Replace the navbar documentation alert with a meaningful route/link or remove
  the dead affordance.
- Remove `.glass-card`, `.glass-panel`, `.glass-input`, and `.glass-button`
  rules only after all consumers are migrated; remove only now-unused aliases.

Verify `git grep` for all four legacy classes is empty, run the full build/tests,
and check the affected routes in both themes when a browser is available.

Suggested commit: `refactor(u7): migrate every remaining feature to the new primitives, delete the legacy glass classes`

---

## Session U8 - End-to-end verification, documentation, cleanup

Execute **U8 only**. This is verification and cleanup; add no features.

Run the full build and the UPC Testing-only browser flow: open the order, choose
a branch by name, look up/add a real item, look up consumer `0556028080`, add a
valid payment, compare the summary with `GET calculate-totals`, send, locate the
request in Order Requests, inspect Request/Response detail, cancel with a
reason, and verify the Testing cancellation URL/status. Never use Production.

Refresh `README.md`, `docs/api-spec.md`, `docs/design-system.md`, and
`docs/database-schema.md` only with verified facts. Confirm no credentials or
generated files are tracked, `var/` remains ignored, and the following checks
are clean:

```powershell
.\scripts\build.ps1
git grep -n "10\.10\.9\.181\|10\.10\.8\.181" -- backend frontend README.md docs/api-spec.md docs/database-schema.md docs/design-system.md
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src
git ls-files | Select-String "var/drafts"
git status --short
```

Report a compact numbered E2E transcript. If any live step is unavailable, name
that exact step and do not call the session complete without user direction.

Suggested commit: `docs(u8): verify the reworked tool end-to-end on UPC Testing and refresh the documentation`
