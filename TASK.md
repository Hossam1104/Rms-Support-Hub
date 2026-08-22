# WPF-01 — Shared Agent Application + Local IPC Foundation

MODEL: Implementation and validation executor
AUTHORITY: GPT-5.6 Sol remains Planner / Architect / Acceptance Authority
PROGRAMME: POS Dual Control-Surface Architecture (CR-001 / ADR-0029)
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/wpf-01-shared-agent-local-ipc`
EPIC: E16 — Agent Platform Re-Architecture (#13017)
PRIMARY STORIES: `US-E16-02` (#13022), `US-E16-04` (#13024)
STATUS: Proposed First Implementation Slice

---

> [!CAUTION]
> # HARD STOP — DO NOT EXECUTE YET
> **DO NOT execute or begin implementation of WPF-01 until GPT-5.6 Sol independently reviews and formally accepts the Architecture Rebaseline PR (`docs/wpf-agent-architecture-rebaseline`).**
> No product code changes, WPF implementation, or SignalR remote mutations may proceed before formal architecture acceptance.

---

## 1. Goal and Objective

The objective of `WPF-01` is to establish the core decoupled application seam in `RmsSupportAgent` and implement the authenticated Windows Named Pipe local IPC foundation, validating the dual control-surface architecture without disrupting the existing delivered browser-direct loopback baseline.

This slice proves that:
1. Privileged logic can be extracted into transport-agnostic command/query handlers.
2. Local callers over Windows Named Pipes authenticate and execute under strict Windows ACLs.
3. Existing loopback HTTP/1.1 Kestrel endpoints continue to operate without behavioral drift.
4. Unauthorized or un-elevated callers are rejected fail-closed.

---

## 2. In-Scope Work for WPF-01

1. **Seam Discovery & Inventory:**
   - Inspect existing `RmsSupportHub.Pos.Agent` endpoints, services, and handlers.
   - Identify the reusable command/query boundary for diagnostics, service control, database recovery, and package trust.
2. **Shared Application Layer:**
   - Extract transport-agnostic command/query handlers and validators into `RmsSupportAgent.Application`.
   - Define `InvocationContext` capturing invocation source (`LocalWpf` vs `RemoteHub`), caller Windows principal, device identity, admin identity, and correlation ID.
   - Preserve mutation leases, idempotency guards, bounded redaction, and durable audit repository.
3. **Transport Preservation:**
   - Keep existing Kestrel HTTPS loopback endpoints (`https://rms-pos-agent.localhost:5001`) active and delegating to the shared application layer.
   - Ensure all existing backend, POS, and Pester tests pass with zero regressions.
4. **Local IPC Foundation:**
   - Implement an authenticated Windows Named Pipe listener in `RmsSupportAgent.Service` (`\\.\pipe\RmsSupportAgent.Ipc`).
   - Configure Windows Security Descriptors restricting pipe access to `LocalSystem` and `NT AUTHORITY\Administrators`.
   - Implement caller Windows identity verification.
   - Create a lightweight client library in `RmsSupportAgent.LocalIpc`.
5. **Initial Typed Handlers:**
   - Implement Agent health/readiness query over Named Pipes.
   - Implement ONE non-destructive typed diagnostic command (e.g., RMS installation discovery query).
6. **Testing & Security Harness:**
   - Write automated integration tests verifying Named Pipe client connection, Windows authentication, ACL rejection for non-admin accounts, and shared handler execution.

---

## 3. Explicitly Out of Scope for WPF-01

- Building the full WPF desktop application UI screens (deferred to Phase 3 / E17).
- Implementing Agent-initiated SignalR Hub client or remote Hub mutations (deferred to Phase 4 / Phase 6).
- Modifying Production configuration, Production order gates, or touching Production environments.
- Modifying POS machines, native RMS services, or live customer databases.
- Deprecating or removing existing browser-direct loopback endpoints.

---

## 4. Security and Governance Guardrails

- Named Pipe ACLs must enforce `LocalSystem` and `Administrators` only.
- Local Windows callers must be authenticated; anonymous IPC is prohibited.
- Generic command execution, arbitrary PowerShell, generic SQL, and arbitrary process launching remain strictly forbidden.
- All operations must emit correlated, durable audit records.
- Testing environment remains the sole authorized runtime target.

---

## 5. Validation and Acceptance Rules for WPF-01

- Run targeted POS application and IPC integration tests.
- Run the full Release backend test suite and frontend tests to prove zero regression.
- Run PowerShell quality gates and memory checks.
- Verify that both Named Pipe and existing HTTPS loopback routes succeed on health probes.
- PR must remain Draft awaiting independent review by GPT-5.6 Sol.
