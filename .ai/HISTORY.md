# Completed Work History

Keep this as a concise milestone index. Detailed implementation evidence belongs
in Git; completed plans with lasting audit value live under `.ai/archive/`.

| Milestone | Status | Evidence | Outcome |
|---|---|---|---|
| Flask multi-module refactor | Completed and superseded | `1f2612d`, `0dcbe16` | Added module-scoped GHC/UPC/Uni-Commerce workflows and UPC validation before the later platform rewrite. |
| .NET 10 + Angular 22 rewrite | Completed | `936bdda` | Replaced the active Flask application with the layered .NET API and Angular SPA baseline. |
| Remediation R0-R10 | Completed | `936bdda` through `b011ffb` | Corrected payload and SQL contracts, moved history to `OrderRequests`, added capability routing and per-session drafts, rebuilt the frontend contract/design system, and removed the legacy Flask tree. |
| UI Rework U0 | Completed | `682fd55`, `ea66830` | Verified `dbo.Branches`, corrected API-host evidence, and retained the separately verified SQL host. |
| UI Rework U1 | Completed | `6fd7f77` | Made Testing the explicit default, persisted and displayed the environment, and gated Production actions. |
| UI Rework U2 | Completed | `5ddc4de` | Serialized and atomically persisted per-session draft patches; the frontend now batches and debounces edits. |
| UI Rework U3 | Completed but live verification pending | `f4c5792`, `3453b19`, `015a627` | Added the capability-gated branch endpoint with cache refresh, the shared searchable selector, and branch-code-only integration. Local backend/frontend/build gates passed; the safe Testing branch read remained unavailable. |
| UI Rework U4 | Completed locally; live database verification pending | `fd5d65d`, `e8d8a81` | Connected item lookup results, typed server totals, request-lifecycle send state, safe endpoint display/custom opt-in, inline validation, and removed the temporary per-field adapter. Added regression coverage for stale lookup values and pre-load totals. Local backend 110/110, frontend 57/57, and production build 419.85 kB passed; the Testing item lookup returned HTTP 502. |
| UI Rework U5 | Completed locally; live database verification pending | `3f6646d` | Established dark-first/light-complete tokens, preserved nine status gradients and the `.glass-*` U7 bridge, added eight standalone shared primitives, capped/queued/deduplicated accessible toasts, persisted sidebar collapse, removed the shell dead gutter, and expanded the development-only kitchen sink. Backend 110/110, frontend 68/68, and the warning-free production build passed with a 429.42 kB initial bundle; browser and live item-population evidence remain unavailable. |
| UI Rework U6 | Completed locally; browser/live item evidence pending | `dac0cc4` | Rebuilt the flat-order workspace with ordered collapsible sections, capability-aware section navigation, a server-value-only summary rail, dense product/payment tables, real loading/empty/error states, and a responsive bottom action bar. Backend 110/110, frontend 81/81, and the warning-free production build passed with a 429.42 kB initial bundle; browser evidence was unavailable and the Testing item lookup returned HTTP 502. |
| UI Rework U7 | Completed locally; browser/live evidence pending | `d3219dd` | Migrated the remaining application surfaces to U5/U6 primitives and tokens, removed the navbar dead alert, deleted all legacy glass consumers/definitions and unused aliases, and preserved routes, drawer tabs, capability behavior, and Production safety. Backend 110/110, frontend 85/85, and the warning-free production build passed with a 427.19 kB initial bundle; no in-app browser was available and the Testing item lookup returned HTTP 502. |
| UI Rework U8 | Completed locally; external verification pending | U8 closeout commit | Reproduced backend/frontend/build gates, completed safe local API and hygiene checks, reconciled the final plan and memory, archived the active U8 plan, and closed the U0-U8 programme locally. Browser visual checks and full safe Testing order population/send/cancel/resend evidence remain deferred; no Production action was attempted. |
| Final project polish | Completed locally; live send evidence pending | `30bb28e`, `3c65eaa`, `0953f63`, `4aee8cd` | Made an empty payment list a valid Cash-on-Delivery order end to end, split the Saudi country code out of `client_phone`/`order_phone` in the builder with a mirrored entry-side helper, pinned UPC first through `orderModulesForDisplay`, routed every visible Riyal amount through `app-riyal`, and removed stale prompts/plans/archives plus two consumer-free UI components. Backend 127/127, frontend 100/100, Release build clean, `npm run build` succeeded at a 427.35 kB initial bundle with one non-blocking style-budget warning. A payment-free UPC **Testing** send must still confirm the RMS accepts `"COD"`. |
| Final Order Requests unification | Completed locally; approved asset/browser/live Testing evidence pending | `44006a4`, `a866f7b`, `948496b`, and closeout commit | Stabilized the branch selector, reconciled the shared Riyal renderer, replaced the overlapping validation/drawer experience with one canonical list/detail route, and implemented same-number resend from the selected stored RequestJson with server-side status and payload guards. Backend 146/146, frontend 105/105, Release/build gates passed; no Production action was attempted. |
| Final Acceptance Hardening | Completed locally; interactive browser/live Testing evidence pending | `0b5cd5e`, `046104c` | Replaced the Riyal placeholder with the provenance-verified SAMA vector, added a CRLF-safe asset verifier, cleared component style-budget warnings, hardened wide/captioned tables and narrow-screen shell layout, and documented the safe UPC Testing gate. Backend 148/148, frontend 107/107, production bundle 426.93 kB, and the repository build wrapper passed; no state-changing or Production action was attempted. |

## Programme Status

- U0-U8 are complete locally. U8 closed the programme after final regression,
  static/hygiene, documentation, and memory verification.
- U5 owns the completed dark-first token system, shared UI primitives, capped
  toast behavior, sidebar/shell offset, and development-only kitchen sink. U6
  owns the completed order-builder layout; U7 owns the completed app-wide
  primitive migration and legacy glass removal.
- Browser visual checks and full safe Testing order population/send/cancel/
  resend evidence remain deferred external acceptance evidence, not active
  implementation work. No active UI rework plan remains.
- The final project polish task followed U8 and is merged into local `main`.
  ADR-0006 and ADR-0007 own the two business rules it introduced.
