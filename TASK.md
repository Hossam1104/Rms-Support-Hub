# P0-D - Testing Integration & Operational Readiness

MODEL: Gemini 3.7 | ROLE: Implementation Preparation / Operational Readiness
PROGRAMME: Staging-Safe Release Candidate v1 | MILESTONE: P0-D Testing Integration & Operational Readiness
Repository: `D:\AI Tools\DBS\Rms-Support-Hub` | Target: `HOSSAM` (Local Windows IIS)

## 1. Objective and execution preference

Prepare and verify the deployed `RmsSupportHub.Testing` IIS site on HOSSAM with
authorized Testing external configuration and bounded end-to-end workflows.
Use Gemini 3.7 for bounded preparation, validation, docs, and config work;
do not consume Luna without Sol escalation for a genuinely high-risk problem.

## 2. Environment boundaries

- Local HOSSAM work includes IIS, local config, development servers, builds, and tests.
- Shared/customer Testing RMS APIs, databases, and gateways require an explicit operation packet before first contact.
- Production is strictly forbidden and outside P0-D authorization.

## 3. Current deployed baseline

- Host/site/pool: `HOSSAM` / `RmsSupportHub.Testing` / No Managed Code, Integrated, ApplicationPoolIdentity.
- URL: `http://localhost:8080/`; physical path: `C:\inetpub\RmsSupportHub.Testing`.
- External config: `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json`; authority: `SUPPORTHUB_EXTERNAL_CONFIG_PATH`.
- Current external config: `{}`. Deployed RC source: `13d590674e894ea720d2f4e3407c23d560923273`; build ID: `c372ecaee6a7d7bc0f026599b1d793f4f1d342f5ce479fa1809e37fa7b900a46`.

## 4. Mandatory safety gates

Do not guess, discover, print, log, commit, or screenshot real Testing values.
Owner input is required for endpoints, connection strings, credentials,
registrations, and database overrides. Configuration presence never authorizes
downstream action. Every shared Testing contact must identify environment,
system, method/action, route/query, body/data, expected effect, reason,
rollback/recovery, and read-only/mutating classification. Without separate
authorization prohibit send, cancel, resend, DB INSERT/UPDATE/DELETE/MERGE,
side-effecting procedures, Main Server/native RMS/customer configuration
mutation, and POS machine mutation. Production is always forbidden.

## 5. Initial executable scope

1. Verify `http://localhost:8080/api/health/ready` and local HOSSAM baseline.
2. Inventory required Testing keys from current repository contracts and produce an OWNER INPUT REQUIRED matrix.
3. Validate JSON, tier, TLS, custom-endpoint prohibition, disabled Production registrations, Testing-only resolution, cancel keys, and redaction offline.
4. Prepare separate exact read-only and mutating packets; execute neither shared probes nor mutations without explicit authorization.
5. Keep external config outside Git/package and maintain REPOSITORY READY=YES, TESTING DEPLOYED=YES, TESTING ACCEPTED=YES, PRODUCTION READY=NO.
6. Update durable state with facts only and stop at the authorization boundary.

## 6. FULL NEXT EXECUTABLE PROMPT - P0-D1 owner-authorized continuation

Continue from repository code, `.ai/STATE.md`, and `.ai/HANDOFF.md`; do not
reconstruct prior work from chat. Preconditions: preserve
`SupportHub:DeploymentTier=Testing`, `Outbound:VerifyTls=true`,
`SupportHub:AllowCustomEndpoints=false`, and disabled Production registrations;
the owner has supplied the six values through the external file (never Git,
logs, screenshots, CI, or memory files):
`ConnectionStrings:GhcEcommerce`, `ModuleEndpoints:GhcTesting`,
`ModuleCancelEndpoints:GhcTesting`, `ConnectionStrings:UpcEcommerceTest`,
`ModuleEndpoints:UpcTesting`, and `ModuleCancelEndpoints:UpcTesting`; and the
owner has separately authorized each exact shared read-only packet.

1. Recheck status, HEAD, origin/main, `python .ai/scripts/context.py`, and handoff; preserve user work.
2. Parse/validate external JSON offline. Confirm Testing, TLS, custom endpoints false, Production disabled, GHC/UPC Testing/cancel resolution, and redaction. Never expose values.
3. Validate local root, readiness, modules, `/api/modules/health`, Online Orders deep links/cards, unavailable/error paths, redaction, and restart persistence. Treat `/api/modules/health` and `POST /api/modules/{key}/test-endpoint?envKey=...` as TCP connectivity checks only, not HTTP/API functional validation.
4. After authorization only, run bounded GHC and UPC Testing TCP checks: local `GET /api/modules/health` for the sweep or `POST /api/modules/{key}/test-endpoint?envKey=GHC%20Testing|UPC%20Testing` per module. Record DNS, host/port classification, TCP result, timeout, TLS applicability, and target environment. Never call Production or send a payload.
5. After separate authorization and exact SQL review, run only existing bounded read-only database work: `POST /api/modules/{key}/test-db?envKey=...` for connection health (no SQL), `GET /api/modules/upc_ecommerce/branches?envKey=UPC%20Testing`, `GET /api/modules/upc_ecommerce/lookup/item?code=<owner-approved>&branchCode=<owner-approved>&envKey=UPC%20Testing`, `GET /api/modules/upc_ecommerce/lookup/consumer?phone=<owner-approved>&envKey=UPC%20Testing`, and UPC `GET /api/modules/upc_ecommerce/order-requests?envKey=UPC%20Testing&page=1&pageSize=25` plus detail/by-order reads using returned identifiers. GHC item/consumer SQL is explicitly unverified; accept it only after schema/query review. GHC OrderRequests remains unavailable. Reject DML, dynamic mutation SQL, persistent temp workflows, unverified procedures, and ambiguous connections.
6. Classify GHC Uni-Commerce from current source as a registered-but-disabled placeholder until a real Testing API/DB contract and read-only workflow exist. Do not invent endpoints or claim acceptance.
7. Keep separate prohibited-operation packets for `POST /api/modules/{key}/send-request`, `POST /api/modules/{key}/cancel-order`, `POST /api/modules/{key}/order-requests/{id}/cancel`, and `POST /api/modules/{key}/order-requests/{id}/resend`. Do not execute them; any future authorization must include environment, system, method, route/body, expected effect, reason, rollback/recovery, and mutating classification. Also prohibit all DML/procedures, Production, Main Server/customer mutation, and POS mutation.
8. Run focused tests, required build/test suite, frontend production build, config/environment safety, OpenAPI/client drift where applicable, `python .ai/scripts/check_memory.py`, `python .ai/scripts/context.py`, and `git diff --check`; report exact counts and distinguish external unavailability from failures.
9. Change product code only for a proven defect, with a minimal regression-tested fix and exact-head/CI review stop. Never patch around missing owner config.
10. Update STATE/HANDOFF with facts, preserve the P0-D0 POS release-PKI blocker and Production readiness NO, keep external config outside Git, and stop at authorization.

Required report: starting/ending SHA, changed files, redacted key matrix,
authorized contacts or `NONE`, GHC/UPC discovery/connectivity/read-only
results, Uni-Commerce maturity/gap, exact validation counts, unchanged POS
blocker, explicit no-Production/no-unauthorized-contact/no-order-mutation/
no-DB-mutation/no-Main-Server-mutation statement, and next milestone.
