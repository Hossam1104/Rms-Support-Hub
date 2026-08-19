# Current Project State

- **Updated:** 2026-08-20
- **Active branch:** `feat/p0c-external-server-config` (PR #25 accepted; pending merge to `main`).
- **Status:** P0-C external server-owned configuration support accepted by Sol review; HOSSAM hosting prerequisite ready.
  - **Sol review decision:** ACCEPT (Critical: 0, High: 0, Medium: 0).
  - **External server configuration:** ACCEPTED. Server-owned `SUPPORTHUB_EXTERNAL_CONFIG_PATH` discovers external JSON outside content root; fail-closed path/content validation; strictly excluded from RC packages.
  - **Hosting Bundle prerequisite on HOSSAM:** READY. Microsoft .NET 10.0.11 - Windows Server Hosting (10.0.11.26373) / ANCM v2 (`aspnetcorev2.dll` v20.0.26205.11) verified.
  - **Testing IIS:** NOT DEPLOYED (`RmsSupportHub.Testing` site, pool, and path `C:\inetpub\RmsSupportHub.Testing` remain absent).
  - **Testing acceptance:** NOT COMPLETE.
  - **Production:** NOT READY (unresolved external Production/POS/fleet/PKI gates preserved).
  - **Non-blocking Low backlog:**
    - `L-P0C-1`: External-config path validation rejects UNC/URL paths but does not independently classify mapped drive letters as DriveType.Network. Local `C:\ProgramData` approved for HOSSAM target.
    - `L-P0C-2`: No dedicated permission-denied filesystem regression independently exercises the UnauthorizedAccessException branch, although implementation fails closed.
  - **Readiness:** REPOSITORY READY = YES, HOSTING PREREQUISITE = READY, EXTERNAL CONFIG SUPPORT = ACCEPTED, TESTING DEPLOYED = NO, TESTING ACCEPTED = NO, PRODUCTION READY = NO.
- **Next milestone:** PR #25 Merge, RC Regeneration from merged `main`, and P0-C HOSSAM Deployment Approval Packet preparation.

## Validation baseline

- .NET SDK: 10.0.400 repository-wide pin.
- Backend Release build: 0 warnings, 0 errors (`--warnaserror`).
- Backend Release tests: 281 passed / 0 failed.
- Frontend tests: 362 passed / 0 failed across 59 test files.
- Frontend production build: passed cleanly (`npm run build -- --configuration production`).
- Riyal asset verifier: passed (`Saudi_Riyal.svg`, 924 bytes).
- Offline runtime negative tests: passed (`test-offline-runtime.ps1`).
- PowerShell quality: passed (`test-powershell-quality.ps1`).
- Broad build: passed (`scripts/build.ps1`).

## Safety and environment boundary

- No IIS site or application pool created (`RmsSupportHub.Testing` remains absent).
- No deployment performed (`C:\inetpub\RmsSupportHub.Testing` remains absent).
- No external configuration file created on disk (`C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json` remains absent).
- No Production, customer, RMS gateway, or database mutation.
