# POS Slice B Boundary

Slice C extends this boundary with the permanent `RmsSupportAgent` identity,
safe legacy-service migration planning, package/certificate/browser policy
contracts, durable audit, fixed RMS-root health summaries, insurance
attachment aggregation, and the operations-console consumer. The controls
below remain the baseline; Slice C does not relax the direct-Agent boundary or
turn the Support Hub API into a privileged relay.

This document records the server-owned boundary delivered by POS Slice B. The
Support Hub remains a direct browser client of the installed Windows POS Agent;
the browser does not become an installer, process launcher, Main Server proxy, or
package store.

## 2026-08-15 security remediation rebaseline

The Slice B remediation closes the reviewed H-1/H-2/H-3 boundaries and the
directly adjacent M-1 through M-4 and L-1 through L-3 findings in the current
implementation:

- Diagnostic stdout/stderr, timeline summaries, and downstream evidence use a
  bounded fail-closed redaction pipeline for structured JSON/XML, connection
  strings, bearer/token/API-key assignments, complete PEM material, and
  truncated private-key blocks. If sanitization fails, output is quarantined
  rather than persisted.
- The fixed package root, `available`, `rollback`, `staging`, Agent
  installation root, and diagnostic artifact root are provisioned and verified
  before reads or writes. Reparse-point escape, unsafe ownership, and
  untrusted ACLs fail closed.
- Package mutation and Repair Installation use one non-blocking machine-wide
  lease. Guided Repair state-changing package checkpoints use the same lease;
  a different principal, idempotency key, or scope cannot bypass a busy
  result. The lease remains held through terminal operation/timeline truth.
- Retained previews are typed POST operations with bounded principal-scoped
  idempotency. Read-only GETs do not allocate retained privileged workflow
  state.
- Main Server transport uses a bounded no-redirect HTTP policy, and endpoint
  binding compares normalized scheme, host, effective port, and base path.
  Snapshot evidence persists the configured environment/profile identity.
- Diagnostic process timeout cancellation and package streaming size checks
  remain bounded before output or files can cross their respective boundaries.

Testing remains synthetic/read-only for RMS and Main Server. This slice does
not launch RMS executables, installers, uninstallers, repair, rollback, or
package activation, and does not issue Main Server state-changing requests.


## Main Server profiles and state

The Agent owns the profile catalog. Testing is the only enabled profile. The
future Production profile is fixed and disabled until a separate approval
enables it. Profile projections contain only a logical profile identifier,
environment, binding state, client label, and the fixed read operations allowed
by the Agent. They do not expose URLs, credentials, authorization material,
connection strings, arbitrary routes, or browser-selected host/branch/POS data.

`GET /api/v1/main-server/state` binds the server-owned read to the discovered
Branch and POS identity and issues only the documented typed read projections:

- `Branch/GetBranchesWithStatus`
- `PosMachine/GetAllPosStatuses`

The Agent uses HTTP/1.1, bounded JSON responses, exact route/query templates,
and recursive redaction of unknown nested values. Stale, ambiguous, mismatched,
unavailable, or secret-bearing responses remain `Unknown` or
`ActionRequired`. Branch/POS installation-state `PUT` contracts are treated as
acknowledgements, not an installation operation, and are never invoked by this
slice.

## Safe Diagnostic Console Run

The console boundary accepts a logical target only. The Agent resolves it to a
fixed manifest entry, executable name, working directory, exact diagnostic
argument template, timeout, output limit, and line limit. Shell execution,
arbitrary arguments, arbitrary working directories, inherited environment
secrets, downloaded code, and user-supplied paths are rejected.

The process seam uses separate bounded standard-output and standard-error
streams, an empty child environment, timeout/process-tree termination, fixed
installation-root checks, and reparse-point rejection. Output is redacted for
credentials, tokens, connection strings, SIDs, private keys, users, and host
paths before it becomes an opaque principal-scoped artifact. A run timeline is
created only after the authorized start `POST`; preview and read-only status do
not create a run.

Routes:

- `POST /api/v1/diagnostic-console/preview/{targetId}`
- `POST /api/v1/diagnostic-console/runs`
- `GET /api/v1/diagnostic-console/runs/{operationId}`

## Safety Snapshot

The preview and capture workflow records only typed safe evidence: device and
client identity, Product Release and drift, canonical service state, database
identity/reachability, fixed-root capacity, approved-backup metadata,
consistency, package/version hashes, and health/timeline correlation IDs.

Snapshot files are written atomically below the Agent-owned fixed storage root,
have bounded size and retention, include an integrity hash, expire, and are
bound to the authenticated principal. Retrieval and verification fail closed on
missing, corrupt, expired, mismatched, stale, or unverifiable records. Raw
configuration, credentials, SQL, arbitrary paths, logs, private keys, and full
events are not part of the snapshot projection.

Routes:

- `GET /api/v1/safety-snapshots/preview`
- `POST /api/v1/safety-snapshots/capture`
- `GET /api/v1/safety-snapshots/{snapshotId}/verify`
- `GET /api/v1/safety-snapshots/{snapshotId}`

A fresh verified snapshot identifier is a required precondition for repair and
package mutation. Capture is protected by the Agent's exact-origin,
administrator, one-use mutation-token, typed-confirmation, and idempotency
boundaries.

## Package and repair boundary

The Agent package manifest is server-owned and versioned. It carries file
hashes and sizes, supported OS/runtime, service identity, ACL and certificate
requirements, and rollback metadata. Preview verifies signature/trust,
checksum, compatibility, archive traversal, exact file allow-list, service and
ACL requirements, certificate/private-key ownership, capacity, and rollback
material before confirmation. The same integrity checks are applied before
staging and before any activation seam is allowed to proceed.

Only the fixed Agent installation boundary and exact service/certificate
allow-list are eligible for lifecycle work. The platform seam owns service
registration, SCM/display-name mapping, LocalSystem and ACL requirements,
certificate/private-key handling, startup health, migration, uninstall safety,
prior-version recovery, and rollback truth. The default synthetic/testing
platform is conservative: it can validate and prepare bounded inputs but does
not activate an RMS package or run repair during repository validation.

Repair operations require a fresh verified snapshot, exact typed confirmation,
one-use authorization, principal-scoped idempotency, and truthful accepted,
running, succeeded, failed, timed-out, cancelled, not-attempted, partial, or
unknown/recovery states. Rollback failure and recovery-required states are
preserved rather than inferred from an HTTP response. Main Server state is never
changed by package or repair workflows.

Guided Repair is a fixed typed checkpoint sequence. Each checkpoint is
independently authorized and resumable only from a verified prior checkpoint.
Advancing a checkpoint never implies package activation, and browser text or
diagnostic logs cannot generate new steps.

Routes:

- `GET /api/v1/packages/status`
- `POST /api/v1/packages/preview/{operationId}`
- `POST /api/v1/packages/operations`
- `GET /api/v1/packages/operations/{operationId}`
- `POST /api/v1/repair/preview/{operationId}[/{snapshotId}]`
- `POST /api/v1/repair/operations`
- `GET /api/v1/repair/operations/{operationId}`
- `POST /api/v1/repair/guided/preview[/{snapshotId}]`
- `GET /api/v1/repair/guided/{guidedRepairId}`
- `POST /api/v1/repair/guided/steps`

## Validation boundary

Slice B validation uses synthetic process, package, snapshot, and Main Server
seams. It does not launch RMS executables, installers, uninstallers, repair,
package activation, or Main Server state-changing requests. No Production or
customer environment is used. The generated OpenAPI document and Angular
client are contract artifacts and must remain deterministic across a second
generation pass.
