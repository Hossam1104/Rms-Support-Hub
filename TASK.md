# WPF-01 - Shared Agent Application + Local IPC Foundation

MODEL: Implementation and validation executor
AUTHORITY: GPT-5.6 Sol remains Planner, Architect, and Acceptance Authority
PROGRAMME: POS Dual Control-Surface Architecture (CR-001 / ADR-0029)
REPOSITORY: `D:\AI Tools\DBS\Rms-Support-Hub`
BRANCH: `feat/wpf-01-shared-agent-local-ipc`
EPIC: E16 - Agent Platform Re-Architecture (#13017)
PRIMARY STORIES: US-E16-02 (#13022), US-E16-04 (#13024)
STATUS: Implemented; bounded OPUS remediation complete locally; awaiting exact-head CI and GPT-5.6 Sol review/acceptance

## Completed WPF-01 implementation

WPF-01 added the shared `InvocationContext`/authorization seam, shared RMS
installation discovery, the typed HTTPS adapter, and the bounded v1
`RmsSupportHub.Pos.LocalIpc` client/server with health and discovery operations.
It preserves legacy HTTPS, Negotiate, CORS, mutation-token/package-trust flows,
the old WinUI project, and all native RMS/customer boundaries. IPC is disabled
by default and focused authorization, audit, ACL, protocol, bound, and parity
tests are included.

## WPF-01 bounded Sol security remediation

Draft PR #32 findings S01-S09 were corrected on this branch. Production ACL:
LocalSystem/Administrators FullControl, explicit duplex-client rights for
`RMS Support Operators`, and NETWORK deny. The client verifies the connected
pipe server PID against the canonical running `RmsSupportAgent` SCM service PID
through query-only SCM access before writing and matches request/correlation IDs.
Group resolution is local-machine-only; source/authority combinations,
diagnostic context propagation, and durable audit semantics are fail-closed.
The stable `System.IO.Pipes.AccessControl` 5.0.0 attempt was removed because .NET 10 already
provides the API and CI treats NU1510 as an error; no preview remains.

## WPF-01 acceptance evidence

The implementation started from clean main `bd83e3b2c223e807f40e684fe61a5281c915674b`.
Release build and POS tests pass with the Testing-only origin environment
variable. Standalone Agent startup was not provisioned because it requires the
machine-owned Testing certificate; in-process HTTPS/Named Pipe checks changed
no machine or RMS state.

Draft PR #32 remains unmerged and must remain Draft for Sol review. The
pre-remediation baseline CI was green; the final bounded OPUS remediation still
requires exact-head CI after delivery.

Local validation after the bounded remediation: Domain 12/12, Application
89/89, Infrastructure 155/155, Agent Integration 234/234 (490/490 POS tests
total); lifecycle remediation 6/6; audit/authority/architecture remediation
25/25; exact protocol bounds 2/2; PowerShell parse gate 37/37; Pester 3.4.0
172/172. Release build passed with 0 warnings and 0 errors, and the checked-in
OpenAPI document was regenerated and verified fresh.

## WPF-01 final bounded OPUS remediation

OPUS-01 through OPUS-13 and OPUS-15 are closed with production-path code and
regression coverage for SCM identity/imports, listener recovery, pipe/semaphore
lifetime, correlation, Local WPF authority, ACL/PID trust, local group typing,
Domain/Contracts parity, typed audit-unavailable 503s, namespace ownership,
Identification impersonation, orphan cleanup, and exact protocol bounds.

OPUS-14 rate limiting and OPUS-16 representative-machine/operator-group E2E
remain explicitly deferred. No WPF-02 work, Production contact, provisioning,
native RMS mutation, customer database access, or runtime deployment occurred.

## WPF-02 - WPF Shell + Local Agent Health Experience

The next executable slice is intentionally recorded here for the next owner.
Do not infer authorization to start it from this prompt.

> HARD STOP - DO NOT EXECUTE WPF-02 until GPT-5.6 Sol reviews and accepts WPF-01.

BRANCH: Create `feat/wpf-02-wpf-shell-local-health` from the accepted WPF-01
head. Do not work on `main`, merge, or mark a PR ready.

### WPF-02 objective

Create a native WPF shell beside the Agent using `RmsSupportHub.Pos.LocalIpc`
as the only local business-operation entry point. Prove bounded health display
and Agent-unavailability recovery without duplicating Agent business logic.

### WPF-02 in scope

1. Add a new repository-consistent WPF project for the desktop shell. Do not
   rename, delete, convert, or rewrite `pos/src/PosAdminTool.WinUI`.
2. Add a minimal shell with navigation chrome, an Agent connection indicator,
   a health view, loading state, unavailable state, retry action, and a clear
   protocol-version incompatibility state.
3. Use `LocalIpcClient.GetHealthAsync` and the existing typed contracts. The WPF
   project must not create named pipes directly, parse arbitrary envelopes, or
   accept caller-provided privilege fields.
4. Keep UI state transport-focused: Agent status, IPC status, protocol version,
   correlation ID, last successful check time, and safe error code/detail only.
5. Add cancellation and bounded retry behavior that cannot create an unbounded
   timer, request, or log loop.
6. Add unit/integration coverage for view-model transitions and healthy, unavailable, malformed-response, timeout, and protocol-mismatch outcomes using test seams.
7. Keep the shell design-token based and avoid raw component color literals.

### WPF-02 out of scope

- RMS service controls, database backup/restore, cleanup/reset, package
  lifecycle, arbitrary diagnostics, or any new privileged capability.
- SignalR, Hub authentication, device registration, fleet supervision, or
  remote operations.
- Production deployment, native RMS service changes, customer database access,
  certificates, PKI, or installer/lifecycle changes.
- Replacing the browser-direct HTTPS path or migrating the old WinUI project.

### WPF-02 validation and delivery

- Read `TASK.md`, `.ai/STATE.md`, run `python .ai/scripts/context.py`, and read only task-relevant sources before editing.
- Run focused WPF tests first, then the full affected POS Release build/tests,
  PowerShell gates, memory checks, and `git diff --check`.
- Run only local Testing runtime checks when the machine-owned certificate and authorization prerequisites are available; never provision or mutate Production/native RMS state.
- Update `.ai/STATE.md`, `.ai/HISTORY.md`, and this task prompt with factual
  evidence. Set `.ai/HANDOFF.md` to `Empty` only after completion.
- Commit and push the feature branch, create a Draft PR with the relevant Azure
  references, wait for exact-head CI, and stop for Sol review. Do not merge.

STOP. Await GPT-5.6 Sol acceptance before executing WPF-02.
