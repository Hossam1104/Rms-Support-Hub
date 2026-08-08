# Completed Work History

A concise milestone index. Detailed implementation evidence belongs in Git;
per-session validation numbers are not repeated here.

| Milestone | Status | Evidence | Outcome |
|---|---|---|---|
| Flask multi-module refactor | Completed and superseded | `1f2612d`, `0dcbe16` | Added module-scoped GHC/UPC/Uni-Commerce workflows and UPC validation before the platform rewrite. |
| .NET 10 + Angular 22 rewrite | Completed | `936bdda` | Replaced the Flask application with the layered .NET API and Angular SPA baseline. |
| Remediation R0-R10 | Completed | `936bdda`..`b011ffb` | Corrected payload and SQL contracts, moved history to `OrderRequests`, added capability routing and per-session drafts, rebuilt the frontend contract and design system, removed the legacy Flask tree. |
| UI Rework U0-U8 | Completed | `682fd55`..`d3219dd` plus the U8 closeout | Verified `dbo.Branches` and API-host evidence; made Testing the explicit default with gated Production actions; serialized atomic per-session draft patches; added the capability-gated branch endpoint and shared searchable selector; connected item lookup, server-owned totals, and real send lifecycle; established the dark-first token system, shared UI primitives, capped toasts, and the dev-only kitchen sink; rebuilt the flat-order workspace; migrated every surface off the legacy `.glass-*` layer. Rationale D1-D13 is recorded in `docs/UI_Rework_Plan.md` and cited from source comments. |
| Final project polish | Completed | `30bb28e`, `3c65eaa`, `0953f63`, `4aee8cd` | Made an empty payment list a valid Cash-on-Delivery order end to end (ADR-0006), split the Saudi country code out of the phone fields (ADR-0007), pinned UPC first in the module display, and routed every visible amount through `app-riyal`. A payment-free UPC **Testing** send must still confirm the RMS accepts `"COD"`. |
| Order Requests unification and stabilization | Completed and synchronized on `main` | `44006a4`..`51ad1e4` | Replaced the overlapping validation/drawer experience with one canonical list/detail route, added same-number resend from the selected stored `RequestJson` with server-side guards, corrected normalized filtering/paging/stat semantics, and made Clear All a single fresh-default reset transaction (ADR-0008, ADR-0009, ADR-0010). |
| Final acceptance hardening | Completed locally | `0b5cd5e`, `046104c` | Replaced the Riyal placeholder with the provenance-verified SAMA vector plus a CRLF-safe verifier, cleared component style-budget warnings, and hardened wide tables and narrow-screen shell layout. |
| QA Support Hub Sessions 00-03 | Completed | `eaeb43e` plus the Session 00-03 commits | Recorded the baseline architecture as ADR-0011; added the hub dashboard, lazy `/tools/*` routes with typed `ToolRouteData`, and the legacy `/modules/:key` compatibility mount; renamed the product to QA Support Hub; added the typography/spacing/z-index token scales, `MotionService`, `ui-icon-button`, and `app-tool-card`. |
| QA Support Hub Sessions 04-08 | Completed | Session 04-08 commits through `20bb51b` | Rebuilt Prompt Studio natively in Angular: Bug, Story, and Test Case typed reactive forms with grouped fields, legacy draft normalization, deterministic builders with detail levels and Generic/Jira/Azure DevOps formats, configurable optional sections, sample data, copy/Markdown/plain-text export, Ctrl/Cmd+Enter, advisory `PromptQualityService`, and `PromptHistoryService` capped at ten records. The standalone HTML prompt generator was audited for parity and retired. |
| QA Support Hub Sessions 09-10 | Completed | `e9d6958`, `7e7b743` | Integrated Online Orders with the shared hub shell, breadcrumbs, and tokenized spacing without changing business behavior; replaced the POS placeholder with an informational Coming Soon workspace, a typed `PosCapability` model, and the migration intake reference. |
| QA Support Hub roadmap correction | Completed | `e352f17` | Deferred Sessions 11-13 by design while the POS Maintenance Tool is developed externally, and simplified POS presentation to Coming Soon with no operations. |
| QA Support Hub Sessions 14-16 | Completed | `0452c88`, `3a2837a`, `4a59893` | Hardened cross-tool responsive consistency, then accessibility, security, and performance (non-fatal storage handling, safe download names, keyboard and landmark fixes, route teardown, offline font build profile), then ran the final integration regression and produced `docs/QA_SUPPORT_HUB_RELEASE_READINESS.md`. |
| Post-release cleanup and visual refresh | Completed | `refactor(app): clean repository and refresh visual system` | Removed superseded programme plans, session prompts, the duplicated command cheat sheet, the `.ai` plan archive, three dead source files, and four unused CSS animations. Added one shared card contract (`--card-*` tokens, equal-height peer grids, pinned actions) applied across the Hub, Prompt Studio, Online Order module, and POS capability cards; redesigned the Hub landing with a decorative lazy-loaded Three.js constellation that degrades to a static gradient (ADR-0012). Prompt Studio and Online Order behavior were unchanged. |

## Programme status

- Every programme above is closed. There is no active plan and no numbered
  implementation session; the repository is in post-release iterative
  maintenance (see `TASK.md`).
- Remaining external evidence, not implementation work: safe UPC **Testing**
  order population/send/cancel/resend acceptance, the Production database index
  decision, and deployment/Production acceptance.
