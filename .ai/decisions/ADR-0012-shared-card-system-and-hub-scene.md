# ADR-0012: One Shared Card Contract and a Decorative Lazy Hub Scene

- **Status:** Accepted
- **Date:** 2026-08-08
- **Task:** Post-release repository cleanup and visual refresh
- **Affected area:** Design tokens, shared UI card surfaces, Hub landing,
  frontend bundle strategy

## Context

Cards had drifted apart across the Hub, the Prompt Studio landing, the Online
Order module picker, and the POS informational page: different radii, padding,
status placement, hover behavior, and — because each grid sized itself from its
own content — visibly unequal heights within a single row of peer choices. The
Hub landing was correct but visually flat for a product that is meant to read
as a professional engineering tool.

## Decisions

1. **One card contract lives in tokens, not in components.**
   `frontend/src/styles/_tokens.css` owns `--card-radius`, `--card-padding`,
   `--card-gap`, `--card-min-height`, `--card-surface`, `--card-surface-quiet`,
   `--card-border`, `--card-border-hover`, `--card-sheen`, `--card-shadow`,
   `--card-shadow-hover`, and `--card-lift`, in both themes. Every card surface
   consumes those names, so shape, elevation, and hover language change in one
   place.
2. **Equal height comes from the grid, never from a fixed pixel height.** Peer
   grids use `grid-auto-rows: 1fr` with `height: 100%` cards; cards are flex
   columns whose action block uses `margin-top: auto`. `--card-min-height` is a
   floor only, so longer content grows instead of clipping. Below the mobile
   breakpoint the rows return to `auto`, because stretching one-column cards to
   a shared height only adds dead space.
3. **Card kinds stay distinct inside one visual language.** `app-tool-card` is
   the navigation card (Hub, Prompt Studio landing). `app-module-card` keeps its
   live environment list, and the POS capability cards stay informational and
   dimmer via `--card-surface-quiet`. Data-heavy workspace panels are not
   converted into decorative tool cards.
4. **Tool identity is a named token pair.** `--tool-<accent>-from/to` supplies
   violet-blue for Prompt Studio, cyan-blue for Online Orders, and muted amber
   for POS. Components pass the accent key (`brand`/`info`/`amber`/`teal`) and
   never a color literal.
5. **Three.js is decoration and must behave like it.** The Hub hero renders a
   particle constellation from `features/hub/hub-scene`, imported dynamically so
   Three.js forms its own lazy chunk that no other feature pulls in and that
   never enters the initial bundle. The canvas is `aria-hidden` and
   pointer-transparent; all content, navigation, and accessibility stay in HTML.
6. **Every failure mode degrades to the same static frame.** Reduced motion
   (`MotionService`), absent WebGL, or a failed dynamic import leave the CSS
   `--scene-backdrop` gradient in place. Rendering pauses on
   `visibilitychange`, device pixel ratio is capped at 1.5, link geometry is
   computed once instead of per frame, the loop runs outside the Angular zone,
   and destroy releases frames, listeners, geometries, materials, and the
   renderer.
7. **Exactly one WebGL scene exists in the application.** Internal routes reuse
   the static `--scene-backdrop` gradient rather than running their own scenes.

## Consequences

- Adding a card means consuming `--card-*` and placing the action last; it does
  not mean writing new geometry values.
- The initial bundle absorbs only the token and hero CSS (436.68 kB → 439.28 kB).
  The Three.js chunk is paid for once, on the Hub, and only when motion and
  WebGL both allow it.
- The Hub must keep passing its tests with the scene absent — that is the
  contract, and `hub-scene.component.spec.ts` plus the Hub spec assert it.
- A future decorative scene elsewhere would need to justify a second WebGL
  context; the default answer is the static gradient.
