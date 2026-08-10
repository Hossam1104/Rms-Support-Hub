# ADR-0017: Clean POS snapshot and isolated project boundary

- Status: Accepted; source-intake evidence remains open
- Affected area: POS import, repository history, project layout, portable/Windows boundary

## Context

The POS project is developed independently. Its historical Git repository and
generated/runtime material are not part of the Support Hub application
contract. A raw history merge would import unrelated branches, build debris,
credentials, and repository policy into the destination.

## Decision

The future import strategy is:

```text
IMPORT STRATEGY: CLEAN TRACKED SOURCE SNAPSHOT
RAW POS GIT HISTORY MERGE: PROHIBITED
```

The source provenance is the verified POS `main` SHA
`25922b499d33bd73f241ffc26c212dd000e81433`. The original repository remains
historical evidence referenced by SHA. A future snapshot must be reviewed for
generated output, `bin/`, `obj/`, frontend dependency/build output, runtime
state, local certificates, secrets, and raw repository/history debris before it
is copied into the destination. No source is imported by INT-00 and the POS
repository is not modified.

The isolated destination concept is:

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
portable. The POS Agent is the Windows composition root; the Support Hub
`Core`/`Data` layers do not gain Windows Infrastructure references.

The standalone POS Angular workspace is frozen/reference-only. Support Hub
owns the final Angular package, lockfile, route, UI, primitives, and generated
typed client. WinUI is retained and is not cut over by this decision.

These directories and projects are not created during INT-00.

## Consequences

- The import has a reviewable content boundary and no accidental raw-history
  merge.
- Generated output can be reproduced in destination CI instead of becoming
  an opaque source dependency.
- Portable Support Hub code remains deployable without Windows-only references.
- Source review, clean-snapshot verification, and destination path ownership
  remain prerequisites for INT-01.
