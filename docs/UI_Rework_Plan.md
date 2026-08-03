# UI Rework & Workflow Remediation Plan - Active U7-U8

> Companion to [`UI_Rework_Prompts.md`](UI_Rework_Prompts.md). Active sessions
> is U7, followed by U8. The .NET rewrite, remediation R0-R10, and UI sessions
> U0 through U6 are complete locally. U3 branch data and U4 database item
> lookup still lack live evidence because the safe Testing infrastructure is
> unavailable; this is not reported as a passed live gate.
>
> Milestone evidence is indexed in [`.ai/HISTORY.md`](../.ai/HISTORY.md).
> Completed audit plans are kept under `.ai/archive/` when they have lasting
> value.

---

## 1. Verdict

The verified engine, data flow, U5 presentation foundation, and U6 builder
workspace are locally sound. The remaining work is the final feature
migration/Testing verification.

Do not re-derive or modify the payload builders and their key-for-key contract
tests, the verified SQL in [`database-schema.md`](database-schema.md),
`OrderRequestRepository`, the `Capabilities` abstraction, the Core/Data/API
layout, per-session drafts, the uniform error envelope, or the completed U4
lookup/totals/send/validation flow.

The remaining operator-facing defects are:

1. **D10 - Two design systems coexist.** U5 established the shared token and
   primitive foundation; the `.glass-*` bridge remains until the U7 migration.
2. **D14 - Dead documentation affordance.** The navbar still uses an alert;
   U7 owns the route-safe replacement.

U5 is closed locally at `3f6646d`, and U6 is closed locally at `dac0cc4`. U6
rebuilt the flat-order workspace with collapsible sections, capability-aware
navigation, a server-value-only summary rail, dense product/payment tables,
real loading/empty/error states, and a responsive bottom action bar. U6 passed
backend 110/110, frontend 81/81, and the warning-free production build with a
429.42 kB initial bundle. Browser visual evidence is unavailable in this
session. U4 endpoint display, server totals, and validation are covered by
local contracts; the database-dependent item lookup returned HTTP 502 and
remains pending for live verification.

---

## 2. Defect register

Severity: **S1** means data loss or a non-functioning feature, **S2** means the
operator cannot reasonably work, and **S3** means quality, drift, or hygiene.

### 2.1 Interface and presentation - active

| ID | Severity | Defect | Owner |
|---|---|---|---|
| D10 | S2 | New shared UI tokens and primitives coexist with the temporary `.glass-*` feature styles. | U5 foundation; U7 migration/removal |
| D14 | S3 | Navbar documentation action calls `alert()` and the page lacks a useful documentation link. | U7 |

### 2.2 Closed U6 register

| ID | Resolution | Evidence |
|---|---|---|
| D11 | The flat-order builder now has an ordered two-column workspace with collapsible sections, capability-aware section navigation, a server-driven summary rail, dense product/payment tables, real loading/empty/error states, and a responsive bottom action bar. | `dac0cc4`, flat-order focused specs; backend 110/110, frontend 81/81, production initial bundle 429.42 kB; browser evidence unavailable |

### 2.3 Closed U5 register

| ID | Resolution | Evidence |
|---|---|---|
| D9 | Toasts now cap visible items at three, queue overflow, collapse repeated messages, pause on hover/focus, support close, promote queued items, and expose an accessible live region. | `toast.service.ts`, `toast.component.ts`, toast tests |
| D12 | Sidebar collapse is persisted and the module shell binds `--sidebar-width` to the actual expanded/collapsed width without the former dead gutter. | `sidebar-state.service.ts`, `sidebar.component.ts`, `module-shell.component.ts`, sidebar test |

### 2.4 Closed U4 register

| ID | Resolution | Evidence |
|---|---|---|
| D2 | Item lookup now passes a typed outcome into Add Product, populates verified English/Arabic/code/price/VAT fields, shows the display-only net price, distinguishes not-found from infrastructure failure, and blocks missing branch selection. | `flat-order.component.ts`, `add-product-dialog.component.ts`, focused tests |
| D8 | Flat-order authoritative totals now come from typed `calculate-totals`; debounced draft mutations refresh them, stale responses are guarded, and the last valid summary is retained during refresh/error. | `flat-order.component.ts`, `quick-stats.component.ts`, focused tests |
| D13 | Send state follows the HTTP lifecycle; the resolved endpoint is read-only by default, custom URL entry requires explicit opt-in, and the temporary `PUT order-field` route/callers are removed. | `api-config.component.ts`, `OrderController.cs`, integration tests |

U4 live item lookup remains an external HTTP 502 limitation, not an open local
implementation defect.

U6 Validate remains a non-sending draft/preview/totals refresh because the
current API has no standalone validation endpoint; `send-request` remains the
server-authoritative validation/send path. No payload, totals, SQL,
capability, draft, or API contract was changed.

---

## 3. Guiding decisions

1. Repair in place. Do not reopen the payload or validation layer.
2. Use only the U0-verified `dbo.Branches` columns in the database contract.
3. The server owns draft persistence and authoritative totals; the client owns
   intent and display state.
4. Testing is the default lane at every layer. Production actions retain typed
   confirmation and are never used for agent-run verification.
5. One design system is the target. The `.glass-*` bridge remains only until
   U7 completes the remaining consumer migration.
6. Dark-first is the primary theme; light is a complete maintained secondary
   theme, not a partial inversion.
7. Every live call made by an agent goes to UPC Testing only.
8. Each session remains independently releasable with focused tests and a
   warning-free production build.

### 3.1 Environment matrix

| Environment | API lane | Database | Connection-string key |
|---|---|---|---|
| UPC Testing (default) | Testing RMS API | `RmsMainTest2` | `UpcEcommerceTest` |
| UPC Production | Production RMS API | `RmsMainProd` | `UpcEcommerceProd` |

Endpoint values and credentials stay in application configuration/user-secrets
or environment variables. They do not belong in tracked files or project
memory.

### 3.2 Scope notes

- U2 serialized and batched draft writes are complete. U4 consumes that flow;
  U6 must not change it.
- Order Validation remains routed and is labelled superseded by Order Requests;
  U7 restyles it without deleting the route.
- GHC E-Commerce and GHC Uni-Commerce remain capability-gated where live
  database credentials or capabilities are unavailable.

---

## 4. Session plan

| Session | Goal | Main scope | Gate |
|---|---|---|---|
| U4 | **Completed locally - live database item lookup pending.** Item lookup, server totals, send lifecycle, safe endpoint controls, inline validation, and adapter removal. | `features/flat-order/**`, endpoint DTO/controller, U4 tests/docs | Local backend 110/110, frontend 57/57, production build 419.85 kB; safe item lookup HTTP 502 |
| U5 | **Completed locally - browser/live evidence pending.** Design-system foundation, shared primitives, capped toasts, sidebar reflow, and kitchen sink. | Tokens, gradients, shared UI, toast, sidebar, shell, kitchen sink, tests/docs | Commit `3f6646d`; backend 110/110, frontend 68/68, production build 429.42 kB; no browser instance |
| U6 | **Completed locally - builder layout (D11).** Two-column workspace with collapsible sections, sticky section navigation, server-driven summary rail, dense product/payment tables, responsive bottom action bar, and real empty/loading/error states. | Commit `dac0cc4`; flat-order feature and new summary-rail/layout components | Backend 110/110, frontend 81/81, production initial bundle 429.42 kB; browser evidence unavailable |
| U7 | **Remaining migration (D10, D14).** Migrate navbar, sidebar, breadcrumb, landing, Order Requests, Uni-Commerce, and Order Validation to U5/U6 primitives; preserve routes and behavior; remove all legacy `.glass-*` rules after consumers are gone. | Remaining layout/features and `_gradients.css` compatibility rules | `git grep` for legacy classes is empty; all routes build and render |
| U8 | **Testing-only end-to-end verification, docs, cleanup.** Verify the UPC Testing flow through Order Requests and cancellation, refresh final documentation, and confirm no credentials/generated files are tracked. | Docs, scripts/build gate, safe browser flow | Full build, required greps, concise E2E transcript, clean diff |

**Dependencies:** U0-U6 are locally complete, with live evidence pending for
database-dependent reads and browser inspection. U7 is next, followed by U8.
U8 depends on the completed U3-U7 local gates.

Run sessions in order. A green build does not convert an unavailable live
Testing dependency into a passed live claim.

---

## 5. Risks

1. U7 is a broad visual rewrite. Keep the U5/U6 primitive contract stable so
   the migration work does not create a parallel design system.
2. The U5 `.glass-*` bridge is intentionally retained; U7 must remove it only
   after the final consumer migration and a repository-wide grep.
3. Live database access is required for U3/U4 item evidence and U8. Report an
   HTTP 502 or other external failure separately from local test results.
4. Removing legacy styles before migration would break screens; the U7 grep is
   the removal gate.
