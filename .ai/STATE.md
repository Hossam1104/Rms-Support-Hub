# Current Project State

- **Updated:** 2026-08-22
- **Repository:** `feat/p0-f-production-mutation-gate`; Draft PR #30 targets `main` at `7cb1351ccb5e06379415675ea2409c22dd3ba6fb`. The authorized correction commit `5c025fec12a84899cadb4f4952c301e2605bdf4f` is pushed to the same branch; exact-head Support Hub CI run `32528347621` completed successfully.
- **Status:** P0-F Sol-remediation implementation and validation are complete; PR #30 remains Draft and awaits independent Sol re-review. No merge or acceptance is claimed.

## Current facts

- Production send/cancel/resend mutations for UPC, GHC E-Commerce, and GHC Uni-Commerce remain server-gated by an owner-configured secret, opaque in-memory token, module + Production + original-session scope, constant-time comparison, bounded failed-attempt windows, server-observed source/module throttling, and a conservative module ceiling. Attempt and token state use bounded expiry-backed caches.
- Frontend Production builder entry is fail-closed when module/environment metadata is unavailable; locked Production opens the unlock flow, unlocked Production is allowed, and Testing remains available only when resolved by authoritative metadata. Unlock backdrop/Cancel close is ignored during in-flight verification.
- Supported-but-status-blocked Order Request actions remain visible and disabled with a reason; unsupported capabilities remain absent. The API-key interface has no fail-open default, missing keys fail before outbound, and required API-key configuration makes an environment structurally unavailable without exposing the value.
- GHC delivery validation and dynamic next-day defaults remain active. The production builder already aligns compiled `order_product_total_value` with row `row_net_total`; the regression now proves the real `GhcEcommerceModule.BuildPayload` / `Validate` flow without manually corrupting payload output. Phone normalization and UPC behavior remain covered.
- GHC Uni-Commerce uses four-decimal VAT/net precision and a fixed server-owned `X-Api-Key` resolved from external `ModuleApiKeys` configuration. No downstream key or unlock secret is tracked.
- GHC Uni and the Testing gateway remain disabled/unavailable where configuration is not provisioned. The verified Uni Testing HTTP:90 `502` is an external dependency blocker; direct HTTPS and Production were not contacted.

## Validation evidence

- Focused backend remediation tests: **68 passed / 0 failed**.
- Backend Release tests: **322 passed / 0 failed**.
- Frontend no-watch suite: **385 passed / 0 failed across 63 files** with `npm test -- --no-watch`.
- Frontend production build: passed.
- Broad `.\scripts\build.ps1`: passed after stopping the stale project-owned Debug API process that held assemblies; Debug tests 322/322, Release build 0 warnings/0 errors, frontend production build passed.
- PowerShell native parse gate: **37/37 passed**; PSScriptAnalyzer was unavailable and is recorded as an unavailable optional dependency, not a code failure.
- `python .ai/scripts/check_memory.py`, `python .ai/scripts/context.py`, and `git diff --check`: passed.
- Pushed correction commit `5c025fec12a84899cadb4f4952c301e2605bdf4f`; exact-head GitHub Actions run `32528347621` / `Support Hub CI` passed, including the packaged Testing release candidate and runtime smoke test.

## Safety and remaining work

- No Production contact, customer order mutation, database mutation, POS trust bypass, Main Server mutation, Azure CRUD, or secret provisioning was performed. Synthetic credentials were used only in tests; the stale project-owned process stopped for build validation was the local Debug API executable.
- External owner Production unlock-secret/API-key provisioning, Uni gateway remediation, POS release PKI/trust material (#12943), and final integrated Online Order + POS smoke (#12947) remain outside this correction. Production readiness remains **NO**.
- Azure traceability maps BR-026 to existing #12949 Production Online Order acceptance; no new Azure work item was created.
- Next review action: keep PR #30 Draft and await Sol independent re-review; no merge or acceptance is claimed.
