# QA Support Hub — Session Execution Prompts

## How to Use This File

Execute one session at a time with KIMI, Luna, Sonnet, or another coding model.

Before every session:

1. Open the repository root in VS Code.
2. Give the model the full prompt for the selected session.
3. Let the session finish completely.
4. Review the diff before starting the next session.
5. Do not combine multiple sessions unless the model has enough context and quota.
6. Keep the POS implementation sessions deferred until the standalone POS source project is available.

The implementation plan is:

`QA_SUPPORT_HUB_IMPLEMENTATION_PLAN.md`

---

# Shared Execution Contract

Include this contract at the beginning of every session when the model does not reliably retain repository instructions.

```text
# Execution Contract

You are implementing one scoped session in the existing QA Support Hub repository.

Before changing code:

1. Read `AGENTS.md` completely.
2. Read `QA_SUPPORT_HUB_IMPLEMENTATION_PLAN.md`.
3. Read `QA_SUPPORT_HUB_SESSION_PROMPTS.md` only for the current session.
4. Read `TASK.md`, `.ai/STATE.md`, and `.ai/HANDOFF.md` when they exist.
5. Inspect the current Git branch, status, diff, and recent commits.
6. Inspect the actual code related to this session.
7. Preserve unrelated user work.
8. Do not reset, discard, or overwrite changes you do not own.

Execution rules:

- Execute the session through completion; do not stop after planning.
- Keep the change limited to the current session.
- Follow the existing Angular and .NET architecture.
- Preserve existing Online Order behavior unless this session explicitly changes it.
- Do not deploy to IIS or any server.
- Do not run Production SQL or state-changing Production operations.
- Do not add credentials, connection strings, secrets, private addresses, or customer data.
- Do not commit generated artifacts, `dist`, `node_modules`, backups, logs, or screenshots.
- Do not introduce an iframe as the final integration mechanism.
- Do not add arbitrary command, PowerShell, or SQL execution fields.
- Use typed models and deterministic behavior.
- Maintain accessibility and reduced-motion support.
- Use targeted validation for the current change.
- Run the full repository suite only when the session explicitly requests it or when a change affects application-wide routing/build behavior.
- Review the final diff.
- Update project state documentation only when materially required.
- Commit with the requested commit message when all validation passes.
- Push only when repository policy and the user’s current workflow allow it.
- Return a concise execution report with: Result, Changes, Validation, Commit, Remaining.
```

---

# Session 00 — Repository Baseline and Architecture Decision

## Objective

Establish the QA Support Hub implementation baseline without changing application behavior.

## Prompt

```text
# Session 00 — Baseline and Architecture

Execute the shared execution contract.

Goal:
Prepare the repository for the QA Support Hub implementation and record the architecture decisions.

Tasks:

1. Inspect the current frontend and backend structure.
2. Locate:
   - Angular app routes
   - Current application shell/layout
   - Theme implementation
   - Shared components
   - Online Order feature routes
   - Existing backend feature boundaries
   - Existing build/test scripts
3. Locate the attached or copied QA Prompt Studio source if it has been added to the repository.
4. Confirm the Prompt Studio currently contains:
   - Bug generator
   - Test Case generator
   - Theme persistence
   - localStorage persistence
   - Three.js effects
   - Copy actions
   - Keyboard shortcut
5. Do not migrate the HTML yet.
6. Create or update:
   - `QA_SUPPORT_HUB_IMPLEMENTATION_PLAN.md`
   - `TASK.md`
   - `.ai/STATE.md`
   - `.ai/HANDOFF.md` only if the project uses them
7. Record these decisions:
   - Current Angular + .NET project is the host.
   - Main product name is QA Support Hub.
   - Routes will be feature-based and lazy-loaded.
   - Prompt Studio will be rebuilt natively in Angular.
   - Online Orders behavior must be preserved.
   - POS Maintenance remains migration-pending until source arrival.
   - No iframe will be used as the final integration.
8. Produce a concise repository map showing where each future feature belongs.
9. Do not make broad code or UI changes.

Validation:
- Documentation links are valid.
- No generated artifacts are added.
- `git diff --check` passes.
- Working tree contains only expected documentation/state changes.

Commit message:
`docs(architecture): define QA Support Hub integration plan`
```

## Completion Gate

- Architecture is documented.
- Active task is clear.
- No application behavior changed.

---

# Session 01 — Shared Route Skeleton and Product Rename

## Objective

Introduce the QA Support Hub route structure and product identity while preserving existing routes.

## Prompt

```text
# Session 01 — Shared Route Skeleton and Product Rename

Execute the shared execution contract.

Goal:
Create the route skeleton for QA Support Hub without migrating feature internals yet.

Tasks:

1. Inspect the existing Angular route configuration and root layout.
2. Introduce or confirm these routes:
   - `/` → QA Support Hub dashboard placeholder
   - `/tools/prompt-studio`
   - `/tools/online-orders`
   - `/tools/pos-maintenance`
3. Lazy-load each tool route.
4. Preserve existing Online Order URLs using redirects or compatibility routes.
5. Rename visible top-level product branding from Online Order Tool to QA Support Hub where it represents the whole application.
6. Do not rename Online Order feature-specific headings that should remain Online Orders.
7. Add typed route metadata for:
   - tool title
   - breadcrumb
   - status
   - accent
8. Add placeholder pages for Prompt Studio and POS Maintenance.
9. The POS page must clearly show `Migration Pending`; it must not contain fake actions.
10. Ensure direct route refresh continues to work with the existing backend SPA fallback.
11. Do not restyle the entire application in this session.

Validation:
- Targeted Angular route tests.
- Angular type check/build.
- Direct navigation to all three routes.
- Existing Online Order route compatibility.
- No backend contract changes.

Commit message:
`feat(shell): add QA Support Hub route structure`
```

## Completion Gate

- All tool routes resolve.
- Existing Online Order navigation still works.
- POS is clearly pending.

---

# Session 02 — Unified Design Tokens, Theme, and Motion Foundation

## Objective

Create the shared visual system used by every tool.

## Prompt

```text
# Session 02 — Design System Foundation

Execute the shared execution contract.

Goal:
Build a shared design-token, theme, and motion foundation without broadly rewriting feature pages.

Tasks:

1. Inspect current global styles and theme services.
2. Create a maintainable design-system structure for:
   - colors
   - typography
   - spacing
   - radius
   - shadows
   - z-index
   - motion durations/easing
3. Use the Prompt Studio visual direction:
   - deep neutral surfaces
   - subtle glass panels
   - controlled gradients
   - clear status colors
   - modern cards
4. Preserve the existing official assets and established app branding rules.
5. Implement one global light/dark theme service.
6. Persist the theme under a namespaced storage key.
7. Add reduced-motion preference:
   - honor `prefers-reduced-motion`
   - optional user override
8. Create shared primitives only where needed:
   - button
   - icon button
   - status badge
   - tool card base
   - page header
   - empty state
   - toast
9. Do not migrate the Prompt Studio form yet.
10. Do not bulk-replace all existing Online Order styles.
11. Remove duplicated new styles created in this session.
12. Ensure text remains sharp during hover animations.

Validation:
- Targeted component tests.
- Theme persistence check.
- Reduced-motion check.
- Light/dark visual check.
- Angular production build.

Commit message:
`feat(design-system): add shared themes and motion tokens`
```

## Completion Gate

- One global theme exists.
- Shared cards and controls are available.
- Reduced motion works.

---

# Session 03 — QA Support Hub Dashboard Cards

## Objective

Build the main external dashboard with three selectable tool cards.

## Prompt

```text
# Session 03 — QA Support Hub Dashboard

Execute the shared execution contract.

Goal:
Implement the root QA Support Hub dashboard with three modern selectable cards.

Cards:

1. QA Prompt Studio
   - route: `/tools/prompt-studio`
   - status: Available
   - action: Open Prompt Studio

2. Online Order Tool
   - route: `/tools/online-orders`
   - status: Available
   - action: Open Online Orders

3. POS Maintenance Tool
   - route: `/tools/pos-maintenance`
   - status: Migration Pending
   - action: View Migration Status or disabled based on the architecture plan

Tasks:

1. Create a typed tool registry as the single source of card metadata.
2. Build the responsive dashboard using the shared Tool Card component.
3. Add:
   - title
   - description
   - icon
   - status badge
   - capability summary
   - action
4. Make cards keyboard accessible.
5. Add subtle entrance, hover, focus, and selection animations.
6. Honor reduced-motion mode.
7. Lazy-load Three.js only if the project can support it without affecting the main bundle.
8. When Three.js is used:
   - hub only
   - pause when page hidden
   - cap pixel ratio
   - provide non-WebGL fallback
9. Do not add continuous heavy animation to tool pages.
10. Add empty/error fallback when tool metadata cannot load.
11. Add targeted tests for navigation and card status.

Validation:
- Desktop, tablet, and mobile layouts.
- Keyboard navigation.
- Reduced-motion behavior.
- Card routing.
- POS card cannot invoke non-existent operations.
- Angular production build.

Commit message:
`feat(hub): add animated QA tool dashboard`
```

## Completion Gate

- The root dashboard is complete.
- All cards represent the correct availability state.
- Available cards navigate correctly.

---

# Session 04 — Prompt Studio Angular Migration Foundation

## Objective

Migrate the standalone Prompt Studio structure into native Angular without enhancing prompt logic yet.

## Prompt

```text
# Session 04 — Prompt Studio Angular Foundation

Execute the shared execution contract.

Goal:
Rebuild the attached standalone QA Prompt Studio as a maintainable Angular feature.

Important:
Do not paste the full HTML document into one Angular template.
Do not retain global window variables or global inline scripts.

Tasks:

1. Analyze the attached Prompt Studio source.
2. Preserve these behaviors:
   - Prompt Studio landing
   - Bug generator entry
   - Test Case generator entry
   - back-to-studio navigation
   - light/dark theme integration
   - form draft persistence
   - character counters
   - sample-data loading
   - prompt preview
   - copy action
   - `Ctrl/Cmd + Enter`
3. Create feature routes:
   - `/tools/prompt-studio`
   - `/tools/prompt-studio/bugs`
   - `/tools/prompt-studio/stories`
   - `/tools/prompt-studio/test-cases`
4. Add three internal Prompt Studio cards:
   - Bug Refinement
   - Story Refinement
   - Test Case Generation
5. Create typed models and reactive forms.
6. Create a feature-level storage service using namespaced keys.
7. Create shared Prompt Studio components:
   - generator workspace
   - input panel
   - output preview
   - copy/download actions
   - quality summary placeholder
8. Create placeholder typed builders:
   - BugPromptBuilder
   - StoryPromptBuilder
   - TestCasePromptBuilder
9. Do not implement the final enhanced templates yet.
10. Use the global shell and theme; remove standalone header/theme duplication.
11. Do not depend on CDN fonts, icons, or Three.js at runtime without repository-approved packaging.

Validation:
- Feature route tests.
- Form persistence tests.
- Keyboard-shortcut test.
- Copy action test where supported.
- Angular type check/build.

Commit message:
`feat(prompt-studio): migrate generator shell to Angular`
```

## Completion Gate

- Prompt Studio is native Angular.
- Three internal generator routes exist.
- Existing interaction basics are preserved.

---

# Session 05 — Enhanced Bug Refinement Generator

## Objective

Implement the production-ready Bug Refiner and deterministic prompt builder.

## Prompt

```text
# Session 05 — Bug Refinement Generator

Execute the shared execution contract.

Goal:
Implement an enhanced Bug Refinement tool that produces highly productive prompts for developer-ready bug reports.

Inputs:

- Raw bug notes
- Existing title
- Module/feature
- Environment
- Build/version
- Preconditions
- Test data
- Steps
- Expected result
- Actual result
- Frequency/reproducibility
- Severity
- Priority
- Business/user impact
- Error message
- Logs
- Attachments
- Related reference
- Output detail: Concise / Standard / Deep
- Target format: Generic / Jira / Azure DevOps

Tasks:

1. Build a typed reactive form.
2. Implement deterministic `BugPromptBuilder`.
3. Keep confirmed user facts unchanged.
4. Explicitly separate:
   - provided facts
   - requested inference
   - missing information
5. Instruct the target AI to use `[NEEDS INVESTIGATION]` for unsupported missing facts.
6. Add optional output sections:
   - missing information
   - fix acceptance criteria
   - retest checklist
   - regression scope
7. Do not ask the target AI to claim a root cause without evidence.
8. Add output modes:
   - concise
   - standard
   - deep
9. Add Jira-friendly and Azure DevOps-friendly variants without coupling to their APIs.
10. Add sample inputs based on generic QA examples only.
11. Add copy and download as `.md` and `.txt`.
12. Add unit tests for prompt output, escaping, missing fields, and deterministic ordering.
13. Add a comparison fixture showing the old prompt and new prompt quality without retaining the old implementation in production code.

Validation:
- Builder unit tests.
- Form validation tests.
- Prompt preview check.
- Copy/download check.
- Targeted Angular build.

Commit message:
`feat(prompt-studio): add enhanced bug refinement prompts`
```

## Completion Gate

- Bug prompts are deterministic and richer.
- Facts are protected.
- Missing information is explicit.

---

# Session 06 — Story Refinement Generator

## Objective

Add a new Story Refiner for implementation-ready user stories.

## Prompt

```text
# Session 06 — Story Refinement Generator

Execute the shared execution contract.

Goal:
Implement the Story Refinement tool as a first-class Prompt Studio feature.

Inputs:

- Raw request/story
- Proposed title
- Persona/actor
- Business goal
- Problem statement
- Current behavior
- Desired behavior
- Scope
- Out of scope
- Business rules
- Dependencies
- Assumptions
- UX references
- API/data considerations
- Security considerations
- Performance considerations
- Accessibility/localization considerations
- Acceptance criteria style: Checklist / Given-When-Then / Both
- Output detail: Concise / Standard / Deep
- Target format: Generic / Jira / Azure DevOps

Required generated-prompt sections:

- Refined title
- User story statement
- Business context
- Functional scope
- Out of scope
- Business rules
- Assumptions
- Dependencies
- Acceptance criteria
- Negative and boundary criteria
- Validation/error behavior
- Permissions/security
- Data integrity
- Performance where relevant
- Accessibility/localization where relevant
- Observability/analytics where relevant
- Open questions
- QA impact
- Suggested test coverage
- Definition of Ready

Tasks:

1. Implement typed models and reactive form.
2. Implement deterministic `StoryPromptBuilder`.
3. Do not silently convert assumptions into requirements.
4. Mark unsupported details as open questions.
5. Ensure acceptance criteria are observable and testable.
6. Add template variants for the three acceptance-criteria styles.
7. Add sample data.
8. Add copy and download actions.
9. Add targeted unit tests.
10. Add route/card status as Available.

Validation:
- Builder tests.
- Given/When/Then formatting tests.
- Missing-input behavior.
- No mutation of provided text.
- Targeted Angular build.

Commit message:
`feat(prompt-studio): add user story refinement generator`
```

## Completion Gate

- Story Refiner is available from the Prompt Studio.
- Generated prompts produce testable stories.
- Assumptions and open questions are explicit.

---

# Session 07 — Enhanced Test Case Generator

## Objective

Upgrade the existing Test Case Generator.

## Prompt

```text
# Session 07 — Test Case Generator Enhancement

Execute the shared execution contract.

Goal:
Enhance the migrated Test Case Generator without removing its current screenshot-inference use case.

Inputs:

- Test case ID
- Requirement/story reference
- Scenario category
- Test case name
- Module/target section
- Environment
- Priority
- Preconditions
- Test data
- Steps
- Expected result
- Postconditions
- Cleanup
- Attachments
- Automation candidacy
- Regression tag
- Output type:
  - Single Test Case
  - Scenario Matrix
  - Jira-friendly
  - Spreadsheet-friendly

Scenario categories:

- Happy
- Negative
- Edge/Boundary
- UI
- Navigation/State
- Security
- Data Integrity
- Performance
- Accessibility
- Localization
- Recovery

Tasks:

1. Expand the typed form and model.
2. Implement deterministic `TestCasePromptBuilder`.
3. Preserve screenshot/evidence inference instructions.
4. Clearly mark inferred values.
5. Add missing-information warnings.
6. Add checks for:
   - vague steps
   - vague expected results
   - duplicate steps
   - no observable outcome
7. Support expected result per step or final expected result.
8. Add spreadsheet-friendly output without generating an actual spreadsheet.
9. Add unit tests for every output mode.
10. Keep copy/download behavior consistent with Bug and Story tools.

Validation:
- Builder tests.
- Scenario-category tests.
- Missing-evidence behavior.
- Targeted Angular build.

Commit message:
`feat(prompt-studio): enhance test case generation workflows`
```

## Completion Gate

- Existing test-case behavior is preserved and expanded.
- Multiple output formats work.
- Inference is clearly labeled.

---

# Session 08 — Prompt Quality Engine, History, and Export

## Objective

Add deterministic quality guidance and reusable prompt productivity features.

## Prompt

```text
# Session 08 — Prompt Quality and Productivity

Execute the shared execution contract.

Goal:
Add deterministic quality analysis, local history, and export features to all Prompt Studio generators.

Quality checks:

- Required information missing
- Steps too vague
- Expected result not observable
- Contradictory expected/actual values
- Missing environment
- Missing test data
- Missing evidence
- Severity/priority inconsistency
- Story missing business goal
- Acceptance criteria not testable
- Ambiguous actor
- Undefined scope
- Vague words such as properly/correctly/as expected
- Potentially sensitive content

Tasks:

1. Implement `PromptQualityService`.
2. Return typed findings:
   - severity
   - field
   - message
   - recommendation
3. Display:
   - completeness score
   - warnings
   - missing fields
   - facts
   - assumptions
   - allowed inference
4. Quality findings must not block generation.
5. Add local prompt history:
   - namespaced storage
   - bounded history size
   - clear history
   - restore prompt
   - copy
   - download
6. Add a sensitive-data notice.
7. Do not send any data to an external AI provider.
8. Add tests for quality rules and history limits.
9. Avoid storing large attachments or file contents in localStorage.
10. Add graceful handling when storage is unavailable.

Validation:
- Quality service unit tests.
- History service tests.
- Storage-failure test.
- Keyboard and accessibility check.
- Targeted build.

Commit message:
`feat(prompt-studio): add prompt quality and local history`
```

## Completion Gate

- Quality guidance works for all three generators.
- History and export are reliable.
- No external data transmission occurs.

---

# Session 09 — Online Order Tool Integration with Shared Shell

## Objective

Integrate the existing Online Order Tool into the new shell without business regression.

## Prompt

```text
# Session 09 — Online Orders Shell Integration

Execute the shared execution contract.

Goal:
Integrate the existing Online Order Tool under the QA Support Hub shell and route structure.

Tasks:

1. Map the existing feature under `/tools/online-orders`.
2. Preserve legacy routes through redirects where practical.
3. Add:
   - shared page header
   - breadcrumbs
   - dashboard/home navigation
   - global theme integration
4. Identify duplicated local styles that conflict with the design system.
5. Replace only safe shared elements:
   - buttons
   - badges
   - page header
   - empty states
   - toasts
6. Do not rewrite business components unnecessarily.
7. Preserve:
   - filters
   - pagination
   - exact search
   - Clear All
   - statistics
   - status mapping
   - request details
   - same-number resend rules
   - current performance behavior
8. Preserve official assets.
9. Add route integration tests.
10. Run the full frontend suite because this session changes application-wide routing and shell behavior.
11. Run backend tests only if API behavior changed.

Validation:
- Existing Online Order workflows.
- Direct route refresh.
- Legacy route redirect.
- Hub → Online Orders → Hub navigation.
- Full frontend tests.
- Production build.
- Existing build wrapper when required by repository policy.

Commit message:
`feat(online-orders): integrate feature into QA Support Hub shell`
```

## Completion Gate

- Online Orders is accessible from the dashboard.
- Existing behavior remains stable.
- Shared shell and theme apply.

---

# Session 10 — POS Maintenance Placeholder and Migration Intake

## Objective

Prepare the application for the future POS project without inventing implementation details.

## Prompt

```text
# Session 10 — POS Migration Preparation

Execute the shared execution contract.

Goal:
Prepare the POS Maintenance feature boundary before the standalone project source is available.

Tasks:

1. Implement `/tools/pos-maintenance` as a high-quality migration-pending page.
2. Show:
   - status
   - intended capabilities
   - migration prerequisites
   - no fake execution controls
3. Add a typed POS capability model with statuses:
   - pending
   - read-only
   - state-changing
   - destructive
   - unavailable
4. Create `docs/POS_MAINTENANCE_MIGRATION_INTAKE.md`.
5. The intake document must request:
   - repository/source
   - stack
   - current operations
   - permissions
   - machine-local vs remote behavior
   - database operations
   - file operations
   - service operations
   - backup/restore
   - secrets
   - logging
   - deployment
   - tests
6. Create a draft operation-classification table.
7. Create a draft API/agent contract document without inventing endpoints.
8. Keep the dashboard card status `Migration Pending`.
9. Do not implement any machine operation.
10. Do not add an arbitrary command field.

Validation:
- Route and card behavior.
- Accessibility.
- Documentation completeness.
- Targeted Angular build.
- `git diff --check`.

Commit message:
`docs(pos-maintenance): prepare secure migration intake`
```

## Completion Gate

- POS is represented accurately.
- Migration requirements are documented.
- No unsafe placeholder action exists.

---

# Session 11 — POS Source Assessment

## Prerequisite

The standalone POS Maintenance source project must be available locally or added as a read-only migration source.

## Prompt

```text
# Session 11 — POS Source Assessment

Execute the shared execution contract.

Prerequisite:
The standalone POS Maintenance project source must be available.

If it is not available:
- Do not invent behavior.
- Return `Blocked — POS Source Required`.
- Do not change implementation code.

Goal:
Produce a complete migration assessment and parity plan.

Tasks:

1. Read the standalone project instructions and source.
2. Inventory:
   - screens
   - commands
   - services
   - SQL usage
   - file operations
   - configuration access
   - backup/restore
   - service start/stop
   - external dependencies
   - permissions
   - secrets
   - logs
   - packaging
3. Classify every operation:
   - read-only
   - reversible
   - state-changing
   - destructive
   - machine-local
   - remote
   - database
   - file
   - service
4. Determine target placement:
   - Angular UI
   - main .NET backend
   - Windows agent
   - temporarily retained external workflow
5. Identify operations that must not be migrated to browser-only code.
6. Define typed request/result contracts.
7. Define authorization roles.
8. Define confirmation levels.
9. Define audit fields.
10. Define migration order.
11. Create `docs/POS_MAINTENANCE_MIGRATION_PLAN.md`.
12. Update the main implementation plan only with confirmed source facts.
13. Do not implement operations in this session.

Validation:
- Every current operation is mapped.
- No credential value is documented.
- No unsupported assumption is recorded as fact.
- `git diff --check`.

Commit message:
`docs(pos-maintenance): map standalone tool migration`
```

## Completion Gate

- Every standalone capability has a migration decision.
- Security and execution architecture are approved for implementation.

---

# Session 12 — POS Maintenance Backend and Agent Contracts

## Prerequisite

Session 11 completed and target architecture approved.

## Prompt

```text
# Session 12 — POS Backend and Agent Contracts

Execute the shared execution contract.

Goal:
Implement only the approved POS Maintenance contracts and safe read-only foundation.

Tasks:

1. Use the confirmed migration plan.
2. Create typed backend contracts for:
   - agent health
   - agent version
   - machine identity
   - supported capabilities
   - operation request
   - operation progress
   - operation result
   - audit correlation
3. Implement authentication/authorization boundaries using existing project patterns.
4. Add target allowlist abstraction.
5. Add operation allowlist abstraction.
6. Implement read-only health/status endpoints first.
7. Do not implement destructive actions.
8. Do not allow arbitrary command text.
9. Do not return secrets.
10. Add timeouts and cancellation contracts.
11. Add sanitized structured logging.
12. Add targeted backend tests.
13. Add a minimal Angular status page consuming only read-only contracts.
14. Keep unavailable actions disabled.

Validation:
- Authorization tests.
- Allowlist tests.
- Contract serialization tests.
- No arbitrary execution path.
- Targeted backend and frontend builds.

Commit message:
`feat(pos-maintenance): add secure agent contracts`
```

## Completion Gate

- Safe read-only communication foundation exists.
- No destructive operation is exposed.

---

# Session 13 — POS Maintenance Operation Migration

## Prerequisite

Implement one approved operation group at a time. Repeat this session for each group rather than migrating everything in one run.

## Prompt

```text
# Session 13 — POS Operation Group Migration

Execute the shared execution contract.

Operation group to migrate:
{{INSERT_ONE_APPROVED_OPERATION_GROUP}}

Goal:
Migrate exactly one confirmed POS Maintenance operation group with parity, authorization, audit, and rollback behavior.

Tasks:

1. Read the source implementation for this operation group.
2. Preserve confirmed behavior.
3. Implement:
   - typed Angular form
   - typed API contract
   - backend/agent handler
   - input validation
   - permission check
   - target allowlist
   - operation allowlist
   - confirmation when state-changing
   - progress
   - timeout
   - sanitized result
   - audit correlation
4. Define idempotency or safe retry behavior.
5. Define rollback or recovery instructions.
6. Do not expose arbitrary script, path, SQL, or command input.
7. Do not broaden scope to other operation groups.
8. Add focused tests.
9. Add parity notes to the migration plan.
10. Mark the capability Available only after validation.

Validation:
- Happy path.
- Authorization denial.
- Invalid target.
- Invalid input.
- Timeout.
- Partial failure.
- Safe retry.
- Audit record.
- UI confirmation.
- Source parity check.

Commit message:
`feat(pos-maintenance): migrate {{OPERATION_GROUP_SLUG}}`
```

## Completion Gate

- One operation group is complete and secure.
- Other groups remain unchanged.

---

# Session 14 — Cross-Tool UI Consistency and Responsive Hardening

## Objective

Make all tools visually consistent without rewriting business logic.

## Prompt

```text
# Session 14 — Cross-Tool UI Hardening

Execute the shared execution contract.

Goal:
Apply the shared design system consistently across the Hub, Prompt Studio, Online Orders, and available POS pages.

Tasks:

1. Audit:
   - typography
   - spacing
   - buttons
   - cards
   - inputs
   - badges
   - empty states
   - loading states
   - toasts
   - page headers
   - breadcrumbs
2. Replace remaining duplicated shell-level styles.
3. Preserve feature-specific visualizations and business states.
4. Ensure:
   - desktop layout
   - tablet layout
   - mobile layout
   - 200% zoom usability
5. Add consistent tool-to-hub navigation.
6. Add route-transition motion with reduced-motion fallback.
7. Ensure animations do not blur text or block interaction.
8. Ensure Prompt Studio split panes stack correctly on narrow screens.
9. Ensure Online Order tables remain usable through responsive strategy.
10. Ensure POS forms show risk/status consistently.
11. Do not change business rules.
12. Add visual regression coverage only if the repository already supports it; do not introduce a large new framework solely for this session.

Validation:
- Responsive manual checks.
- Keyboard checks.
- Reduced-motion checks.
- Targeted component tests.
- Full frontend production build.

Commit message:
`refactor(ui): unify QA Support Hub tool experiences`
```

## Completion Gate

- All tools look like one product.
- Responsive behavior is consistent.
- Business behavior is unchanged.

---

# Session 15 — Accessibility, Security, and Performance Hardening

## Objective

Complete non-functional hardening before final regression.

## Prompt

```text
# Session 15 — Non-Functional Hardening

Execute the shared execution contract.

Goal:
Harden the QA Support Hub for accessibility, frontend security, and performance.

Accessibility:

- semantic headings
- form labels
- visible focus
- keyboard navigation
- screen-reader announcements
- contrast
- reduced motion
- dialog focus management
- error summaries
- touch target size
- remove forced non-scalable viewport behavior

Security:

- no secrets in browser storage
- safe text rendering
- no unsafe innerHTML
- no arbitrary command input
- no external AI transmission without explicit configuration
- sanitized logs
- authorization around POS endpoints
- operation and target allowlists
- sensitive-data warning in Prompt Studio

Performance:

- lazy-loaded tools
- lazy-loaded Three.js
- pause animation when hidden
- cap canvas pixel ratio
- no global high-CPU idle loop
- bundle budget review
- no repeated API request on route change
- debounced local persistence

Tasks:

1. Audit current implementation.
2. Fix confirmed issues only.
3. Add focused tests.
4. Record remaining accepted risks.
5. Run the full frontend suite.
6. Run backend security/authorization tests when POS APIs exist.
7. Run production build and bundle review.

Commit message:
`fix(platform): harden accessibility security and performance`
```

## Completion Gate

- No critical accessibility/security issue remains.
- Animation and bundles are controlled.
- POS safety boundaries are verified.

---

# Session 16 — Final Integration Regression and Release Preparation

## Objective

Validate the complete unified application and prepare release documentation without deploying.

## Prompt

```text
# Session 16 — Final Integration and Release Readiness

Execute the shared execution contract.

Goal:
Complete final regression, documentation, and release packaging for QA Support Hub. Do not deploy in this session.

Tasks:

1. Verify root dashboard.
2. Verify Prompt Studio:
   - Bug Refiner
   - Story Refiner
   - Test Case Generator
   - Quality warnings
   - History
   - Copy
   - Download
   - keyboard shortcuts
3. Verify Online Orders:
   - route compatibility
   - filters
   - pagination
   - exact search
   - Clear All
   - statistics
   - details
   - current status rules
4. Verify POS:
   - migration-pending state when not migrated, or
   - all approved migrated capabilities when available
5. Verify:
   - light theme
   - dark theme
   - reduced motion
   - desktop
   - tablet
   - mobile
   - direct route refresh
   - browser back/forward
6. Run:
   - full backend tests
   - full frontend tests
   - Release backend build
   - Angular production build
   - repository build wrapper
   - asset verification
   - memory/state checks
   - secret scan
   - `git diff --check`
7. Update:
   - README
   - architecture documentation
   - route map
   - Prompt Studio usage
   - POS migration status
   - release notes
   - TASK/state/history files used by the project
8. Build the deployment artifact outside the repository only when repository policy requires release packaging.
9. Do not deploy to IIS.
10. Commit final release-readiness documentation.

Commit messages as applicable:

`test(platform): complete QA Support Hub regression`

`docs(platform): finalize QA Support Hub release guide`
```

## Completion Gate

- Full validation passes.
- Documentation reflects actual feature availability.
- Release artifact is ready but not deployed.
- Git is clean and synchronized according to repository policy.

---

# Session Status Tracker

Update this section after each session.

```text
Session 00 — Completed
Session 01 — Completed
Session 02 — Completed
Session 03 — Active
Session 04 — Not Started
Session 05 — Not Started
Session 06 — Not Started
Session 07 — Not Started
Session 08 — Not Started
Session 09 — Not Started
Session 10 — Not Started
Session 11 — Blocked until POS source is supplied
Session 12 — Blocked until Session 11 approval
Session 13 — Blocked until approved operation group is selected
Session 14 — Not Started
Session 15 — Not Started
Session 16 — Not Started
```

Allowed statuses:

```text
Not Started
In Progress
Completed
Completed with Follow-up
Blocked
Deferred
```

---

# Model Assignment Recommendation

Use smaller or faster models for:

- Session 02
- Session 03
- Session 05
- Session 06
- Session 07
- Session 08
- Session 10
- Session 14

Use the strongest available planning/reasoning model for:

- Session 00
- Session 01
- Session 09
- Session 11
- Session 12
- Session 13
- Session 15
- Session 16

Recommended workflow:

```text
Architecture/review:
KIMI high reasoning, Luna high, Opus-class model, or equivalent

Scoped implementation:
KIMI, Luna, Sonnet, or equivalent

Final integration review:
Strongest available reasoning model
```

Do not let the model execute a later session when the previous completion gate is not satisfied.
