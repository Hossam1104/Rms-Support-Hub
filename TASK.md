# RMS+ Support Hub — Active Task

Program:
RMS+ Support Hub UI / Branding Refactor

Active Session:
02 — Asset Pipeline & Brand Foundation

Previous:
01 — Completed

Known gap:
`frontend/public/assets/Saudi_Riyal.svg` is currently missing; Session 02 must resolve it deliberately.

# Global Contract for Every Luna Session

Repository root:

```text
D:\AI Tools\DBS\online_order_tool
```

Before each session:

1. Read `AGENTS.md`.
2. Read `.ai/STATE.md`.
3. Read `.ai/HANDOFF.md`.
4. Read `TASK.md` if present.
5. Inspect Git state.
6. Inspect only session-relevant source.
7. Execute the task completely.
8. Run targeted validation.
9. Run the required full validation gates.
10. Review the final task-scoped diff.
11. Update durable state only when materially useful.
12. Commit automatically.
13. Fast-forward merge to `main`.
14. Push `main`.
15. Verify `origin/main...main == 0 0`.
16. Delete the session branch.

Safety:

```text
Never git reset --hard.
Never force push.
Never discard unrelated work.
Never auto-resolve semantic conflicts.
Never modify Production.
Never run Production SQL.
Never invoke state-changing Online Order actions during UI validation.
Never implement POS operations.
```

Use session-local Git maintenance suppression when required:

```text
git -c gc.auto=0 -c maintenance.auto=false ...
```

The following business behavior is frozen unless a session explicitly says otherwise:

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


# SESSION 02 â€” Asset Pipeline & Brand Foundation

## Goal

Make the supplied assets reusable, semantic, centralized and safe.

## Branch

```text
refactor/rms-hub-02-assets
```

## Required Work

### 1. Reinspect actual assets

Use current:

```text
./assets
```

Never assume files are unchanged.

### 2. Normalize frontend asset placement

Use the repositoryâ€™s existing public/static convention.

Prefer semantic folders such as:

```text
frontend/public/assets/
â”œâ”€â”€ brand/
â”œâ”€â”€ modules/
â”œâ”€â”€ payments/
â”œâ”€â”€ commerce/
â””â”€â”€ system/
```

Do not duplicate an already-approved asset unless migration is required.

Preserve the verified Riyal behavior.

### 3. Central asset catalog

Create one typed mapping, preferably:

```text
frontend/src/app/core/config/app-assets.ts
```

Expose semantic references for:
- RMS
- DBS
- UPC
- GHC/Whites
- Riyal
- Visa
- Mastercard
- Mada
- Tabby
- Tamara
- offer
- loader
- confirmed CustomMessage assets

Do not create a service unless runtime logic needs it.

### 4. Reusable logo/brand primitive

Only if useful across multiple screens, add a lightweight logo/brand component or shared pattern supporting:
- size
- alt
- decorative state
- `object-fit: contain`
- consistent aspect handling

Do not over-engineer.

### 5. Integrate hierarchy

RMS:
- global navbar
- Hub hero/product identity

DBS:
- secondary attribution only

UPC:
- UPC E-Commerce module card/shell/sidebar

GHC/Whites:
- only where actual module/context supports it

### 6. CustomMessage assets

Inspect contents. Map only confirmed semantics. Unknown assets stay unused and documented.

No new message functionality.

## Validation

Targeted:
- asset catalog
- logo primitive
- Hub/module card

Then:

```text
npm --prefix frontend test -- --watch=false
npm --prefix frontend run build -- --configuration production
git diff --check
```

## Commit

```text
feat(brand): integrate RMS+ Support Hub asset system
```

---
