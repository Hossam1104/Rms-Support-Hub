# ADR-0030: WPF local artifact delivery boundary

## Status

Proposed — pending GPT-5.6 Sol acceptance

## Context

WPF-06 needs to expose fixed Branch/Cashier backup inventory and let an
authorized local administrator save approved database backups and Support
Bundles to a local destination. WPF must remain a thin typed client: it must
not receive server paths or bytes, browse the Agent filesystem, duplicate
artifact validation, or create a second export path.

## Decision

The Agent owns artifact storage, principal binding, expiry and checksum
validation, fixed database target scope, destination policy, audit ordering,
bounded streaming, same-destination coordination, and atomic finalization.
Database backup and Support Bundle export use the same typed Agent delivery
service. Local IPC carries only an opaque artifact ID, logical artifact kind,
safe destination filename/path selected by the native output dialog, and an
explicit overwrite confirmation when a destination already exists. The Agent
derives authority and caller identity from its trusted invocation context.

For local export, the authenticated caller SID is resolved to the machine-owned
ProfileList entry and its fixed Desktop, Documents, and Downloads roots. The
Agent never uses LocalSystem's interactive-folder environment to select a
destination. Source reads remain Agent-owned; destination create, replace,
rename, rollback, and cleanup operations run only through a dedicated export
authority under the authenticated caller token. The normal IPC identity
boundary is unchanged for every other operation.

Every export records a durable accepted event before destination I/O and a
durable completed, failed, or cancelled event afterward. A missing final audit
cannot produce success; overwrite operations keep a same-directory rollback
file and compensate new or replaced outputs when final audit persistence is
unavailable. Destination mutation is coordinated by a bounded,
reference-counted keyed lease whose idle keys are removed.

Database catalog reads require a valid authenticated principal and exact owner
match. The explicit legacy compatibility mode additionally permits only
historical null-owner records; null is never an unrestricted read. Retention
is scoped by database and owner principal. Database-backup requests are limited
to Branch/Cashier and `.bak`; Support Bundle requests require a null database
target and `.zip`.

Inventory is metadata-only, fixed to Branch and Cashier, principal-scoped,
bounded, newest-first, and never returns server paths. Local operators may
read inventory; local administrators alone may create backups or export
artifacts. RemoteHub and unauthenticated callers are denied. Restore is not a
WPF-06 capability and remains separately governed.

## Consequences

- WPF has no Agent, SQL, HTTP, process, PowerShell, Named Pipe, or filesystem
  reader dependency; its only machine output is the approved Save As flow.
- An audit failure fails closed before export or compensates a newly-written
  destination, so successful UI outcomes remain truthful.
- Missing, expired, checksum-mismatched, oversized, unsafe, or wrong-principal
  artifacts cannot be delivered and do not disclose server paths.
- The shared seam keeps Support Bundle and database-backup delivery aligned;
  future remote/fleet delivery must introduce a separately authorized typed
  channel rather than reusing local destination input.
