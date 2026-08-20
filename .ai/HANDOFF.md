Status: Blocked

Completed:
- Read live task/state and established clean `main@1cfe651ea4d6a2ab914f2d32e8006f6c55c023be`.
- Verified `4200`, `5200`, and `8080` local Hub/API surfaces; browser card/guard contracts remain exact and secure.
- Proved `support-hub.integration.test` resolves to `127.0.0.1`, TCP `4443` has no listener, and direct secure navigation is connection-refused / browser `support_hub_unavailable`.
- Proved the matching INT-13 provisioning state file is missing while the old staged runtime remains; its build identity is stale (`03e2c02`, 2026-08-15).
- Proved the project-named Agent service is loopback-only and its anonymous health endpoints return HTTP 200.
- No customer/Production contact, POS mutation, machine write, repository product change, commit, push, or PR occurred.

Validation:
- Frontend: 59 files / 362 tests passed; production build passed.
- Backend: Release build passed with 0 warnings/errors; 281 tests passed.
- Focused INT-13D Pester: 61 passed / 0 failed; PowerShell quality: 37 files clean; `git diff --check` passed.
- Full POS solution attempt: Domain 12/0, Application 82/0, Infrastructure 113/42, Agent integration 58/113; the failures are environment-blocked machine ACL/control-file and related live-composition checks in the non-elevated shell, and no navigation defect was implicated.

Exact next action / owner packet:
- HOSSAM, Testing only; elevated Administrator session; INT-13P/INT-13D local Support Hub runtime resources only.
- First explicitly reconcile the missing ownership state versus the occupied `C:\ProgramData\DBS\RmsSupportHub\Int13Testing\SupportHubRuntime`; the existing scripts intentionally refuse state-loss adoption/cleanup. Restore a verified owner state backup, or authorize a reviewed cleanup/reprovision of only the exact marker-owned hosts entries, certificates, Agent resources, and stale SupportHubRuntime. Do not infer ownership from markers alone.
- After ownership is restored, run `scripts/start-pos-agent-testing.ps1 -IUnderstandTestingOnly`, then verify secure `4443` root/deep-link/build identity and Agent `5001` health. Keep Production/customer/RMS/database/native-service actions out of scope.
