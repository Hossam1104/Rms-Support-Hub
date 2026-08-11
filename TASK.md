# RMS+ Support Hub - Maintenance

**Role:** Implemented (INT-04 Agent Host / Runtime Composition; next gate owner authorization)
**Branch:** `main`
**Repository:** `Hossam1104/Rms-Support-Hub`

## Current phase

INT-00 POS cross-project architecture closure and INT-00R transport hardening
are complete. The separate Windows Agent, direct browser-to-loopback boundary,
trusted HTTPS, HTTP/1.1 transport, LNA/browser-policy matrix, Negotiate,
anonymous exact-origin preflight, mutation-token contract, per-device scope,
and clean source-import boundary are canonical in the ADRs and integration
documents.

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
INT-04 COMPLETE

INT-03:
COMPLETE

INT-03F:
COMPLETE

NEXT:
INT-05 - BROWSER TRANSPORT / OPENAPI / CLIENT ADAPTER - OWNER AUTHORIZATION REQUIRED

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
Origin enforcement, mutation-token foundation, safe service-owned storage
ports, and only the `/health/live`, `/health/ready`, and `/api/v1/session`
foundation routes. No feature operations, OpenAPI, Angular, or Support Hub
frontend/backend integration was added.

NEXT:
INT-05 - BROWSER TRANSPORT / OPENAPI / CLIENT ADAPTER

OWNER AUTHORIZATION REQUIRED
NOT YET EXECUTED

INT-05 MUST NOT BE EXECUTED IN THIS SESSION.
