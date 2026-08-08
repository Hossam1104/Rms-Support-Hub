# POS Maintenance Tool - Future Integration Reference

## Purpose

This document preserves the security and architecture questions for a future
dedicated POS integration session. The POS Maintenance Tool is being developed
independently outside this repository, so source assessment and integration are
intentionally deferred. RMS+ Support Hub does not implement, connect to, or infer
POS operations today.

## Source Availability

**External development in progress.** The repository contains the RMS+ Support Hub
informational placeholder at `frontend/src/app/features/pos-maintenance/`. The
independent POS project will be considered for integration after development is
complete and a future migration session is authorized.

## Required Source Inputs

When the external project is ready for a future source assessment, provide:

- Original POS Maintenance Tool source code.
- Confirmed repository, archive, or filesystem path and project ownership.
- Solution and project files.
- Dependency and package files, lock files, and build scripts.
- Configuration files and safe configuration samples.
- UI assets, icons, fonts, and other assets required by the application.
- Existing tests, deployment notes, runbooks, and operational documentation.

Do not commit credentials, tokens, connection strings, customer data, or secret
configuration values. Redacted samples are acceptable where safe.

## Required Configuration Inputs

Capture from the supplied source and its approved environment:

| Input | Current status |
| --- | --- |
| Framework and runtime | [REQUIRES SOURCE REVIEW] |
| UI framework | [REQUIRES SOURCE REVIEW] |
| Windows-specific dependencies | [REQUIRES SOURCE REVIEW] |
| Libraries and packages | [REQUIRES SOURCE REVIEW] |
| External tools or processes invoked | [REQUIRES SOURCE REVIEW] |
| Environment and branch/POS identity configuration | [REQUIRES SOURCE REVIEW] |
| Database connection and authentication model | [REQUIRES SOURCE REVIEW] |
| Secret storage and protected configuration | [REQUIRES SOURCE REVIEW] |
| Logging, audit, and error handling | [REQUIRES SOURCE REVIEW] |
| Deployment and rollback requirements | [REQUIRES SOURCE REVIEW] |

## Dependency Review Checklist

The source assessment must identify, for every dependency:

- Package, runtime, framework, or native component name and version.
- License and support constraints where relevant.
- Windows-only APIs, services, drivers, or machine-local assumptions.
- External executables, scripts, scheduled tasks, or process boundaries.
- Network endpoints, protocols, certificates, and proxy assumptions.
- Database providers, client tools, and supported SQL Server versions.
- Configuration and secret inputs required at build or runtime.
- Testability, replacement, and migration risks.

Unknown values remain **[REQUIRES SOURCE REVIEW]** until verified from source.

## Operation Classification Checklist

Inventory each actual operation from the source before proposing a Hub
workflow. Classify it as one or more of:

- Read-only diagnostic
- Configuration read
- Configuration write
- File operation
- Database operation
- Windows service operation
- Process execution
- Network operation
- Privileged machine-local operation
- Destructive operation

For each operation record its input type, output, side effects, target machine,
failure behavior, audit needs, and whether it can be represented as an explicit,
typed, allow-listed operation. Do not claim that any operation exists before
source review.

| Operation | Classification | Target and side effects | Source evidence |
| --- | --- | --- | --- |
| [REQUIRES SOURCE REVIEW] | [REQUIRES SOURCE REVIEW] | [REQUIRES SOURCE REVIEW] | [REQUIRES SOURCE REVIEW] |

## Privilege and Security Checklist

The source assessment must identify whether each operation requires:

- Local administrator permission.
- Windows service control.
- File-system write permission.
- Database credentials.
- SQL Server access.
- Registry access.
- Process execution.
- Network access.
- Protected configuration access.

Record the least privilege, account or identity, machine boundary, approval
path, audit event, timeout, failure handling, and rollback behavior. Current
values are **[REQUIRES SOURCE REVIEW]**.

## Target Architecture Direction

The preferred direction for machine-local operations is:

```text
Angular RMS+ Support Hub
        ->
.NET API
        ->
secured machine-local Windows agent
        ->
approved POS maintenance operations
```

This is a target direction, not an implemented system. No agent, machine
identity, or authorization boundary exists in this repository today. The
backend/agent contracts, authentication, machine identity, and authorization
boundaries are owned by the integration architecture step described in
[POS_MAINTENANCE_INTEGRATION_READINESS.md](POS_MAINTENANCE_INTEGRATION_READINESS.md).

## Explicit Prohibited Generic Execution Surfaces

The future POS tool must never expose generic execution surfaces such as:

- Arbitrary PowerShell textbox.
- Arbitrary command prompt textbox.
- Arbitrary SQL textbox.
- Arbitrary script upload and run.
- Generic executable path launcher.

Future operations must be explicit, typed, and allow-listed. For example, a
future contract may expose `BackupDatabase(request)` or
`RestartApprovedService(serviceId)`, but must not expose a generic
`ExecuteCommand(...)` or `RunPowerShell(...)` surface. The concrete contracts
must wait for source assessment and the integration architecture step.

## Future Integration Entry Criteria

The future integration assessment is executable only when all of the following
are available:

- Source project supplied.
- Repository or filesystem path confirmed.
- Build and dependency files available.
- Safe configuration samples available where needed.
- UI assets required by the source application available.
- No credentials committed to the supplied source or intake materials.

If any item is missing, the future assessment must report that exact missing
intake item rather than inventing source facts, operations, dependencies, or
privileges.
