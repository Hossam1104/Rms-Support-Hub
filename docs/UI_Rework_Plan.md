# UI Rework & Workflow Remediation Plan - Active U5-U8

> Companion to [`UI_Rework_Prompts.md`](UI_Rework_Prompts.md). Active sessions
> are U5 through U8. The .NET rewrite, remediation R0-R10, and UI sessions U0
> through U4 are complete locally. U3 branch data and U4 database item lookup
> still lack live evidence because the safe Testing infrastructure is
> unavailable; this is not reported as a passed live gate.
>
> Milestone evidence is indexed in [`.ai/HISTORY.md`](../.ai/HISTORY.md).
> Completed audit plans are kept under `.ai/archive/` when they have lasting
> value.

---

## 1. Verdict

The verified engine and data flow are now locally sound; the remaining work is
the frontend cockpit and presentation system.

Do not re-derive or modify the payload builders and their key-for-key contract
tests, the verified SQL in [`database-schema.md`](database-schema.md),
`OrderRequestRepository`, the `Capabilities` abstraction, the Core/Data/API
layout, per-session drafts, the uniform error envelope, or the completed U4
lookup/totals/send/validation flow.

The remaining operator-facing defects are:

1. **D9 - Toasts are uncapped and un-deduplicated.** Repeated failures can
   stack over the page and obscure the primary action.
2. **D10 - Two design systems coexist.** R8 built `shared/ui/**` on new
   tokens, while the order builder still uses the `.glass-*` bridge. U5
   establishes the shared token and primitive foundation; U7 owns bridge
   removal.
3. **D11 - The order builder has no hierarchy or summary rail.** The six
   cards remain a single full-width column; U6 owns the two-column rebuild.
4. **D12 - Sidebar collapse leaves a dead gutter.** The shell still uses a
   fixed expanded offset; U5 owns the real-width CSS variable and persistence.
5. **D14 - Dead documentation affordance.** The navbar still uses an alert;
   U7 owns the route-safe replacement.

U4 is closed locally at `fd5d65d` plus the review correction and transition
commit. Endpoint display, server totals, and validation were reported green by
the handoff and are covered by local contracts; the database-dependent item
lookup returned HTTP 502 and remains pending for live verification.

---

## 2. Defect register

Severity: **S1** means data loss or a non-functioning feature, **S2** means the
operator cannot reasonably work, and **S3** means quality, drift, or hygiene.

### 2.1 Interface and presentation - active

| ID | Severity | Defect | Owner |
|---|---|---|---|
| D9 | S2 | Toasts have no visible cap, queue, duplicate collapse, hover/focus pause, or action-safe responsive placement. | U5 |
| D10 | S2 | New shared UI tokens and primitives coexist with the temporary `.glass-*` feature styles. | U5 foundation; U7 migration/removal |
| D11 | S2 | The builder is a single undifferentiated column with no completion hierarchy or summary rail. | U6 |
| D12 | S2 | Collapsing the 280px sidebar leaves the main content at the expanded offset. | U5 |
| D14 | S3 | Navbar documentation action calls `alert()` and the page lacks a useful documentation link. | U7 |

### 2.2 Closed U4 register

| ID | Resolution | Evidence |
|---|---|---|
| D2 | Item lookup now passes a typed outcome into Add Product, populates verified English/Arabic/code/price/VAT fields, shows the display-only net price, distinguishes not-found from infrastructure failure, and blocks missing branch selection. | `flat-order.component.ts`, `add-product-dialog.component.ts`, focused tests |
| D8 | Flat-order authoritative totals now come from typed `calculate-totals`; debounced draft mutations refresh them, stale responses are guarded, and the last valid summary is retained during refresh/error. | `flat-order.component.ts`, `quick-stats.component.ts`, focused tests |
| D13 | Send state follows the HTTP lifecycle; the resolved endpoint is read-only by default, custom URL entry requires explicit opt-in, and the temporary `PUT order-field` route/callers are removed. | `api-config.component.ts`, `OrderController.cs`, integration tests |

U4 live item lookup remains an external HTTP 502 limitation, not an open local
implementation defect.

---

## 3. Guiding decisions

1. Repair in place. Do not reopen the payload or validation layer.
2. Use only the U0-verified `dbo.Branches` columns in the database contract.
3. The server owns draft persistence and authoritative totals; the client owns
   intent and display state.
4. Testing is the default lane at every layer. Production actions retain typed
   confirmation and are never used for agent-run verification.
5. One design system is the target. The `.glass-*` bridge remains only until
   U7 so U5 can establish primitives without breaking existing screens.
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
  U5 must not change it.
- Order Validation remains routed and is labelled superseded by Order Requests;
  U7 restyles it without deleting the route.
- GHC E-Commerce and GHC Uni-Commerce remain capability-gated where live
  database credentials or capabilities are unavailable.

---

## 4. Session plan

| Session | Goal | Main scope | Gate |
|---|---|---|---|
| U4 | **Completed locally - live database item lookup pending.** Item lookup, server totals, send lifecycle, safe endpoint controls, inline validation, and adapter removal. | `features/flat-order/**`, endpoint DTO/controller, U4 tests/docs | Local backend 110/110, frontend 57/57, production build 419.85 kB; safe item lookup HTTP 502 |
| U5 | **Design-system foundation (D9, D10, D12).** Dark-first tokens with complete light overrides; nine status gradients and mesh hero retained; token-based shared primitives; capped/queued/deduplicated toasts; persisted sidebar state and real shell offset; development-only kitchen sink. | `styles/_tokens.css`, `styles/_gradients.css`, `shared/ui/**`, toast, sidebar, module shell, kitchen sink, design-system docs | Kitchen sink covers every primitive/state/theme/status pill; focused tests and full build pass |
| U6 | **Order-builder layout (D11).** Two-column workspace with collapsible sections, sticky section navigation, server-driven summary rail, dense product/payment tables, responsive bottom action bar, and real empty/loading states. | Flat-order feature and new summary-rail/layout components | 1920/1280/900/600px browser flow with no overlap or page horizontal scroll |
| U7 | **Remaining migration (D10, D14).** Migrate navbar, sidebar, breadcrumb, landing, Order Requests, Uni-Commerce, and Order Validation to U5/U6 primitives; preserve routes and behavior; remove all legacy `.glass-*` rules after consumers are gone. | Remaining layout/features and `_gradients.css` compatibility rules | `git grep` for legacy classes is empty; all routes build and render |
| U8 | **Testing-only end-to-end verification, docs, cleanup.** Verify the UPC Testing flow through Order Requests and cancellation, refresh final documentation, and confirm no credentials/generated files are tracked. | Docs, scripts/build gate, safe browser flow | Full build, required greps, concise E2E transcript, clean diff |

**Dependencies:** U0-U4 are locally complete, with live evidence pending for
database-dependent reads. U5 is next, followed by U6, U7, and U8. U8 depends on
the completed U3-U7 local gates.

Run sessions in order. A green build does not convert an unavailable live
Testing dependency into a passed live claim.

---

## 5. Risks

1. U5-U7 are a broad visual rewrite. Keep the U5 primitive contract stable so
   U6 and U7 can consume it without parallel design systems.
2. The U5 `.glass-*` bridge is intentionally retained; U7 must remove it only
   after the final consumer migration and a repository-wide grep.
3. Live database access is required for U3/U4 item evidence and U8. Report an
   HTTP 502 or other external failure separately from local test results.
4. Removing legacy styles before migration would break screens; the U7 grep is
   the removal gate.
