# RMS+ Support Hub — Maintenance

**Role:** Review (POS architecture checkpoint only; no execution)
**Branch:** `main`
**Repository:** `Hossam1104/Rms-Support-Hub`

## Current phase

No active refactor programme. The RMS+ Support Hub UI, branding, and rename
programme is complete, reviewed, and closed. INT-00 POS cross-project
architecture decision closure is complete.

CLAUDE OPUS 5
POS INTEGRATION ARCHITECTURE CHECKPOINT

STATUS:
REVIEW REQUIRED / NO EXECUTION AUTHORIZED

The checkpoint is narrowly limited to process isolation; browser -> loopback
architecture; Local Network Access; managed Chrome/Edge policy;
Negotiate/browser policy; hostname/port/certificate; CORS; antiforgery;
identity; audit/resource ownership; destination project isolation; and the
clean source import boundary. Do not request another full R2 review.

INT-01 remains blocked until this checkpoint passes and the owner explicitly
authorizes it. No POS source is imported or modified by this task.

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
- POS privileged operations belong to a separate Windows Agent reached directly
  by the Support Hub browser. The general `RmsSupportHub.Api` is not their
  proxy or execution path.
- Production is out of bounds: no Production access, SQL, deployment, or
  state-changing action. Testing is the default environment.
- No credential, token, or connection-string value in a tracked file.

## Read first

1. `.ai/STATE.md`
2. `python .ai/scripts/context.py`
3. `docs/REPOSITORY_STRUCTURE.md` and `docs/README.md`
4. Only then the specific source, tests, and documents the task names
