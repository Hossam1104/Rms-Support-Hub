# Active Handoff

- **Status:** Blocked
- **Gate:** INT-13D secure Support Hub origin and final protected browse closure on PR #7 branch `int-13p-testing-agent-provisioning`.
- **Checkpoint:** implementation `77c5d70`; current documentation head `b967f4f`; PR #7 remains open pending live evidence.
- **Completed:** Exact Testing origin configuration; separate loopback host and certificate ownership; real Angular/API external staging and Kestrel startup path; ownership-scoped cleanup; exact-origin browser launcher integration.
- **Validation:** Focused Pester `22/22`; POS Release build/tests and WinUI publish passed; frontend `56/56` files / `345/345` tests and production build passed; backend Release build passed; PowerShell/Node syntax and `git diff --check` passed; PR #7 CI passed all five lanes.
- **Known baseline:** Repository-wide `scripts/build.ps1` remains `190/192` with two unchanged 404-vs-405 backend assertions; no task-scoped source defect was identified there.
- **Live blocker:** Current PowerShell is non-elevated. Setup/start correctly refuse before machine writes; Support Hub port 4443 and Agent port 5001 are not listening, and the existing Agent/disposable services are stopped.
- **Browser blocker:** The connected in-app browser surface reported no available channels; the repository Chrome/Edge launcher requires an elevated parent and was not run for INT-13D.
- **Not claimed:** Live secure-origin responses, certificate provisioning, Chrome/Edge protected reads, Negotiate/session, authorization, mutation-token, UI proof, or Agent-dispatched service control.
- **Changed files:** `scripts/PosTestingConfiguration.psm1`, `scripts/PosSupportHubProvisioning.psm1`, `scripts/start-pos-agent-testing.ps1`, setup/remove/browser launcher integration, exact-origin Agent fixture/evidence, focused tests, and INT-13 docs/state.
- **Next action:** In an elevated owner-authorized Testing PowerShell, run `scripts/start-pos-agent-testing.ps1 -IUnderstandTestingOnly`; verify the exact root/deep route and Agent health, then run both Limited interactive-user browser channels before any disposable service action.
- **Risks:** Never use Production/customer services, wildcard or loopback-disable policies, elevated GUI browsers, raw credentials/tokens/private keys, or retries after `OutcomeUnknown`.
