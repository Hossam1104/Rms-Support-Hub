# RMS+ Support Hub - Maintenance

**Role:** Plan (INT-03 Windows Infrastructure + retained WinUI import; owner gate)
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
and destination-owned build lanes are established. INT-02 imported the approved
portable Domain/Application/Contracts source and its two portable test suites.
The existing Support Hub backend and POS frontend remain independent; no POS
UI, Agent runtime, Infrastructure, WinUI, or privileged operation was added.

STATUS:
INT-01 COMPLETE

INT-02 COMPLETE

INT-03 - WINDOWS INFRASTRUCTURE + RETAINED WINUI IMPORT

STATUS:
OWNER AUTHORIZATION REQUIRED / NOT YET EXECUTED

INT-02 imported only the authorized portable Domain/Application/Contracts
boundary from POS provenance `25922b499d33bd73f241ffc26c212dd000e81433`.
Domain/Application/Contracts target `net10.0`; Infrastructure remains a
skeleton and Agent remains inert. Do not import POS Infrastructure, Agent
runtime, WinUI, POS Angular, or privileged operations until INT-03 or a later
authorized gate.

INT-03 MUST NOT BE EXECUTED IN THIS SESSION.
