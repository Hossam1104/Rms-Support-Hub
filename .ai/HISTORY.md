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
| L-1 through L-4 deferred hardening closure | Branch `fix/pos-agent-deferred-hardening` off `548ff98`; new PR (unmerged, pending independent review) | Removed the two dead machine-trust-reloading parameterless constructors (L-1); added regression proof that metadata-only OpenAPI composition has no trust/lifecycle authority and ignores the obsolete config keys unconditionally (L-2); closed every early-return audit-completeness gap in the PowerShell lifecycle so each started/attempted operation reaches exactly one terminal outcome under its original OperationId (L-3); proved the normal lifecycle entry point cannot reach the Pester-only `TestOnlyTrustFixture` seam and retained it with that proof (L-4). Did not reopen ADR-0027 or any accepted trust-boundary decision. Validation: POS 420 passed/0 failed (+10 over the PR #21 baseline of 410), PowerShell Pester 172 passed/0 failed (+13 over the baseline of 159); footprint bounded to `pos/` and `scripts/` so the broad `.\scripts\build.ps1` regression was not re-run. |

## Current programme status

INT-00 through INT-13 and Slice B are repository-validated and merged to
`main`. Slice C remains subject to independent Terra review and external
representative-machine, Production signing/PKI, fleet, and customer gates.

No Production, customer, RMS, Main Server, database, registry, certificate,
SCM, browser-policy, or live package-activation mutation has been executed by
the Slice C work. No private key was exported or committed.
