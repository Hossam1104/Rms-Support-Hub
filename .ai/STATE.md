# Current Project State

- **Updated:** 2026-08-15
- **Repository baseline:** PR #10 merged at `16fd303`; typed Downloader,
  Artifact Download, Cleanup, Branch Reset, database Backup/Restore, and
  service control remain the accepted completed baseline.
- **Current session outcome:** read-only representative UPC runtime and Main
  Server OpenAPI reconnaissance completed; no production code, installer,
  service action, database mutation, or Main Server mutation was run.
- **Next executable task:** root `TASK.md` is the full large Slice A
  implementation prompt for the final POS operator workspace and diagnostic
  evidence. It is implementation, not planning.

## Reconciled runtime facts

- Authoritative RMS Product Release is the fixed
  `C:\ProgramData\RMS_Plus\ReleaseNumber.txt`; representative value `5.7.4`.
- Authoritative Client is Cashier UI `Settings:TheClient`; representative
  value `UPC`. Branch/Cashier/UI BuildNumbers are separate drift evidence.
- Installed mode is Branch + Cashier. Branch/POS identity sources agree on the
  representative installation.
- Actual RMS SCM names are `RMS.BranchService`, `RMS.CashierService`, and
  `RMSServiceManager`. The current catalog's `RMSServicesManager` spelling is a
  confirmed defect assigned to Slice A.
- Branch, Cashier, and Service Manager use fixed Serilog Console/File sources;
  bounded native logs and Windows SCM/.NET/Application Error/WER events contain
  useful exception/stack evidence. No raw messages are stored in the repo.
- `C:\ProgramData\RMS_Plus` contains local installer/uninstaller and complete
  RMS payload surfaces; the four component apphost hashes matched the installed
  apphosts. Supported repair switches/rollback remain unproven.

## Main Server findings

- UPC Swagger exposes OpenAPI v1 (523 paths) and v2 (26 paths). The documents
  define Branch/POS install-state `PUT`s with query-bound identity and empty
  status responses, but no package body, operation ID, polling, cancellation,
  idempotency, or rollback contract.
- The install inventory response schema is credential-bearing and must never
  be proxied, cached, logged, or bundled.
- Global Swagger Bearer security is not reliable per-route runtime evidence:
  two safe POS lookup GETs were anonymously reachable while Branch status GETs
  returned 401.
- Installed runtime Main Server and owner-provided Swagger bases use different
  hosts. Future integration uses Agent-owned allow-listed client profiles and
  local discovered Branch/POS binding; Angular never supplies a URL or target.
- Whites is expected to host the same application but could not be reached on
  the UPC VPN. The owner confirmed UPC-only connectivity; equivalence remains
  an explicit read-only gate.
- Main Server GET is pre-authorized only when side-effect-free. Any
  POST/PUT/PATCH/DELETE or state-changing GET requires explicit owner approval
  before live invocation.

## Accelerated remaining roadmap

1. **Slice A:** Product Release/Client/drift, service-name correction, Health
   Check, DB/capacity/backup health, Failure Analyzer, bounded logs/events,
   Support Bundle, Incident Timeline, final responsive operator UI, and
   regression preservation of completed operations.
2. **Slice B:** Agent-owned Main Server profiles and Branch/POS install-state
   operations, Safety Snapshot, Safe Diagnostic Console Run, Deployment,
   Repair/Guided Repair, and real Agent package/install/upgrade/uninstall/
   rollback boundary.
3. **Slice C:** M-1 managed browser policy, M-2 Production certificate
   lifecycle, durable Production audit, package/ACL and representative-device
   proofs, Whites comparison, fleet rollout, and Claude Opus 5 High independent
   security/readiness review.

Slices A and B remain separate because Slice B adds confined process launch,
remote credentials/mutations, installer rollback, and package ownership; it
must consume Slice A's stable typed evidence model.

## Validation baseline and open evidence

- PR #10 baseline: POS 311 tests passed; frontend 345 tests passed; Release
  build passed without warnings; generated client was byte-stable.
- This planning/reconnaissance change requires documentation diff/memory/secret
  validation only; it does not change buildable product source.
- Pending representative-device proof remains non-destructive Branch/Cashier
  Backup plus approved artifact/checksum retention across Agent restart. No
  Restore is authorized for that proof.
- Production/customer deployment remains blocked on M-1, M-2, durable audit,
  package/ACL ownership, final representative evidence, and independent review.
