# RMS+ Support Hub — Active Task

Program:
RMS+ Support Hub UI / Branding Refactor

Active Session:
04 — Landing, Three.js, Shared Cards, Icons & Motion

Previous:
03 — Completed

Role:
Implement

Expected branch:
refactor/rms-hub-04-landing-motion

Expected commit:
feat(ui): refresh RMS+ Hub landing and motion system

## Global Contract

Repository root:

```text
D:\AI Tools\DBS\online_order_tool
```

Before each session:

1. Read `AGENTS.md`.
2. Read `.ai/STATE.md`.
3. Run `python .ai/scripts/context.py`.
4. Read `.ai/HANDOFF.md` only when its status is `In Progress` or `Blocked`.
5. Read only the source, tests, and documentation named in this task, plus
   task-related changed files.
6. Inspect Git state before changing files.
7. Execute the task completely.
8. Run targeted validation first, then the required full validation gates.
9. Review the final task-scoped diff and remove temporary changes.
10. Update durable state only when materially useful.
11. Commit automatically using the expected commit message.
12. Fast-forward merge the session branch to `main`.
13. Push `main` to `origin`.
14. Verify `origin/main...main == 0 0`.
15. Delete the session branch after synchronization.

Use session-local Git maintenance suppression when required:

```text
git -c gc.auto=0 -c maintenance.auto=false ...
```

Safety:

```text
Never git reset --hard.
Never force push.
Never discard unrelated changes.
Never auto-resolve semantic conflicts.
Never modify Production.
Never run Production SQL.
Never invoke state-changing Online Order actions during UI validation.
Never implement POS operations.
```

The following business behavior is frozen unless this session explicitly says
otherwise:

- Prompt Studio canonical outputs
- Prompt Quality semantics
- Prompt history max = 10
- draft persistence
- Copy/Markdown/Text export
- Ctrl/Cmd+Enter
- Online Order API contracts
- Online Order DTOs/payloads
- filters/paging/status behavior
- capability guard
- payment codes
- integration/module keys
- POS remains Coming Soon

Repository guardrails:

- Treat `docs/request_examples/**` and mirrored backend test fixtures as the
  payload contract.
- Treat current repository SQL plus `docs/database-schema.md` as the database
  contract; never guess a column or JSON key.
- Keep backend dependencies flowing Core -> Data -> API, with API as the
  composition root.
- Gate module behavior through `IOrderModule.Capabilities`; do not add
  module-key string comparisons.
- Keep connection strings and credentials outside tracked files.
- Use the Testing environment for agent-run live verification. Never send,
  cancel, or resend against Production.
- Do not edit generated or runtime paths: `bin/`, `obj/`, `node_modules/`,
  `dist/`, `.angular/`, or `var/`.
- Component styles must consume design tokens; raw color literals belong only
  in the designated token/gradient files.

## Session 04 — Landing, Three.js, Shared Cards, Icons & Motion

## Goal

Make the **RMS+ Support Hub** landing visually compelling and finalize the shared card/icon/motion language.

## Branch

```text
refactor/rms-hub-04-landing-motion
```

## Three.js Boundary

Three.js already exists.

Do NOT:

- install another 3D framework
- create additional WebGL scenes in modules
- move Three.js into the root initial bundle

### 1. Landing hero

Use:

- RMS logo
- RMS+ Support Hub title
- concise subtitle
- environment/status
- subtle DBS attribution

Reduce dead vertical hero space.

### 2. Refine current Hub Three.js scene

Preserve:

- dynamic/lazy import
- Hub-only
- aria-hidden/decorative
- pointer-events none
- DPR cap
- visibility pause
- teardown
- reduced-motion fallback
- functional independence from WebGL

Enhance only with restrained technical/RMS visual identity.

No giant models/textures/post-processing stack.

### 3. Main tool cards

Cards:

1. QA Prompt Studio
2. Online Order Tool
3. POS Maintenance Tool

Requirements:

- equal-height peers
- aligned logo/icon/title/status
- aligned footer action
- capability list
- same card contract
- strong focus
- restrained hover
- POS = Coming Soon

### 4. Icon language

Reuse existing icon system.

Add meaningful icons to:

- capability summaries
- card actions
- status/meta
- common section headings

Do not add an icon dependency if current project already has adequate icons.

### 5. Motion language

Use MotionService.

Add subtle:

- card entrances
- hover/focus
- icon movement
- status reveal
- section expansion

Reduced motion removes transforms/entrances.

No heavy table animation.

## Validation

```text
npm --prefix frontend test -- --watch=false
npm --prefix frontend run build -- --configuration production
```

Record:

- initial bundle
- Three.js lazy chunk
- warnings

Browser if available:

- WebGL scene
- fallback
- reduced motion
- light/dark
- card keyboard navigation
- no layout shift
- console clean

## Commit

```text
feat(ui): refresh RMS+ Hub landing and motion system
```
