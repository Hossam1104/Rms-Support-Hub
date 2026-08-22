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
| Slice C foundation | PR #18; `0b380ef` | Permanent `RmsSupportAgent` identity, deployment, browser policy, durable audit, fixed RMS health, Support Bundle, operations console. |
| Production Agent lifecycle activation | `036bb62`; PR #21 | Machine-pinned package trust, publication envelopes, SCM lifecycle, ACL/certificate prerequisites, checkpoints, rollback/recovery, health gates. |
| PR #21 rollback & root-of-trust hardening | Commits `59f8015`, `da16529`; PR #21 | Sealed ProgramData trust authority, mandatory signer pins, startup snapshot freezing, correlated lifecycle audit, and verified rollback payloads. |
| PR #21 acceptance & merge | Opus 5 review (0 Crit/0 High/0 Med); Sol approval; PR #21 merged | Accepted with L-1..L-4 deferred hardening; Production rollout gated on external PKI/fleet evidence. |
| PR #22 deferred hardening merge | Opus 5 review; Sol approval; PR #22 merged to `main` | Closed L-1 (dead constructors), L-2 (OpenAPI metadata isolation), L-3 (audit completeness), and L-4 (retained fixture isolation). |

## Current programme status

| Milestone | Evidence | Outcome |
|---|---|---|
| P0-A server-owned Testing environment authority | Commits `08a54a8`, `500a8b3`, `a05eb5e`; PR #23 | Server-side deployment tier and registered environment resolution; removed browser DB/URL authority; closed M-1; Sol accepted; merged to `main`. |
| P0-B deterministic Testing/Staging release pipeline | Branch `feat/staging-release-candidate-pipeline`; PR #24 | Integrated Support Hub CI, fixed build identity, manifests, integrity hashes, offline runtime independence, sanitized Testing defaults. |
| P0-B review, remediation & SDK toolchain correction | Commits `6ec14dc`, `30d3339`, `9af49d6`, `f8a7af3`, `b6baecd`; PR #24 | Closed H-1, M-1, M-2, M-3; re-pinned .NET SDK 10.0.400 repo-wide; Support Hub CI `32184944831` & POS CI `32184944709` green. |
| P0-B final Sol acceptance & merge | Opus APPROVE; Sol merge authorization; PR #24 merged to `main` | Final review passed; .NET SDK 10.0.400 pinned repo-wide; merged to `main`. |
| P0-C external server configuration support | Hosting Bundle 10.0.11 / ANCM v2; PR #25; Sol ACCEPT | Server-owned external JSON loader discovering `SUPPORTHUB_EXTERNAL_CONFIG_PATH` outside content root with strict URL/UNC rejection. Merged to `main`. |
| P0-C HOSSAM Testing IIS deployment & read-only acceptance | Merge `13d5906`; Support Hub CI `32308207050`; RC Build `c372eca` | Deployed Testing RC to local IIS `RmsSupportHub.Testing` (8080). Verified all 7 read-only acceptance probes with 100% success. |
| P0-D GHC/Uni-Commerce Testing integration | PR #26; merge `fce6a66`; Support Hub CI `32448707628` | Delivered bounded GHC and Uni-Commerce Testing integration with verified schemas, request history, draft persistence, consumer lookup. Merged to `main`. |
| P0-E Downstream rejection diagnosis | `docs/p0-e-downstream-rejection-diagnosis/README.md` | Proved downstream rejection causes: GHC delivery fields/VAT alignment; Uni gateway `X-Api-Key` and 4-decimal `ItemVat`. |
| P0-F Production mutation gate & downstream remediation | Commit `7141b84`; PR #30; backend 317, frontend 379 | Added server-enforced Production unlock gate, read-only Order Requests while locked, capability-aware actions, GHC delivery/VAT fixes, Uni 4-decimal VAT. |
| P0-F Sol remediation & security hardening | Commits `5c025fec`, `e0493f5`; Support Hub CI `32534444783` | Bounded throttling caches, fail-closed builder metadata/API-key contracts, effective-HTTPS, Secure session cookies, empty trusted-forwarding allowlists, Production HSTS. |
| P0-F Sol acceptance & merge | Merge `9272041638e2da97ac6ff5e4e251c2d370acc47e`; PR #30 merged to `main` | GPT-5.6 Sol accepted PR #30 after full security review and CI verification. Production mutation unlock gates and GHC/Uni fixes merged to `main`. |
| WPF Architecture Rebaseline & Azure Backlog Synchronization | Branch `docs/wpf-agent-architecture-rebaseline`; CR-001; ADR-0029; Azure Epics E16..E19 (#13017–#13020) and 51 stories (#13021–#13071); BRD v1.1 | Owner approved WPF dual control-surface architecture for POS maintenance. Documented CR-001 (BR-027..BR-040), ADR-0029, and 10-phase conversion roadmap (Phases 0–9). Created Azure iterations POS-07..POS-10, Epics E16–E19, 51 User Stories; reconciled E11 (closed as superseded roadmap) and E12. Synchronized traceability matrices. No runtime product code modified; no Production contact; Production readiness remains NO. |

INT-00 through INT-13, Slice B, Slice C, P0-A, P0-B, P0-C, P0-D, and P0-F are repository-validated, merged to `main`, and deployed/verified on local Testing where authorized. P0-E diagnosis is completed and documented. WPF Architecture Rebaseline (CR-001, ADR-0029, E16–E19) is established.

Authorized HOSSAM local Testing deployment and activation occurred; no Production, customer, Main Server, RMS, database, native-service, PKI, or fleet mutation occurred, and no shared/customer Testing integration mutation occurred. Downstream GHC/Uni rejection diagnosis (#12892, #12899) completed; real POS release PKI and final integrated Online Order + POS smoke are re-baselined under E19; Production readiness remains NO.

## WPF-01

| Milestone | Evidence | Outcome |
|---|---|---|
| WPF-01 Shared Agent Application + Local IPC Foundation | Delivery commits on `feat/wpf-01-shared-agent-local-ipc`; Draft PR #32; final POS/Support Hub CI green | Added the shared invocation/application seam, typed RMS installation discovery adapter, bounded Windows Named Pipe client/server with explicit three-principal ACL, protocol/error bounds, generated client contract, and focused authorization/transport/parity tests. Awaiting GPT-5.6 Sol review; no WPF UI, SignalR, Production, native RMS, or customer database changes. |
| WPF-01 Sol security remediation | Commits `ab4bf1b`, `1c26e55` on `feat/wpf-01-shared-agent-local-ipc`; Draft PR #32 remains Draft | Closed S01-S09 with least-privilege operator ACL, explicit NETWORK deny, LocalSystem server-token verification, local-only group resolution, truthful invocation propagation, strict correlation matching, source-coherent authorization, and fail-closed diagnostic audit semantics. Stable AccessControl 5.0.0 was tried, then removed because .NET 10 provides the API and strict CI treats NU1510 as an error. Local POS build/tests pass; hosted CI still has unrelated Infrastructure ACL-fixture and Support Hub external-configuration failures. No WPF UI, SignalR, Production, native RMS, or customer database changes. |
