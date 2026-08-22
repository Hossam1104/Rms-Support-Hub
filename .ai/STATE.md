# Current Project State

- **Updated:** 2026-08-22
- **Repository:** `docs/wpf-agent-architecture-rebaseline`; PR #30 was merged to `main` at `9272041638e2da97ac6ff5e4e251c2d370acc47e`.
- **Status:** WPF Architecture Decision Point completed at design level. CR-001 created; ADR-0029 created; Azure DevOps iterations POS-07..POS-10 created; Epics E16..E19 created and child stories populated; E11 reconciled and closed; E12 updated. NO WPF implementation has begun. Production readiness remains NO.

## Current facts

- PR #30 was accepted by GPT-5.6 Sol and merged to `main` at `9272041638e2da97ac6ff5e4e251c2d370acc47e`, protecting Production order mutations across UPC, GHC E-Commerce, and GHC Uni-Commerce.
- The owner approved the WPF dual control-surface architecture for POS maintenance:
  - Central Support Hub: ASP.NET Core backend + Angular administrator dashboard.
  - Local Machine: always-running `RmsSupportAgent.Service` + standalone native `RmsSupportAgent.Desktop.Wpf`.
  - Control Model: WPF -> Windows Named Pipes -> Agent shared command/query layer; Angular Admin -> Hub -> persistent outbound SignalR -> Agent shared command/query layer.
- CR-001 (`docs/CR-001_WPF_AGENT_ADMIN_SUPERVISION.md`) and ADR-0029 (`.ai/decisions/ADR-0029-wpf-agent-dual-control-and-admin-supervision.md`) are established and accepted for architecture.
- BRD.md is updated to Version 1.1 with requirements BR-027 through BR-040.
- Azure DevOps hierarchy is updated and reconciled:
  - New Iterations: `POS-07 - WPF Agent Architecture`, `POS-08 - WPF Local Experience`, `POS-09 - Admin Fleet Supervision`, `POS-10 - WPF Migration and Rollout`.
  - Epics E16 (#13017), E17 (#13018), E18 (#13019), E19 (#13020) created with 51 new User Stories (#13021–#13071).
  - Epic E11 (#12849) reconciled and closed as superseded roadmap; child stories #12943 and #12947 superseded by replacement E19 stories #13060 and #13066.
  - Epic E12 (#12850) updated to reflect the WPF/Agent supervised target architecture.
- Conversion plan (`docs/WPF_AGENT_CONVERSION_PLAN.md`) defines Phases 0 through 9; first implementation slice is `WPF-01 — Shared Agent Application + Local IPC Foundation`.
- NO WPF implementation has begun; product code, certificates, PKI, and native RMS services are untouched.
- Uni Testing HTTP:90 `502 Bad Gateway` remains an external Online Order blocker but does NOT block WPF Phase 1 architecture work.

## Validation evidence

- Azure DevOps CLI execution: 4 new iteration nodes created, 4 Epics (E16–E19) created, 51 User Stories created and parent-linked, E11/E12 reconciled.
- Traceability matrices (`AZURE_DEVOPS_BACKLOG.md` and `AZURE_DEVOPS_TRACEABILITY.md`) synchronized with actual Azure IDs.
- `python .ai/scripts/check_memory.py` and `python .ai/scripts/context.py`: verified.
- `git diff --check`: clean.

## Safety and remaining work

- No product runtime code was modified.
- No WPF desktop application code was implemented.
- No Production contact, secret provisioning, POS machines, certificates, PKI, or native RMS services were touched.
- Production readiness remains **NO** until full controlled acceptance gates, external secrets/API-keys, and release PKI are complete.
- Next review action: Submit Draft PR for GPT-5.6 Sol rebaseline review. DO NOT execute WPF-01 until accepted.
