# RMS+ Support Hub — Maintenance

**Role:** Implement (small, scoped maintenance only)
**Branch:** `main`
**Repository:** `Hossam1104/Rms-Support-Hub`

## Current phase

No active refactor programme. The RMS+ Support Hub UI, branding, and rename
programme is complete, reviewed, and closed. The repository is in post-release
maintenance.

The next programme is **POS Maintenance integration planning**. It has not
started, and no external POS source has been supplied to this repository. Read
`docs/POS_MAINTENANCE_INTEGRATION_READINESS.md` before proposing any POS work.

## Standing constraints

- Do not change persisted storage keys. The byte-exact list is in
  `.ai/STATE.md`; their prefixes intentionally reflect earlier product names and
  no migration exists.
- Do not change Online Order behavior: API endpoints, DTOs, JSON contracts,
  payload mappings, module keys, capabilities, payment values, statuses,
  filters, sorting, paging, calculations, or send/cancel/resend semantics.
- Do not change Prompt Studio behavior: section counts (Bug 11, Story 7, Test
  Case 9), history cap of ten, drafts, exports, shortcuts, or quality semantics.
  No external AI execution.
- POS stays Coming Soon and non-operational. No generic execution surface —
  no arbitrary PowerShell, command, SQL, script upload, or executable launcher.
- Production is out of bounds: no Production access, SQL, deployment, or
  state-changing action. Testing is the default environment.
- No credential, token, or connection-string value in a tracked file.

## Read first

1. `.ai/STATE.md`
2. `python .ai/scripts/context.py`
3. `docs/REPOSITORY_STRUCTURE.md` and `docs/README.md`
4. Only then the specific source, tests, and documents the task names
