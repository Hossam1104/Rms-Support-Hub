# ADR-0011: QA Support Hub Baseline Architecture

- **Status:** Accepted
- **Date:** 2026-08-06
- **Session:** QA Support Hub Session 00 — Repository Baseline and Architecture Decision
- **Affected area:** Programme architecture, frontend routing, feature boundaries, integration mechanism

## Context

The repository hosts the Online Order Tool (.NET 10 API, Angular 22 SPA). A
standalone single-file QA Prompt Studio (`prompt_generator/index.html`) was
added to the repository, and a POS Maintenance Tool is planned but its source
has not been supplied. The unified target architecture was delivered through
Sessions 00-16; the programme plan and session prompts that drove them were
executed and removed after the release (see `.ai/HISTORY.md` and Git history).

## Decisions

1. The current Angular + .NET project is the host platform for the unified
   application; no separate host or rewrite is introduced.
2. The main product name is **QA Support Hub**. The Online Order Tool remains
   a feature inside it and keeps its own feature-level naming.
3. Routes are feature-based and lazy-loaded (`/tools/prompt-studio`,
   `/tools/online-orders`, `/tools/pos-maintenance`), with redirects preserving
   existing Online Order URLs.
4. QA Prompt Studio is rebuilt natively in Angular from
   `prompt_generator/index.html`; the HTML file is migration reference only and
   its confirmed behaviors (Bug and Test Case generators, theme and
   localStorage persistence, Three.js effects, copy actions, `Ctrl+Enter`
   shortcut) are preserved as Angular components, services, and typed models.
5. Online Orders business behavior and API contracts are preserved; integration
   changes routes, shell, and styles only.
6. POS Maintenance remains a visible, non-operational **Coming Soon** page
   while its independent project is developed externally. Integration is
   intentionally deferred until that project is complete and a dedicated
   migration session is authorized; only the future integration reference and
   informational placeholder are maintained beforehand.
7. No iframe is used as the final integration mechanism for any tool.

## Consequences

- All three tools share one shell, theme, design tokens, and motion system.
- Prompt Studio MVP generation stays client-side and deterministic; backend
  APIs are added only for shared history, team templates, or AI integration.
- POS privileged operations must run through a secured backend module or
  Windows agent with allowlists and audit; no arbitrary command, PowerShell, or
  SQL execution fields.
- `docs/REPOSITORY_STRUCTURE.md` records where each kind of change belongs so
  later tasks do not re-derive placement.
