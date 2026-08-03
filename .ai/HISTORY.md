# Completed Work History

Keep this as a concise milestone index. Detailed implementation evidence belongs
in Git; completed plans with lasting audit value live under `.ai/archive/`.

| Milestone | Status | Evidence | Outcome |
|---|---|---|---|
| Flask multi-module refactor | Completed and superseded | `1f2612d`, `0dcbe16` | Added module-scoped GHC/UPC/Uni-Commerce workflows and UPC validation before the later platform rewrite. |
| .NET 10 + Angular 22 rewrite | Completed | `936bdda` | Replaced the active Flask application with the layered .NET API and Angular SPA baseline. |
| Remediation R0-R10 | Completed | `936bdda` through `b011ffb` | Corrected payload and SQL contracts, moved history to `OrderRequests`, added capability routing and per-session drafts, rebuilt the frontend contract/design system and removed the legacy Flask tree. |
| UI Rework U0 | Completed | `682fd55`, `ea66830` | Verified `dbo.Branches`, corrected API-host evidence, and retained the separately verified SQL host. |
| UI Rework U1 | Completed | `6fd7f77` | Made Testing the explicit default, persisted and displayed the environment, and gated Production actions. |
| UI Rework U2 | Completed | `5ddc4de` | Serialized and atomically persisted per-session draft patches; the frontend now batches and debounces edits. |
| UI Rework U3 | Completed but live verification pending | `f4c5792` plus the current U3 build correction | Added the capability-gated `dbo.Branches` endpoint with cache refresh, the shared searchable selector, and branch-code-only integration across the current consumers. Backend tests and the production build pass; the safe Testing read returned HTTP 500 and browser evidence is still pending. |

## Active Continuation

- UI Rework U3 is locally complete at `f4c5792`; its live Testing/browser gate is
  pending because the safe branch read returned HTTP 500.
- UI Rework U4-U8 remain the active continuation, with U4 next.
