# POS Maintenance Tool - Future Integration Reference

## Purpose and current intake status

This document preserves the source, security, and architecture questions for a
future dedicated POS integration session. The POS Maintenance Tool is
developed independently. RMS+ Support Hub does not implement, connect to, or
infer POS operations today.

INT-00 closed the destination-side architecture only. The POS repository is
read-only in that session and no source was imported or inspected here. The
planning candidate is:

```text
POS SOURCE: MERGE-READY CANDIDATE AT 25922b499d33bd73f241ffc26c212dd000e81433
```

That SHA is provenance, not proof that deployment, device, browser, SQL, SMB,
SCM, restore, maintenance, downloader, or remote-trigger behavior has been
validated. The next owner must assess the candidate source and complete the
rows below from actual evidence.

## Required source inputs

When the external project is ready for the explicitly authorized integration
session, provide or verify:

- Original POS Maintenance Tool source code at the approved provenance SHA.
- Confirmed repository, archive, or filesystem path and project ownership.
- Solution and project files.
- Dependency and package files, lock files, and build scripts.
- Configuration files and safe configuration samples.
- UI assets, icons, fonts, and other assets required by the application.
- Existing tests, deployment notes, runbooks, and operational documentation.
- The approved clean tracked snapshot manifest and excluded-file report.

Do not commit credentials, tokens, connection strings, customer data, local
certificates, or secret configuration values. Redacted samples are acceptable
where safe. Do not merge raw POS Git history.

## Required configuration inputs

Capture from the supplied source and its approved environment. Unknown values
remain `[REQUIRES SOURCE REVIEW]` until verified from source and deployment
evidence.

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
| Deployment, certificate, browser policy, and rollback requirements | [REQUIRES SOURCE REVIEW] |
| OpenAPI surface and generated-client inputs | [REQUIRES SOURCE REVIEW] |

## Dependency review checklist

The source assessment must identify, for every dependency:

- Package, runtime, framework, or native component name and version.
- License and support constraints where relevant.
- Windows-only APIs, services, drivers, or machine-local assumptions.
- External executables, scripts, scheduled tasks, or process boundaries.
- Network endpoints, protocols, certificates, and proxy assumptions.
- Database providers, client tools, and supported SQL Server versions.
- Configuration and secret inputs required at build or runtime.
- Testability, replacement, and migration risks.
- Whether generated output can be reproduced in destination CI rather than
  imported as source.

## Operation classification checklist

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
failure behavior, audit needs, timeout, cancellation behavior, and whether it
can be represented as an explicit, typed, allow-listed operation. Do not claim
that any operation exists before source review.

| Operation | Classification | Target and side effects | Source evidence |
| --- | --- | --- | --- |
| [REQUIRES SOURCE REVIEW] | [REQUIRES SOURCE REVIEW] | [REQUIRES SOURCE REVIEW] | [REQUIRES SOURCE REVIEW] |

The future Agent must use the conservative trigger outcome boundary:

```text
Proven pre-dispatch input/policy/DNS/SSRF/cancellation rejection
→ NotAttempted

Positive acknowledgement
→ Accepted

Dispatch/connection/transport/timeout/cancellation ambiguity
→ OutcomeUnknown

Any received non-success remote trigger response,
without an authoritative side-effect-free remote contract
→ OutcomeUnknown
```

`Failed` requires future authoritative evidence that the remote operation was
not accepted. No generic HTTP status provides that evidence, and no automatic
retry follows `OutcomeUnknown`.

## Privilege and security checklist

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
operation values remain `[REQUIRES SOURCE REVIEW]`.

The accepted POS service identity is `LocalSystem`. This decision is preserved,
not reopened by INT-00, together with loopback-only hosting, local
Administrators authorization, server-owned allowlists, strict typed operation
policy, explicit SQL credentials, and explicit SMB credential flow. The gate
below remains open:

```text
ADR-012 LOCAL SYSTEM / SESSION 0 SMB:
OPEN - REPRESENTATIVE DEVICE EVIDENCE REQUIRED
```

The Agent must never widen its listener to LAN access as a workaround for LNA,
certificate, authentication, or browser-policy failure.

## Target architecture direction

The approved direct-transport direction is:

```text
Support Hub Angular
    |
    | HTTPS to fixed loopback Agent origin
    | LNA permission/policy
    | Windows Negotiate
    | exact origin/CORS + approved antiforgery contract
    v
RmsSupportHub.Pos.Agent
    |
    +--> POS Domain / Application / Contracts
    +--> Windows Infrastructure
          +--> SQL
          +--> SCM
          +--> SMB/filesystem
          +--> backup trigger HTTP
```

The general `RmsSupportHub.Api` is not in the initial privileged path. The
portable Support Hub `Core` and `Data` projects do not reference Windows POS
Infrastructure. The Agent owns the authoritative OpenAPI contract and the
Support Hub owns the final generated/typed Angular consumer. The standalone
POS Angular workspace is frozen/reference-only. WinUI is retained.

The future Agent origin uses the fixed shape:

```text
https://rms-pos-agent.localhost:<fixed-port>
```

The Agent binds only to loopback, uses HTTPS and a machine-trusted certificate,
and has no discovery, LAN hostname, wildcard listener, or certificate-warning
bypass. Managed Chrome/Edge deployment uses exact Support Hub origin entries in
`LocalNetworkAccessAllowedForUrls` and, where supported,
`LoopbackNetworkAllowedForUrls`. `AuthServerAllowlist` contains the exact Agent
hostname. Kerberos is preferred; NTLM fallback is acceptable pending evidence
unless a deployment baseline requires Kerberos-only behavior. Credential
delegation is not required, so `AuthNegotiateDelegateAllowlist` remains unset.

## Explicit prohibited generic execution surfaces

The future POS tool must never expose generic execution surfaces such as:

- Arbitrary PowerShell textbox.
- Arbitrary command prompt textbox.
- Arbitrary SQL textbox.
- Arbitrary script upload and run.
- Generic executable path launcher.

Future operations must be explicit, typed, and allow-listed. For example, a
future contract may expose `BackupDatabase(request)` or
`RestartApprovedService(serviceId)`, but must not expose a generic
`ExecuteCommand(...)` or `RunPowerShell(...)` surface. Concrete contracts wait
for source assessment and the integration implementation gate.

## Clean import and destination layout

The future import is a clean tracked source snapshot. Raw POS Git history merge
is prohibited. The approved destination concept is:

```text
/pos
  /src
    RmsSupportHub.Pos.Domain
    RmsSupportHub.Pos.Application
    RmsSupportHub.Pos.Contracts
    RmsSupportHub.Pos.Infrastructure
    RmsSupportHub.Pos.Agent
    PosAdminTool.WinUI
  /tests
    ...
```

Portable POS projects target `net10.0`. Windows Infrastructure and Agent
projects remain Windows-targeted. Existing Support Hub backend projects remain
portable. Do not create these directories or projects until INT-01 is
authorized.

The clean snapshot must exclude generated output, build/runtime directories,
local environment debris, secrets/certificates, and raw repository/history
metadata according to the approved source manifest. The original repository
remains historical evidence referenced by SHA.

## Future integration entry criteria

The future integration assessment is executable only when all of the following
are available:

- The owner has passed the `CLAUDE OPUS 5 POS INTEGRATION ARCHITECTURE
  CHECKPOINT`.
- Source provenance and the repository path are confirmed.
- Build and dependency files are available.
- Safe configuration samples are available where needed.
- UI assets required by the source application are available.
- A clean tracked snapshot has been reviewed for generated/runtime/history
  debris and secrets.
- No credentials are committed to the supplied source or intake materials.
- The Agent/browser LNA, Negotiate, certificate, CORS, and antiforgery
  evidence plan is approved.
- The owner explicitly authorizes INT-01.

If any item is missing, the future assessment must report that exact missing
intake item rather than inventing source facts, operations, dependencies, or
privileges. INT-01 remains blocked after INT-00.
