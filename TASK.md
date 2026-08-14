# POS Final Functional Integration + UX + Production Packaging

## Role and objective

**Role:** `Implement / Validate / Deliver`
**Repository:** `Hossam1104/Rms-Support-Hub`
**Scope:** consolidate the completed POS Agent capabilities into the final
operator experience and prepare the real Agent installation/package boundary.

Start from a synchronized `main` containing the completed POS Downloader /
Deployment + Cleanup / Maintenance slice. Verify the actual `main` head before
working; do not assume a commit hash from this task file.

This is an implementation task. Do not run a separate planning cycle. Work as
the only active executor unless the owner explicitly changes that instruction.

The following POS capabilities are complete inputs to this task and must be
preserved rather than redesigned:

- Agent health, Windows Negotiate session, exact-origin transport, local
  Administrators authorization, device/configuration/connectivity/service reads,
  and typed service control.
- RMS installation discovery and sanitized Branch/Cashier diagnostics.
- Typed Agent-owned Branch/Cashier database backup and restore, including
  canonical targets, fixed roots, durable approved database-backup catalog,
  opaque artifact handles, exact restore confirmation, bounded native SQL,
  target-specific service coordination, one-use mutation tokens, bounded
  idempotency/concurrency, principal-scoped REST/SSE operation truth, and safe
  audit evidence.
- Typed downloader branch selection, server-owned trigger/discovery/download,
  opaque browser artifact download, principal-scoped operation state/SSE, and
  explicit `outcomeUnknown` truth.
- Typed maintenance cleanup and branch-reset preview/execute, server-owned
  policy recomputation, one-use principal-bound preview challenges, exact
  confirmation, mutation tokens, bounded idempotency/concurrency, and
  partial/recovery/unknown outcome evidence.
- Generated Agent OpenAPI and Angular client contracts, plus the first
  `/tools/pos-maintenance` operator rail for downloader artifacts and
  maintenance previews.

## Mandatory invariants

Keep all of the following unchanged:

- Windows Negotiate authentication and server-resolved local Built-in
  Administrators authorization; UAC elevation is not an authorization shortcut.
- Exact Support Hub Origin, loopback Agent binding, HTTP/1.1 constraints, and
  fail-closed SID handling.
- One-use mutation tokens bound to the authenticated principal, exact method,
  operation, target, path, and Origin. Preview challenges remain principal-
  bound, expiring, exact-confirmation, and one-use.
- Browser requests contain only bounded typed inputs. No raw SMB credentials,
  connection strings, SQL, filesystem paths, Windows service names, generic
  process/PowerShell/filesystem endpoints, or uploaded scripts may cross into
  Angular.
- Artifact and operation identifiers remain opaque. Artifact access remains
  principal-scoped, bounded, expiring, and safe for Content-Disposition.
- Ambiguous trigger, cleanup, reset, backup, restore, or service outcomes are
  retained as unknown/recovery truth and are never automatically retried.
- Backend dependencies continue Core -> Data/Infrastructure -> API/Agent with
  the Agent as the composition root. Existing downloader and maintenance
  services remain the typed application seam.
- Component styles consume design tokens; do not introduce raw color literals
  outside the designated token/gradient files.
- Testing is the only environment for agent-run live verification. Never call,
  cancel, resend, reset, restore, or clean Production/customer installations.

## Workstream A - final operator experience

Make `/tools/pos-maintenance` one coherent operator workspace instead of a
collection of adjacent feature shelves.

1. Re-read the existing POS component, direct Agent transport, generated client,
   shared UI primitives, and the current typed contracts before editing. Keep
   the existing RMS recovery controls intact; do not regress the existing
   service-control confirmation flow.
2. Integrate discovery, diagnostics, service status/control, downloader branch
   selection, artifact results, cleanup/reset previews, and database
   backup/restore into a clear information hierarchy. The page must make the
   server-owned target and current authorization state obvious before showing a
   destructive control.
3. Preserve truthful lifecycle UX for accepted, running, completed, failed,
   not-attempted, partial/recovery-required, and outcome-unknown states. Long
   operations must continue to refresh through the existing REST/SSE pattern or
   an explicitly bounded equivalent; a stale accepted response must not look
   complete.
4. Keep exact confirmation phrases and destructive controls opt-in. Expired,
   replayed, mismatched, unauthorized, or unavailable challenges/tokens must
   leave the UI in a recoverable state with a new-preview path.
5. Make the operator rail responsive, keyboard accessible, screen-reader
   legible, and safe under refresh/navigation. Preserve reduced-motion and
   existing design-token/style-budget requirements.
6. Use typed generated-client models at the transport boundary. Do not add
   `any`, generic URL builders, raw path display, client-side policy decisions,
   or browser-side guesses about RMS services/databases/tables.
7. Exercise artifact downloads through authenticated fetch and a sanitized
   filename. Do not create a browser link from a server path or place a token
   in a URL/query string.

## Workstream B - real Agent installation and package boundary

Turn the current build/provisioning pieces into a reviewable, repeatable Agent
installation boundary without performing a Production deployment.

1. Inspect and reuse the existing `scripts/` provisioning and packaging
   surfaces, including `publish-iis.ps1`,
   `setup-pos-agent-testing.ps1`, `start-pos-agent-testing.ps1`,
   `remove-pos-agent-testing.ps1`, `PosAgentWindowsProvisioning.psm1`,
   `PosSupportHubProvisioning.psm1`, and `PosTestingConfiguration.psm1`.
   Do not create a second competing installer model.
2. Define and implement the real Agent package shape: versioned publish output,
   executable/configuration boundaries, service identity/startup behavior,
   ownership and ACL expectations, trusted certificate/private-key handling,
   DPAPI or environment-backed secret placement, health checks, and an
   idempotent install/upgrade/uninstall/rollback path. Credentials and private
   keys must never be committed or embedded in a package.
3. Keep Testing-only provisioning explicitly separated from the eventual
   Production package. Testing manifests and disposable services may be
   created or cleaned only with ownership checks and the documented
   `IUnderstandTestingOnly` guard. `WhatIf` and fail-closed conflict behavior
   must remain available.
4. Ensure the package boundary starts the Agent with the exact fixed loopback
   HTTPS/HTTP/1.1 contract, exact Support Hub Origin configuration, and the
   server-owned storage roots required by the downloader, generic artifacts,
   and durable RMS database-backup catalog.
5. Add package/install verification for clean-machine layout, service ACLs,
   certificate selection, secret absence from logs/output, upgrade idempotence,
   removal ownership, and health endpoint readiness. If a required step needs
   elevation, record the exact evidence still missing rather than attempting a
   UAC workaround.
6. Keep the final Production fleet gate open for the two external decisions:

   - **M-1 - managed browser policy:** organization-managed Chrome/Edge policy
     and Local Network Access/IWA behavior for the exact Support Hub and Agent
     origins.
   - **M-2 - Production certificate lifecycle:** issuance, trust distribution,
     renewal/rotation, private-key ACL, hostname, and rollback ownership for
     the Production Agent/Support Hub certificate chain.

   Document these gates and their owners/evidence requirements; do not claim
   them resolved through local Testing provisioning.

## Workstream C - integration and test evidence

Add focused automated coverage for the final operator journey and package
boundary without touching real customer state.

- Retain all existing POS domain/application/infrastructure/Agent tests and
  extend the Agent integration suite for cross-feature authorization,
  principal isolation, token/challenge replay and expiry, idempotent replay,
  operation refresh/SSE terminal truth, artifact expiry/missing/wrong
  principal, and no-sensitive-field response assertions.
- Add frontend tests for authorized/non-authorized rendering, disabled and
  recovery states, exact confirmation handling, outcome-unknown messaging,
  artifact download behavior, refresh resilience, and keyboard-accessible
  destructive controls.
- Add package/provisioning tests or deterministic WhatIf/offline checks for
  package contents, versioning, ownership, ACL/certificate decisions,
  idempotent reruns, and cleanup refusal outside owned Testing targets.
- Use synthetic/fake SQL, filesystem, SMB, HTTP, service, and certificate seams
  for destructive-path automation. No real Restore, branch reset, cleanup, or
  customer database write is authorized.
- Carry forward the pending non-destructive representative-device proof for
  Branch and Cashier Backup, approved artifact/checksum retention across an
  Agent restart, and any safe downloader/health evidence. An elevated
  Administrator session may be required; record the precise blocker if it is
  unavailable.

## Required source and documentation review

Read only the task-related sources before changes, then expand when concrete
references require it:

- `pos/src/RmsSupportHub.Pos.Agent/Program.cs`, all Agent endpoint/runtime
  files, mutation-token/security middleware, operation stores, and OpenAPI
  transformers.
- `pos/src/RmsSupportHub.Pos.Contracts/V1/**` for the current typed contract.
- `frontend/src/app/features/pos-maintenance/**`, the direct Agent transport,
  generated client, shared UI primitives, and POS styles.
- `scripts/` packaging/provisioning files named above and their related tests.
- `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` and the current
  `.ai/STATE.md` for live-gate history.
- `.ai/PROJECT.md`, `.ai/DECISIONS.md`, and the matching ADR only when a stable
  architecture or existing decision is affected.

Do not redesign the completed RMS database backup/restore slice. Do not edit
generated/runtime directories (`bin/`, `obj/`, `node_modules/`, `dist/`,
`.angular/`, `var/`) by hand.

## Validation and delivery

Run targeted checks first, then the broad gates:

```powershell
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo -warnaserror
dotnet test pos/RmsSupportHub.Pos.slnx --nologo

Set-Location frontend
npm test -- --watch=false --no-progress
npm run build -- --configuration production
npm run generate:pos-agent-client
Set-Location ..

git diff --check
```

Verify a second OpenAPI/client generation pass is byte-stable, scan the
task-related diff for credentials/connection strings/raw paths, inspect the
final diff, and distinguish unavailable elevated live evidence from automated
failures. If the repository's broad build script covers the same gates, run it
and retain the exact result.

After implementation and validation, use the normal repository governance:

1. Commit only intentional task changes on a feature branch.
2. Push the branch and open the normal PR with validation evidence.
3. Merge only after required checks/governance pass, then synchronize local
   `main` with `--ff-only` and leave a clean tree.
4. Runtime restart/probing is last. Stop only stale project-owned Support Hub
   development processes when necessary, run `scripts/dev.ps1` if authorized,
   probe actual responding frontend/backend/Agent health endpoints, and leave
   successful owner processes running. Never report a URL from configuration
   alone.

## Non-goals and completion response

Do not execute a Production/customer deployment, close M-1 or M-2, perform a
real Restore, mutate a real RMS database, broaden the Agent into a generic
filesystem/process/PowerShell/SQL API, or redesign the already-complete RMS
database recovery contracts.

At completion, update `.ai/STATE.md` with durable facts, set `.ai/HANDOFF.md`
to `Empty`, record the milestone concisely in `.ai/HISTORY.md`, and update
`.ai/PROJECT.md` only for lasting architecture/package conventions. Return only:

### Result
Completed, Partially Completed, or Blocked.

### Changes
Concise task-related changes by file.

### Validation
Commands executed and results.

### Remaining
Only unresolved work, blockers, or risks.
