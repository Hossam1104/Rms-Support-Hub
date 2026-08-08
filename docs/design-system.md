# Design System - dark-first foundation

U5 establishes a shared visual language for the operational order desk. The
primary theme is dark: ink/navy page and panel surfaces, a readable slate text
ramp, a sea-glass/Atlantic teal action accent, and semantic colors for success, warning,
danger, information, and neutral states. Light is maintained as a complete
secondary theme rather than a partial inversion.

## Binding rules

- Component styles consume semantic custom properties. Raw color literals are
  allowed only in `frontend/src/styles/_tokens.css` and
  `frontend/src/styles/_gradients.css`.
- Default content surfaces use `--surface-*` tokens. Gradients are intentional
  accents for status pills and the mesh hero only.
- New shared primitives and migrated feature screens use the shared primitives
  and tokens. The legacy `.glass-*` compatibility layer was removed in U7.
- Every interactive control has a visible `:focus-visible` treatment, a true
  disabled state, and reduced-motion behavior where it animates.

Useful check:

```powershell
git grep -n -E "#[0-9a-fA-F]{3,8}" -- frontend/src/app
git grep -n "glass-card\|glass-input\|glass-button\|glass-panel" -- frontend/src
```

Both commands should be empty after U7. Raw feature colors remain restricted to
the token and gradient definition files.

## Token hierarchy

`frontend/src/styles/_tokens.css` is the definition site for surfaces, text,
semantic states, borders, focus, layout, radius, shadow, and motion.

| Family | Important tokens | Use |
|---|---|---|
| Surfaces | `--surface-page`, `--surface-panel`, `--surface-raised`, `--surface-interactive`, `--surface-overlay`, `--surface-muted`, `--surface-hover`, `--surface-selected` | Page, cards, controls, overlays, disabled/quiet regions, hover and selection |
| Text | `--text-primary`, `--text-secondary`, `--text-muted`, `--text-disabled`, `--text-inverse`, `--text-accent` | A deliberate readable text ramp in both themes |
| Semantic | `--state-success-*`, `--state-warning-*`, `--state-danger-*`, `--state-info-*`, `--state-neutral-*` | Foreground, soft background, border, and emphasis states |
| Controls | `--input-bg`, `--input-border`, `--border-focus`, `--focus-ring`, `--focus-ring-danger`, `--divider` | Form fields, keyboard focus, table rules, and section rhythm |
| Tables | `--table-row-hover`, `--table-row-zebra` | Data density without raw colors in feature styles |
| Layout | `--sidebar-expanded-width`, `--sidebar-collapsed-width`, `--sidebar-width`, `--navbar-height` | Shell geometry and sidebar reflow |
| Typography | `--font-main`, `--font-mono`, `--text-xs`–`--text-2xl`, `--weight-regular/semibold/bold/heavy`, `--leading-tight/normal` | Shared type scale for new primitives |
| Spacing | `--space-1`–`--space-8` (4px base) | Shared rhythm for new primitives |
| Z-index | `--z-sticky`, `--z-sidebar`, `--z-navbar`, `--z-dropdown`, `--z-overlay`, `--z-dialog`, `--z-toast` | One layering scale; toast stays above dialogs |
| Shape/elevation | `--radius-sm/md/lg/xl/pill`, `--shadow-sm/md/lg`, `--shadow-glow` | Consistent shape and depth scale |
| Motion | `--d-fast`, `--d`, `--d-slow`, `--ease-spring`, `--ease-out`, `--transition-*` | Shared timing and easing; reduced motion collapses durations |
| Cards | `--card-radius`, `--card-padding`, `--card-gap`, `--card-min-height`, `--card-surface`, `--card-surface-quiet`, `--card-border`, `--card-border-hover`, `--card-sheen`, `--card-shadow`, `--card-shadow-hover`, `--card-lift`, `--card-icon-lift`, `--card-action-shift`, `--card-status-shift` | The one card contract shared by every card surface; motion distances collapse with reduced motion |
| Tool identity | `--tool-brand-from/to`, `--tool-info-from/to`, `--tool-amber-from/to`, `--tool-teal-from/to` | Per-tool icon, edge light, and hover accent |
| Hub scene | `--scene-node`, `--scene-link`, `--scene-halo`, `--scene-backdrop` | Constellation colors and the static gradient fallback |

Legacy compatibility aliases such as `--bg-*`, `--primary`, `--success`,
`--danger`, and `--glass-*` were removed after the U7 migration proved that
they were unused. Current feature styles consume semantic tokens directly.

## Gradient discipline and status map

`frontend/src/styles/_gradients.css` contains the nine status gradients and the
mesh hero. It does not define a gradient as the default surface of a new
primitive.

| Status | Label | Accent |
|---|---|---|
| 1 | New | `--grad-info` |
| 2 | Confirmed | `--grad-indigo` |
| 3 | Ready | `--grad-teal` |
| 4 | With Delegate | `--grad-amber` |
| 5 | Rejected | `--grad-danger` |
| 6 | Canceled by Client | `--grad-rose-slate` |
| 7 | Canceled by Admin | `--grad-rose` |
| 8 | Processing | `--grad-brand` plus a restrained pulse ring |
| 9 | Done | `--grad-success` |

Use `<app-status-pill [status]="9">` for the verified status mapping. The
`page-header` may use `--grad-mesh` as a deliberate hero treatment. The
legacy glass compatibility utilities are no longer defined or consumed.

## Shared primitives

The standalone signal-based components are exported through
`frontend/src/app/shared/ui/index.ts`:

- `ui-card`: default, raised, and keyboard-activatable interactive surfaces;
  header/body/footer projection and disabled behavior.
- `ui-section`: title, description, completion marker, action projection,
  accessible collapse toggle, expanded state, and reduced-motion-safe rhythm.
- `ui-field`: label, optional label status marker, required marker, hint, inline
  error, generated IDs, and the `describedBy()` signal used to link projected
  controls.
- `ui-input`: text/email/number/tel/url/search inputs with Angular
  `ControlValueAccessor` support, small/medium sizes, disabled/read-only/
  invalid states, and prefix/suffix projection.
- `ui-select`: native-select semantics with typed options, placeholder,
  Angular forms support, small/medium sizes, disabled and invalid states.
- `ui-button`: primary, secondary, ghost, and danger variants; small/medium
  sizes; icons; loading; disabled; submit/reset/button semantics; and a
  `pressed` output that ignores duplicate activation while busy.
- `ui-icon-button`: icon-only action button with a mandatory accessible
  label, ghost/secondary/danger variants, small/medium sizes, and an
  `active` toggle state exposed through `aria-pressed`.
- `app-tool-card`: base card for a hub tool entry — gradient-accent icon,
  title, description, and an `available` / `migration-pending` status badge,
  composed on the interactive `ui-card` so keyboard activation and the
  shared focus ring come from the same contract.
- `ui-table`: native table markup with dense, sticky-header, zebra, wide-table,
  horizontal overflow, accessible/visually-hidden caption, and empty-state
  projection.
- `ui-toolbar`: start/center/end projection, compact mode, wrapping, and
  narrow-screen fallback.

Compose `ui-field` with `ui-input`/`ui-select`. Compose it with the existing
`app-searchable-select` for branch search; U5 does not duplicate the U3
searchable behavior.

## The card contract

Every card surface in the application — hub tiles, Prompt Studio generator
cards, Online Order module cards, POS capability cards — consumes the same
`--card-*` tokens, so radius, border, padding rhythm, elevation, and hover
language are defined once. See ADR-0012.

Three card kinds share that language without being interchangeable:

| Kind | Example | Behavior |
|---|---|---|
| Navigation card | `app-tool-card` on the Hub and Prompt Studio landing | One routed action; the whole card is the link |
| Information card | POS planned capability areas | No action; quieter `--card-surface-quiet` surface |
| Live/selection card | `app-module-card` | Contains its own interactive controls, so the card itself is not a single link |

Data-heavy workspace panels and forms keep their own layout. They may use the
card tokens for surface and border, but they are not reshaped into tool cards.

### Equal height

Peer cards in one grid must line up. The grid owns the height, not the card:

```css
.grid { display: grid; grid-auto-rows: 1fr; align-items: stretch; }
.grid > app-tool-card { height: 100%; }
```

and the card itself is a flex column whose last block is pinned:

```css
.card__content { display: flex; flex-direction: column; min-height: var(--card-min-height); }
.card__action { margin-top: auto; }
```

`--card-min-height` is a floor, never a fixed height, so longer descriptions or
extra capability chips grow the row instead of clipping. At the mobile
breakpoint the grids return to `grid-auto-rows: auto`, because stretching
single-column cards to a shared height only adds dead space.

### Structure and interaction

Order is fixed: header (icon container + status pill), title, description,
capability chips, then the footer action. Hover and keyboard focus receive the
same treatment — `translateY(var(--card-lift))`, a `--card-border-hover` edge,
`--card-shadow-hover`, a brighter top edge light, and a small icon nudge. There
is no tilt and no scale. Under reduced motion every transform is dropped and
the border/shadow feedback carries the interaction.

Tool identity is a named accent, never a literal: `app-tool-card` takes
`accent="brand" | "info" | "amber" | "teal"`, which maps to
`--tool-<accent>-from/to`. Prompt Studio is violet-blue, Online Orders is
cyan-blue, and POS is a muted amber because it is not operational.

## Hub scene

The Hub hero renders a decorative Three.js particle constellation with a small
themed core/halo from `features/hub/hub-scene`. It is atmosphere only:

- Three.js is imported dynamically, so it forms its own lazy chunk that no other
  feature loads and that never enters the initial bundle.
- The canvas is `aria-hidden` and pointer-transparent. All content, navigation,
  headings, and accessibility live in HTML.
- Reduced motion (`MotionService`), missing WebGL, or a failed import all leave
  the static `--scene-backdrop` gradient in place. The Hub is fully usable in
  every one of those cases.
- Rendering pauses on `visibilitychange`, device pixel ratio is capped at 1.5,
  link geometry is computed once rather than per frame, the loop runs outside
  the Angular zone, and destroy releases frames, listeners, geometries,
  materials, and the renderer.

This is the only WebGL scene in the application. Internal routes reuse the
static `--scene-backdrop` gradient instead of running their own.

The existing shared kit also includes stat tiles, status pills, data tables,
drawers, dialogs, empty states, skeletons, pagination, JSON trees, copy
buttons, filter chips, the Riyal glyph, page headers, and environment badges.

### Riyal amounts

All human-visible Saudi currency amounts use `app-riyal`; feature templates do
not spell the currency as `SAR` or a textual abbreviation. The component points
to the approved `/assets/Saudi_Riyal.svg` asset, uses the current text color,
and keeps an accessible `Saudi Riyal` label. The checked-in asset is the
approved two-path SAMA vector from the [Saudi Riyal Symbol guideline]
(https://sama.gov.sa/en-US/Currency/SRS/Pages/Guideline.aspx), with canonical
SHA-1 `02b0fe79a4c8f39f6344682e7ef4dcb5f21cf938`. Its official direct asset
URL is `https://sama.gov.sa/ar-sa/Currency/Documents/Saudi_Riyal_Symbol-2.svg`.
Run
`cd frontend; npm run test:riyal-asset` to verify its provenance, viewBox,
path count, and absence of text or external references. The component paints it
through a CSS mask so the symbol inherits `currentColor` in both themes.

### Searchable branch selector

Branch selection uses `app-searchable-select` and submits the branch code only.
The open option list keeps a fixed row geometry while the pointer crosses
options; hover does not change the active keyboard option or trigger animated
overlay repositioning. Keyboard navigation remains responsible for active-row
movement and scrolling.

### Order Requests search workbench

The Order Requests list uses an explicit-apply filter workbench because its
reads can be expensive. The header owns the environment badge, manual refresh,
30-second auto-refresh switch, and collapse control. Primary filters use the
shared `ui-card`, `ui-field`, `ui-input`, `ui-button`, and
`app-searchable-select` primitives; order number is exact by default and
Enter submits an intentional search. Statuses occupy a separate multi-select
chip row, while loading and retryable error states sit below the workbench
instead of competing with status treatment.

The workbench consumes only semantic tokens and the existing status gradients.
At narrow widths it becomes a single-column layout, wraps active chips and
status controls, keeps Apply/Clear reachable, and leaves the wide data grid's
horizontal scrolling inside the table shell rather than on the page. Focus
rings, programmatic labels, live loading/error regions, and reduced-motion
rules remain part of the shared interaction contract.

## Toast behavior

`ToastService` owns a signal-backed visible list and queue:

- no more than three toasts are visible;
- overflow is queued and promoted when a visible item closes;
- consecutive identical message/type pairs become one item with a `xN` count;
- timed dismissal is paused by hover or keyboard focus and resumed afterward;
- each toast has a manual close button, a semantic live region, a variant icon,
  and a responsive bottom-right position;
- reduced motion disables entrance and spinner animation through the global
  motion rules.

The service is deterministic under fake timers, which keeps cap, queue,
deduplication, pause/resume, dismissal, and promotion tests independent of a
browser or API.

## Sidebar and shell

`SidebarStateService` publishes the `collapsed` signal and persists it under
the approved local-storage key `order-tool.sidebar-collapsed`. The sidebar
uses `--sidebar-expanded-width` or `--sidebar-collapsed-width`; the module
shell writes the matching value into its local `--sidebar-width` custom
property, and `main-content` uses that variable for its offset. At narrow
widths the sidebar becomes a compact, labelled icon rail and the main content
uses the collapsed offset so forms and grids do not sit underneath an expanded
navigation surface.

## Themes and reduced motion

`ThemeService` is the single global light/dark theme. It sets
`data-theme="dark"` or `data-theme="light"` on `<html>` and resolves in this
order: an explicit user choice persisted under the namespaced
`qa-support-hub:theme` storage key wins; otherwise the system
`prefers-color-scheme` is followed live until the user picks a theme. Only
explicit choices are persisted. Both theme selectors define the complete
surface, text, border, input, semantic, focus, table, and shadow values
needed by the shared kit.

`MotionService` is the single global motion preference with three states:
`system` (default, follows `prefers-reduced-motion` live), `reduce`, and
`full`. An explicit choice is persisted under `qa-support-hub:motion` and
overrides the system preference in either direction. The service stamps
`data-motion="reduce" | "full"` on `<html>`; the reduced-motion rules in
`_tokens.css` (duration collapse), `_animations.css` (global catch-all), and
`_gradients.css` (status-pill pulse) key off that attribute, with the media
query as the pre-bootstrap fallback (`html:not([data-motion="full"])`).
JS-driven motion (the stat-tile count-up directive) reads the same attribute
first, then the media query. The navbar exposes a cycle toggle
(system -> reduce -> full) through `ui-icon-button`.

## Kitchen sink

`/_kitchen-sink` is registered only when `environment.production` is false.
It demonstrates every U5 primitive and its important states, the U3
searchable branch selector, all nine status pills, both themes, focus and
disabled states, the capped/deduplicated/queued toast stack, existing shared
components, and reduced-motion-relevant controls. It is not linked from
production navigation and is tree-shaken from the production route graph.

## Rollout boundary

- U5 owns this foundation, toast behavior, sidebar reflow, and the kitchen
  sink. It does not redesign feature page layout.
- U6 consumes these primitives for the two-column order-builder workspace,
  collapsible sections, dense tables, and server-totals summary rail.
- U7 migrated the remaining feature pages and deleted the `.glass-*` bridge and
  aliases proven unused. U8 completed local verification and closed the UI
  rework programme. Final acceptance hardening keeps the warning budgets
  unchanged, removes the remaining component style-budget warnings, and adds
  local Edge route evidence at the required desktop/tablet/mobile viewports;
  connected interactive-browser and safe live Testing evidence remain external
  gates.
