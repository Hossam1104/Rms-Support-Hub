# Current Project State

- **Updated:** 2026-08-22
- **Repository:** `docs/wpf-agent-architecture-rebaseline`; PR #30 was merged to `main` at `9272041638e2da97ac6ff5e4e251c2d370acc47e`.
- **Status:** WPF Architecture Decision Point and Backlog Rebaseline completed. CR-001 created and refined; ADR-0029 created and refined; two-layer local authorization model established; Online Orders E05/E06 Azure state truthfully reconciled; Azure DevOps iterations POS-07..POS-10 created; Epics E16..E19 created and child stories populated; E11 reconciled and closed; E12 updated. NO WPF implementation has begun. Production readiness remains NO.

## Current facts

- PR #30 was accepted by GPT-5.6 Sol and merged to `main` at `9272041638e2da97ac6ff5e4e251c2d370acc47e`, protecting Production order mutations across UPC, GHC E-Commerce, and GHC Uni-Commerce.
- Online Orders Azure state reconciled:
  - Epic E05 (#12843) transitioned to Closed following PR #30 closure of diagnosis story #12892 (all 9 child stories #12884–#12892 are Closed).
  - Epic E06 (#12844) remains Active following PR #30 closure of diagnosis story #12899 (#12893–#12899 Closed; #12900–#12902 remain New/Conditional).
- The owner and architecture authority approved the WPF dual control-surface architecture for POS maintenance:
  - Central Support Hub: ASP.NET Core backend + Angular administrator dashboard.
  - Local Machine: always-running `RmsSupportAgent.Service` + standalone native `RmsSupportAgent.Desktop.Wpf`.
  - Control Model: WPF -> Windows Named Pipes -> Agent shared command/query layer; Angular Admin -> Hub -> persistent outbound SignalR -> Agent shared command/query layer.
  - Two-Layer Local Authorization:
    - Layer A (IPC Connection): Windows Named Pipe ACLs restricted to `SYSTEM`, `Local Administrators`, and dedicated local `RMS Support Operators` group (failing closed for unauthorized identities).
    - Layer B (Per-Command Authorization): Agent application layer validates authenticated Windows principal and role per typed command (low/medium-risk non-destructive operations permitted for operators; high-risk mutating actions requiring administrator elevation and explicit confirmation).
- CR-001 (`docs/CR-001_WPF_AGENT_ADMIN_SUPERVISION.md`) and ADR-0029 (`.ai/decisions/ADR-0029-wpf-agent-dual-control-and-admin-supervision.md`) are established and accepted for architecture.
- BRD.md is updated to Version 1.1 with requirements BR-027 through BR-040 (BR-034 updated to two-layer local authorization; BR-040 preserved as admin-only central fleet control).
- Azure DevOps hierarchy is updated and reconciled:
  - New Iterations: `POS-07 - WPF Agent Architecture`, `POS-08 - WPF Local Experience`, `POS-09 - Admin Fleet Supervision`, `POS-10 - WPF Migration and Rollout`.
  - Epics E16 (#13017), E17 (#13018), E18 (#13019), E19 (#13020) created with 51 new User Stories (#13021–#13071).
  - Stories #13023 (US-E16-03), #13024 (US-E16-04), and #13041 (US-E17-11) updated in Azure DevOps with two-layer authorization and operator group acceptance criteria while remaining New.
  - Epic E11 (#12849) reconciled and closed as superseded roadmap; child stories #12943 and #12947 superseded by replacement E19 stories #13060 and #13066.
  - Epic E12 (#12850) updated to reflect the WPF/Agent supervised target architecture.
- Conversion plan (`docs/WPF_AGENT_CONVERSION_PLAN.md`) defines Phases 0 through 9; first implementation slice is `WPF-01 — Shared Agent Application + Local IPC Foundation`.
- NO WPF implementation has begun; product code, certificates, PKI, and native RMS services are untouched.
- Uni Testing HTTP:90 `502 Bad Gateway` remains an external Online Order blocker but does NOT block WPF Phase 1 architecture work.

## Validation evidence

- Azure DevOps CLI execution: work items updated (#12892 Closed, #12899 Closed, E05 #12843 Closed, E06 #12844 verified Active, #13023, #13024, #13041 updated in New state).
- Traceability matrices (`AZURE_DEVOPS_BACKLOG.md` and `AZURE_DEVOPS_TRACEABILITY.md`) synchronized with actual Azure IDs and states.
- `python .ai/scripts/check_memory.py` and `python .ai/scripts/context.py`: verified.
- `git diff --check`: clean.

## Safety and remaining work

- No product runtime code was modified.
- No WPF desktop application code was implemented.
- No Production contact, secret provisioning, POS machines, certificates, PKI, or native RMS services were touched.
- Production readiness remains **NO** until full controlled acceptance gates, external secrets/API-keys, and release PKI are complete.
- Next review action: Submit Draft PR for GPT-5.6 Sol rebaseline review. DO NOT execute WPF-01 until accepted.
