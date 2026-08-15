# POS Slice C Requirements

Status: requirements only. Slice C is not implemented by the Slice B security
remediation.

Slice C is the Production/fleet deployment and evidence gate for the POS Agent.
It must preserve the direct browser-to-Agent boundary, the exact secure Support
Hub origin, the Agent-owned typed operation model, and the Testing-only proof
rules already recorded in [`POS_SLICE_B_BOUNDARY.md`](POS_SLICE_B_BOUNDARY.md).

## 1. Canonical Production POS entry

- The Hub must expose one canonical Production POS entry whose URL, TLS
  certificate identity, and Agent binding are fleet-managed and documented.
- The card and route must hand the browser directly to the managed Support Hub
  origin; the Support Hub API must not become a relay or generic Main Server
  proxy.
- The Agent must continue to require the exact configured Origin, Windows
  Negotiate identity, derived local Administrator authorization where needed,
  one-use mutation authorization, typed confirmation, and principal-scoped
  idempotency.
- No fallback origin, insecure alternate host, browser-supplied Agent URL, or
  user-selected machine target may be introduced.

## 2. Permanent Agent service and onboarding

- Ship one permanent SCM service named `RmsSupportAgent` with display name
  `RMS Support Agent`. The Testing-only `RmsSupportHub.Pos.Int13.TestService`
  must never be part of the Production package.
- Define zero-touch and on-demand onboarding with explicit UAC behavior,
  certificate issuance/trust, private-key ACLs, exact browser policy, service
  identity, service-owned directory ACLs, configuration, health checks, and
  rollback/uninstall behavior.
- Provide an enterprise-silent deployment path with signed packages,
  inventory/version evidence, repair and upgrade sequencing, and an operator
  escape path when onboarding cannot establish trust.
- Prove ownership before migrating or removing a legacy Testing/service entry;
  preserve unrelated services, certificates, registry values, and policy.
- Document upgrade, repair, uninstall, rollback, interrupted-operation
  recovery, and restart behavior. Each result must distinguish accepted,
  running, succeeded, failed, partial, unknown, rollback-failed, and
  recovery-required states.

## 3. Update, download, and asset health

Implement typed, server-owned evidence and lifecycle rules for the fixed RMS
sources documented in
[`POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md`](POS_RUNTIME_AND_MAIN_SERVER_API_DISCOVERY.md):

- `C:\ProgramData\Branch` and `C:\ProgramData\Cashier` update caches;
- `C:\ProgramData\RMS_Plus_Downloads` download backlog and activity;
- `C:\ProgramData\RMS_Plus_ReleaseRepo` release repository;
- `C:\ProgramData\RMS_Plus` installed release and setup evidence;
- the authenticated user's `AppData\Local\DBS_RMS+_POS` settings; and
- the product's insurance attachment metadata under
  `C:\ProgramData\DBS\POS`.

Every source requires fixed-root resolution, owner/ACL/reparse checks, bounded
file/age/byte/record limits, manifest/checksum/signature/trust validation where
applicable, and explicit business-state rules before mutation. The workflow
`ReleaseRepo` download -> operator install -> `RMS_Plus` setup must not be
inferred from folder presence alone.

Insurance evidence is metadata-only: existence, counts, bytes, age
distribution, oldest item, and capacity pressure. Slice C must never expose
attachment contents or raw sensitive filenames, and must not perform age-only
orphan deletion or default cleanup.

Product drift must compare the authoritative release, component builds,
installation identity, service identity, and approved package manifest without
guessing a version or treating a registry uninstall command as an executable
authorization.

## 4. Native diagnostics and Support Bundle

- Use the fixed native Branch/Cashier log roots and bounded Windows event
  sources as primary evidence. Keep the Diagnostic Console a constrained
  fallback with a fixed manifest, stopped-service precondition, exact image,
  empty child environment, bounded output, and child-only timeout cleanup.
- Apply the shared fail-closed redaction pipeline before timeline, artifact,
  snapshot, transport, or bundle retention. A sanitization failure quarantines
  the output and emits only a safe categorical result.
- Generate a Support Bundle from typed, principal-scoped, time-bounded safe
  summaries. It must exclude insurance contents, raw registry command values,
  credentials, tokens, private keys, connection strings, arbitrary paths,
  unbounded logs, and opaque child-process output.
- Native logs remain the primary Failure Analyzer source. SCM and crash events
  are bounded corroboration; recommendations never become generated commands
  or automatic repair.

## 5. Production audit and fleet evidence

- Define the Production certificate lifecycle: issuance, renewal,
  distribution, trust, private-key ACL, rotation overlap, revocation, and
  recovery. Define the managed Chrome/Edge policy lifecycle for LNA,
  loopback, Negotiate, and exact-host allow-lists.
- Collect representative and fleet evidence for the permanent service,
  LocalSystem/session behavior, exact-origin browser transport, certificate
  trust, authorization, bounded operations, package lifecycle, and uninstall
  safety. Testing evidence cannot be promoted as Production proof.
- Complete the M-1 managed browser policy and M-2 Production certificate
  lifecycle gates, then obtain the independent Claude Opus 5 High security
  review. The review must explicitly cover H-1/H-2/H-3, the adjacent M/L
  controls, and the final deployment artifacts.
- Compare the Whites environment against the UPC evidence without copying
  credentials, raw configuration, or customer data into repository memory.
- Record durable audit facts in `.ai/STATE.md`, `.ai/HISTORY.md`, and the
  appropriate decision record only after the evidence is reproducible.

## 6. POS workspace visual redesign (owner-approved direction)

The owner has approved a substantial modern visual redesign of the POS
Maintenance workspace **for Slice C**. It is deliberately not implemented in the
Testing provisioning remediation, and it must not be dropped from this plan.

Direction:

- Richer dimensional, layered design with controlled 3D depth; glass and depth
  surfaces used as structure, not decoration.
- Modern hover elevation, tactile buttons, and smooth panel transitions.
- Animated health indicators and animated service-state transitions.
- Health Check progress animation and Backup/Restore progress visualization.
- Repair workflow step animation and Incident Timeline motion.
- Visual gauges for storage, database, and update health.
- Improved loading and skeleton states.
- Modern dark mode and responsive layouts across the supported viewports.

Constraints:

- Honour `prefers-reduced-motion`; every animation must have a static
  equivalent that still communicates state.
- Full keyboard accessibility and visible focus must survive the redesign.
- No excessive gaming-style effects. No heavy WebGL unless a specific need is
  objectively justified and cheaper alternatives are shown to be insufficient.
- Styles continue to consume design tokens; raw colour literals stay in the
  designated token and gradient files.
- The redesign changes presentation only. It must not relax the exact Support
  Hub origin, HTTPS, Negotiate, Administrator authorization, direct
  browser-to-Agent transport, typed confirmation, or one-use mutation
  authorization, and must not surface filesystem paths, credentials, or raw
  Agent errors.

## 7. Explicit non-goals

Slice C does not authorize Production execution during implementation. It does
not authorize RMS installer/uninstaller/repair/rollback/package activation,
Main Server state-changing requests, database mutation, deletion of RMS data,
registry mutation, or broad filesystem cleanup without a later, explicit,
owner-approved execution gate.
