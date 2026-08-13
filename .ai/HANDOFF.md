# Active Handoff

- **Status:** Blocked
- **Gate:** INT-13 representative-device and live operational evidence under owner-authorized INT-13P.
- **Completed:** Exact loopback host, trusted LocalMachine certificate, Release Agent service, dedicated disposable Testing service, server-owned opaque allow-list target, setup/remove tooling, and independent SCM harness lifecycle.
- **Live proof:** Anonymous health, trusted HTTPS, HTTP/1.1, loopback-only listener, exact-origin preflight, and wrong-origin/method/header rejection passed.
- **Current blocker:** Protected Negotiate calls return `401`; the non-browser SSPI context reports `SEC_E_NO_CREDENTIALS`. No connected in-app/extension Chrome or Edge browser is available, so LNA, browser Negotiate, UI reads, mutation-token, and Agent-dispatched SCM evidence are not run.
- **Evidence:** `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` contains the historical blocked run and the timestamped INT-13P partial run.
- **Validation:** Provisioning publish/health and `-WhatIf` setup/remove previews passed; configured POS Release build passed with 0 warnings/errors; POS tests passed 7/76/60/114; OpenAPI generation, frontend 345-test suite, and Angular build passed. Broad `scripts/build.ps1` reaches 190 backend passes and the two known unchanged 404-vs-405 route assertions after the stale API lock was removed; frontend `npm ci` reports 5 existing audit vulnerabilities and blocked optional install scripts.
- **Next Action:** From a connected Chrome/Edge session with usable Windows Negotiate credentials, run the fixed direct path against the already-running Testing Agent, capture protected reads/token/typed disposable-service outcomes, then update this handoff and evidence truthfully.
- **Risks:** Do not retry an `OutcomeUnknown`; do not remove the owned prerequisites until live continuation or explicit cleanup is complete; never use Production/customer services.
