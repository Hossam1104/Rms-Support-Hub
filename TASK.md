# RMS+ Support Hub - Maintenance

**Role:** Implement (INT-06I-F1 Scalar/OpenAPI contract accuracy + live browser closure)
**Branch:** `int-06i-admin-auth-scalar`
**Repository:** `Hossam1104/Rms-Support-Hub`

## Current phase

INT-00 POS cross-project architecture closure and INT-00R transport hardening
are complete. The separate Windows Agent, direct browser-to-loopback boundary,
trusted HTTPS, HTTP/1.1 transport, LNA/browser-policy matrix, Negotiate,
anonymous exact-origin preflight, mutation-token contract, per-device scope,
and clean source-import boundary are canonical in the ADRs and integration
documents.

INT-06I is the single bounded active implementation session. It remediates the
INT-06H UAC-filtered browser authorization finding by resolving local
Administrator membership from the authenticated Windows account rather than
the current browser token, and it adds the permanent non-production
Scalar/OpenAPI documentation gate. No feature operation, POS UI activation,
Support Hub backend relay, or INT-07 work is authorized in this session.

INT-01 - DESTINATION PROJECT / BUILD / CI SKELETON: COMPLETE

INT-02 - PORTABLE DOMAIN / APPLICATION / CONTRACTS IMPORT: COMPLETE

INT-03 - WINDOWS INFRASTRUCTURE + RETAINED WINUI IMPORT: COMPLETE

The approved POS provenance snapshot at
`25922b499d33bd73f241ffc26c212dd000e81433` was imported into destination-owned
projects. Windows Infrastructure and its tests build and pass; retained WinUI
publishes successfully with packaged resources. The Support Hub backend/frontend
remain independent.

That SHA remains the historical INT-01/INT-02/INT-03 import provenance. The
owner-authorized INT-03R correction produced the Agent source provenance
`010abc52dc110cfde3dc2c53e057890ff6edaf97` for INT-04+.

STATUS:
INT-05:
COMPLETE / ACCEPTED AFTER INT-05F

INT-05F:
COMPLETE

INT-CI01:
COMPLETE

PORTABLE UBUNTU POS CI:
GREEN

PRE-EXISTING 9 APPLICATION TEST FAILURES:
RESOLVED

OPENAPI GENERATOR:
ISOLATED TOOLING DEPENDENCY GRAPH

ANGULAR TYPESCRIPT:
6.x

GENERATOR TYPESCRIPT:
5.x PEER-COMPATIBLE

GLOBAL LEGACY PEER BYPASS:
NONE

INT-03:
COMPLETE

INT-03F:
COMPLETE

NEXT:
INT-06 - LIVE TRANSPORT SECURITY EVIDENCE - BLOCKED / FAILED

CLAUDE OPUS PRIVILEGED-BOUNDARY REVIEW:
PROV-1 CLOSED BY INT-03R / LIVE EVIDENCE OPEN

INT-03R:
COMPLETE

CORRECTED POS AGENT PROVENANCE:
010abc52dc110cfde3dc2c53e057890ff6edaf97

INT-04 - AGENT HOST / RUNTIME COMPOSITION:

COMPLETE

The destination-owned Agent is a headless ASP.NET Core Windows Service-capable
host with fixed HTTPS origin `https://rms-pos-agent.localhost:5001`, HTTP/1.1
loopback binding, Negotiate/local-Administrators policy, exact-origin CORS and
Origin enforcement, server-owned mutation-token issuance, safe service-owned
storage ports, and only the health/session/token foundation routes. INT-05 adds
the authoritative versioned OpenAPI document, deterministic Support Hub
generated types, and an isolated direct-Agent Angular transport. INT-05F
isolates the OpenAPI generator's TypeScript 5 peer dependency from the
Angular TypeScript 6 graph. No feature
operations or POS UI activation was added.

INT-CI01 corrected host-dependent Windows path handling in the portable POS
Application maintenance and downloader seams. The destination-owned Windows
path policy is now deterministic on Ubuntu and Windows, the nine pre-existing
Application failures are resolved, and no Agent feature route or POS UI was
activated.

INT-06 - LIVE TRANSPORT SECURITY EVIDENCE:
BLOCKED / FAILED (historical)

INT-06F - ELEVATED LIVE TRANSPORT SECURITY EVIDENCE: BLOCKED / FAILED (historical)

INT-06G - PRE-ELEVATED LIVE TRANSPORT SECURITY EVIDENCE: BLOCKED / FAILED
Machine/certificate/LocalSystem/loopback/Negotiate/CORS/Origin/route evidence
was collected and cleaned.

INT-06H - REAL BROWSER RUNTIME EVIDENCE: COMPLETE AS DEFECT EVIDENCE
Actual installed Chrome 151.0.7922.77 and Edge 151.0.4129.78 proved the exact
public-source secure page, direct browser-to-Agent health/session path, and
exact LNA allow/block behavior. Browser Negotiate/session passed with no SID
exposure, but the normal browser session was not authorized as a local
Administrator: mutation-token returned 403 while an equivalent elevated
Windows request returned safe `operation_not_supported`, exposing the
UAC-filtered-token authorization defect.

INT-06I - UAC-SAFE ADMINISTRATOR AUTHORIZATION + SCALAR/OPENAPI DOCUMENTATION:
IMPLEMENTED / INDEPENDENT SECURITY REVIEW REQUIRED
Production now resolves local Administrator membership from the authenticated
Windows account through `NetUserGetLocalGroups` with `LG_INCLUDE_INDIRECT`,
compares group SIDs to the well-known Built-in Administrators SID, and fails
closed on lookup failure. Session and mutation-token authorization use the
same server-derived boundary; synthetic claims are IntegrationTest-only.
Scalar.AspNetCore `2.16.18` is pinned, docs are Development/IntegrationTest
only, AI Agent/default fonts are disabled, all current operations/responses/
DTO properties are documented, and generated OpenAPI/client drift is checked.

INT-06I-F1 corrected framework-versus-endpoint response representations,
added runtime response-shape and route/OpenAPI parity guards, and added a
docs-only local Scalar CSP so the non-production page renders its local assets
without external fonts or CDN bundles.

POST-REMEDIATION LIVE BROWSER EVIDENCE: PASS
Real installed Chrome 151.0.7922.109 and Edge 151.0.4129.78 ran as normal
medium-integrity limited-token processes. Both direct Agent sessions returned
200 with `isAuthorized=true`; unknown mutation requests returned safe 400
`operation_not_supported`, with no SID exposure, no token, and no POS
operation executed. Production Kestrel returned 404 for `/scalar`, `/scalar/`,
and `/openapi/v1.json`; Development Scalar rendered all four foundation
operations with local assets only.

PR STATE: OPEN / NOT MERGED / INDEPENDENT SECURITY REVIEW REQUIRED

VALIDATION:
POS Release build 0 warnings/errors; Domain 7/7, Application 76/76,
Infrastructure 60/60, Agent 95/95; frontend 341/341 and production build;
deterministic OpenAPI/client generation; Agent and frontend audits report no
vulnerabilities; and retained WinUI publish produced the executable plus 27
`.pri` and 66 `.xbf` resources. The repository-wide `scripts/build.ps1` still
has two unrelated pre-existing backend route-status test failures.

NEXT:
INDEPENDENT SECURITY REVIEW

INT-07: OWNER AUTHORIZATION REQUIRED / NOT YET EXECUTED

INT-07 MUST NOT BE EXECUTED IN THIS SESSION.
