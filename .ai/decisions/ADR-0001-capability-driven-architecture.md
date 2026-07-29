# ADR-0001: Capability-Driven Layered API and Typed SPA

- **Status:** Accepted
- **Date:** 2026-07-25
- **Affected area:** Backend layers, module registry, API contracts, frontend routing

## Context

The application supports RMS workflows with different capabilities and maturity. Earlier module-key conditionals and duplicated frontend module metadata allowed behavior to drift.

## Decision

Keep domain behavior and module capability declarations in Core, SQL implementations in Data, HTTP composition in API, and a typed Angular SPA that consumes live module metadata. Gate supported behavior with `IOrderModule.Capabilities`, not module-key string comparisons.

## Consequences

- Module availability and route behavior have one backend source.
- Core remains independently testable and dependency-light.
- API and frontend DTOs must remain synchronized.
- Capabilities describe features; they do not provide user authorization.

## Revisit When

- A new service boundary, independently deployed module, or generated API-client strategy makes the current layering insufficient.

## Evidence

- `backend/src/OnlineOrderTool.Core/Modules/IOrderModule.cs`
- `backend/src/OnlineOrderTool.Core/Modules/ModuleRegistry.cs`
- `backend/src/OnlineOrderTool.Api/Program.cs`
- `frontend/src/app/core/services/module.service.ts`
- `frontend/src/app/core/guards/capability.guard.ts`
