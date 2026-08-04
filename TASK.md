# Current Task

- **Task ID:** FINAL-PROJECT-POLISH
- **Status:** Completed Locally
- **Role:** Closed

## Objective

Two approved business-rule corrections (an optional payment sends as Cash on
Delivery; the phone field carries the local number only), UPC-first module
ordering, Riyal asset standardization, repository cleanup, and a local merge
into `main`. The preceding U0-U8 UI rework programme remains complete.

## Closeout

- An empty payment list is a valid Cash-on-Delivery order in the validator,
  the payload, and the summary/payments UI (ADR-0006).
- `Normalizers.NormalizeLocalPhone` splits the Saudi country code out of
  `client_phone` and `order_phone` in the builder, mirrored at the entry
  boundary by `phone.util.ts` (ADR-0007).
- UPC is pinned first through `orderModulesForDisplay`; no module-key branch
  was added. Every visible Riyal amount renders through `app-riyal`.
- Stale prompts, completed plans, superseded archives, and two consumer-free
  UI components were removed; live documentation links were repointed.
- Backend 127/127, frontend 100/100, Release build and `npm run build` passed.
  The work is merged into local `main`. Nothing was pushed or deployed.
- No Production send, cancel, or resend was attempted.

## Deferred

- Browser visual verification and full safe Testing order population, send,
  cancel, and resend evidence remain unavailable.
- One payment-free send against UPC **Testing** must confirm the RMS accepts
  `order_payment_method` `"COD"` (see the ADR-0006 open risk in STATE.md).
- `Saudi_Riyal.svg`, `upc_logo.svg`, and `whites_logo.svg` are unverified
  placeholders awaiting approved assets.

## Constraints Retained

- Do not modify payload builders, validators, totals, SQL, request fixtures,
  API contracts, capabilities, draft persistence, dependencies, or features
  outside an explicitly approved rule change.
- Do not push, deploy, reset, stash, rebase, amend, or use Production.
- Do not edit generated/runtime paths or store secrets/customer data in tracked
  files or `.ai/`.
