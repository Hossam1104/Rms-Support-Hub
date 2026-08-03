# UI Rework Execution Prompts - Historical U0-U8

Companion to [`UI_Rework_Plan.md`](UI_Rework_Plan.md). U0-U8 are complete
locally and recorded in [`.ai/HISTORY.md`](../.ai/HISTORY.md). Browser visual
verification and full safe Testing order acceptance remain deferred external
evidence; there is no active execution session.

---

## Historical safety rules

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

## U8 closeout record

U8 completed the local regression/build gates, safe read-only Testing-lane
checks, static and security hygiene checks, documentation reconciliation, and
programme cleanup. The backend/frontend contracts, payload builders, totals,
SQL, capabilities, and Production safety were not changed.

The in-app browser was unavailable, so 1920/1280/900/600 viewport and theme
checks were deferred. Safe synthetic item probes were inconsistent: one
returned HTTP 200 with `success=false` and a repeat returned HTTP 502. No
approved real Testing item was available for a safe order workflow.
Consequently, item population, payment, send, Order Requests drawer
inspection, Request/Response JSON inspection, cancellation, and resend remain
external acceptance evidence rather than claimed passes.

The U8 verification plan was archived, active prompts were retired, project
memory was updated, and the programme was closed locally. No Production action
was attempted.
