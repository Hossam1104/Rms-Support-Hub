# Design System — Bold Gradient / Vibrant

Replaces the dark-glassmorphism system from the original rewrite
(`_glassmorphism.css` + the glass variables in the old `_variables.css`) —
completed in remediation session R8; see [`.ai/HISTORY.md`](../.ai/HISTORY.md).

## The binding rule

**No component stylesheet may contain a raw hex value.** Every color, in
every `.component.ts`'s inline `styles: [...]` array and every file under
`frontend/src/styles/`, must read a custom property defined in
`_tokens.css` or `_gradients.css`. Those two files are the *only* place raw
hex/`rgba(...)` literals are allowed to live — they are the definition
site the rest of the app reads from.

Verify with:
```powershell
Select-String -Path src/app -Include *.ts,*.css -Pattern "#[0-9a-fA-F]{3,6}" -Recurse
```
Expect zero hits (the two style files above are not under `src/app`, so a
correctly-scoped grep against `src/app` alone should already be empty).

## Files

- `frontend/src/styles/_tokens.css` — every custom property: brand gradient
  stops, semantic gradients, back-compat single-color aliases, surfaces,
  radius scale, shadow scale, motion (durations + easings), plus the
  `[data-theme="light"]` override block and the global
  `prefers-reduced-motion` duration collapse.
- `frontend/src/styles/_gradients.css` — the status-pill gradient map, the
  `meshDrift`/`shimmerSweep` keyframes, and the legacy `.glass-card` /
  `.glass-panel` / `.glass-input` / `.glass-button` utility classes, kept as
  a token-backed bridge (see "Rollout" below).
- `frontend/src/app/core/services/theme.service.ts` — **unchanged this
  session**. Still just toggles `document.documentElement.setAttribute('data-theme', 'dark' | 'light')`
  and persists to `localStorage`; `_tokens.css` supplies both value sets.

## Token catalogue

| Token | Value | Use |
|---|---|---|
| `--brand-500` / `--accent-500` / `--hot-500` | `#7C3AED` / `#DB2777` / `#F97316` | Brand gradient stops (violet → pink → orange) |
| `--grad-brand` | `linear-gradient(135deg, ...)` | Primary CTAs, active nav, brand accents |
| `--grad-success` / `--grad-danger` / `--grad-info` / `--grad-muted` | | Semantic surfaces |
| `--grad-indigo` / `--grad-teal` / `--grad-amber` / `--grad-rose` / `--grad-rose-slate` | | Status-pill-only hues (see below) |
| `--grad-mesh` | 3-layer radial gradient | `page-header`'s hero background |
| `--on-gradient` | `#ffffff` | Text/icon color painted on any gradient surface |
| `--radius-sm/md/lg/xl/pill` | `12/18/26/34/999px` | Full radius scale |
| `--shadow-sm/md/lg` + `--shadow-glow` | | Elevation + brand glow |
| `--d-fast/--d/--d-slow` | `140/240/420ms` | Motion durations |
| `--ease-spring` / `--ease-out` | `cubic-bezier(.34,1.56,.64,1)` / `cubic-bezier(.16,1,.3,1)` | Motion easings |
| `--transition-fast` / `--transition-normal` | `var(--d-fast) var(--ease-out)` / `var(--d) var(--ease-out)` | Back-compat aliases — most existing component styles already read these two names |

`--primary`, `--primary-gradient`, `--success`, `--warning`, `--danger`,
`--bg-*`, `--text-*`, `--glass-*` also still exist, as aliases onto the new
palette — see "Rollout".

## Status-pill gradient map

Keyed to `RequestOrderHeaders.OrderStatus` (1–9), see
`backend/src/OnlineOrderTool.Core/OrderRequestStatus.cs`:

| Status | Label | Gradient |
|---|---|---|
| 1 | New | `--grad-info` |
| 2 | Confirmed | `--grad-indigo` |
| 3 | Ready | `--grad-teal` |
| 4 | With_Delegate | `--grad-amber` |
| 5 | Rejected | `--grad-danger` |
| 6 | CanceledByClient | `--grad-rose-slate` |
| 7 | CanceledByAdmin | `--grad-rose` |
| 8 | Processing | `--grad-brand` + pulsing ring (`.status-pill--8::after`) |
| 9 | Done | `--grad-success` |

Use `<app-status-pill [status]="9">` (`shared/ui/status-pill`) — it applies
the `.status-pill--N` class from `_gradients.css` and pops (scale spring)
whenever `status` changes.

## Shared UI kit (`frontend/src/app/shared/ui/`)

`gradient-card`, `stat-tile` (count-up via `shared/directives/count-up.directive.ts`),
`status-pill`, `json-tree` (+ `json-tree-node`, recursive), `drawer` (CDK
a11y focus trap, Esc/backdrop close), `confirm-dialog` (danger/brand
variant, optional required-reason textarea), `empty-state`, `skeleton`,
`data-table` (CDK virtual scroll + staggered row entrance), `pagination`,
`riyal` (CSS-mask technique — see below), `copy-button`, `filter-chip`,
`page-header` (mesh hero, `meshDrift`). Barrel export: `shared/ui/index.ts`.

Dev-only showcase: `/_kitchen-sink` (`features/kitchen-sink/`), registered
in `app.routes.ts` only when `!environment.production` — the production
build's `fileReplacements` swap makes that branch provably dead, so the
route and its lazy chunk are tree-shaken out of the production bundle
entirely (verified: no `kitchen-sink-component` chunk in
`ng build --configuration production` output).

### Riyal CSS-mask technique

`Saudi_Riyal.svg` is a single-color glyph (`fill="currentColor"`). Painting
it via `<img>` would lock its color to whatever the SVG's own fill
resolves to inside an isolated image context (which doesn't inherit CSS at
all) — instead, `shared/ui/riyal` paints a `background-color: currentColor`
box masked by the SVG shape (`mask` / `-webkit-mask: url(...) no-repeat
center / contain`), so the glyph always matches surrounding text color
without a duplicate colored asset per theme.

### Reduced motion

`_animations.css` collapses every `animation-duration`/`transition-duration`
to `0.01ms` under `@media (prefers-reduced-motion: reduce)` — a global
catch-all, since not every keyframe reads a `--d-*` token directly.
`_tokens.css` additionally collapses the duration tokens themselves at the
same breakpoint (belt-and-suspenders for anything computing off them in
JS). The one animation that can't be handled by CSS alone —
`count-up.directive.ts`'s `requestAnimationFrame` loop — checks
`window.matchMedia('(prefers-reduced-motion: reduce)')` itself and renders
the final value on the first frame instead of animating.

## Rollout

This session (R8) swaps the *foundation* — tokens, gradients, the shared
kit — and restyles `landing`, the shell (`navbar`/`sidebar`/`breadcrumb`),
and `flat-order` (its `quick-stats` sub-component now uses `app-stat-tile`)
onto it, per the prompt's explicit scope. It deliberately does **not**
touch `unicommerce`, `order-requests`, or `order-validation` — those are
later sessions' jobs (R9 rebuilds Order Requests; R10 rebuilds the
Uni-Commerce/order-builder UI).

Those not-yet-restyled features still use the pre-R8 `.glass-card` /
`.glass-panel` / `.glass-input` / `.glass-button` utility class names in
their templates. Rather than leave them referencing now-deleted CSS
variables (an unstyled regression across most of the app), `_gradients.css`
keeps these four class names defined — now sourced entirely from the new
token set (gradient buttons instead of a flat indigo, the new radius/shadow
scale, etc.) — as a compatibility bridge. When a feature is restyled in its
own session, prefer the new `shared/ui` components over these classes; do
not add new usages of `.glass-*` going forward.
