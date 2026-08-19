# Current Project State

- **Updated:** 2026-08-19
- **Active branch:** `feat/p0c-external-server-config` (branch off baseline `4e4de71b941e92ac6fcb1885940ca28fe13a778e`).
- **Status:** P0-C local IIS hosting prerequisite verified on HOSSAM and external server-owned JSON configuration support implemented.
  - **Hosting Bundle prerequisite on HOSSAM:** Owner authorized local Hosting Bundle installation. Microsoft .NET 10.0.11 - Windows Server Hosting (10.0.11.26373) / Microsoft ASP.NET Core Module V2 (110.0.26205.0) installed and verified: `C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll` (v20.0.26205.11) and `C:\Program Files (x86)\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll` (v20.0.26205.11). W3SVC/WAS Running; stock Default Web Site responds HTTP 200 OK on port 80; no reboot was required; no `RmsSupportHub.Testing` site, pool, or path was created.
  - **External server configuration support:** Server-owned environment variable `SUPPORTHUB_EXTERNAL_CONFIG_PATH` discovers external JSON outside the deployable application root (e.g. `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json`). Path validation rejects URLs (`http://`, `https://`, `ftp://`), UNC/network paths (`\\...`, `//...`), relative paths, and paths inside the content root. Fail-closed on missing file, unreadable file, or malformed JSON without disclosing secret values.
  - **Configuration precedence:** `packaged appsettings.json` < `packaged appsettings.{Environment}.json` < `server-owned external JSON` < `environment variables` < `command-line arguments`.
  - **Package gates & exclusions:** `appsettings.override.json` and `*.override.json` are ignored in `.gitignore`, removed from publish staging in `build-release-candidate.ps1`, forbidden in `verify-release-candidate.ps1`, and regression-tested in `test-release-candidate-safety.ps1`.
  - **Readiness:** REPOSITORY READY = YES, HOSTING PREREQUISITE = READY, EXTERNAL CONFIG SUPPORT = IMPLEMENTED, TESTING DEPLOYED = NO, TESTING ACCEPTED = NO, PRODUCTION READY = NO.
- **Next milestone:** P0-C External Server Configuration Acceptance Review (GPT-5.6 Sol, Review-only). NO IIS deployment, site creation, or merge.

## Validation baseline

- .NET SDK: 10.0.400 repository-wide pin.
- Backend Release build: 0 warnings, 0 errors (`--warnaserror`).
- Backend Release tests: 281 passed / 0 failed (including 28 focused unit/integration tests for external configuration loader and precedence).
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
