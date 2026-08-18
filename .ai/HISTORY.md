# Completed Work History

Concise milestone index. Detailed implementation evidence belongs in Git and
the linked planning/evidence documents.

## Platform and Support Hub

| Milestone | Evidence | Outcome |
|---|---|---|
| Flask to .NET/Angular rewrite and R0-R10 | `936bdda`..`b011ffb` | Replaced the Flask baseline and corrected payload/SQL contracts, capability routing, drafts, history, and legacy-tree boundaries. |
| UI, branding, and Order Requests programme | `682fd55`..`d3219dd`; `44006a4`..`51ad1e4` | Established Testing defaults, token-owned UI, RMS+ identity, shared cards, canonical request history, guarded resend, and atomic reset. |
| Final cleanup/readiness | Git history and readiness docs | Removed superseded plans/source and recorded the POS integration boundary. |

## POS Maintenance integration

| Milestone | Evidence | Outcome |
|---|---|---|
| INT-00 through INT-05F | Architecture docs and POS restore/build history | Isolated the POS solution, direct loopback Agent transport, HTTP/1.1, exact-origin CORS, Negotiate, certificate, mutation-token, OpenAPI, and generated-client boundaries. |
| INT-CI01 through INT-08 | `c560d97`; PRs #3-#5 | Restored CI determinism, completed admin authorization/browser contract review, and added typed read and opaque service-control operations without live Production mutation. |
| INT-13 / INT-13C / INT-13D | `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`; PR #7 | Closed authorized Testing transport/browser/IWA evidence at the exact origins; no Production or customer execution was performed. |
| RMS discovery and database recovery | PR #9; `615fda2` | Added fixed RMS evidence and durable, typed Branch/Cashier backup/restore boundaries with fail-closed recovery truth; live writes were not authorized. |
| POS downloader, cleanup, maintenance, and Slice A | `de3dc8f`; PR #12 | Added typed Agent-owned maintenance workflows, diagnostics, timeline, Support Bundle, generated contracts, and the operations workspace. |
| Slice B security remediation | PR #14; `19f609b` | Closed H-1/H-2/H-3 with bounded redaction, fixed roots, one machine-wide mutation lease, typed POST previews, and exact endpoint binding. |
| Testing runtime/security gates | PRs #15 and #17 | Added PowerShell parser gates, secure `:4443` ownership/build identity, exact-origin handoff, and Testing-only semaphore proof. |

## POS Slice C

| Milestone | Evidence | Outcome |
|---|---|---|
| Slice C foundation | PR #18; `0b380ef` | Added permanent `RmsSupportAgent` identity/migration, plan-first deployment, browser/certificate policy, durable audit, fixed RMS health, Support Bundle, and operations-console contracts. |
| Production Agent lifecycle activation | `036bb62`; PR #21 | Added machine-pinned package trust, deterministic publication/envelopes, typed SCM lifecycle, ACL/certificate prerequisites, checkpoints, rollback/recovery, and health-gated activation. |
| PR #21 trusted rollback/recovery hardening | `59f8015`; PR #21 | Anchored recovery to checkpoint `PreviousVersion`, retained signed manifest+archive only, re-extracted and re-verified rollback payloads, health-gated recovery, preserved explicit rollback recovery, and fixed PowerShell 5.1 seams. |
| PR #21 root-of-trust remediation | `da16529`; PR #21 | Sealed the exact non-configurable ProgramData trust authority, removed synthetic OpenAPI trust in favor of metadata-only composition, made both signer pins mandatory in C# and PowerShell, froze verifier authority to the startup snapshot, correlated PowerShell lifecycle audit events by operation ID, rejected obsolete configuration presence, and preserved rollback/certificate/H-1/H-2/H-3 controls. Validation evidence: POS 410, backend 194, Pester 159, PowerShell quality 29 files, broad build, frontend production build, and memory checks passed with no skips. |
| PR #21 acceptance and merge | Opus 5 HIGH review; Sol merge authorization; PR #21 merged to `main` | Independent review found 0 Critical/High/Medium; merge approved with four deferred non-blocking Low hardening items (L-1 through L-4, recorded in `.ai/STATE.md`); Production/fleet rollout remains gated on external signer/PKI/fleet/customer evidence. |
| PR #22 deferred hardening acceptance and merge | Opus 5 HIGH review (0 Crit/0 High/0 Med/3 Low); Sol merge authorization; PR #22 merged to `main` | Closed L-1 (dead constructors removed), L-2 (OpenAPI metadata isolation proved), L-3 (early-return audit completeness), and L-4 (retained test fixture proved unreachable from normal entry). Three Low findings recorded as non-blocking backlog debt. Validation: POS 420 passed (Domain 12, Application 82, Infrastructure 155, Agent integration 171), Pester 172 passed, PowerShell quality 29 clean, backend 194 passed. |

## Current programme status

| Milestone | Evidence | Outcome |
|---|---|---|
| P0-A server-owned Testing environment authority & M-1 closure | Commits `08a54a8`, `500a8b3`, `a05eb5e`; PR #23; backend 252 passed, frontend 362 passed across 59 files, production and broad builds passed | Enforced server-side deployment tier and registered environment resolution; removed browser database/URL authority; fail-closed `DeploymentTierParser` strict allowlist closed M-1; Opus re-review 0 Critical / 0 High / 0 Medium; Sol merge authorization; merged to `main`. L-1..L-3 and N-1..N-2 deferred as non-blocking debt. P0-B release candidate pipeline is next phase. |
| P0-B deterministic Testing/Staging release candidate pipeline | Implementation branch `feat/staging-release-candidate-pipeline`; backend 253/253, frontend 362/362 across 59 files, production/broad builds, Riyal/offline scans, deterministic ZIP repeat, fresh extraction, and packaged runtime smoke passed | Added integrated Support Hub CI, fixed source/build identity, release/configuration manifests, integrity hashes and ZIP sidecar, clean extraction verification, local health endpoints, offline runtime independence, package exclusions, Testing template, deployment/rollback/smoke documentation, and N-2 omitted-tier coverage. No IIS, Production, customer, RMS, or order mutation was performed. |
| P0-B Git delivery and exact-head CI | Draft PR #24 at `3f9bed46d9227b31090e5fcf61dbe65369f76ec5`; Support Hub CI passed; POS CI had one unrelated 42-test ACL/rollback failure | Branch pushed for independent review; no merge, deployment, Production/customer, RMS, or order mutation performed. |


INT-00 through INT-13, Slice B, and Slice C (including PR #22 deferred hardening)
are repository-validated and merged to `main`. Slice C remains subject to
external representative-machine, Production signing/PKI, fleet, and customer
gates.

No Production, customer, RMS, Main Server, database, registry, certificate,
SCM, browser-policy, or live package-activation mutation has been executed by
the Slice C work. No private key was exported or committed.
