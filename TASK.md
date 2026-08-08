# SESSION 05 — Prompt Studio UI Harmonization

## Goal

Apply the new compact branded system to Prompt Studio while preserving its simplified behavior.

## Branch

```text
refactor/rms-hub-05-prompt-studio-ui
```

## Hard Output Contracts

Do not change canonical sections.

Bug = exactly 11 sections.
Story = exactly 7 sections.
Test Case = exactly 9 sections.

Do not change:
- quality semantics
- history max 10
- drafts
- copy
- Markdown
- text export
- Ctrl/Cmd+Enter

### 1. Landing cards

Equal-height:
- Bug Refinement
- Story Refinement
- Test Case Generation

Each:
- meaningful icon
- title
- concise explanation
- capability summary
- aligned footer action

### 2. Generator workspaces

Compact:
- page header
- form section spacing
- label/helper gaps
- preview header/action area
- Prompt Quality
- history

Use shared surface/card/density tokens.

### 3. Icons

Add useful icons for:
- Load Sample
- Generate
- Clear
- Copy
- Download
- Quality
- History

Preserve accessible names.

### 4. Motion

Use restrained feedback/entrance only.

Reduced motion must disable nonessential transforms.

## Validation

Run:
- focused builder/generator/history/storage/export tests
- full frontend suite
- production build
- `git diff --check`

Explicitly confirm prompt-builder contracts unchanged.

## Commit

```text
refactor(prompt-studio): harmonize RMS+ compact visual system
```

---
