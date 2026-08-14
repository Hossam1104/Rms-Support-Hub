# ADR-0022: Typed Agent-owned RMS database recovery

## Status

Accepted — 2026-08-14

## Context

Support Hub needs a safe operator workflow for backing up and restoring the
installed Branch and Cashier RMS databases. The browser is an untrusted
cross-origin client relative to the privileged Windows Agent, and the existing
POS process must not become a generic SQL, filesystem, service, or command
proxy. Restore outcomes can be ambiguous and service/database recovery must be
truthful.

## Decision

Expose only typed `branch` and `cashier` Agent targets. The Agent owns the
canonical database names, associated RMS service mapping, fixed backup and
database-file roots, SQL statements, logical-file restore plan, artifact
catalog, and operation state. The browser supplies only a logical target,
bounded idempotency key, opaque approved artifact ID, exact target-specific
confirmation text, and a one-use mutation token bound to the Windows SID,
Origin, method, operation, and exact route.

Backups are registered only after the server-owned SQL backup produces a
non-empty file beneath the approved root. Restore validates the approved
artifact and SQL file list, coordinates only the corresponding RMS service,
uses the existing bounded logical-to-physical restore plan, verifies database
identity afterward, and attempts to return a previously running service to its
original state. Ambiguous dispatch or recovery is reported as
`outcomeUnknown` with `recoveryRequired`; no automatic retry is performed.

Progress and operation reads are principal-scoped, bounded, sanitized, and
available through REST plus read-only SSE. Privileged audit events retain only
safe operation, principal, correlation, service-coordination, dispatch, and
outcome details inside a non-transport Agent sink.

## Consequences

- Raw connection strings, credentials, database names selected by callers, SQL,
  physical paths, service names, and arbitrary commands do not cross the
  browser boundary.
- The Agent process remains the composition root for the privileged capability;
  Core/Data/API layering is not expanded for POS work.
- The approved-backup catalog is durable: it persists as Agent-owned,
  schema-versioned JSON beneath the backup root with atomic writes, fail-closed
  corruption handling, and physical revalidation (path containment, reparse
  rejection, size, SHA-256) on every read, so approved backups survive an
  Agent restart. It has its own retention policy
  (`RmsDatabaseStorageOptions.BackupRetention`, default 30 days, plus the
  existing per-database count cap) that is fully independent of the generic
  in-memory `ArtifactCatalog`/`RuntimeRetentionPolicy.ArtifactLifetime` used
  for browser-download artifacts, so a backup never disappears merely because
  a download capability expired.
- Operation/audit retention (progress state, privileged audit events) remains
  process-local and restart-invalidated; durable audit/storage integration
  remains a future deployment decision.
- Restore no longer requires the target Branch/Cashier database itself to be
  reachable. It preflights against SQL Server `master`, the installed
  connection configuration, and the approved backup artifact (`RESTORE
  VERIFYONLY`/`FILELISTONLY`, database-identity match); it works even when the
  target database is unavailable or missing, which is the actual recovery
  scenario. Backup still requires the live target database and identity
  confirmation.
- Live backup/restore and service coordination require an authorized Testing
  execution session. Automated tests use synthetic fixtures and never restore
  the installed RMS databases. Live non-destructive backup validation for this
  slice was attempted but blocked by the current session's non-elevated
  Windows process token (SCM access denied); this is an execution-environment
  gate, not a code or design gap — see `.ai/STATE.md`.
