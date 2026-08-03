# UI Rework & Workflow Remediation Plan - Programme Closed Locally

> Companion to [`UI_Rework_Prompts.md`](UI_Rework_Prompts.md). This is the
> historical closeout record for U0-U8. The .NET rewrite, remediation R0-R10,
> and UI sessions U0 through U8 are complete locally. Browser visual
> verification and full safe UPC Testing order acceptance remain deferred
> because the required external/browser evidence was unavailable; no active
> implementation session remains.
>
> Milestone evidence is indexed in [`.ai/HISTORY.md`](../.ai/HISTORY.md).
> Completed audit plans are kept under `.ai/archive/` when they have lasting
> value.

---

## 1. Verdict

The verified engine, data flow, U5 presentation foundation, U6 builder
workspace, U7 migration, and final local U8 checks are sound. The U0-U8
programme is closed locally; no active implementation work remains.

Do not re-derive or modify the payload builders and their key-for-key contract
tests, the verified SQL in [`database-schema.md`](database-schema.md),
`OrderRequestRepository`, the `Capabilities` abstraction, the Core/Data/API
layout, per-session drafts, the uniform error envelope, or the completed U4
lookup/totals/send/validation flow.

There are no remaining active operator-facing defects from the U5-U7 visual
rework scope. D10 and D14 are closed in the U7 register below.

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

### 2.1 Interface and presentation - closed U7

| ID | Resolution | Evidence |
|---|---|---|---|
| D10 | All remaining feature surfaces now use U5/U6 primitives and tokens; the `.glass-*` definitions and dead aliases were removed. | `d3219dd`; legacy-class and alias greps empty; frontend 85/85; production initial bundle 427.19 kB |
| D14 | The navbar dead documentation alert was removed; the shell retains meaningful navigation and accessible controls. | `d3219dd`; `alert(` grep empty |

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
5. One design system is the target. U7 completed the remaining migration and
   removed the `.glass-*` bridge and unused compatibility aliases.
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
  U7 restyled it without deleting the route.
- GHC E-Commerce and GHC Uni-Commerce remain capability-gated where live
  database credentials or capabilities are unavailable.

---

## 4. Session plan

| Session | Goal | Main scope | Gate |
|---|---|---|---|
| U4 | **Completed locally - live database item lookup pending.** Item lookup, server totals, send lifecycle, safe endpoint controls, inline validation, and adapter removal. | `features/flat-order/**`, endpoint DTO/controller, U4 tests/docs | Local backend 110/110, frontend 57/57, production build 419.85 kB; safe item lookup HTTP 502 |
| U5 | **Completed locally - browser/live evidence pending.** Design-system foundation, shared primitives, capped toasts, sidebar reflow, and kitchen sink. | Tokens, gradients, shared UI, toast, sidebar, shell, kitchen sink, tests/docs | Commit `3f6646d`; backend 110/110, frontend 68/68, production build 429.42 kB; no browser instance |
| U6 | **Completed locally - builder layout (D11).** Two-column workspace with collapsible sections, sticky section navigation, server-driven summary rail, dense product/payment tables, responsive bottom action bar, and real empty/loading/error states. | Commit `dac0cc4`; flat-order feature and new summary-rail/layout components | Backend 110/110, frontend 81/81, production initial bundle 429.42 kB; browser evidence unavailable |
| U7 | **Completed locally (D10, D14).** Migrated navbar, sidebar, breadcrumb, landing, Order Requests, Uni-Commerce, and Order Validation to U5/U6 primitives; preserved routes and behavior; removed all legacy `.glass-*` rules and dead aliases. | Remaining layout/features, shared components, and `_gradients.css` compatibility rules | Commit `d3219dd`; backend 110/110, frontend 85/85, production initial bundle 427.19 kB; browser unavailable; Testing item lookup HTTP 502 |
| U8 | **Completed locally - external verification pending.** Final regression, safe local API checks, documentation reconciliation, hygiene checks, and programme closeout are complete. | Closeout documentation, memory, plan retirement, and verification evidence | Backend 110/110, frontend 85/85, Release build 0 warnings/0 errors, Angular initial bundle 427.19 kB, local read-only API/proxy/routes passed; browser and full safe Testing order evidence deferred |

**Dependencies:** U0-U8 are locally complete. Browser inspection and
database-dependent order acceptance remain external deferred evidence, not
active implementation dependencies.

Historical sequence was U0 through U8. A green build does not convert an
unavailable live Testing dependency into a passed live claim.

---

## 5. Risks

1. U7 was a broad visual rewrite. The U5/U6 primitive contract remains stable
   and no parallel design system was introduced.
2. Live database access is still required for U3/U4 item evidence and complete
   Testing order acceptance. Safe synthetic item probes were inconsistent: one
   returned HTTP 200 with `success=false` and a repeat returned HTTP 502. Both
   outcomes remain documented separately from local test results.
3. Browser inspection was unavailable. No visual evidence was fabricated;
   viewport/theme acceptance remains deferred. No Production action was used.

---

## 6. Final Closeout (U8)

### Shipped

- U0-U8 local programme work is complete, including the U7 app-wide
  primitive migration and legacy glass removal.
- U8 completed final backend/frontend regression, static and hygiene checks,
  documentation reconciliation, project-memory cleanup, and active-plan
  retirement.
- No payload, SQL, API, capability, dependency, or Production behavior
  changed during U8.

### Deferred Verification

- Browser visual, theme, and responsive checks at 1920, 1280, 900, and 600
  pixels were not run because no in-app browser was available.
- Full safe Testing order population, send, Order Requests UI drawer,
  Request/Response JSON inspection, cancellation, and resend were not run
  because no approved real Testing item was available for a safe workflow.
- Safe synthetic item probes were inconsistent: one returned HTTP 200 with
  `success=false` and a repeat returned HTTP 502. The prior U4/U7 HTTP 502
  remains recorded. No item-population pass is claimed.

### Known External Blockers

- The browser connector reported no available browser instance.
- Testing item population depends on an approved safe item and external
  database behavior; no safe real item was available.
- Live acceptance remains external. Local read-only API calls are not
  send/cancel evidence.

### Discrepancies

- No active U7/U8 implementation discrepancy remains.
- `README.md`, `docs/api-spec.md`, and `docs/database-schema.md` were checked
  against the current code; no verified fact required changing them.
- No active UI rework plan remains; the U8 plan is archived.

### Final Validation

- Backend tests: 110/110; frontend tests: 17 files/85 tests; Release build:
  0 warnings/0 errors; Angular initial bundle: 427.19 kB.
- `check_memory.py`, Markdown links, glass/raw-style, runtime/generated,
  secret/hygiene, conflict/debug, and `git diff --check` passed.
- Local frontend routes/proxy, API module catalog, Testing endpoint metadata,
  branch list, and Order Requests list/detail read paths passed. No Production
  action was attempted.
