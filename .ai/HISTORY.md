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

## Active Continuation

- UI Rework U8 is the remaining active session.
- U5 owns the completed dark-first token system, shared UI primitives, capped
  toast behavior, sidebar/shell offset, and development-only kitchen sink. U6
  owns the completed order-builder layout; U7 owns the completed app-wide
  primitive migration and legacy glass removal. U8 owns Testing-only
  verification, documentation, and cleanup.
