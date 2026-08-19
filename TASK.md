# P0-C HOSSAM FINAL IIS DEPLOYMENT — EXPLICIT OWNER APPROVAL REQUIRED

MODEL: Claude Sonnet 5 HIGH | ROLE: Implement / Operational Verification
PROGRAMME: Staging-Safe Release Candidate v1 | MILESTONE: P0-C HOSSAM Controlled Testing Deployment & Read-Only Acceptance
Repository: `D:\AI Tools\DBS\Rms-Support-Hub` | Target: `HOSSAM` (Local Windows IIS)

### 1. MANDATORY APPROVAL GATE
NO DEPLOYMENT, IIS MUTATION, SITE CREATION, OR FILE SYSTEM MUTATION MAY OCCUR UNTIL THE USER PROVIDES EXPLICIT APPROVAL FOR THE EXACT APPROVAL PACKET BELOW.
TASK.md EXISTING DOES NOT AUTHORIZE DEPLOYMENT.

### 2. P0-C DEPLOYMENT OBJECTIVE
Once explicitly authorized by the owner, execute the controlled initial deployment of the fresh Release Candidate to local IIS on host `HOSSAM` in the `Testing` environment and collect read-only acceptance evidence.

### 3. APPROVED LOCAL TARGET SPECIFICATION
- Host: `HOSSAM`
- Environment: `Testing`
- IIS Site: `RmsSupportHub.Testing`
- Application Pool: `RmsSupportHub.Testing` (No Managed Code, Integrated, ApplicationPoolIdentity)
- Physical Path: `C:\inetpub\RmsSupportHub.Testing`
- Binding: `http://*:8080/`
- External Config Path Authority: `SUPPORTHUB_EXTERNAL_CONFIG_PATH`
- External Config File: `C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json`
- Runtime Writable Path: `C:\inetpub\RmsSupportHub.Testing\var\drafts`
- Hosting Bundle / ANCM: Ready (ASP.NET Core Module v2 verified)

### 4. REQUIRED MUTATION SEQUENCE (PENDING OWNER APPROVAL)
1. Create external configuration directory `C:\ProgramData\RmsSupportHub\Testing`.
2. Write owner-authorized Testing configuration `appsettings.override.json`.
3. Apply config ACL: Administrators (Full Control), `IIS AppPool\RmsSupportHub.Testing` (Read).
4. Create IIS application pool `RmsSupportHub.Testing`.
5. Create deployment directory `C:\inetpub\RmsSupportHub.Testing`.
6. Extract verified Release Candidate package into deployment directory.
7. Create runtime directory `C:\inetpub\RmsSupportHub.Testing\var\drafts`.
8. Grant `IIS AppPool\RmsSupportHub.Testing` Modify permission on `var\drafts`.
9. Configure environment variable `SUPPORTHUB_EXTERNAL_CONFIG_PATH=C:\ProgramData\RmsSupportHub\Testing\appsettings.override.json` on the IIS application pool.
10. Create IIS site `RmsSupportHub.Testing` bound to `http://*:8080/`.
11. Start site and application pool.
12. Run read-only acceptance probes.

### 5. READ-ONLY ACCEPTANCE PROBES
Only the following read-only HTTP probes are permitted:
- `GET http://localhost:8080/`
- `GET http://localhost:8080/api/health/live`
- `GET http://localhost:8080/api/health/ready`
- `GET http://localhost:8080/api/modules`
- `GET http://localhost:8080/build-identity.json`
- SPA deep-link navigation and local static assets

STRICT PROHIBITIONS:
- No RMS gateway, API, or database probe without separate explicit authorization.
- No order send, cancel, or resend.
- No Main Server mutation or native RMS service control.
- No Production requests.
