# Active Handoff

- **Status:** Blocked
- **Gate:** INT-13C representative-device/live operational evidence on PR #7 branch `int-13p-testing-agent-provisioning`, current head `b5e4de1`.
- **Completed:** Exact Chrome/Edge policy provisioning, typed `REG_MULTI_SZ` BackConnection merge, ownership/WhatIf/cleanup paths, fail-closed rollback, focused tests, and the Limited interactive-user browser harness.
- **Machine proof:** Testing setup completed; exact host/TLS, loopback-only Agent, anonymous health/CORS/HTTP/1.1, Chrome/Edge 151 policy verification, and disposable prerequisites remain live.
- **Browser proof:** Chrome and Edge launched through the task-scoped runner with matching channels, fresh profiles, non-elevated Medium integrity, and no recorded credentials/tokens. The exact-origin attempts were blocked by the unavailable Support Hub page; the explicit localhost smoke path rendered the page but did not confirm protected Agent reads.
- **Current blocker:** `https://support-hub.integration.test:4443/tools/pos-maintenance` did not serve the real Support Hub page in either channel. Protected browser reads, Negotiate/session, authorization, mutation-token, UI, and Agent-dispatched service control were not run.
- **Evidence:** `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` has a timestamped INT-13C section; plan/readiness/state record the automatic provisioning contract and blocked continuation.
- **Validation:** Pester `16/16` passed; Node syntax and `git diff --check` passed; setup and remove WhatIf passed; authorized idempotent Testing setup and direct policy verification passed; exact-origin and localhost Chrome/Edge harness attempts were truthful `BLOCKED` with Medium/non-elevated launch classification.
- **Next Action:** Serve the real workspace at the configured exact HTTPS origin, rerun both browser channels from the Limited interactive-user launcher, then capture protected reads, server-derived authorization, one-use token, and one bounded opaque-target action if available.
- **Risks:** Never use Production/customer services, wildcard or loopback-disable policies, elevated GUI browsers, raw service names/secrets in evidence, or retries after `OutcomeUnknown`; leave Testing prerequisites running until continuation or explicit cleanup.
