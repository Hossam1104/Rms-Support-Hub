# SESSION 07 — Cross-Project UI Closure & Browser Matrix

## Goal

Finish global UI consistency and prove the result visually.

## Branch

```text
refactor/rms-hub-07-ui-closure
```

## Session 06 Review Carry-Forward

- Reset `animation-delay` to `0ms !important` under reduced motion for
  nonessential animations.
- Standardize the stagger-handling approach across Hub and Prompt Studio.
- Keep any `capabilityIcon()` redesign optional; do not make it mandatory for
  Session 07 acceptance.

## Required Work

### 1. Asset audit

Verify each integrated asset:
- semantic use
- no duplicate copies
- correct sizing
- no distortion
- correct alt/decorative state

List intentionally unused supplied assets and why.

### 2. Global consistency

Audit:
- navbar
- page headers
- breadcrumbs
- cards
- tables
- forms
- statuses
- icons
- loaders
- messages
- panels
- empty states

Fix only real inconsistencies.

### 3. Equal-height peers

Verify:
- Hub cards
- Prompt Studio landing cards
- Online Order module cards
- POS capability cards
- payment method cards if card-based

### 4. Global table audit

No true table should:
- touch card edges
- lack readable row separation
- have inconsistent totals
- cause shell overflow

### 5. REQUIRED browser matrix

Routes:

```text
/
 /tools/prompt-studio
 /tools/prompt-studio/bugs
 /tools/prompt-studio/stories
 /tools/prompt-studio/test-cases
 /tools/online-orders
 /tools/online-orders/modules/<actual-key>/order
 /tools/online-orders/modules/<actual-key>/order-requests
 /tools/pos-maintenance
```

Widths:

```text
1440 × 900
1024 × 900
900 × 900
768 × 900
390 × 844
```

Validate:
- one H1
- one primary main
- no shell overflow
- compact density
- table borders
- table margins
- equal cards
- logo scaling
- icons/accessibility
- light theme
- dark theme
- reduced motion
- Three.js only on Hub
- keyboard focus
- no new console/page errors

If browser tooling is unavailable:
- do not claim completion
- return Partially Completed
- leave a precise browser-validation handoff

### 6. Performance

Record:
- initial bundle
- Three.js lazy chunk
- largest relevant feature chunks
- warnings

Investigate major unexplained growth.

## Full Validation

Required:
- frontend full suite
- backend/repository wrapper
- production build
- `git diff --check`

## Commit

```text
refactor(ui): close RMS+ cross-project visual consistency
```

---
