# UI Rework Execution Prompts - Active Sessions U5-U8

Companion to [`UI_Rework_Plan.md`](UI_Rework_Plan.md). U0-U4 are complete
locally and recorded in [`.ai/HISTORY.md`](../.ai/HISTORY.md). U3 branch data
and U4 item lookup still lack live evidence because the safe Testing dependency
is unavailable; execute U5-U8 in order, one session at a time.

---

## Rules for every active session

- Follow `AGENTS.md`, `TASK.md`, and the selected session's targeted read list.
  Do not treat archived plans as current instructions.
- Never invent a SQL column or JSON key. Use `docs/database-schema.md`,
  `docs/request_examples/**`, current code, and contract tests.
- Every live call is UPC Testing only. Never send, cancel, or resend against
  Production.
- Keep credentials and customer data out of tracked files, logs, and `.ai/`.
- Work only in the selected session's scope. Do not start U6 layout or U7
  migration during U5.
- Run the required Verify block and report unavailable live/browser evidence
  honestly. A green build is not visual or live verification.
- Do not edit generated/runtime paths, add dependencies, push, deploy, reset,
  stash, rebase, or amend commits.
- Keep successful output concise, review the task diff, update project memory,
  and leave `.ai/HANDOFF.md` Empty when the session completes.

## Completed U4 closeout

U4 was reviewed at `fd5d65d` and closed locally. The review correction covers
stale database-filled lookup values during a new search and fabricated stat
tiles before the first server totals response. Local backend/frontend/build
gates pass; database-dependent item lookup remains HTTP 502.

---

## Session U5 - Design system: dark-first rebuild

Execute **U5 only**. Read `TASK.md`, `.ai/STATE.md`,
`.ai/plans/UI-U5-design-system.md`, UI Rework Plan D9/D10/D12 and decisions
5-6, this section, `docs/design-system.md`, and the targeted token, gradient,
animation, shared UI, toast, sidebar, shell, kitchen-sink, and direct test files.

### Required outcome

1. Make `_tokens.css` dark-first with distinct page/panel/raised/overlay,
   interactive/muted/hover/selected surfaces, readable text ramp, one accent,
   semantic colors, borders, input states, and focus rings. Keep complete light
   overrides and preserve radius, shadow, motion, easing, and reduced-motion
   behavior. Retain only still-used compatibility aliases.
2. In `_gradients.css`, preserve all nine status gradients, mesh hero, and
   intentional accents. Do not use gradients as default surfaces. Keep
   `.glass-*` until U7 and do not use it in new primitives.
3. Create and export standalone signal-based `ui-card`, collapsible
   `ui-section`, `ui-field`, `ui-input`, `ui-select`, `ui-button`, `ui-table`,
   and `ui-toolbar`. Support semantic markup, accessible focus/labels, true
   disabled/read-only/invalid states, button variants primary/secondary/ghost/
   danger, small/medium sizes, loading, dense/sticky/zebra tables, and
   responsive toolbar wrapping. Compose with the U3 searchable selector.
4. Rebuild toasts with at most three visible items, queued overflow, identical
   consecutive-message collapse with a `xN` indicator, timed auto-dismiss,
   hover/focus pause and resume, manual close, bottom-right responsive
   placement, live-region semantics, and reduced motion.
5. Publish and persist sidebar collapse; make the shell's `--sidebar-width`
   match expanded/collapsed width and remove the dead gutter. Preserve narrow
   screen overlay behavior and keyboard access.
6. Expand the development-only kitchen sink to demonstrate every primitive,
   state, theme, focus ring, nine status pills, searchable select, toast cap,
   deduplication, queue promotion, and reduced-motion behavior.
7. Update `docs/design-system.md` with token hierarchy, no-raw-color rule,
   primitive catalogue, toast/sidebar behavior, and U6/U7 boundaries.

### Verify

```powershell
dotnet test backend/OnlineOrderTool.slnx --nologo
cd frontend
npm test -- --watch=false
cd ..
.\scripts\build.ps1
python .ai/scripts/context.py
python .ai/scripts/check_memory.py
git diff --check
git grep -n "#[0-9a-fA-F]" -- frontend/src/app frontend/src/styles
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src
```

The raw-color search must have no matches outside designated token/gradient
files; legacy classes may remain for existing U7 consumers, but no new U5
primitive may use them. Verify the kitchen sink in both themes, keyboard focus,
disabled controls, four identical errors collapsing to one `x4` toast, queued
toast promotion, hover pause, reduced motion, and sidebar reflow when a browser
is available. If no browser is available, say so without fabricating evidence.

Suggested commit: `feat(u5): rebuild design system and shared UI primitives`

---

## Session U6 - Order builder layout rebuild

Execute **U6 only**. Read UI Rework Plan D11, the U6 row, current state/diff,
the direct flat-order/UI files, and the U5 shared primitives. Reuse U3 picker,
U4 server totals/data flow, and U2 store.

Required outcome:

- Two-column builder using `ui-section`: fluid collapsible Order/Customer/
  Products/Payments/Payload sections, completion derived from server validation,
  sticky section navigation, and a sticky approximately 340px summary rail.
- Add `order-summary-rail` with item count, subtotal, VAT, discount, delivery,
  total, paid, balance, validation issues, environment badge, Validate, and Send.
  Values come from server totals; invalid Send is disabled with a reason.
- Products/payments use dense sticky `ui-table`, correct row net totals, inline
  quantity/discount through the draft store, explicit delete, and empty states.
- Use `ui-field` with deliberate spans and skeletons until draft load completes.
- Below 1200px use a sticky bottom total/Send bar; below 768px use one column;
  prevent page horizontal scroll.

Verify at 1920/1280/900/600px when a browser is available, plus the full build
and test gates. Do not change payload, validator, SQL, or capability contracts.

Suggested commit: `feat(u6): two-column order builder workspace with a sticky summary rail`

---

## Session U7 - Migrate the rest of the app and remove legacy classes

Execute **U7 only**. Read UI Rework Plan D10/D14 and decision 5, current state/
diff, and the listed layout/features. Reuse U5/U6 primitives; preserve routes
and behavior.

Required outcome:

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

Report a compact numbered E2E transcript. If any live step is unavailable,
name that exact step and do not call the session complete without user
direction.

Suggested commit: `docs(u8): verify the reworked tool end-to-end on UPC Testing and refresh the documentation`
