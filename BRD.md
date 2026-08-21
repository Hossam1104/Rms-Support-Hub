# RMS+ Support Hub — Business Requirements Document (BRD)

**Version:** 1.0  
**Status:** Baseline for delivery planning  
**Product:** RMS+ Support Hub  
**Repository:** `Hossam1104/Rms-Support-Hub`  
**Prepared:** 2026-08-21

## 1. Executive Summary

RMS+ Support Hub is an internal engineering and support platform that unifies three operational areas:

1. **QA Productivity Tooling**
2. **Online Order Integration Operations**
3. **Secure POS Diagnostics and Maintenance**

The product replaces fragmented scripts, manual SQL, direct API calls and standalone support utilities with governed, environment-aware workflows. It uses a shared Angular/.NET application for central capabilities and a machine-local Windows Agent for privileged POS operations.

## 2. Business Problem

Before RMS+ Support Hub, QA and support activities were distributed across multiple tools and operator-specific processes. This created inconsistent workflows, environment-selection risk, manual payload construction, fragmented troubleshooting, limited auditability and duplicated maintenance utilities.

The Support Hub provides one governed workspace with controlled integration routing, shared UX, centralized business rules and a secure local-agent boundary for machine-level operations.

## 3. Business Objectives

- **BO-01:** Unify QA, Online Order and POS support tooling.
- **BO-02:** Reduce operational and Production-environment risk.
- **BO-03:** Standardize QA bug/story/test-case authoring.
- **BO-04:** Improve Online Order build, validation, submission and troubleshooting.
- **BO-05:** Improve POS diagnostics and controlled maintenance.
- **BO-06:** Improve auditability of privileged operations.
- **BO-07:** Improve release confidence through deterministic builds and CI.
- **BO-08:** Support additional integration modules without redesigning the platform.
- **BO-09:** Establish end-to-end traceability from requirements to implementation and validation.

## 4. Stakeholders

| Role | Primary Need |
|---|---|
| QA Engineer | Build test payloads, inspect responses, refine bugs/stories, generate test cases |
| QA Lead | Standardize quality and validate delivery against requirements |
| Support Engineer | Diagnose order, database, service and POS incidents |
| RMS Operator | Perform bounded operational actions |
| Development Team | Reproduce issues and inspect controlled evidence |
| Architect / Technical Lead | Protect architecture, security and environment boundaries |
| Management | Track scope, readiness, progress and roadmap |
| System Administrator | Deploy and operate Support Hub and POS Agent infrastructure |

## 5. Product Scope

### 5.1 QA Prompt Studio
- Bug refinement
- User-story refinement
- Test-case generation
- Generic Markdown, Jira and Azure DevOps output
- Deterministic quality feedback
- Bounded browser-local history
- Client-side privacy boundary

### 5.2 Online Order Operations
Supported/registered integration families:
- UPC E-Commerce
- GHC E-Commerce
- GHC Uni-Commerce
- OMS placeholder
- Call Center placeholder

Common capabilities:
- server-owned environment configuration;
- draft management;
- branch/item/consumer lookup where supported;
- server-side totals;
- payload validation;
- exact JSON preview;
- request submission;
- request-history inspection where supported;
- cancellation/resend where supported by the downstream contract.

### 5.3 POS Maintenance
- machine and RMS health;
- RMS service visibility;
- database diagnostics;
- backup and guarded restore;
- backup downloader;
- cleanup and branch reset;
- incident timeline;
- Support Bundle;
- Safety Snapshots;
- constrained diagnostics;
- package lifecycle;
- rollback/recovery;
- machine-owned trust;
- direct HTTPS browser-to-Agent communication.

### 5.4 Platform and Release Management
- Testing-first environment policy;
- Production policy gates;
- deterministic release candidates;
- build identity;
- external server-owned configuration;
- IIS deployment;
- CI validation;
- OpenAPI contracts;
- backend/frontend/POS regression suites.

## 6. Business Requirements

- **BR-001 Unified Workspace:** One application shall expose all supported tools through a consistent shell.
- **BR-002 Operational Safety:** The platform shall not expose arbitrary SQL, command execution, filesystem access, service targeting or browser-selected infrastructure targets.
- **BR-003 Environment Separation:** Testing and Production shall be separate authorities; Testing shall not silently contact Production.
- **BR-004 Server-owned Configuration:** Endpoints, database connections and sensitive routing shall be resolved server-side.
- **BR-005 Deterministic QA Authoring:** QA Prompt Studio shall produce consistent structured outputs for bugs, stories and test cases.
- **BR-006 Client-specific Contracts:** Online Order modules shall preserve their real downstream payload and behavior differences.
- **BR-007 Payload Preview:** Users shall be able to inspect exact server-generated JSON before dispatch.
- **BR-008 Server-side Calculations:** Totals, VAT, delivery and payment reconciliation shall be authoritative on the backend where applicable.
- **BR-009 Controlled Lookup:** Supported modules shall provide bounded item, consumer and branch lookups using server-owned configuration.
- **BR-010 Request Visibility:** Supported integrations shall expose bounded request-history lists and detailed request/response evidence.
- **BR-011 Safe Cancellation:** Cancellation shall be available only for supported and eligible orders.
- **BR-012 Safe Resend:** Resend shall preserve authoritative stored request identity/data according to the client contract.
- **BR-013 POS Local Boundary:** Privileged POS operations shall execute through the local RMS Support Agent, not through the central API as a privileged relay.
- **BR-014 Windows Authorization:** POS privileged endpoints shall require Windows authentication and local-administrator authorization.
- **BR-015 Secure Transport:** POS Agent traffic shall use HTTPS loopback with exact-origin browser policy.
- **BR-016 Native Service Protection:** Native RMS services shall not be arbitrarily deleted or controlled outside approved workflows.
- **BR-017 Sanitized Diagnostics:** Diagnostic evidence shall be bounded and shall not expose credentials, private keys or unrestricted sensitive content.
- **BR-018 Safe Database Recovery:** Backup/restore shall use typed, validated, concurrency-safe workflows.
- **BR-019 Package Trust:** Agent lifecycle shall enforce machine-owned trust, signer validation, integrity and rollback-aware behavior.
- **BR-020 Auditability:** Security-sensitive POS operations shall emit durable sanitized audit evidence.
- **BR-021 Deterministic Release:** Releases shall carry source/build identity and integrity evidence.
- **BR-022 External Deployment Configuration:** Deployment secrets shall remain outside application packages.
- **BR-023 Fail Closed:** Missing or untrusted configuration shall block capability rather than fall back to guessed/browser-provided values.
- **BR-024 Extensible Modules:** New integrations shall be introduced through module contracts and capability metadata.
- **BR-025 Traceability:** Business requirements shall map to Azure DevOps work items, GitHub PRs, tests and release evidence.

## 7. Functional Requirements

### QA Prompt Studio
- Create structured bug refinement.
- Create structured story refinement.
- Create structured test cases.
- Export Generic Markdown, Jira and Azure DevOps formats.
- Provide deterministic quality guidance.
- Keep bounded browser-side history.
- Avoid external AI-provider transmission for Prompt Studio generation.

### Online Order Core
- Discover registered modules and capabilities.
- Select only server-approved environments.
- Load/save operator drafts.
- Calculate authoritative totals.
- Build and validate exact module payloads.
- Preview compiled JSON.
- Submit through server-resolved endpoints.
- Return sanitized downstream results.
- Provide bounded endpoint/database diagnostics.

### UPC E-Commerce
- Branch, item and consumer lookup.
- UPC-specific flat-order payload.
- Order submission.
- OrderRequests list/detail.
- Safe cancellation.
- Same-number resend.
- Testing/Production policy separation.

### GHC E-Commerce
- Verified GHC item and consumer lookup.
- GHC-specific contact and delivery fields.
- GHC-specific card/bank and credit-customer payment fields.
- GHC-specific flat-order payload.
- Testing submission through server-owned routing.
- Request history where verified data exists.
- Cancellation only where supported.

### GHC Uni-Commerce
- Specialized invoice payload.
- Invoice/return metadata and multi-row items.
- Complete draft persistence.
- Consumer lookup where verified.
- Item lookup only when an authoritative compatible source exists.
- Testing invoice submission.
- History only when an authoritative source exists.
- No invented cancel/resend behavior.

### POS Maintenance
- RMS installation discovery.
- Supported service health.
- Database health.
- Approved backup and guarded restore.
- Approved branch backup download.
- Cleanup and branch reset.
- Operational health.
- Incident timeline.
- Safe Support Bundle.
- Safety Snapshots.
- Manifest-defined diagnostics.
- Trusted package lifecycle.
- Exact HTTPS/authentication boundaries.

## 8. Non-Functional Requirements

- **Security:** fail closed on invalid trust/configuration/authorization.
- **Reliability:** deterministic state for long-running and privileged operations.
- **Performance:** list/search operations shall avoid loading unnecessary large payloads.
- **Maintainability:** reuse common abstractions where contracts permit.
- **Testability:** backend, frontend and POS capabilities shall retain automated coverage.
- **Auditability:** privileged operations shall provide bounded durable evidence.
- **Deployment Safety:** release packages shall not embed secrets.
- **Compatibility:** local POS components shall support the approved Windows estate.
- **Observability:** health should distinguish unavailable, unconfigured and policy-disabled states where practical.

## 9. Security and Governance

1. No committed credentials/private keys.
2. No browser-supplied connection strings.
3. No browser-supplied arbitrary downstream endpoints.
4. No wildcard POS Agent CORS.
5. No anonymous privileged POS operations.
6. No generic remote command or SQL execution.
7. Production disabled in Testing deployments.
8. Package trust controlled by machine-owned configuration.
9. Release evidence tied to exact source identity.
10. Native RMS services protected from unrelated cleanup.
11. Security-sensitive changes receive appropriate independent review.

## 10. Environment Strategy

### Development
Source development and automated validation.

### Testing
Default operational tier. Production resources remain policy-disabled. Testing configuration is server-owned and mutation tests use dedicated synthetic data.

### Production
Separate authorized tier requiring authoritative configuration, release/package trust, PKI/certificates, deployment/rollback evidence and final acceptance.

## 11. Current Delivery Status — 2026-08-21

### Delivered on `main`
- Unified Support Hub shell
- QA Prompt Studio
- Online Order shared architecture
- UPC E-Commerce major workflow
- Environment safety and Production policy gating
- Server-owned external configuration
- Deterministic release-candidate pipeline
- IIS Testing deployment capability
- POS Agent foundation
- POS diagnostics, recovery and maintenance slices
- POS package-trust/lifecycle architecture and security hardening

### In Review — Draft PR #26
- Verified GHC Testing-schema integration
- GHC request-history work
- Additional GHC-specific payload/UI fields
- GHC Uni-Commerce Testing configuration
- Uni-Commerce consumer/history work where supported
- Local Testing POS trust/runtime provisioning
- Associated backend/frontend/POS regression coverage

### Planned / Remaining
- Independent review, CI acceptance and merge of PR #26
- Diagnose downstream rejection causes observed during synthetic GHC/Uni Testing sends
- Uni-Commerce item lookup if an authoritative compatible source becomes available
- Uni-Commerce cancel/resend if upstream contracts are provided
- Production configuration and acceptance
- Real release PKI and Production signer establishment
- Representative/fleet rollout validation
- Managed browser/certificate rollout where required
- OMS integration
- Call Center integration
- Remaining low-priority configuration/observability hardening
- Azure DevOps requirements/delivery traceability

## 12. Out of Scope Unless Separately Approved

- arbitrary Production access from Testing;
- arbitrary SQL or PowerShell console;
- unrestricted filesystem browser;
- unrestricted Windows service manager;
- central API as privileged POS relay;
- invented behavior for integrations without authoritative contracts;
- migration of user-bound encrypted legacy credentials into machine service context.

## 13. Business Acceptance

A supported module/environment is acceptable when:
1. registration and capability metadata are correct;
2. required server configuration exists;
3. environment policy permits the target;
4. required database/endpoint diagnostics pass;
5. exact business payload can be reviewed;
6. malformed requests are blocked;
7. supported lookup/history workflows return expected data;
8. mutations obey business rules;
9. no sensitive infrastructure data is exposed to the browser;
10. automated tests and CI gates pass;
11. deployment evidence identifies exact tested source;
12. Production is not claimed ready until its separate gates are satisfied.

## 14. Success Measures

- Reduced manual Postman/SQL/script use for covered workflows.
- Reduced environment-selection errors.
- Faster diagnosis of Online Order and POS incidents.
- High automated regression pass rate.
- Lower escaped-defect rate for covered workflows.
- High percentage of Azure DevOps stories linked to PR and validation evidence.

## 15. Traceability Model

`Business Requirement -> Epic -> User Story -> Acceptance Criteria -> GitHub PR -> Automated Tests -> Deployment Evidence`

Azure DevOps should own delivery hierarchy. GitHub remains the source of truth for code, PRs and CI evidence. This BRD remains the business-level scope baseline.
