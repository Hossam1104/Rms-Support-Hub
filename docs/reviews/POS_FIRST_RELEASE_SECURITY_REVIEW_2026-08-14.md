# POS First-Release Security & Readiness Review

**Reviewer:** Claude Opus 5 (independent security/readiness review)
**Review date:** 2026-08-14
**Scope:** INT-06I + INT-07 + INT-08 + INT-13 (POS Maintenance integration,
first release)
**Baseline reviewed:** `242fcd7` (branch `main`, PR #7 merged)
**Remediation executor:** Claude Sonnet 5 (this document), branch
`pos-first-release-review-remediation`

## Result

| Question | Answer |
|---|---|
| Critical findings | 0 |
| High findings | 0 |
| Medium findings | 2 (M-1, M-2) |
| Low findings | 6 (L-1 – L-6) |
| Informational findings | 3 (I-1 – I-3) |
| Testing-scoped first release | **APPROVED** |
| Production / customer deployment | **NOT YET APPROVED** — blocked on M-1 and M-2 |

Testing-environment approval and Production-environment approval are
**separate gates**. Nothing in this remediation, and no future POS work,
changes Production/customer approval status until M-1 and M-2 are each
independently implemented and reviewed. See `.ai/STATE.md` and
`docs/POS_MAINTENANCE_INTEGRATION_READINESS.md` for the current tracked gate
state.

## Medium findings — tracked as future blockers, not remediated here

Per the review's own scope, M-1 and M-2 require production managed-fleet
architecture decisions that are explicitly out of scope for this remediation
pass. They are recorded here as open blockers, to be handed to the next
execution session as their own scoped task.

| ID | Title | Why it blocks Production | Status |
|---|---|---|---|
| M-1 | Managed-endpoint browser policy | The current Chrome/Edge policy provisioning (`scripts/PosAgentWindowsProvisioning.psm1`) is designed for a single representative Testing device with local Administrator-driven registry writes. Production requires a managed-fleet delivery mechanism (e.g., GPO/Intune) instead of a locally-run provisioning script model. | **OPEN — not started** |
| M-2 | Production certificate lifecycle | The current certificate provisioning (`scripts/PosSupportHubProvisioning.psm1`, `scripts/setup-pos-agent-testing.ps1`) creates a self-managed, non-exportable, machine-local certificate suitable for one Testing device. Production needs a defined issuance, renewal, distribution, and revocation lifecycle across a fleet. | **OPEN — not started** |

## Low findings — remediated

| ID | Title | Resolution | Evidence |
|---|---|---|---|
| L-1 | Mutation token lifetime too long (5 min) | `MutationTokenOptions.Lifetime` default reduced to 60 seconds; `MutationTokenTests.cs` covers the new default, near-expiry validity, and expiry-at-60s boundary. | `pos/src/RmsSupportHub.Pos.Agent/MutationTokens/MutationTokenOptions.cs`, `pos/tests/RmsSupportHub.Pos.Agent.IntegrationTests/MutationTokenTests.cs` |
| L-2 | INT-13 live evidence document contained inaccurate code-attribution claims | Full audit against current code. Corrected: (1) fabricated mutation-token problem codes `MutationTokenAlreadyConsumed`/`MutationTokenExpired` (actual: both map to HTTP 403 `mutation_token_invalid`); (2) misattributed test coverage for the real `WindowsAdministratorGroupChecker`; (3) wrong class name `LocalAdministratorGroupChecker` in the live evidence row. Token-lifetime and origin (`:4443`) claims were re-checked and found already correct. All historical live-observation content was preserved unmodified; corrections are additive notes, not deletions. | `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` ("L-2 Evidence Audit — Corrections" section and three inline `[L-2 CORRECTION: ...]` markers) |
| L-3 | Testing cleanup script had no test coverage for state-loss recovery | Confirmed the existing strict marker-based, fail-closed ownership design was already sound (no hostname-inference or "delete anything RMS-looking" logic existed). Hardened the corrupt-state-file error path with an explicit message, and made the script's internal functions safely dot-source-testable (`Invoke-Int13PCleanup` + `$MyInvocation.InvocationName` guard) without changing real invocation behavior. Added 13 focused Pester tests: state file absent/present/corrupt/wrong-marker/wrong-schema-version, owned-vs-unrelated manifest file removal, manifest path-escape refusal, unrelated hosts-entry preservation, and ownership-flag no-ops for service/certificate/configuration. | `scripts/remove-pos-agent-testing.ps1`, `scripts/tests/RemovePosAgentTesting.Tests.ps1` (13/13 passed) |
| L-4 | Browser block-policy pattern matching missed scheme-less/host-only patterns and `URLBlocklist`/`URLAllowlist` precedence | Fixed pattern matching to cover full origin patterns, scheme-less bare-host patterns, `[*.]host`/`*.host` wildcards, exact host, and port-aware comparison; added `URLBlocklist`/`URLAllowlist` precedence checking (Allowlist exempts a Blocklist match) with fail-closed behavior on ambiguous/malformed patterns. Added 9 focused Pester tests covering exact bare hostname, wildcard hostname, unrelated hostname, exact HTTPS origin, origin with port, `URLBlocklist` conflict, `URLAllowlist` exception, malformed-pattern fail-closed, and an existing non-conflicting enterprise policy. | `scripts/PosAgentWindowsProvisioning.psm1` (`Test-PosPolicyPatternMatchesOrigin`, `Get-PosListPolicyEntries`, `Assert-PosNoBlockingPolicyMatch`), `scripts/tests/PosAgentWindowsProvisioning.Tests.ps1` |
| L-5 | Dormant `ServiceControlAction.Delete` primitive | Removed the unused `Delete` action and its `sc.exe delete` execution branch from the Domain enum and `WindowsServiceManager`; the Agent contract only ever exposed Start/Stop/Restart, so no capability regression. | `pos/src/RmsSupportHub.Pos.Domain/Enums/ServiceControlAction.cs`, `pos/src/RmsSupportHub.Pos.Infrastructure/Windows/WindowsServiceManager.cs`, `pos/tests/RmsSupportHub.Pos.Domain.Tests/ServiceControlActionTests.cs` |
| L-6 | Dead `MutationTokenEndpointFilter` | Confirmed unused (no route registered it) and removed. | `pos/src/RmsSupportHub.Pos.Agent/MutationTokens/MutationTokenEndpointFilter.cs` (deleted) |

## Informational findings

| ID | Title | Disposition |
|---|---|---|
| I-1 | Build/origin documentation mismatch (docs referenced a different Testing origin than CI used) | Fixed: `pos/openapi/README.md` and `.github/workflows/pos-ci.yml` now consistently reference the exact Testing origin `https://support-hub.integration.test:4443`. |
| I-2 | `ServiceSummaryDto.DisplayName` wording implied the raw Windows service name is never disclosed | Decision: retain current behavior. `DisplayName` is returned only to an already-authenticated, server-verified local Administrator over the exact-origin, Negotiate-authenticated channel — this is an intended, authorized disclosure, not a leak. Only documentation wording implying an absolute "never disclosed" guarantee needed correction; no code change was made. |
| I-3 | Historical INT-13 live-evidence entries (blocked/partial runs from earlier sessions) cannot be independently re-verified today | No remediation possible or appropriate: these are frozen point-in-time records of machine states from prior sessions (absent DNS, absent certificate, elevation blockers, etc.) that no longer exist to re-observe. Marked historical; preserved verbatim per L-2's audit approach. Informational only — does not affect the CLOSED status of INT-13 or the PASS result of the final live run. |

## Remediation status

All six Low findings and both applicable Informational findings (I-1, I-2)
are remediated on branch `pos-first-release-review-remediation`. I-3 requires
no action. M-1 and M-2 remain open and are explicitly out of scope for this
remediation; they are the subject of the next task handed to the following
execution session (see root `TASK.md` after this remediation merges).

This document is the durable record of the review outcome and remediation
status. Do not delete or rewrite it in place for future findings — file a new
dated review document and cross-link instead.
