# ADR-0005: Testing-Default Environment Safety

- **Status:** Accepted
- **Date:** 2026-07-27
- **Affected area:** Module environments, send/cancel flows, application shell

## Context

Dictionary ordering previously selected Production implicitly, the active lane was not persistently visible, and cancel could ignore the operator's environment selection.

## Decision

Mark an explicit default environment, prefer non-Production fallback, persist the choice per module, display the active lane, pass it to operational endpoints, and require typed UI confirmation for Production actions. Agent-run live verification uses Testing only.

## Consequences

- Refresh and deep links retain a safer, visible lane.
- Environment choice is scoped per module.
- UI confirmation reduces accidents but is not server-side authentication or authorization.

## Revisit When

- A real identity/permission model can enforce environment access server-side.

## Evidence

- `backend/src/RmsSupportHub.Core/Modules/ModuleEnvironmentResolver.cs`
- `backend/src/RmsSupportHub.Core/Models/ModuleEnvironment.cs`
- `backend/tests/RmsSupportHub.Tests/OrderControllerEnvironmentTests.cs`
- `frontend/src/app/core/services/module.service.ts`
- `frontend/src/app/shared/ui/env-badge/env-badge.component.ts`
