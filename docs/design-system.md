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
| Shape/elevation | `--radius-sm/md/lg/xl/pill`, `--shadow-sm/md/lg`, `--shadow-glow` | Consistent shape and depth scale |
| Motion | `--d-fast`, `--d`, `--d-slow`, `--ease-spring`, `--ease-out`, `--transition-*` | Shared timing and easing; reduced motion collapses durations |

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
- `ui-table`: native table markup with dense, sticky-header, zebra, wide-table,
  horizontal overflow, accessible/visually-hidden caption, and empty-state
  projection.
- `ui-toolbar`: start/center/end projection, compact mode, wrapping, and
  narrow-screen fallback.

Compose `ui-field` with `ui-input`/`ui-select`. Compose it with the existing
`app-searchable-select` for branch search; U5 does not duplicate the U3
searchable behavior.

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

`ThemeService` continues to set `data-theme="dark"` or `data-theme="light"`
and persist the choice. Both selectors define the complete surface, text,
border, input, semantic, focus, table, and shadow values needed by the shared
kit. `_animations.css` remains the global safety net: `prefers-reduced-motion`
collapses CSS animations/transitions, disables smooth scrolling, and the
token-level duration collapse covers components that use token aliases.

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
  local desktop/mobile route evidence; connected interactive-browser and safe
  live Testing evidence remain external gates.
