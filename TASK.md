# RMS+ Support Hub - Maintenance

**Role:** Plan (INT-04 Agent Host / Runtime Composition; owner gate)
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
publishes successfully with packaged resources. The Agent remains inert, and
the Support Hub backend/frontend remain independent.

That SHA remains the historical INT-01/INT-02/INT-03 import provenance. The
owner-authorized INT-03R correction produced the candidate Agent source
provenance `010abc52dc110cfde3dc2c53e057890ff6edaf97` for INT-04+; it tracks
the existing Agent artifact catalog without authorizing Agent runtime work.

STATUS:
INT-03 COMPLETE

INT-03:
COMPLETE

INT-03F:
COMPLETE

NEXT:
CLAUDE OPUS PRIVILEGED-BOUNDARY FOLLOW-UP

CLAUDE OPUS PRIVILEGED-BOUNDARY REVIEW:
BLOCKED - PROV-1

INT-03R:
COMPLETE

CORRECTED POS AGENT PROVENANCE:
010abc52dc110cfde3dc2c53e057890ff6edaf97

INT-04 — AGENT HOST / RUNTIME COMPOSITION:

OWNER AUTHORIZATION REQUIRED
NOT YET EXECUTED

INT-04:
NOT EXECUTED

INT-04 MUST NOT BE EXECUTED IN THIS SESSION.
