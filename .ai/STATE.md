# Current Project State

- **Updated:** 2026-08-14
- **Branch:** `agent/rms-installation-diagnostics` (RMS installation discovery and database diagnostics slice; based on synchronized `main` `8349ef4`)
- **Repository:** `Hossam1104/Rms-Support-Hub`; local path `D:\AI Tools\DBS\Rms-Support-Hub`
- **Programme:** INT-00 through INT-08 and INT-13 complete/accepted/validated live on representative Testing machine. The independent POS First-Release Security & Readiness Review (Claude Opus 5) is complete: 0 Critical, 0 High, 2 Medium (M-1, M-2), 6 Low, 3 Informational. All Low/applicable-Informational findings are remediated on the current branch; see `docs/reviews/POS_FIRST_RELEASE_SECURITY_REVIEW_2026-08-14.md`.
- **Release approval scope — do not conflate these:**
  - **Testing-environment first release: APPROVED.**
  - **Production/customer deployment: NOT APPROVED.** Blocked on M-1 (managed-endpoint browser policy — needs fleet delivery, e.g. GPO/Intune, replacing the single-device provisioning script) and M-2 (production certificate lifecycle — needs issuance/renewal/distribution/revocation across a fleet, replacing the single-device self-managed certificate). Neither is started; both are explicitly out of scope for the current remediation branch and are handed to the next execution session as their own task via root `TASK.md`.
- **Current gate:** INT-06I independent security review PASS (PR #3 merged); INT-07 PR #4 merged; INT-08 PR #5 merged; INT-13 PR #7 complete with live operational evidence (Chrome/Edge Medium-integrity Negotiate IWA, protected reads, mutation-token binding/replay rejection, Agent-dispatched disposable service control). First-release security review remediation (L-1–L-6, I-1–I-2) is complete on `pos-first-release-review-remediation`, pending validation and PR.
- **Current feature slice:** RMS installation discovery, sanitized Branch/Cashier database diagnostics, bounded endpoint reachability, canonical RMS SCM visibility, and Support Hub dashboard integration are implemented on `agent/rms-installation-diagnostics`. M-1/M-2 remain open and untouched.

## Application

- Angular 22 SPA and .NET 10 Web API. Prompt Studio and Online Orders are
  available; `/tools/pos-maintenance` is a direct operational POS evidence and
  service-control workspace.
- Routes are lazy and typed through `ToolRouteData`. Business/API, payload,
  SQL, module-key, and persisted-storage contracts remain unchanged.
- POS feature ownership is the separate `RmsSupportHub.Pos.Agent`; the Hub
  never relays privileged POS traffic through `RmsSupportHub.Api`, `Core`, or
  `Data`.

## POS architecture and gates

- Agent origin: `https://rms-pos-agent.localhost:5001`; headless,
  Windows-Service-capable, loopback-only, HTTPS/HTTP/1.1, production Negotiate,
  exact-origin CORS, server-resolved local Built-in Administrators membership,
  and fail-closed SID handling.
- INT-03R Agent provenance: `010abc52dc110cfde3dc2c53e057890ff6edaf97`;
  historical INT-01/02/03 import provenance: `25922b499d33bd73f241ffc26c212dd000e81433`.
- INT-05 owns versioned `/pos/openapi`, generated client, and direct
  `HttpBackend` transport. INT-06I owns UAC-safe authorization and non-
  production Scalar/OpenAPI. INT-07 owns device, connectivity, redacted
  configuration, service-status reads, and the direct Angular workspace.
- INT-08 owns only typed `services.control`: opaque allow-listed service IDs,
  target/method/path-bound one-use tokens, bounded idempotency/concurrency, and
  truthful typed outcomes. Production runtime OpenAPI remains hidden.
- The RMS discovery slice adds a server-owned `IRmsInstallationDiscovery`, a
  dedicated secret-bearing database diagnostic seam, fixed `DB_NAME()` probes,
  and the canonical RMS service catalog (`RMS.BranchService`,
  `RMS.CashierService`, `RMSServicesManager`). Raw connection strings and
  credentials never enter discovery, API, or UI models.
- INT-13 owns certificate/hostname/representative-device/live operational evidence:
  - Exact Support Hub origin `https://support-hub.integration.test:4443`
  - Direct Agent origin `https://rms-pos-agent.localhost:5001`
  - Automated Chrome & Edge IWA and loopback network policy provisioning
  - Automated `BackConnectionHostNames` REG_MULTI_SZ provisioning
  - Non-elevated Medium-integrity interactive browser evidence harness
  - Disposable Windows Service harness `RmsSupportHub.Pos.Int13.TestService`
  - Live Negotiate authentication, protected reads, mutation token issuance/consumption/replay rejection, and disposable service restart verified.

## Security and review record

- INT-06/06F/06G/06H blocked states are historical. INT-06I remediated local
  Administrator resolution with indirect local-group membership and the
  Built-in Administrators SID; independent review PASS found no Critical/High.
- Post-remediation Chrome/Edge browser authorization and Scalar/OpenAPI
  evidence passed; no SID/token exposure. PR #3 is merged normally.
- INT-13 live operational evidence recorded in `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md`
  with zero credential prompts, server-derived authorization, token binding, and replay rejection.
  An L-2 audit (2026-08-14) corrected three inaccurate code-attribution claims in that document
  without altering any historical live observation; see the document's own "L-2 Evidence Audit —
  Corrections" section.
- POS First-Release Security & Readiness Review (Claude Opus 5, 2026-08-14):
  `docs/reviews/POS_FIRST_RELEASE_SECURITY_REVIEW_2026-08-14.md`. Testing-scoped first release
  approved; Production/customer deployment remains blocked on M-1 and M-2 (see above).

## Compatibility contracts

Persisted keys are byte-exact; no migration exists:

```text
onlineOrderTool.activeEnvironment.<moduleKey>
qa-support-hub:theme
qa-support-hub:motion
qa-support-hub.prompt-studio.history
qa-support-hub.prompt-studio.bug-draft
qa-support-hub.prompt-studio.story-draft
qa-support-hub.prompt-studio.test-case-draft
order-tool.sidebar-collapsed
```

Raw colors stay in token files. The Hub scene is decorative/lazy and safely
degrades; all current UI feature styles consume design tokens.

## Validation baseline

| Gate | Result |
|---|---|
| POS Release build | Passed, 0 warnings/errors with Testing origin set |
| POS tests | Domain 9, Application 76, Infrastructure 71, Agent 119 passed |
| Frontend tests | 56 files / 345 tests passed |
| Frontend production build | Passed; 456.48 kB initial, 40.72 kB POS lazy; no budget warnings |
| OpenAPI/client drift | OpenAPI regenerated during POS Release build; openapi-typescript client generation deterministic |
| Real RMS read-only validation | Five installed files parsed; Branch/Cashier databases reached with fixed `SELECT DB_NAME()`; metadata consistent; Branch/Cashier SCM running; Services Manager absent |
| Pester tests | 22/22 passed (`scripts/tests/*.Tests.ps1`) |
| POS Agent live | `https://rms-pos-agent.localhost:5001/health/live` and `/health/ready` returned 200 (HTTP/1.1) |
| Secure Support Hub live | `https://support-hub.integration.test:4443/` and `/tools/pos-maintenance` returned 200 |
| Chrome normal-user IWA | Passed (`Medium` integrity, non-elevated, Negotiate auth, 6 protected reads 200, local Admin authorized) |
| Edge normal-user IWA | Passed (`Medium` integrity, non-elevated, Negotiate auth, 6 protected reads 200, local Admin authorized) |
| Mutation token & replay | Passed (one-use consumption 200 accepted, replay rejection 403 Forbidden) |
| Disposable service control | Passed (`RmsSupportHub.Pos.Int13.TestService` restarted via Agent SCM dispatch, refreshed to `running`) |
| Evidence record | `docs/evidence/POS_INT13_LIVE_OPERATIONAL_EVIDENCE.md` updated with PASS closure matrix |

## Deferred boundaries

- Testing is default; no Production calls, SQL changes, or deployment were performed.
- RMS validation performed no service control and no database write; the fixed
  SQL probe only queried `DB_NAME()`.
- UPC live/fixture acceptance and deployment/Production acceptance remain deferred.
- `ConnectionStrings:UpcEcommerceTest` is absent locally; related live calls
  are environment setup, not an INT-07 defect.

