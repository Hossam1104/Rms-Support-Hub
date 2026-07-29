# ADR-0004: Atomic Per-Session File-Backed Drafts

- **Status:** Accepted
- **Date:** 2026-07-27
- **Affected area:** Session middleware, draft service, flat-order editing

## Context

A process-global draft and concurrent per-field writes allowed browsers and overlapping requests to overwrite each other or expose partial files.

## Decision

Issue an HttpOnly GUID session cookie, key draft JSON by session and module, serialize each key's load-modify-write, write through a temporary file and atomic replace, and batch/debounce order-data edits. The client keeps local edits authoritative instead of adopting asynchronous server echoes mid-edit.

## Consequences

- Browser sessions and concurrent patches no longer share a single draft.
- The DTO remains compatible with existing draft JSON.
- Local disk still lacks defined retention, encryption, and multi-instance coordination.
- Lock entries live for the process lifetime.

## Revisit When

- Deployment becomes multi-instance, retention/audit requirements are defined, or drafts must survive host replacement.

## Evidence

- `backend/src/OnlineOrderTool.Api/Middleware/SessionIdMiddleware.cs`
- `backend/src/OnlineOrderTool.Core/Services/DraftManager.cs`
- `backend/tests/OnlineOrderTool.Tests/DraftManagerTests.cs`
- `frontend/src/app/features/flat-order/draft.store.ts`
