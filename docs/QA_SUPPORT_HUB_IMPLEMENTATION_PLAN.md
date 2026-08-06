# QA Support Hub — Unified Implementation and Migration Plan

## 1. Document Purpose

This document defines the target architecture, user experience, migration approach, delivery phases, acceptance criteria, and technical guardrails for transforming the existing Online Order Tool into a unified **QA Support Hub**.

The unified application will provide one modern dashboard with selectable cards that navigate to:

1. **QA Prompt Studio**
2. **Online Order Tool**
3. **POS Maintenance Tool**

The implementation will be executed in small sessions by KIMI, Luna, Sonnet, or another coding model. The companion document `QA_SUPPORT_HUB_SESSION_PROMPTS.md` contains the execution prompts.

This plan covers development and integration. Server deployment is outside the scope until the final release session.

---

## 2. Executive Recommendation

Use the current **Angular 22 frontend and .NET 10 backend** as the host platform for all three tools.

Build one deployable application with:

- A shared Angular application shell.
- A unified design system.
- Lazy-loaded tool routes.
- One global theme and motion system.
- Feature-isolated frontend folders.
- Feature-isolated backend modules.
- Shared authentication, configuration, logging, error handling, and audit conventions.
- A secured backend or Windows agent for privileged POS maintenance operations.

Do not keep the tools as independent pages inside iframes. Iframes would make shared styling, navigation, accessibility, state, security, and responsive behavior difficult to maintain.

The attached QA Prompt Studio should be migrated from its single-file HTML/CSS/JavaScript implementation into native Angular components while preserving its useful interaction concepts: dark/light themes, animated cards, form persistence, prompt preview, copy actions, keyboard shortcuts, and the Bug/Test Case generators.

---

## 3. Product Vision

### Product Name

**QA Support Hub**

### Product Goal

Provide QA engineers and support engineers with one secure, consistent workspace for:

- Refining raw bugs into developer-ready reports.
- Refining rough business stories into implementation-ready user stories.
- Generating detailed test cases and QA prompts.
- Monitoring and managing online order requests.
- Running approved POS maintenance operations.
- Moving between tools without leaving the application or learning different interfaces.

### Primary Users

- QA engineers
- QA leads
- Support engineers
- Technical support leads
- Release and operations engineers
- Authorized POS support personnel

---

## 4. Current State

### 4.1 Online Order Tool

The current project already contains:

- Angular frontend.
- .NET backend.
- Existing Online Order functionality.
- Shared application hosting through the backend.
- Existing light/dark behavior and modernized UI work.
- Established tests and build scripts.

This project becomes the foundation of the QA Support Hub.

### 4.2 QA Prompt Studio

The attached Prompt Studio is currently a standalone HTML document containing:

- A landing hub with two animated cards.
- Bug Report Generator.
- Test Case Generator.
- Dark and light themes.
- Three.js background and card effects.
- Local-storage form persistence.
- Character counters.
- Sample-data loaders.
- Copy-to-clipboard actions.
- `Ctrl + Enter` generation shortcut.
- Prompt templates embedded directly in JavaScript.

The migration must preserve the valuable behavior while replacing the single-file structure with maintainable Angular components, services, typed models, reusable templates, and tests.

### 4.3 POS Maintenance Tool

The POS Maintenance Tool is currently a separate project and has not yet been supplied for migration.

The main dashboard can include its card in an explicit **Coming Soon** or **Migration Pending** state until the source project is available.

Because maintenance operations may stop services, read configuration, create backups, execute approved scripts, or access machine resources, the final integration must not rely on browser-only code. It will require one of these patterns after source review:

- A secured backend module operating on the server.
- A secured Windows service/agent installed on approved POS machines.
- An approved remote-execution mechanism with allowlisted operations.

The recommended long-term pattern is an Angular feature plus a .NET orchestration API plus a secured local Windows agent for machine-level operations.

---

## 5. Target User Experience

## 5.1 Main Dashboard

The application root route displays a modern dashboard titled **QA Support Hub**.

The dashboard contains three primary cards:

### QA Prompt Studio

- Status: Available
- Route: `/tools/prompt-studio`
- Purpose: Refine bugs, refine stories, and generate test cases.
- Accent: Purple/blue or red/purple.
- Card action: `Open Prompt Studio`

### Online Order Tool

- Status: Available
- Route: `/tools/online-orders`
- Purpose: Existing online-order monitoring and request operations.
- Accent: Blue/cyan.
- Card action: `Open Online Orders`

### POS Maintenance Tool

- Status: Migration Pending initially.
- Route: `/tools/pos-maintenance`
- Purpose: Approved POS support and maintenance workflows.
- Accent: Amber/orange.
- Card action before migration: `Migration Pending`
- Card action after migration: `Open POS Maintenance`

### Dashboard Behavior

- Cards are fully keyboard accessible.
- Cards support hover/focus animation without reducing text clarity.
- Each card displays status, short description, icon, and action.
- The dashboard supports light and dark modes.
- Animation honors `prefers-reduced-motion`.
- The three cards remain usable on desktop, tablet, and mobile.
- The dashboard must load quickly; heavy animation must not block navigation.

---

## 5.2 Application Shell

The shared shell contains:

- Global brand: QA Support Hub.
- Home/dashboard control.
- Current tool title.
- Breadcrumbs.
- Light/dark theme control.
- Reduced-motion preference.
- Environment indicator when applicable.
- Global notification/toast area.
- Responsive navigation.
- Optional user profile and permissions area when authentication is introduced or extended.

Every tool must use this shell instead of implementing its own top-level header and theme logic.

---

## 5.3 Tool Navigation

Recommended routes:

```text
/
└── Dashboard

/tools/prompt-studio
├── /bugs
├── /stories
└── /test-cases

/tools/online-orders
└── Existing Online Order routes

/tools/pos-maintenance
├── /overview
├── /machine-status
├── /backup
├── /configuration
├── /services
└── Other approved operations after migration analysis
```

All routes should be lazy-loaded.

The browser back button, direct route refresh, bookmarks, and deep links must work.

---

## 6. Unified Frontend Architecture

Recommended structure:

```text
frontend/src/app/
├── app.config.ts
├── app.routes.ts
├── core/
│   ├── config/
│   ├── guards/
│   ├── http/
│   ├── layout/
│   ├── models/
│   ├── services/
│   └── state/
├── shared/
│   ├── components/
│   ├── directives/
│   ├── forms/
│   ├── motion/
│   ├── pipes/
│   ├── styles/
│   └── utilities/
├── design-system/
│   ├── tokens/
│   ├── components/
│   ├── themes/
│   └── motion/
└── features/
    ├── hub/
    ├── prompt-studio/
    │   ├── bug-refiner/
    │   ├── story-refiner/
    │   ├── test-case-generator/
    │   ├── prompt-preview/
    │   ├── prompt-history/
    │   ├── data-access/
    │   └── models/
    ├── online-orders/
    └── pos-maintenance/
```

### Architectural Rules

- Use standalone Angular components unless the existing project standard requires modules.
- Use typed reactive forms.
- Keep prompt templates outside components.
- Keep feature state inside feature services/stores.
- Do not place business logic in templates.
- Do not use global window variables.
- Do not use inline styles for feature implementation.
- Do not use `innerHTML` for dynamic content unless sanitized and justified.
- Do not duplicate shared buttons, cards, modals, inputs, or theme code.
- Do not couple Online Orders to Prompt Studio or POS Maintenance internals.
- Shared code must be generic and live under `shared` or `design-system`.

---

## 7. Unified Backend Architecture

Recommended structure:

```text
backend/src/
├── OnlineOrderTool.Api/
├── OnlineOrderTool.Application/
├── OnlineOrderTool.Domain/
├── OnlineOrderTool.Infrastructure/
└── Features/
    ├── OnlineOrders/
    ├── QaPrompts/
    └── PosMaintenance/
```

Adapt this structure to the actual repository rather than forcing a rewrite.

### Backend Responsibilities

#### Online Orders

Keep current behavior and contracts stable.

#### QA Prompt Studio

MVP prompt generation can remain client-side because it composes deterministic prompt templates.

Add backend APIs only when required for:

- Shared prompt history.
- Team templates.
- Authentication-based preferences.
- Prompt analytics.
- AI-provider integration.
- Centralized audit or retention.

#### POS Maintenance

All privileged operations must run server-side or through an approved agent.

Required controls:

- Authentication and authorization.
- Operation allowlist.
- Target-machine allowlist.
- Input validation.
- Explicit confirmation for destructive actions.
- Structured audit logs.
- Timeout and cancellation handling.
- No arbitrary shell-command field.
- No raw credentials returned to the browser.
- Secrets stored outside source control.
- Idempotent operations where possible.
- Read-only status checks separated from state-changing actions.

---

## 8. Shared Design System

## 8.1 Design Direction

Use a polished glass/dark interface inspired by the attached Prompt Studio, but make it maintainable and performance-aware.

Core visual language:

- Deep neutral background.
- Layered translucent surfaces.
- Soft borders.
- Controlled gradients.
- Clear status colors.
- Rounded cards and panels.
- Strong typography hierarchy.
- High-contrast form states.
- Consistent spacing.
- Motion used to improve orientation, not distract.

### Recommended Tokens

```text
Color
- background/deep
- background/surface
- background/elevated
- border/subtle
- border/interactive
- text/primary
- text/secondary
- text/muted
- accent/primary
- accent/secondary
- success
- warning
- error
- info

Spacing
- 4, 8, 12, 16, 20, 24, 32, 40, 48

Radius
- 8, 12, 16, 24, pill

Motion
- fast: 120–160 ms
- standard: 200–280 ms
- emphasized: 320–420 ms
```

### Shared Components

Create reusable components for:

- App shell.
- Tool card.
- Section card.
- Page header.
- Breadcrumbs.
- Buttons.
- Icon buttons.
- Inputs.
- Selects.
- Text areas.
- Chips.
- Status badges.
- Empty states.
- Loading skeletons.
- Toasts.
- Confirmation dialogs.
- Split input/output workspace.
- Code/prompt preview.
- Copy button.
- Theme toggle.
- Motion preference toggle.

---

## 8.2 Animation Rules

The attached Prompt Studio uses continuous Three.js animation, 3D card tilt, particles, and animated grids. Preserve the modern visual feel, but apply these constraints:

- Heavy Three.js animation is allowed on the main hub only.
- Tool workspaces should use lightweight CSS transitions.
- Pause animation when the page is hidden.
- Disable or simplify animation for `prefers-reduced-motion`.
- Avoid pointer-tracking work on every element.
- Avoid animation that blurs text.
- Keep card tilt subtle.
- Cap canvas pixel ratio.
- Lazy-load Three.js only for the hub.
- Provide a non-WebGL fallback.
- Do not make animation a dependency for navigation or usability.

---

## 9. QA Prompt Studio Functional Plan

## 9.1 Prompt Studio Landing

Display three internal cards:

1. Bug Refinement
2. Story Refinement
3. Test Case Generation

Optional future cards:

- Test Scenario Pack
- API Test Prompt
- SQL Validation Prompt
- Release Checklist Prompt

Do not implement future cards in the first scope unless they are marked as disabled placeholders.

---

## 9.2 Bug Refinement Tool

### Inputs

- Raw bug notes.
- Existing title.
- Module/feature.
- Environment.
- Application/build version.
- Preconditions.
- Test data.
- Steps to reproduce.
- Expected result.
- Actual result.
- Frequency/reproducibility.
- Severity.
- Priority.
- Business/user impact.
- Error message.
- Logs.
- Attachments.
- Related ticket/reference.
- Output detail level:
  - Concise
  - Standard
  - Deep
- Target format:
  - Generic Markdown
  - Jira-friendly
  - Azure DevOps-friendly

### Output Prompt Requirements

The generated prompt must instruct the target AI to:

- Preserve confirmed facts.
- Separate facts from inference.
- Never invent missing evidence.
- Mark unresolved fields as `[NEEDS INVESTIGATION]`.
- Produce atomic reproduction steps.
- Produce observable expected and actual results.
- Detect contradictions.
- Suggest severity and priority only when not provided.
- Explain suggested values separately.
- Identify likely missing diagnostics.
- Identify possible regression scope without claiming root cause.
- Produce developer-ready and tester-ready output.
- Preserve attachment names.
- Avoid unsupported root-cause claims.
- Optionally produce acceptance criteria for the fix.
- Optionally produce a retest checklist.

### Generated Bug Output Sections

```text
Bug Title
Environment
Preconditions
Test Data
Steps to Reproduce
Expected Result
Actual Result
Frequency
Severity
Priority
Impact
Evidence
Missing Information
Fix Acceptance Criteria
Retest Checklist
```

The final template should be configurable so the user can disable optional sections.

---

## 9.3 Story Refinement Tool

### Inputs

- Raw story/request.
- Proposed title.
- Persona/actor.
- Business goal.
- Problem statement.
- Current behavior.
- Desired behavior.
- Scope.
- Out of scope.
- Business rules.
- Dependencies.
- Assumptions.
- UX references.
- API/data considerations.
- Security considerations.
- Performance considerations.
- Localization/accessibility considerations.
- Acceptance-criteria style:
  - Checklist
  - Given/When/Then
  - Both
- Output detail level:
  - Concise
  - Standard
  - Deep
- Target format:
  - Generic Markdown
  - Jira-friendly
  - Azure DevOps-friendly

### Output Prompt Requirements

The generated prompt must instruct the target AI to produce:

- Refined story title.
- User story statement.
- Business context.
- Functional scope.
- Explicit out-of-scope section.
- Business rules.
- Assumptions and dependencies.
- Acceptance criteria.
- Negative and boundary acceptance criteria.
- Validation and error behavior.
- Permissions and security expectations.
- Data integrity expectations.
- Performance expectations when relevant.
- Accessibility and localization expectations when relevant.
- Analytics/observability expectations when relevant.
- Open questions.
- QA impact.
- Suggested test coverage categories.
- Definition of Ready checklist.

The prompt must not silently convert uncertain assumptions into requirements.

---

## 9.4 Test Case Generator

Retain the current Test Case Generator and improve it with:

- Scenario category.
- Priority.
- Preconditions.
- Test data.
- Atomic steps.
- Expected result per step or final result mode.
- Postconditions.
- Cleanup.
- Environment.
- Requirement/story reference.
- Attachments.
- Automation candidacy.
- Regression-suite tag.
- Positive, negative, boundary, UI, accessibility, security, data-integrity, performance, localization, and recovery categories.
- Output formats:
  - Single test case
  - Scenario matrix
  - Jira import-friendly text
  - Spreadsheet-friendly table
- Fact/inference indicators.
- Missing-information warnings.
- Duplicate-step and vague-result checks.

---

## 9.5 Prompt Quality Engine

The Prompt Studio should include a deterministic quality layer before generating the final prompt.

### Quality Checks

- Required fields missing.
- Contradictory expected/actual result.
- Steps too vague.
- No observable result.
- Missing environment.
- Missing test data.
- Missing evidence.
- Severity/priority inconsistency.
- Story without business goal.
- Acceptance criteria not testable.
- Ambiguous actors.
- Undefined scope.
- Unbounded words such as “properly,” “correctly,” or “as expected.”
- Potentially sensitive content.

### UI Output

Display:

- Completeness score.
- Quality warnings.
- Missing-information list.
- Facts detected.
- User-provided assumptions.
- Fields the target AI is allowed to infer.
- Generated prompt preview.

The score is guidance only and must not block prompt generation.

---

## 9.6 Prompt Template Architecture

Move prompt text out of components.

Recommended model:

```typescript
interface PromptTemplate<TInput> {
  id: string;
  name: string;
  version: string;
  description: string;
  build(input: TInput, options: PromptOptions): string;
}
```

Recommended services:

- `BugPromptBuilder`
- `StoryPromptBuilder`
- `TestCasePromptBuilder`
- `PromptQualityService`
- `PromptStorageService`
- `ClipboardService`
- `PromptExportService`

Requirements:

- Deterministic output.
- Unit-testable builders.
- Template version visible in generated prompt metadata when enabled.
- No hard dependency on a specific AI provider.
- User input escaped safely.
- No HTML execution from prompt content.

---

## 9.7 Prompt History and Preferences

MVP:

- Store recent prompts locally.
- Save user theme.
- Save reduced-motion preference.
- Save last selected output format.
- Allow clear-history.
- Allow copy and download as `.md` or `.txt`.

Future backend phase:

- Team templates.
- Shared prompt library.
- User accounts.
- Role-based templates.
- Server-side history retention.
- Prompt usage analytics.

Do not store sensitive ticket content server-side without an approved retention policy.

---

## 10. Online Order Tool Integration

The existing Online Order Tool must become a feature inside the shared shell without changing its proven business behavior.

### Required Work

- Move or map existing routes under `/tools/online-orders`.
- Preserve direct links through route redirects when necessary.
- Wrap pages in the shared shell.
- Replace local duplicate styles with design-system components gradually.
- Preserve current filters, paging, status behavior, assets, APIs, and performance.
- Keep the Online Order feature independently testable.
- Do not mix Prompt Studio state with Online Order state.
- Add a dashboard card that routes to the Online Order landing page.

### Compatibility

Provide redirects for legacy routes where practical so existing bookmarks continue to work.

---

## 11. POS Maintenance Migration Strategy

## 11.1 Before Source Arrival

Implement:

- Dashboard card.
- `/tools/pos-maintenance` placeholder page.
- Migration status.
- Capability summary.
- Disabled action state.
- No fake maintenance actions.
- No guessed API contracts.

Create a migration intake checklist requesting:

- Source repository.
- Technology stack.
- Current operations.
- Required permissions.
- Local/remote execution model.
- Configuration files.
- Secret handling.
- Machine targets.
- SQL use.
- Backup/restore behavior.
- Service-control behavior.
- Logging.
- Existing tests.
- Packaging/deployment model.

## 11.2 Discovery After Source Arrival

Classify every operation:

```text
Read-only
Reversible
State-changing
Destructive
Machine-local
Remote
Database-related
Service-related
File-related
```

Decide for each operation whether it belongs in:

- Angular UI.
- Main .NET backend.
- Windows maintenance agent.
- Existing external tool retained temporarily.

## 11.3 Recommended Target Architecture

```text
Browser
  → QA Support Hub Angular UI
    → QA Support Hub .NET API
      → Authorized POS Maintenance Agent
        → Allowlisted machine operation
```

### Agent Requirements

- Windows service.
- Mutual authentication or signed requests.
- Machine allowlist.
- Operation allowlist.
- No arbitrary command execution.
- Structured result contract.
- Timeout.
- Cancellation where possible.
- Health endpoint.
- Version endpoint.
- Audit correlation ID.
- Least-privilege service account.
- Signed and versioned deployment.
- Safe upgrade and rollback.

### POS UI Requirements

- Target selection.
- Live connection status.
- Read-only overview.
- Operation risk label.
- Confirmation for state-changing actions.
- Typed required inputs.
- Progress state.
- Result summary.
- Sanitized logs.
- Retry only when safe.
- Audit reference.
- Clear separation between testing and production targets.

---

## 12. Security Requirements

### Global

- No credentials in source control.
- No secrets in browser storage.
- No raw connection strings returned to the client.
- No arbitrary URL or command execution.
- Validate all inputs.
- Encode all rendered user text.
- Use a strict Content Security Policy where possible.
- Remove CDN runtime dependencies from production or pin and self-host approved assets.
- Protect state-changing endpoints with authentication and authorization.
- Use anti-forgery protections where applicable.
- Add correlation IDs.
- Sanitize logs.
- Rate-limit sensitive operations.
- Record privileged actions.

### Prompt Studio

- Treat entered bug/story content as potentially sensitive.
- Local history must be clearable.
- Display a sensitive-data warning.
- Do not send content to an AI provider without explicit user action and provider configuration.
- Do not claim AI analysis when only deterministic prompt generation occurs.

### POS Maintenance

- Require role-based access.
- Require confirmation for state-changing actions.
- Audit actor, target, operation, start time, completion time, result, and correlation ID.
- Never expose raw passwords.
- Never provide an arbitrary PowerShell or SQL text box in the browser.

---

## 13. Accessibility Requirements

- Keyboard navigation for every card and control.
- Visible focus indicators.
- Semantic headings.
- Form labels and descriptions.
- Screen-reader status announcements.
- Sufficient color contrast.
- No meaning conveyed by color alone.
- Reduced-motion support.
- Accessible dialogs.
- Error summary and inline errors.
- Responsive zoom support.
- No forced `user-scalable=no` in the migrated Angular application.
- Minimum touch-target sizing.
- Tooltips not required to understand core actions.

---

## 14. Performance Requirements

- Lazy-load all tool features.
- Lazy-load Three.js only on the hub.
- Avoid global continuous animations in tool pages.
- Keep initial application shell lightweight.
- Reuse existing Online Order performance improvements.
- Use route-level code splitting.
- Avoid importing POS dependencies before the route is opened.
- Pause background animation when document visibility changes.
- Cache static prompt templates.
- Use debounced local persistence.
- Prevent repeated API calls on navigation.
- Define bundle budgets for the shell and each feature.

Suggested targets:

```text
Dashboard interactive on internal network: under 2 seconds
Route navigation after load: under 500 ms
Prompt generation: under 100 ms
No noticeable typing latency
No continuous high CPU usage when idle
```

---

## 15. Testing and Validation Strategy

To control model quota and execution time:

### Every Session

Run only targeted validation for changed files and affected features.

Examples:

- Type check.
- Focused unit tests.
- Focused lint.
- Focused build.
- Direct route smoke check.

### Milestone Sessions

Run:

- Full backend tests.
- Full frontend tests.
- Production build.
- Existing repository build wrapper.
- Routing checks.
- Security and secret scan.

### Final Integration

Run:

- Full regression.
- Responsive checks.
- Accessibility checks.
- Cross-tool navigation.
- Legacy route compatibility.
- Production artifact verification.

Do not delete existing tests merely to reduce quota. Avoid regenerating large test suites after every session.

---

## 16. Delivery Phases

## Phase A — Foundation

### Deliverables

- Product rename to QA Support Hub.
- Shared app shell.
- Unified route structure.
- Design tokens.
- Shared theme.
- Shared motion preferences.
- Hub route skeleton.
- Legacy route compatibility plan.

### Exit Criteria

- Existing Online Order pages still work.
- Theme works globally.
- Direct routes still refresh.
- No business behavior changed.

---

## Phase B — Dashboard Hub

### Deliverables

- Three tool cards.
- Responsive grid.
- Card status model.
- Modern animation.
- Keyboard support.
- POS migration-pending state.
- Tool metadata configuration.

### Exit Criteria

- Each available card navigates correctly.
- Disabled card cannot trigger fake operations.
- Reduced motion works.
- Hub is responsive.

---

## Phase C — Prompt Studio Migration

### Deliverables

- Angular Prompt Studio shell.
- Bug Refiner.
- Story Refiner.
- Test Case Generator.
- Prompt preview.
- Copy/download.
- Local history.
- Quality checks.
- Template builders.
- Unit tests.

### Exit Criteria

- Existing useful prompt behavior is preserved.
- Story refinement is available.
- Prompt quality warnings work.
- Prompt output is deterministic.
- No embedded monolithic script remains.

---

## Phase D — Online Order Integration

### Deliverables

- Online Orders under shared route.
- Shared shell and breadcrumbs.
- Shared styles where safe.
- Legacy redirects.
- No regression to filters, paging, statuses, or actions.

### Exit Criteria

- Existing business workflows pass.
- Route refresh works.
- Dashboard navigation works.
- Performance remains acceptable.

---

## Phase E — POS Migration Preparation

### Deliverables

- Placeholder feature.
- Migration intake checklist.
- Security classification template.
- Agent/API contract draft.
- UI route and shared components ready.

### Exit Criteria

- No guessed POS behavior.
- Source-project dependencies documented.
- Migration can begin immediately after source arrival.

---

## Phase F — POS Migration

Begins only after the source project is supplied.

### Deliverables

- Source assessment.
- Operation inventory.
- Target architecture decision.
- Shared UI migration.
- Backend APIs.
- Optional Windows agent.
- Authorization.
- Audit.
- Safe confirmations.
- Targeted tests.
- Migration parity report.

### Exit Criteria

- Approved operations match current tool behavior.
- Destructive operations are protected.
- No arbitrary execution path exists.
- Rollback strategy is documented.
- Standalone project can be retired only after acceptance.

---

## Phase G — Integration Hardening

### Deliverables

- Accessibility pass.
- Responsive pass.
- Performance pass.
- Security review.
- Cross-tool regression.
- Documentation.
- Release artifact.
- Deployment plan.

### Exit Criteria

- All three tool states are accurate.
- Available tools are healthy.
- Pending tools are clearly identified.
- Shared styles are consistent.
- Full build passes.
- No secrets or generated artifacts are committed.

---

## 17. Data and State Decisions

### MVP

Use browser storage only for:

- Theme.
- Motion preference.
- Prompt form drafts.
- Prompt history when enabled.
- Last selected tool options.

Use namespaced keys:

```text
qa-support-hub.theme
qa-support-hub.motion
qa-support-hub.prompt-studio.drafts
qa-support-hub.prompt-studio.history
```

Provide a clear-history action.

### Future

Introduce backend persistence only with:

- User identity.
- Retention rules.
- Sensitive-data classification.
- Deletion behavior.
- Audit expectations.

---

## 18. Configuration-Driven Tool Registry

Implement a typed registry instead of hard-coding cards repeatedly.

Example:

```typescript
export interface QaToolDefinition {
  id: 'prompt-studio' | 'online-orders' | 'pos-maintenance';
  title: string;
  description: string;
  route: string;
  icon: string;
  status: 'available' | 'migration-pending' | 'maintenance' | 'disabled';
  accent: string;
  capabilities: string[];
}
```

Benefits:

- One source of truth for dashboard cards.
- Status can be changed without rewriting the layout.
- Easier permission filtering later.
- Easier addition of future QA tools.

---

## 19. Migration Rules

- Preserve existing Online Order behavior.
- Rebuild the Prompt Studio natively in Angular.
- Do not paste the entire standalone HTML into an Angular template.
- Do not retain global scripts.
- Do not use iframes for final integration.
- Do not migrate POS behavior until its source and permissions are reviewed.
- Keep each feature independently removable and testable.
- Keep commits scoped by session.
- Update current-state documentation after each session.
- Do not deploy to the server during feature sessions.
- Do not perform Production data changes during development.

---

## 20. Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Monolithic merge creates tight coupling | High | Feature routes, typed contracts, shared-only generic code |
| Three.js increases bundle and CPU usage | Medium | Lazy-load on hub, reduced-motion fallback, pause when hidden |
| Prompt output becomes verbose but not useful | High | Output modes, quality rules, deterministic templates, examples |
| AI prompt builders invent missing facts | High | Fact/inference separation and explicit missing-data markers |
| POS operations exposed through browser unsafely | Critical | Backend/agent architecture, allowlists, RBAC, audit |
| Online Order behavior regresses during restyling | High | Integrate shell first, restyle incrementally, preserve contracts |
| POS source arrives late | Medium | Placeholder and migration contract now; execution sessions deferred |
| Shared styles break existing pages | Medium | Design tokens and wrapper components before broad replacement |
| Lower-capability models modify too much per session | High | Small session prompts, explicit scope, targeted validation |
| Documentation drifts | Medium | Update state and session completion markers every session |

---

## 21. Definition of Done

The unified QA Support Hub is complete when:

- The root page is a modern, animated, accessible card dashboard.
- QA Prompt Studio opens from its card.
- Online Order Tool opens from its card.
- POS Maintenance opens from its card after migration, or is clearly marked pending beforehand.
- All tools use the same shell, theme, typography, controls, spacing, and motion rules.
- Bug prompts are more complete and developer-ready.
- Story prompts generate testable, implementation-ready stories.
- Test Case prompts remain available and are enhanced.
- Prompt quality warnings identify missing or weak inputs.
- Online Order behavior is preserved.
- POS privileged operations are secured and audited.
- Desktop, tablet, and mobile layouts work.
- Reduced-motion mode works.
- Full validation and production build pass.
- No secret, artifact, backup, or standalone source dump is committed.
- Project documentation accurately reflects the final structure.

---

## 22. Recommended Implementation Order

1. Baseline and architecture.
2. Shared shell and route structure.
3. Design system and motion.
4. Main dashboard cards.
5. Prompt Studio Angular migration.
6. Bug Refiner enhancement.
7. Story Refiner implementation.
8. Test Case Generator enhancement.
9. Prompt quality/history/export.
10. Online Order shell integration.
11. POS placeholder and migration contract.
12. POS source assessment.
13. POS backend/agent migration.
14. Accessibility, performance, and security hardening.
15. Final regression and release preparation.

The POS implementation sessions must remain deferred until the standalone source project is available.

---

## 23. Repository Map (Session 00 Baseline)

Verified against the repository on 2026-08-06 at commit `eaeb43e`. The Session 00 architecture decisions are recorded in `.ai/decisions/ADR-0011-qa-support-hub-baseline.md`.

### 23.1 Frontend Anchors

- Routes: `frontend/src/app/app.routes.ts` — all lazy `loadComponent` routes. `''` renders `features/landing`; `modules/:key` renders `features/module-shell` with `order`, `unicommerce`, and `order-requests` children (the last guarded by `core/guards/capability.guard.ts`); legacy `requests*` redirects are preserved; `_kitchen-sink` is development-only.
- Application root: `frontend/src/app/app.ts` — `RouterOutlet` plus global toast only; no global chrome.
- Shell: `frontend/src/app/features/module-shell/` composing `layout/navbar`, `layout/sidebar`, and `layout/breadcrumb`; sidebar offset via `core/services/sidebar-state.service.ts`.
- Theme: `frontend/src/app/core/services/theme.service.ts` writes `data-theme` on `<html>` and persists to `localStorage` (default dark). Tokens live in `frontend/src/styles/_tokens.css` with `_gradients.css`, `_typography.css`, and `_animations.css` partials imported by `frontend/src/styles.css`.
- Shared UI: `frontend/src/app/shared/ui/` design-system primitives (button, input, select, card, table, dialogs, status pill, copy button, skeletons, etc.), plus `shared/components/` (toast, status badge, JSON viewer) and `shared/directives/`.
- Existing Online Order features: `features/flat-order/`, `features/order-requests/`, `features/unicommerce/`, each with co-located components and stores.

### 23.2 Backend Anchors

- `backend/src/OnlineOrderTool.Core/` — `Modules/` (`IOrderModule`, `ModuleCapabilities`, `ModuleRegistry`, GHC/UPC/Uni-Commerce implementations), `Services/` (payload builders, validators, totals, drafts, API client), `Models/`, `DTOs/`, and repository interfaces.
- `backend/src/OnlineOrderTool.Data/` — Dapper repositories and `SqlServerConnectionFactory`.
- `backend/src/OnlineOrderTool.Api/` — composition root (`Program.cs`), capability guard, middleware, and controllers (Lookup, Module, Order, OrderRequests, Payment, Product).
- Tests: `backend/tests/OnlineOrderTool.Tests/` (xUnit, including contract tests mirroring `docs/request_examples/`) and co-located frontend `*.spec.ts` files (Vitest via `ng test`).
- Build/test wrappers: `scripts/build.ps1` (backend tests, Release build, Angular production build) and `scripts/dev.ps1` (API on :5200, `ng serve` on :4200 with `/api` proxy).

### 23.3 Future Feature Placement

| Feature | Frontend home | Backend home |
|---|---|---|
| Hub dashboard | `frontend/src/app/features/hub/` (Session 03) | None for MVP |
| QA Prompt Studio | `frontend/src/app/features/prompt-studio/` (Sessions 04-08) | Client-side MVP; a backend module only when shared history, team templates, or AI integration require it |
| Online Order Tool | Existing `features/flat-order`, `features/order-requests`, `features/unicommerce`, re-parented under `/tools/online-orders` (Session 09) | Unchanged: Core `Modules/` and existing controllers |
| POS Maintenance | `frontend/src/app/features/pos-maintenance/` placeholder (Session 10) | Deferred: secured module and/or Windows agent after source review (Sessions 11-13) |
| Shared shell/theme/motion | `frontend/src/app/core/layout` evolution of `module-shell` plus `shared/` and `design-system/` additions (Sessions 01-02) | N/A |

### 23.4 Migration Source Confirmation

`prompt_generator/index.html` is present in the repository as migration reference only — it is never served at runtime and is never embedded through an iframe. Confirmed contents:

- Bug Report generator (`generateBugPrompt`).
- Test Case generator (`generateTestCasePrompt`, sample loader, clear form).
- Theme persistence (`localStorage['studio_theme']`, light-theme overrides).
- Per-field `localStorage` form persistence (`studio_*` keys).
- Three.js effects (`initThree`, particle background, per-card renderers).
- Copy-to-clipboard actions (`navigator.clipboard.writeText` with toast feedback).
- `Ctrl/Cmd + Enter` generation keyboard shortcut.
