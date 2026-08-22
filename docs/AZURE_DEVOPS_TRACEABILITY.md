# RMS+ Support Hub — Azure DevOps Traceability Matrix

**Azure Organization:** `DBSMENA`
**Azure Project:** `Rms_Support_Hub`
**Process Template:** `Agile-RMS` (Hierarchy: Epic -> User Story)
**Rebaseline Date:** `2026-08-22`
**BRD Source:** [`BRD.md`](../BRD.md) (Version 1.1)
**Architecture Rebaseline:** [`docs/CR-001_WPF_AGENT_ADMIN_SUPERVISION.md`](CR-001_WPF_AGENT_ADMIN_SUPERVISION.md) / [ADR-0029](../.ai/decisions/ADR-0029-wpf-agent-dual-control-and-admin-supervision.md)
**Backlog Blueprint:** [`docs/AZURE_DEVOPS_BACKLOG.md`](AZURE_DEVOPS_BACKLOG.md)

## Delivery Hierarchy Summary

- **Total Epics:** 19 (10 Closed, 2 Active, 7 New/Planned)
- **Total User Stories:** 175 (96 Closed, 1 Active, 78 New/Planned/Conditional)
- **Total Work Items:** 194
- **Closed (Done / Superseded):** 96 Stories / 10 Epics
- **Active (Ongoing Governance & Active Integrations):** 1 Story / 2 Epics (E06, E15)
- **New (Planned / Architecture Baseline):** 78 Stories / 7 Epics (E12, E13, E14, E16, E17, E18, E19)

## Azure Classification Structure

Azure DevOps uses **Area Paths** and **Iteration Paths** with distinct, orthogonal responsibilities:

- **Area Path** answers *"What part of RMS+ Support Hub does this work belong to?"* — representing functional / product domain ownership.
- **Iteration Path** answers *"In what delivery phase was/is this work performed?"* — representing delivery milestones and phases without calendar sprint assumptions.

### Product Areas (Area Paths)
- `Rms_Support_Hub\Platform` — Unified application shell, shared infrastructure, CI/CD, deployment, and cross-domain governance.
- `Rms_Support_Hub\QA` — Prompt Studio authoring tools, test case generation, and export utilities.
- `Rms_Support_Hub\Online Orders` — Integration adapters, environment selection, payload authoring, and request lifecycle (UPC, GHC, Uni-Commerce, future OMS/Call Center).
- `Rms_Support_Hub\POS` — Local POS Support Agent service, Windows Named Pipes IPC, WPF standalone desktop, SignalR outbound Hub connectivity, and machine-owned package trust lifecycle.

### Delivery Phases (Iteration Paths)
- **QA Delivery Phases:**
  - `Rms_Support_Hub\QA-01 - Prompt Studio`
  - `Rms_Support_Hub\QA-02 - Future Enhancements`
- **Online Orders Delivery Phases:**
  - `Rms_Support_Hub\OO-01 - Core Platform`
  - `Rms_Support_Hub\OO-02 - UPC E-Commerce`
  - `Rms_Support_Hub\OO-03 - GHC E-Commerce`
  - `Rms_Support_Hub\OO-04 - GHC Uni-Commerce`
  - `Rms_Support_Hub\OO-05 - Integrated Testing`
  - `Rms_Support_Hub\OO-06 - Production Readiness`
  - `Rms_Support_Hub\OO-07 - Future Integrations`
- **POS Delivery Phases:**
  - `Rms_Support_Hub\POS-01 - Secure Agent Foundation` (Historical)
  - `Rms_Support_Hub\POS-02 - Diagnostics and Recovery` (Historical)
  - `Rms_Support_Hub\POS-03 - Package Lifecycle and Security` (Historical)
  - `Rms_Support_Hub\POS-04 - Local Integration and Acceptance` (Historical)
  - `Rms_Support_Hub\POS-05 - Production Readiness` (Planned)
  - `Rms_Support_Hub\POS-06 - Operational Hardening` (Planned)
  - `Rms_Support_Hub\POS-07 - WPF Agent Architecture` (New Target — E16)
  - `Rms_Support_Hub\POS-08 - WPF Local Experience` (New Target — E17)
  - `Rms_Support_Hub\POS-09 - Admin Fleet Supervision` (New Target — E18)
  - `Rms_Support_Hub\POS-10 - WPF Migration and Rollout` (New Target — E19)
- **Platform Delivery Phases:**
  - `Rms_Support_Hub\PLAT-01 - Platform Foundation`
  - `Rms_Support_Hub\PLAT-02 - Release and Testing Deployment`
  - `Rms_Support_Hub\PLAT-03 - Integration Acceptance`
  - `Rms_Support_Hub\PLAT-04 - Production Readiness`
  - `Rms_Support_Hub\PLAT-05 - Operational Hardening`
  - `Rms_Support_Hub\PLAT-06 - Governance and Traceability`

## Traceability Matrix

| Alias | Azure ID | Type | Title | Parent | State | Area | Iteration | BRD | Evidence / Status |
|---|---:|---|---|---|---|---|---|---|---|
| **E01** | 12839 | Epic | [E01] Platform Foundation & Unified Support Hub | — | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-01 - Platform Foundation | BR-001, BR-002, BR-024, BR-025 | [PR #19](https://github.com/Hossam1104/Rms-Support-Hub/pull/19) |
| **E02** | 12840 | Epic | [E02] QA Prompt Studio | — | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005, BR-025 | [Repository](https://github.com/Hossam1104/Rms-Support-Hub) |
| **E03** | 12841 | Epic | [E03] Online Order Core Platform | — | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-003, BR-004, BR-006, BR-007, BR-008, BR-009, BR-023, BR-024 | [PR #23](https://github.com/Hossam1104/Rms-Support-Hub/pull/23) |
| **E04** | 12842 | Epic | [E04] UPC E-Commerce | — | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-003, BR-004, BR-006, BR-007, BR-008, BR-009, BR-010, BR-011, BR-012 | [PR #23](https://github.com/Hossam1104/Rms-Support-Hub/pull/23) |
| **E05** | 12843 | Epic | [E05] GHC E-Commerce | — | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-003, BR-004, BR-006, BR-007, BR-008, BR-009, BR-010, BR-011 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) / [PR #30](https://github.com/Hossam1104/Rms-Support-Hub/pull/30) |
| **E06** | 12844 | Epic | [E06] GHC Uni-Commerce | — | **Active** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-003, BR-004, BR-006, BR-007, BR-008, BR-009, BR-010, BR-023 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) / [PR #30](https://github.com/Hossam1104/Rms-Support-Hub/pull/30) (Active — 3 stories conditional) |
| **E07** | 12845 | Epic | [E07] POS Maintenance — Secure Agent Foundation | — | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-013, BR-014, BR-015, BR-016, BR-017, BR-020, BR-023 | [PR #7](https://github.com/Hossam1104/Rms-Support-Hub/pull/7) |
| **E08** | 12846 | Epic | [E08] POS Maintenance — Diagnostics & Recovery | — | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-016, BR-017, BR-018, BR-020 | [PR #9](https://github.com/Hossam1104/Rms-Support-Hub/pull/9) |
| **E09** | 12847 | Epic | [E09] POS Maintenance — Package Lifecycle & Security | — | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-015, BR-019, BR-020, BR-023 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **E10** | 12848 | Epic | [E10] Release, CI & Testing Deployment | — | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-003, BR-021, BR-022, BR-023, BR-025 | [PR #24](https://github.com/Hossam1104/Rms-Support-Hub/pull/24) |
| **E11** | 12849 | Epic | [E11] Local Integrated Testing Acceptance | — | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-03 - Integration Acceptance | BR-003, BR-004, BR-013, BR-015, BR-019, BR-023 | Superseded by CR-001 / E16-E19 |
| **E12** | 12850 | Epic | [E12] Production Readiness & Controlled Rollout | — | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-04 - Production Readiness | BR-003, BR-015, BR-019, BR-021, BR-022, BR-023, BR-025, BR-026 | Planned roadmap |
| **E13** | 12851 | Epic | [E13] Future Online Order Integrations | — | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-07 - Future Integrations | BR-006, BR-024 | Planned roadmap |
| **E14** | 12852 | Epic | [E14] Operational Hardening & Observability | — | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-017, BR-020, BR-023 | Planned roadmap |
| **E15** | 12853 | Epic | [E15] Delivery Governance & Traceability | — | **Active** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-06 - Governance and Traceability | BR-025 | [PR #27](https://github.com/Hossam1104/Rms-Support-Hub/pull/27) |
| **E16** | 13017 | Epic | [E16] Agent Platform Re-Architecture | — | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-027, BR-030, BR-032, BR-033, BR-034, BR-035, BR-036, BR-037 | CR-001 / ADR-0029 |
| **E17** | 13018 | Epic | [E17] WPF Standalone Local Operations | — | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-027, BR-028, BR-030, BR-034, BR-036, BR-038, BR-039 | CR-001 / ADR-0029 |
| **E18** | 13019 | Epic | [E18] Admin Fleet Supervision & Remote Support | — | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-030, BR-031, BR-032, BR-033, BR-035, BR-037, BR-038, BR-040 | CR-001 / ADR-0029 |
| **E19** | 13020 | Epic | [E19] WPF Migration, Compatibility & Rollout | — | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-019, BR-021, BR-027, BR-038, BR-039 | CR-001 / ADR-0029 |
| **US-E01-01** | 12854 | User Story | [US-E01-01] Unified application shell | E01 (#12839) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-01 - Platform Foundation | BR-001, BR-024 | [PR #19](https://github.com/Hossam1104/Rms-Support-Hub/pull/19) |
| **US-E01-02** | 12855 | User Story | [US-E01-02] Shared responsive design system | E01 (#12839) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-01 - Platform Foundation | BR-001 | Merged to main |
| **US-E01-03** | 12856 | User Story | [US-E01-03] Central API composition | E01 (#12839) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-01 - Platform Foundation | BR-001, BR-004 | Merged to main |
| **US-E01-04** | 12857 | User Story | [US-E01-04] Health and readiness endpoints | E01 (#12839) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-01 - Platform Foundation | BR-001, BR-004 | Merged to main |
| **US-E02-01** | 12858 | User Story | [US-E02-01] Bug refinement | E02 (#12840) | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005 | Merged to main |
| **US-E02-02** | 12859 | User Story | [US-E02-02] User-story refinement | E02 (#12840) | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005 | Merged to main |
| **US-E02-03** | 12860 | User Story | [US-E02-03] Test-case generation | E02 (#12840) | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005 | Merged to main |
| **US-E02-04** | 12861 | User Story | [US-E02-04] Multi-format export | E02 (#12840) | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005, BR-025 | Merged to main |
| **US-E02-05** | 12862 | User Story | [US-E02-05] Deterministic quality feedback | E02 (#12840) | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005 | Merged to main |
| **US-E02-06** | 12863 | User Story | [US-E02-06] Client-side privacy and bounded history | E02 (#12840) | **Closed** | Rms_Support_Hub\QA | Rms_Support_Hub\QA-01 - Prompt Studio | BR-005 | Merged to main |
| **US-E03-01** | 12864 | User Story | [US-E03-01] Module registry and capability model | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-006, BR-024 | Merged to main |
| **US-E03-02** | 12865 | User Story | [US-E03-02] Server-owned environment selection | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-003, BR-004, BR-023 | [PR #23](https://github.com/Hossam1104/Rms-Support-Hub/pull/23) |
| **US-E03-03** | 12866 | User Story | [US-E03-03] Draft lifecycle | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-001, BR-006 | Merged to main |
| **US-E03-04** | 12867 | User Story | [US-E03-04] Server-side totals and VAT calculations | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-008 | Merged to main |
| **US-E03-05** | 12868 | User Story | [US-E03-05] Exact compiled JSON preview | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-007 | Merged to main |
| **US-E03-06** | 12869 | User Story | [US-E03-06] Server-side payload validation | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-006, BR-007 | Merged to main |
| **US-E03-07** | 12870 | User Story | [US-E03-07] Generic send workflow | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-006, BR-010 | Merged to main |
| **US-E03-08** | 12871 | User Story | [US-E03-08] Bounded endpoint/database diagnostics | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-004, BR-017 | Merged to main |
| **US-E03-09** | 12872 | User Story | [US-E03-09] Common item/consumer lookup contracts | E03 (#12841) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-01 - Core Platform | BR-009 | Merged to main |
| **US-E04-01** | 12873 | User Story | [US-E04-01] UPC Testing environment | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-003, BR-004 | Merged to main |
| **US-E04-02** | 12874 | User Story | [US-E04-02] UPC branch lookup | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-009 | Merged to main |
| **US-E04-03** | 12875 | User Story | [US-E04-03] UPC item lookup | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-009 | Merged to main |
| **US-E04-04** | 12876 | User Story | [US-E04-04] UPC consumer lookup | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-009 | Merged to main |
| **US-E04-05** | 12877 | User Story | [US-E04-05] UPC-specific order payload | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-006, BR-007 | Merged to main |
| **US-E04-06** | 12878 | User Story | [US-E04-06] UPC order submission | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-006, BR-010 | Merged to main |
| **US-E04-07** | 12879 | User Story | [US-E04-07] UPC OrderRequests list and filters | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-010 | Merged to main |
| **US-E04-08** | 12880 | User Story | [US-E04-08] UPC OrderRequest detail | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-010 | Merged to main |
| **US-E04-09** | 12881 | User Story | [US-E04-09] UPC safe cancellation | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-011 | Merged to main |
| **US-E04-10** | 12882 | User Story | [US-E04-10] UPC same-number resend | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-012 | Merged to main |
| **US-E04-11** | 12883 | User Story | [US-E04-11] UPC Production policy gate | E04 (#12842) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-02 - UPC E-Commerce | BR-003, BR-023 | [PR #23](https://github.com/Hossam1104/Rms-Support-Hub/pull/23) |
| **US-E05-01** | 12884 | User Story | [US-E05-01] Verify GHC Testing database schema | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-004, BR-009 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-02** | 12885 | User Story | [US-E05-02] GHC item lookup | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-009 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-03** | 12886 | User Story | [US-E05-03] GHC consumer lookup | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-009 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-04** | 12887 | User Story | [US-E05-04] Preserve GHC-specific order fields | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-006, BR-007 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-05** | 12888 | User Story | [US-E05-05] GHC payment metadata | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-006, BR-008 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-06** | 12889 | User Story | [US-E05-06] GHC request history | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-010 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-07** | 12890 | User Story | [US-E05-07] GHC Testing environment activation | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-003, BR-004 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-08** | 12891 | User Story | [US-E05-08] GHC synthetic Testing send | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-006, BR-010 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E05-09** | 12892 | User Story | [US-E05-09] Diagnose downstream GHC send rejection | E05 (#12843) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-03 - GHC E-Commerce | BR-006, BR-010 | Diagnosed in P0-E / PR #30 |
| **US-E06-01** | 12893 | User Story | [US-E06-01] Specialized invoice payload builder | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-006, BR-007 | Merged to main |
| **US-E06-02** | 12894 | User Story | [US-E06-02] Complete Uni-Commerce draft persistence | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-001, BR-006 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E06-03** | 12895 | User Story | [US-E06-03] Uni-Commerce Testing environment configuration | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-003, BR-004 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E06-04** | 12896 | User Story | [US-E06-04] Uni-Commerce consumer lookup | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-009 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E06-05** | 12897 | User Story | [US-E06-05] Uni-Commerce request/invoice history adapter | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-010 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E06-06** | 12898 | User Story | [US-E06-06] Uni-Commerce synthetic Testing send | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-006, BR-010 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E06-07** | 12899 | User Story | [US-E06-07] Diagnose downstream Uni-Commerce send rejection | E06 (#12844) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-006, BR-010 | Diagnosed in P0-E / PR #30 |
| **US-E06-08** | 12900 | User Story | [US-E06-08] Uni-Commerce item lookup | E06 (#12844) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-009, BR-023 | Blocked: No item master in RmsEcommerceStg |
| **US-E06-09** | 12901 | User Story | [US-E06-09] Uni-Commerce cancellation | E06 (#12844) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-011, BR-023 | Blocked: No upstream cancellation API |
| **US-E06-10** | 12902 | User Story | [US-E06-10] Uni-Commerce resend | E06 (#12844) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-04 - GHC Uni-Commerce | BR-012, BR-023 | Blocked: No upstream resend API |
| **US-E07-01** | 12903 | User Story | [US-E07-01] Permanent RMS Support Agent service | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-013 | [PR #7](https://github.com/Hossam1104/Rms-Support-Hub/pull/7) |
| **US-E07-02** | 12904 | User Story | [US-E07-02] HTTPS loopback listener | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-015 | [PR #16](https://github.com/Hossam1104/Rms-Support-Hub/pull/16) |
| **US-E07-03** | 12905 | User Story | [US-E07-03] Windows Negotiate authentication | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-014 | [PR #8](https://github.com/Hossam1104/Rms-Support-Hub/pull/8) |
| **US-E07-04** | 12906 | User Story | [US-E07-04] Local Administrators authorization | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-014 | [PR #8](https://github.com/Hossam1104/Rms-Support-Hub/pull/8) |
| **US-E07-05** | 12907 | User Story | [US-E07-05] Exact-origin CORS | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-015 | [PR #17](https://github.com/Hossam1104/Rms-Support-Hub/pull/17) |
| **US-E07-06** | 12908 | User Story | [US-E07-06] Direct browser-to-Agent boundary | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-013, BR-015 | [PR #17](https://github.com/Hossam1104/Rms-Support-Hub/pull/17) |
| **US-E07-07** | 12909 | User Story | [US-E07-07] Ownership-aware Testing provisioning | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-013, BR-021 | [PR #15](https://github.com/Hossam1104/Rms-Support-Hub/pull/15) |
| **US-E07-08** | 12910 | User Story | [US-E07-08] Build identity and runtime ownership validation | E07 (#12845) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-01 - Secure Agent Foundation | BR-021 | [PR #18](https://github.com/Hossam1104/Rms-Support-Hub/pull/18) |
| **US-E08-01** | 12911 | User Story | [US-E08-01] RMS installation discovery | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-016 | Merged to main |
| **US-E08-02** | 12912 | User Story | [US-E08-02] RMS service health | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-016 | Merged to main |
| **US-E08-03** | 12913 | User Story | [US-E08-03] RMS database diagnostics | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-017 | Merged to main |
| **US-E08-04** | 12914 | User Story | [US-E08-04] Database backup | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-018 | [PR #9](https://github.com/Hossam1104/Rms-Support-Hub/pull/9) |
| **US-E08-05** | 12915 | User Story | [US-E08-05] Guarded database restore | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-018, BR-020 | [PR #9](https://github.com/Hossam1104/Rms-Support-Hub/pull/9) |
| **US-E08-06** | 12916 | User Story | [US-E08-06] DB backup downloader | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-017, BR-018 | [PR #10](https://github.com/Hossam1104/Rms-Support-Hub/pull/10) |
| **US-E08-07** | 12917 | User Story | [US-E08-07] Cleanup preview and execution | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-016, BR-020 | [PR #13](https://github.com/Hossam1104/Rms-Support-Hub/pull/13) |
| **US-E08-08** | 12918 | User Story | [US-E08-08] Branch reset preview and execution | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-016, BR-018, BR-020 | [PR #13](https://github.com/Hossam1104/Rms-Support-Hub/pull/13) |
| **US-E08-09** | 12919 | User Story | [US-E08-09] Operational health | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-017 | [PR #18](https://github.com/Hossam1104/Rms-Support-Hub/pull/18) |
| **US-E08-10** | 12920 | User Story | [US-E08-10] Incident timeline | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-020 | [PR #18](https://github.com/Hossam1104/Rms-Support-Hub/pull/18) |
| **US-E08-11** | 12921 | User Story | [US-E08-11] Safe Support Bundle | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-017, BR-020 | [PR #18](https://github.com/Hossam1104/Rms-Support-Hub/pull/18) |
| **US-E08-12** | 12922 | User Story | [US-E08-12] Safety Snapshots | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-018, BR-020 | [PR #18](https://github.com/Hossam1104/Rms-Support-Hub/pull/18) |
| **US-E08-13** | 12923 | User Story | [US-E08-13] Constrained diagnostic console | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-002, BR-017 | [PR #13](https://github.com/Hossam1104/Rms-Support-Hub/pull/13) |
| **US-E08-14** | 12924 | User Story | [US-E08-14] Bounded Main Server profile/read workflow | E08 (#12846) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-02 - Diagnostics and Recovery | BR-004, BR-017 | Merged to main |
| **US-E09-01** | 12925 | User Story | [US-E09-01] Canonical machine-owned package trust | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019, BR-023 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-02** | 12926 | User Story | [US-E09-02] Distinct signer-pin validation | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-03** | 12927 | User Story | [US-E09-03] Immutable startup trust snapshot | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019, BR-023 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-04** | 12928 | User Story | [US-E09-04] Trusted package verification | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-05** | 12929 | User Story | [US-E09-05] Install / Upgrade / Repair lifecycle | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-06** | 12930 | User Story | [US-E09-06] Uninstall lifecycle | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-07** | 12931 | User Story | [US-E09-07] Rollback and recovery | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-019 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-08** | 12932 | User Story | [US-E09-08] Durable lifecycle audit | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-020 | [PR #21](https://github.com/Hossam1104/Rms-Support-Hub/pull/21) |
| **US-E09-09** | 12933 | User Story | [US-E09-09] Deferred security hardening remediation | E09 (#12847) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-03 - Package Lifecycle and Security | BR-015, BR-023 | [PR #22](https://github.com/Hossam1104/Rms-Support-Hub/pull/22) |
| **US-E10-01** | 12934 | User Story | [US-E10-01] Deterministic release candidate | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-021 | [PR #24](https://github.com/Hossam1104/Rms-Support-Hub/pull/24) |
| **US-E10-02** | 12935 | User Story | [US-E10-02] Release integrity manifest | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-021 | [PR #24](https://github.com/Hossam1104/Rms-Support-Hub/pull/24) |
| **US-E10-03** | 12936 | User Story | [US-E10-03] Offline runtime validation | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-021, BR-023 | [PR #24](https://github.com/Hossam1104/Rms-Support-Hub/pull/24) |
| **US-E10-04** | 12937 | User Story | [US-E10-04] Sanitized Testing package configuration | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-003, BR-022 | [PR #24](https://github.com/Hossam1104/Rms-Support-Hub/pull/24) |
| **US-E10-05** | 12938 | User Story | [US-E10-05] External server-owned configuration | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-004, BR-022 | [PR #25](https://github.com/Hossam1104/Rms-Support-Hub/pull/25) |
| **US-E10-06** | 12939 | User Story | [US-E10-06] IIS Testing deployment | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-021, BR-022 | Merged to main |
| **US-E10-07** | 12940 | User Story | [US-E10-07] Exact build identity verification | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-021 | [PR #24](https://github.com/Hossam1104/Rms-Support-Hub/pull/24) |
| **US-E10-08** | 12941 | User Story | [US-E10-08] Backend/frontend/POS CI gates | E10 (#12848) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-02 - Release and Testing Deployment | BR-021, BR-025 | Merged to main |
| **US-E11-01** | 12942 | User Story | [US-E11-01] Protected GHC/Uni Testing configuration | E11 (#12849) | **Closed** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-05 - Integrated Testing | BR-003, BR-004, BR-023 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E11-02** | 12943 | User Story | [US-E11-02] Local Testing POS signing/trust boundary | E11 (#12849) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-04 - Local Integration and Acceptance | BR-015, BR-019 | Superseded by US-E19-03 (#13060) |
| **US-E11-03** | 12944 | User Story | [US-E11-03] Secure Support Hub local origin | E11 (#12849) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-04 - Local Integration and Acceptance | BR-015 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E11-04** | 12945 | User Story | [US-E11-04] POS Agent local runtime | E11 (#12849) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-04 - Local Integration and Acceptance | BR-013, BR-015 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E11-05** | 12946 | User Story | [US-E11-05] Preserve native RMS services during cleanup | E11 (#12849) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-04 - Local Integration and Acceptance | BR-016 | [PR #26](https://github.com/Hossam1104/Rms-Support-Hub/pull/26) |
| **US-E11-06** | 12947 | User Story | [US-E11-06] End-to-end Online Order + POS local smoke | E11 (#12849) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-03 - Integration Acceptance | BR-003, BR-004, BR-013 | Superseded by US-E19-09 (#13066) |
| **US-E12-01** | 12948 | User Story | [US-E12-01] Authoritative Production Support Hub configuration | E12 (#12850) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-04 - Production Readiness | BR-003, BR-004, BR-022 | Planned roadmap |
| **US-E12-02** | 12949 | User Story | [US-E12-02] Production Online Order acceptance | E12 (#12850) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-06 - Production Readiness | BR-003, BR-006, BR-026 | Planned roadmap (Gated by PR #30) |
| **US-E12-03** | 12950 | User Story | [US-E12-03] Real Production Agent/WPF package signer PKI | E12 (#12850) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-05 - Production Readiness | BR-015, BR-019 | Planned roadmap |
| **US-E12-04** | 12951 | User Story | [US-E12-04] Real Testing release signer PKI | E12 (#12850) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-05 - Production Readiness | BR-019, BR-021 | Planned roadmap |
| **US-E12-05** | 12952 | User Story | [US-E12-05] Production Agent device / certificate lifecycle | E12 (#12850) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-05 - Production Readiness | BR-015, BR-019, BR-033 | Planned roadmap |
| **US-E12-06** | 12953 | User Story | [US-E12-06] Managed browser policy rollout (Superseded) | E12 (#12850) | **Closed** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-05 - Production Readiness | BR-015 | Superseded by CR-001 / ADR-0029 |
| **US-E12-07** | 12954 | User Story | [US-E12-07] Representative-machine Production rehearsal - Agent Service + WPF + Angular Admin | E12 (#12850) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-05 - Production Readiness | BR-021, BR-025, BR-027 | Planned roadmap |
| **US-E12-08** | 12955 | User Story | [US-E12-08] Fleet/customer deployment procedure - Agent Service + WPF bundle | E12 (#12850) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-04 - Production Readiness | BR-021, BR-038 | Planned roadmap |
| **US-E12-09** | 12956 | User Story | [US-E12-09] Production rollback rehearsal - Agent/WPF | E12 (#12850) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-04 - Production Readiness | BR-019, BR-021, BR-038 | Planned roadmap |
| **US-E12-10** | 12957 | User Story | [US-E12-10] Production go-live acceptance - WPF/Agent supervised architecture | E12 (#12850) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-04 - Production Readiness | BR-003, BR-025, BR-027, BR-039 | Planned roadmap |
| **US-E13-01** | 12958 | User Story | [US-E13-01] OMS contract discovery | E13 (#12851) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-07 - Future Integrations | BR-006, BR-024 | Planned roadmap |
| **US-E13-02** | 12959 | User Story | [US-E13-02] OMS implementation | E13 (#12851) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-07 - Future Integrations | BR-006, BR-024 | Conditional on authoritative contract |
| **US-E13-03** | 12960 | User Story | [US-E13-03] Call Center contract discovery | E13 (#12851) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-07 - Future Integrations | BR-006, BR-024 | Planned roadmap |
| **US-E13-04** | 12961 | User Story | [US-E13-04] Call Center implementation | E13 (#12851) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-07 - Future Integrations | BR-006, BR-024 | Conditional on authoritative contract |
| **US-E13-05** | 12962 | User Story | [US-E13-05] Shared module onboarding checklist | E13 (#12851) | **New** | Rms_Support_Hub\Online Orders | Rms_Support_Hub\OO-07 - Future Integrations | BR-024 | Planned roadmap |
| **US-E14-01** | 12963 | User Story | [US-E14-01] Improve module-health reason visibility | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-017, BR-023 | Planned roadmap |
| **US-E14-02** | 12964 | User Story | [US-E14-02] External-config mapped-drive classification | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-004, BR-023 | Planned roadmap |
| **US-E14-03** | 12965 | User Story | [US-E14-03] Permission-denied external-config regression | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-004, BR-023 | Planned roadmap |
| **US-E14-04** | 12966 | User Story | [US-E14-04] Resolve platform-specific ACL test reliability | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-023 | Planned roadmap |
| **US-E14-05** | 12967 | User Story | [US-E14-05] Operational runbook consolidation | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-020 | Planned roadmap |
| **US-E14-06** | 12968 | User Story | [US-E14-06] Support diagnostics UX refinement | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-017 | Planned roadmap |
| **US-E14-07** | 12974 | User Story | [US-E14-07] Uni-Commerce read-query timeout and consumer lookup performance hardening | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-017, BR-023 | Planned backlog |
| **US-E14-08** | 12975 | User Story | [US-E14-08] Capability-driven GHC frontend field gating | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-001, BR-024 | Planned backlog |
| **US-E14-09** | 12976 | User Story | [US-E14-09] Uni draft persistence and export preview resilience | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-001, BR-023 | Planned backlog |
| **US-E14-10** | 12977 | User Story | [US-E14-10] Order-history ascending sort URL query contract | E14 (#12852) | **New** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-05 - Operational Hardening | BR-004, BR-023 | Planned backlog |
| **US-E15-01** | 12969 | User Story | [US-E15-01] Establish Azure DevOps hierarchy | E15 (#12853) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-06 - Governance and Traceability | BR-025 | [PR #27](https://github.com/Hossam1104/Rms-Support-Hub/pull/27) |
| **US-E15-02** | 12970 | User Story | [US-E15-02] Link User Stories to GitHub PRs | E15 (#12853) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-06 - Governance and Traceability | BR-025 | [PR #27](https://github.com/Hossam1104/Rms-Support-Hub/pull/27) |
| **US-E15-03** | 12971 | User Story | [US-E15-03] Add acceptance criteria to active work | E15 (#12853) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-06 - Governance and Traceability | BR-025 | [PR #27](https://github.com/Hossam1104/Rms-Support-Hub/pull/27) |
| **US-E15-04** | 12972 | User Story | [US-E15-04] Link validation evidence to work items | E15 (#12853) | **Closed** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-06 - Governance and Traceability | BR-025 | [PR #27](https://github.com/Hossam1104/Rms-Support-Hub/pull/27) |
| **US-E15-05** | 12973 | User Story | [US-E15-05] Maintain BRD-to-backlog traceability | E15 (#12853) | **Active** | Rms_Support_Hub\Platform | Rms_Support_Hub\PLAT-06 - Governance and Traceability | BR-025 | [PR #27](https://github.com/Hossam1104/Rms-Support-Hub/pull/27) |
| **US-E16-01** | 13021 | User Story | [US-E16-01] Inventory and parity-map existing Agent capabilities | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-027, BR-030 | Architecture baseline |
| **US-E16-02** | 13022 | User Story | [US-E16-02] Extract shared Agent command/query application layer | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-027, BR-030 | Architecture baseline |
| **US-E16-03** | 13023 | User Story | [US-E16-03] Define local/remote invocation context and authorization source | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-030, BR-034, BR-035, BR-040 | Architecture baseline |
| **US-E16-04** | 13024 | User Story | [US-E16-04] Secure WPF-to-Agent Windows Named Pipe transport | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-028, BR-034 | Architecture baseline |
| **US-E16-05** | 13025 | User Story | [US-E16-05] Agent-initiated SignalR Hub connection | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-032, BR-033 | Architecture baseline |
| **US-E16-06** | 13026 | User Story | [US-E16-06] Machine registration and per-device identity | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-032, BR-033 | Architecture baseline |
| **US-E16-07** | 13027 | User Story | [US-E16-07] Offline event queue, reconnect and state synchronization | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-028, BR-036 | Architecture baseline |
| **US-E16-08** | 13028 | User Story | [US-E16-08] Unified correlation, audit, progress and cancellation contracts | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-030, BR-035, BR-037 | Architecture baseline |
| **US-E16-09** | 13029 | User Story | [US-E16-09] Agent/WPF version compatibility contract | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-031, BR-038 | Architecture baseline |
| **US-E16-10** | 13030 | User Story | [US-E16-10] Architecture security and failure-mode test harness | E16 (#13017) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-07 - WPF Agent Architecture | BR-030, BR-034, BR-035, BR-036 | Architecture baseline |
| **US-E17-01** | 13031 | User Story | [US-E17-01] WPF shell and local machine dashboard | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-027, BR-028 | Architecture baseline |
| **US-E17-02** | 13032 | User Story | [US-E17-02] Agent/RMS service health and approved service control | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-027, BR-028, BR-030 | Architecture baseline |
| **US-E17-03** | 13033 | User Story | [US-E17-03] Database health and diagnostics | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-027, BR-028 | Architecture baseline |
| **US-E17-04** | 13034 | User Story | [US-E17-04] Database backup/download and guarded restore | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-030 | Architecture baseline |
| **US-E17-05** | 13035 | User Story | [US-E17-05] Logs and safe Support Bundle | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-030 | Architecture baseline |
| **US-E17-06** | 13036 | User Story | [US-E17-06] Safety Snapshots and incident timeline | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-030 | Architecture baseline |
| **US-E17-07** | 13037 | User Story | [US-E17-07] Cleanup and branch-reset workflows | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-030 | Architecture baseline |
| **US-E17-08** | 13038 | User Story | [US-E17-08] Package install/upgrade/repair/uninstall | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-038 | Architecture baseline |
| **US-E17-09** | 13039 | User Story | [US-E17-09] Rollback and recovery | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-038 | Architecture baseline |
| **US-E17-10** | 13040 | User Story | [US-E17-10] Local activity/audit view | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-037 | Architecture baseline |
| **US-E17-11** | 13041 | User Story | [US-E17-11] Local Windows authorization and high-risk confirmation UX | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-034 | Architecture baseline |
| **US-E17-12** | 13042 | User Story | [US-E17-12] Offline standalone operation | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-028, BR-036 | Architecture baseline |
| **US-E17-13** | 13043 | User Story | [US-E17-13] WPF functional parity acceptance matrix | E17 (#13018) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-08 - WPF Local Experience | BR-027, BR-028, BR-039 | Architecture baseline |
| **US-E18-01** | 13044 | User Story | [US-E18-01] Registered machine inventory | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-033 | Architecture baseline |
| **US-E18-02** | 13045 | User Story | [US-E18-02] Agent heartbeat and machine health dashboard | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-031 | Architecture baseline |
| **US-E18-03** | 13046 | User Story | [US-E18-03] WPF heartbeat, crash and version monitoring | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-031, BR-038 | Architecture baseline |
| **US-E18-04** | 13047 | User Story | [US-E18-04] Central issues/alerts aggregation | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-031 | Architecture baseline |
| **US-E18-05** | 13048 | User Story | [US-E18-05] Machine detail and operational timeline | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-037 | Architecture baseline |
| **US-E18-06** | 13049 | User Story | [US-E18-06] Remote typed diagnostics and log collection | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-035 | Architecture baseline |
| **US-E18-07** | 13050 | User Story | [US-E18-07] Remote Support Bundle | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-035 | Architecture baseline |
| **US-E18-08** | 13051 | User Story | [US-E18-08] Remote database backup request and artifact status | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-035 | Architecture baseline |
| **US-E18-09** | 13052 | User Story | [US-E18-09] Approved remote RMS service control | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-035, BR-040 | Architecture baseline |
| **US-E18-10** | 13053 | User Story | [US-E18-10] Controlled remote package lifecycle | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-035, BR-038, BR-040 | Architecture baseline |
| **US-E18-11** | 13054 | User Story | [US-E18-11] Admin RBAC and remote command policy | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-040 | Architecture baseline |
| **US-E18-12** | 13055 | User Story | [US-E18-12] Remote command progress, cancellation and idempotency | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-035 | Architecture baseline |
| **US-E18-13** | 13056 | User Story | [US-E18-13] Fleet version/update visibility | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-038 | Architecture baseline |
| **US-E18-14** | 13057 | User Story | [US-E18-14] Central audit search and correlation | E18 (#13019) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-09 - Admin Fleet Supervision | BR-029, BR-037 | Architecture baseline |
| **US-E19-01** | 13058 | User Story | [US-E19-01] Preserve/reuse existing E07-E09 capability contracts | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-027, BR-030 | Architecture baseline |
| **US-E19-02** | 13059 | User Story | [US-E19-02] Side-by-side browser/WPF compatibility mode | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-027, BR-039 | Architecture baseline |
| **US-E19-03** | 13060 | User Story | [US-E19-03] Migrate Testing release signer/trust to Agent+WPF bundle | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-019, BR-038 | Supersedes #12943 |
| **US-E19-04** | 13061 | User Story | [US-E19-04] Service + WPF installer and upgrade path | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-038 | Architecture baseline |
| **US-E19-05** | 13062 | User Story | [US-E19-05] Existing Agent installation migration | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-038, BR-039 | Architecture baseline |
| **US-E19-06** | 13063 | User Story | [US-E19-06] Agent/WPF package trust and version compatibility | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-019, BR-038 | Architecture baseline |
| **US-E19-07** | 13064 | User Story | [US-E19-07] WPF crash recovery / Agent continuity proof | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-031 | Architecture baseline |
| **US-E19-08** | 13065 | User Story | [US-E19-08] Side-by-side functional/security parity validation | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-027, BR-039 | Architecture baseline |
| **US-E19-09** | 13066 | User Story | [US-E19-09] Representative-machine integrated Testing acceptance | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-027, BR-039 | Supersedes #12947 |
| **US-E19-10** | 13067 | User Story | [US-E19-10] Deprecate browser-direct privileged POS path | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-039 | Architecture baseline |
| **US-E19-11** | 13068 | User Story | [US-E19-11] Pilot deployment | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-039 | Architecture baseline |
| **US-E19-12** | 13069 | User Story | [US-E19-12] Fleet rollout and rollback procedure | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-038, BR-039 | Architecture baseline |
| **US-E19-13** | 13070 | User Story | [US-E19-13] Support/admin runbooks and training | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-027, BR-029 | Architecture baseline |
| **US-E19-14** | 13071 | User Story | [US-E19-14] Final migration acceptance | E19 (#13020) | **New** | Rms_Support_Hub\POS | Rms_Support_Hub\POS-10 - WPF Migration and Rollout | BR-027, BR-039 | Architecture baseline |
