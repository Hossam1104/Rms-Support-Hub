# CLAUDE SONNET 5 HIGH
## P0-C — Controlled Testing/Staging IIS Deployment and Read-Only Acceptance Evidence

MODEL: Claude Sonnet 5 HIGH | ROLE: Implement / Operational Verification
PROGRAMME: Staging-Safe Release Candidate v1 | MILESTONE: P0-C controlled deployment and read-only acceptance
Repository: `D:\AI Tools\DBS\Rms-Support-Hub` | Branch: `main` | Base: Merged PR #24 on `main`

### 1. MANDATORY APPROVAL GATE
NO EXTERNAL WRITE OR DEPLOYMENT MAY OCCUR UNTIL THE USER PROVIDES FRESH EXPLICIT APPROVAL FOR THE SPECIFIC TESTING/STAGING TARGET. TASK.md existing DOES NOT authorize deployment.
Before requesting approval, P0-C must prepare/display an approval packet containing:
1. Environment: Testing/Staging only.
2. Target host/server: Exact approved IIS host.
3. IIS target: Site / application / application pool / destination path.
4. Artifact: Exact merged-main source SHA, ZIP SHA-256, Build ID, release-manifest identity.
5. Deployment method: Exact copy / deploy mechanism.
6. Configuration: Server-owned Testing config source, placeholders/secrets injection approach, no committed secrets.
7. Expected effects: Files, config, site, and app-pool changes.
8. Rollback: Exact backup / restore procedure.
9. Runtime prerequisites: .NET 10 Hosting Bundle, ASP.NET Core Module v2, IIS app pool, writable `var/drafts` ACL.
10. Post-deployment probes: Read-only probes only.
If explicit target approval is absent: prepare the approval packet -> STOP -> ask owner for explicit approval. DO NOT DEPLOY.

### 2. RELEASE CANDIDATE REQUIREMENTS
- Regenerate/verify RC from MERGED MAIN (do not merely reuse pre-merge PR artifact).
- `sourceCommit` in `release-manifest.json` must equal the merged-main commit being deployed.
- Verify ZIP SHA-256 sidecar, verify `file-integrity.sha256`, and perform fresh-extract verification before deployment.
- Verify sanitized Testing package configuration (`appsettings.json` matches Testing template; disabled registrations; no concrete topology).
- Deploy only with externally supplied authorized Testing configuration. Confirm no Production topology in packaged defaults.

### 3. READ-ONLY ACCEPTANCE PROBES
Post-deploy acceptance may initially include ONLY:
- GET `/`, GET `/api/health/live`, GET `/api/health/ready`, GET `/api/modules`, GET `/build-identity.json`, SPA deep-link, and local static assets.
Confirm:
- `DeploymentTier` = `Testing`, served build identity = deployed manifest, local health = expected, `var/drafts` writable, static assets local, SPA fallback works.

### 4. STRICT SAFETY BOUNDARIES
Do NOT automatically perform: order send/cancel/resend, database write, customer data mutation, Production request, Main Server mutation, RMS service control, certificate mutation, or fleet rollout.
Any RMS gateway/API/DB read-only probe beyond local Hub health requires separate explicit authorization if contacting a customer/shared environment.

### 5. EVIDENCE COLLECTION
Collect durable evidence: deployment timestamp, target environment, source SHA, Build ID, ZIP hash, HTTP status evidence, rollback checkpoint, sanitized config evidence.
Never commit customer IPs, credentials, secrets, connection strings, or patient/insurance/order payload data.

### 6. READINESS DISTINCTIONS
P0-C must clearly distinguish: REPOSITORY READY / TESTING DEPLOYED / TESTING ACCEPTED / PRODUCTION READY.
Production readiness must remain false unless all external gates are closed.
