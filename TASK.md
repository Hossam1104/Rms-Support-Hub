# GPT-5.6 SOL
## P0-C — External Server Configuration Acceptance Review

MODEL: GPT-5.6 SOL | ROLE: Review-only
PROGRAMME: Staging-Safe Release Candidate v1 | MILESTONE: P0-C External Server Configuration Review
Repository: `D:\AI Tools\DBS\Rms-Support-Hub` | Branch: `feat/p0c-external-server-config`

### 1. REVIEW OBJECTIVE
Perform an independent, strict acceptance review of the P0-C external server-owned JSON configuration implementation and verified local Hosting Bundle prerequisite.

This is a **REVIEW-ONLY** session.
DO NOT merge automatically.
DO NOT create IIS sites or application pools.
DO NOT deploy the application or create real Testing configuration files.

### 2. VERIFICATION REQUIREMENTS
The reviewer must independently inspect and verify:
1. **Exact PR Head Identity:** Verify git HEAD and remote PR head match exactly.
2. **Diff Review:** Inspect the complete technical diff for `feat/p0c-external-server-config` against `main`.
3. **Configuration Precedence:** Confirm effective resolution order:
   `packaged appsettings.json` < `packaged appsettings.{Environment}.json` < `server-owned external JSON` < `environment variables` < `command-line arguments`.
4. **Path & Security Boundaries:** Verify that `SUPPORTHUB_EXTERNAL_CONFIG_PATH`:
   - Rejects URLs (`http://`, `https://`, `ftp://`, `file://`), UNC/network paths (`\\...`, `//...`), relative paths, and paths inside the application content root.
   - Fails closed on missing file, unreadable file, or malformed JSON without disclosing secrets.
   - Maintains server-owned boundaries (no browser authority, no client path control).
5. **Package Gates & Exclusions:** Verify that `appsettings.override.json` and `*.override.json` are strictly excluded from git, publish outputs, and RC packages.
6. **Validation Suite:** Run and verify:
   - Backend Release build (0 warnings, 0 errors with `--warnaserror`).
   - Full backend Release tests (`dotnet test backend/RmsSupportHub.slnx -c Release`).
   - Full frontend tests (`npx ng test --watch=false --progress=false`).
   - Frontend production build (`npm run build -- --configuration production`).
   - Riyal asset verification (`npm run test:riyal-asset`).
   - Offline runtime tests (`.\scripts\test-offline-runtime.ps1`).
   - PowerShell quality checks (`.\scripts\test-powershell-quality.ps1`).
   - Memory and context checks (`python .ai/scripts/context.py`, `python .ai/scripts/check_memory.py`, `git diff --check`).
7. **Exact-Head CI Workflows:** Verify exact-head runs for both `Support Hub CI` and `POS CI`.

### 3. OUTCOME
Conclude with a clear verdict: **ACCEPT** or **REQUEST CHANGES**.
If accepted, record approval for subsequent owner merge action.
