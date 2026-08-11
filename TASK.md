# RMS+ Support Hub - Maintenance

**Role:** Plan (INT-02 portable Domain/Application/Contracts import; owner gate)
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

The isolated `/pos` solution, portable project boundaries, Windows skeletons,
and destination-owned build lanes are established. No POS business or runtime
source was imported. The existing Support Hub backend and frontend remain
independent and unchanged.

STATUS:
INT-01 COMPLETE

INT-02 - PORTABLE DOMAIN / APPLICATION / CONTRACTS IMPORT

STATUS:
OWNER AUTHORIZATION REQUIRED / NOT YET EXECUTED

Start INT-02 only after owner/planner verification and explicit authorization,
using a fresh context. INT-02 may import only the authorized portable
Domain/Application/Contracts source boundary. Do not import Windows
Infrastructure, Agent runtime, WinUI, POS Angular, or privileged operations in
INT-02.

INT-02 MUST NOT BE EXECUTED IN THIS SESSION.
