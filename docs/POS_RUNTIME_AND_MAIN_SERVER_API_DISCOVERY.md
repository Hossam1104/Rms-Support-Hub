# POS Runtime and Main Server API Discovery

## Scope and safety

This is the sanitized 2026-08-15 reconnaissance record for the representative
UPC Testing POS. It reconciles repository `main` at PR #10 merge `16fd303`
with the installed RMS+ runtime and the owner-provided Main Server Swagger.

Only filesystem, SCM/CIM, registry, event-log, and configuration reads were
performed. Network reconnaissance used GET only. No installer, uninstaller,
service action, diagnostic child process, database mutation, or Main Server
mutation was invoked. Raw configuration, connection strings, credentials,
tokens, log messages, Swagger documents, and private certificate material are
not retained here.

## Repository baseline

PR #10 is complete. The Agent already owns typed Downloader, Artifact Download,
Cleanup, Branch Reset, database Backup/Restore, service status/control,
principal-scoped REST/SSE operation truth, one-use mutation tokens and preview
challenges, bounded idempotency/concurrency, and opaque artifacts. These are
inputs to the remaining product work and must not be redesigned without a
concrete defect.

## Installed RMS+ map

| Role | Installed location | Observed runtime |
| --- | --- | --- |
| Suite metadata and local payload | `C:\ProgramData\RMS_Plus` | `RMSInfo.json`, authoritative `ReleaseNumber.txt`, installer/uninstaller, install trace, prerequisites, and a complete `RMS` component payload |
| Branch Server | `C:\Workspaces\DBS\RMS\RMS.BranchServer` | x64, .NET 10, `RMS.POS.BranchServerApi.exe` |
| Cashier Server | `C:\Workspaces\DBS\RMS\RMS.CashierServer` | x64, .NET 10, `RMS.POS.CashierServerApi.exe` |
| Cashier UI | `C:\Workspaces\DBS\RMS\RMS.CashierUI` | x64, .NET 10 Windows Desktop, `RMS.Pos.Cashier.UI.exe` |
| Service Manager | `C:\Workspaces\DBS\RMS\RMSServicesManager` | x64, .NET 7, `Rms.Pos.ServiceManager.exe` |

The Cashier UI entry assembly is `RMS.Pos.Cashier.UI`, assembly/file version
`1.0.0.0`. Its product version is source-build metadata, not the RMS product
release. It is configuration-driven: `appsettings.json` supplies client,
branch/local endpoint, gRPC endpoint, and component build values. Companion
assemblies show gRPC and SignalR clients. No client identity is embedded in the
executable metadata. The executable was not launched.

## Release, client, and identity sources

| Fact | Authoritative source | Observation |
| --- | --- | --- |
| Product release | `C:\ProgramData\RMS_Plus\ReleaseNumber.txt` | `5.7.4`; trimmed length 5; no control characters |
| Client | `RMS.CashierUI\appsettings.json`, `Settings:TheClient` | `UPC` |
| Branch identity | `RMSInfo.json`, `BranchCode` | Present; agrees with Branch, Cashier, and UI configuration |
| POS identity | `RMSInfo.json`, `POSNumber` | Present; agrees with Cashier configuration |
| Installation identifier | `RMSInfo.json`, `UninstallGUID`, with Branch `InstallationGuid` as fallback | Present in RMSInfo; the Branch fallback was empty |
| Component builds | Branch `BuildNumber`, Cashier `BuildNumber`, UI `Settings:BuildNumber` | All `5.7.4`; match the product release in this installation |
| Installation mode | Installed component directories/configuration | Branch + Cashier |

`ReleaseNumber.txt` must be read through a fixed Agent-owned path, trimmed,
bounded, control-character rejected, and treated as optional evidence. It is
the primary product release. Component builds and executable versions remain
separate drift evidence.

## Authoritative RMS maintenance source catalog — 2026-08-15

The owner-confirmed filesystem map below is the server-owned source catalog
for future typed Maintenance/Health evidence. The Agent must resolve every
entry from fixed configuration; Angular never supplies a user, root, path,
filename, age threshold, or deletion request. This session performed no RMS
folder mutation and did not delete or copy any item.

| Source | Meaning and permitted future evidence | Safety boundary |
| --- | --- | --- |
| `C:\ProgramData\Branch` | Branch-service update cache: bounded update history/status, pending or stale package indicators, disk usage, and update diagnostics | Fixed canonical root, bounded file/age/byte/record counts; no deletion in Slice B |
| `C:\ProgramData\Cashier` | Cashier-service update cache received from Branch: the same bounded update evidence | Fixed canonical root and bounded reads; no deletion in Slice B |
| `C:\ProgramData\DBS\POS` | POS insurance invoice attachments referenced by invoice-header attachment identifiers | Sensitive business data; metadata only (exists, count, bytes, age distribution, oldest item, capacity pressure); never contents, raw filenames, bundles, age-only orphan deletion, or default cleanup |
| `C:\ProgramData\Logs\Branch\BranchLogs` | Primary native Branch RMS logs | Fixed root, rotation-aware bounded TXT/log reads, reparse rejection, bounded files/age/bytes/lines/records, then the fail-closed redaction pipeline |
| `C:\ProgramData\Logs\Cashier` | Primary native Cashier RMS logs for Cashier and Service Manager evidence | Same fixed-root, rotation, reparse, bound, and redaction controls as Branch |
| `C:\ProgramData\RMS_Plus` | RMS setup/install evidence, `RMSInfo.json`, `ReleaseNumber.txt`, local payload, installer/uninstaller evidence | Read-only evidence in this session; no setup executable, installer, uninstaller, or payload mutation |
| `C:\ProgramData\RMS_Plus_Downloads` | POS master-data download backlog, activity, size/capacity, and stale/failed evidence | Ownership and business-state rules are required before any cleanup |
| `C:\ProgramData\RMS_Plus_ReleaseRepo` | Latest releases downloaded by the POS auto-update cycle before the operator installs them into `RMS_Plus` | Presence is not trust; future use requires owner, exact manifest, checksum, signature/trust, compatibility, size, path, and reparse validation; no copy/install here |
| `C:\Users\<authenticated-user>\AppData\Local\DBS_RMS+_POS` | Per-user POS theme and notification settings | Resolve the authenticated Windows principal server-side; never enumerate profiles or accept Angular user/path input |

The known update workflow is `ReleaseRepo` download → operator chooses
install in POS → files are copied into `RMS_Plus` → setup runs from
`RMS_Plus`. It is documented as workflow evidence only; no trust or execution
is inferred from folder presence.

Native RMS logs are the primary Failure Analyzer sources. SCM events and
.NET Runtime/Application Error/WER remain bounded corroborating sources. The
fixed Diagnostic Console is a fallback only when those native sources do not
provide sufficient evidence; it never becomes a generic process runner.

## Read-only RMS registry discovery

A representative-machine reconnaissance read inspected the uninstall
registration in both `HKLM` 64-bit and 32-bit views and `HKCU` 64-bit and
32-bit views. No registry value was changed and no uninstall/modify command
was executed. The only RMS-owned Add/Remove Programs candidate was found at:

`HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\RMS_a270dcffb6e64377948841a6607ff0d2`

The sanitized allow-list for future server-side typed evidence is limited
to the following values:

- `DisplayName = RMS+ POS v5.7.4`
- `Publisher = DBS`
- `DisplayVersion = 5.7.4`
- `InstallLocation = C:\ProgramData\RMS_Plus`
- `InstallDate = 20260812`
- presence of `EstimatedSize`, `WindowsInstaller`, `NoModify`, and `NoRepair`
- presence-only flags for `ModifyPath`, `DisplayIcon`, `UninstallString`, and
  `QuietUninstallString`; their raw values/arguments are never exposed or
  executed

The displayed Visual Studio and Postman entries in other views were not
RMS-owned and are excluded. No separate `HKLM\Software\DBS`, `RMS`, or
`RMS+` maintenance root was found in the inspected views. Future registry
reads must use this exact allow-list, suppress credentials/tokens/private
keys/connection strings, and retain uninstall registration only as evidence
for a server-owned verified package manifest.

The installed `RMSInfo.json:UninstallGUID` remains the primary installation
identifier. The registry uninstall key is corroborating product/publisher
and install-location evidence, not an authorization to run an untrusted
uninstaller.


## Executable and SCM map

| Product label | Actual SCM name | Image and runtime facts | Observed state |
| --- | --- | --- | --- |
| Branch Service | `RMS.BranchService` | Exact Branch apphost; no arguments; Auto; LocalSystem; no declared dependency or recovery action | Running, exit code 0 |
| Cashier Service | `RMS.CashierService` | Exact Cashier apphost; no arguments; Auto; LocalSystem; no declared dependency or recovery action | Running, exit code 0 |
| RMS Services Manager | `RMSServiceManager` | Exact Service Manager apphost; no arguments; Auto; LocalSystem | Running, exit code 0 |

The server-owned catalog uses the real SCM name `RMSServiceManager` and keeps
the friendly Service Manager display label separate. The Testing-only
`RmsSupportHub.Pos.Int13.TestService` is not an RMS business service and belongs
only in Advanced Diagnostics when the Agent is in Testing.

All three service app dependencies include Windows Service hosting plus
Serilog Console and File sinks. No service command-line arguments were
configured. The canonical working directory for any future diagnostic run is
the verified executable directory. LocalSystem is the service account and the
Agent service identity, but that does not authorize an unrestricted process
launcher.

## Logs, events, and failure-analysis feasibility

Configured native log roots are fixed:

- Branch: `C:\ProgramData\Logs\Branch\BranchLogs`
- Cashier and Service Manager: `C:\ProgramData\Logs\Cashier`
- Cashier updater: the fixed `LogDownloadAutoUpdate` directory beneath the UI
  installation
- Installer: `C:\ProgramData\RMS_Plus\Logs\InstallTrace.log`

The Branch, Cashier, and Service Manager configurations write to both Console
and rolling text files. At inspection time, the bounded native log sample
contained many exception markers and conventional `at Namespace.Type.Method`
stack frames. PDBs were present beside first-party assemblies. This proves that
class/method evidence is often available, not that every failure contains it.

Relevant Windows sources were also present:

- Service Control Manager events 7000 and 7009 for Branch/Cashier start
  failures;
- `.NET Runtime` 1026, `Application Error` 1000, and Windows Error Reporting
  1001 records tied to the Branch executable;
- the same crash-provider chain tied to the Cashier UI.

No raw event or log message was retained. A future analyzer can safely combine
bounded time windows from these exact sources with SCM state, DB/connectivity
evidence, release drift, and recent Agent operations. It must redact connection
strings, credentials, tokens, URLs with user-info, local identities, and
unbounded exception payloads before transport or bundle creation.

### Safe Diagnostic Console Run

Static evidence makes a controlled console run technically plausible: the SCM
images have no special arguments, the apps support Windows Service hosting,
Console sinks are configured, and the exact executable/working directories
are known. It is not yet safe to claim production readiness. Starting these
apps can bind ports, open databases, and start background synchronization.

The eventual fallback is therefore limited to the three canonical catalog
entries and must enforce all of the following server-side:

- resolve the SCM image; canonicalize beneath the expected RMS install root;
- reject reparse-point escape and any image mismatch;
- require the target service to be stopped;
- use the exact executable with no browser path, arguments, shell, script, or
  environment input;
- `UseShellExecute=false`, fixed working directory, redirected stdout/stderr,
  bounded duration/output, and an Agent-owned child-process handle;
- kill only the child created by that operation on timeout;
- sanitize exception type/message/stack frames before retention or transport;
- Testing/synthetic automation first; representative RMS execution requires
  separate owner approval.

## Local installation and repair material

`C:\ProgramData\RMS_Plus` contains `RMSPlus_Installer.exe`,
`Uninstaller.exe`, .NET 10.0.9 prerequisites, an install trace, uninstall
registration, and local Branch/Cashier/UI/Service Manager payload directories.
The four payload apphost hashes matched the corresponding installed apphost
hashes at inspection time. Windows uninstall registration points to the local
uninstaller and requests elevation.

No supported silent repair/install/uninstall switches or rollback manifest
were established, and no setup executable was run. The local payload is strong
evidence for on-device file repair, but execution semantics remain an explicit
Slice B implementation/test item.

## Main Server resolution

The installed full Main Server base address is present and consistent in:

1. Branch `BranchSettings:MainBaseUrl`;
2. Cashier `PosBasicInfoSettings:MainServerBaseUrl`.

`RMSInfo.json:MainServerUrl` identifies the same installed host/port but omits
the application path. These installed full endpoints are authoritative for the
current POS runtime. The client label `UPC` is display/cross-validation
evidence, not a URL.

The owner-provided UPC management/Swagger base is
`http://10.10.9.181:8080/rmsmainserverApi/`. It differs from the installed
runtime host. The Agent must not derive one by editing the other or by changing
subnets. Future Main Server integration needs an Agent-owned, allow-listed
client API profile whose exact base is provisioned outside Angular and checked
against the discovered client. Browser requests can select only a server-owned
logical operation; they cannot provide a base URL, Branch Code, POS Number,
authorization header, or API key.

## Swagger/OpenAPI discovery

### UPC

`GET http://10.10.9.181:8080/rmsmainserverApi/swagger/index.html` returned 200.
Its `index.js` explicitly referenced `v1/swagger.json` and
`v2/swagger.json`.

| Document | OpenAPI | Paths | Schemas | Methods |
| --- | --- | ---: | ---: | --- |
| v1 | 3.0.1 / API `v1` | 523 | 728 | GET 278, POST 179, PUT 51, DELETE 24, PATCH 1 |
| v2 | 3.0.1 / API `v2` | 26 | 219 | GET 24, POST 6, PUT 2 |

Both documents declare root security as an `apiKey` named `Bearer` in the
`Authorization` header. Live GET checks proved that this root declaration is
not reliable per-route authorization evidence: Branch status/lookups returned
401 without a header, while two POS lookup routes returned 200. The Agent must
use an explicit per-operation allowlist and credential policy and must not
assume that Swagger root security matches runtime enforcement.

### Whites

The owner-provided Whites URL is
`http://10.10.20.126:8090/rmsmainserverApi/swagger/index.html`. Its Swagger UI
and the two UPC-discovered candidate document paths timed out on the UPC VPN.
The owner confirmed that this session is connected only to UPC. Whites is
therefore an expected same-application profile, not a verified equivalent.
No contract difference is claimed. Contract hash/path/schema comparison is a
Slice B gate when a Whites-connected read-only session is available.

## Branch API contract

All listed v1 operations have no OpenAPI `operationId`. `api-version` is an
optional query parameter in the document. Only HTTP 200 is documented; no
non-200 error schema is defined for these operations.

| Operation | Method and route | Inputs/body | Documented response | State-changing |
| --- | --- | --- | --- | --- |
| Ready-to-install inventory | `GET /api/Branch/GetReadyInstallBranch` | `api-version` query; no body | `InstallBranchDto[]` | No, but response schema contains a credential-bearing property and must never be proxied or bundled |
| Installed inventory | `GET /api/Branch/GetInstallBranch` | `api-version` query; no body | `InstallBranchDto[]` | No, but same sensitive schema exclusion applies |
| Mark installed | `PUT /api/Branch/InstalledBranch` | `BranchCode` and `api-version` query; no body | `EmptyResponseDTO { Message, IsDone }` | Yes |
| Refresh installation identifier | `PUT /api/Branch/UpdateBranchInstallationGuid` | `branchCode` and `api-version` query; no body | `EmptyResponseDTO` | Yes |
| Mark uninstalled | `PUT /api/Branch/UnInstalledBranch` | `BranchCode`, `strict` (documented default `false`), and `api-version` query; no body | `EmptyResponseDTO` | Yes |
| Branch status snapshot | `GET /api/Branch/GetBranchesWithStatus` | optional branch/name/POS/version filters plus `api-version`; no body | `BranchStatusVm[]` with online/last-online/POS status data | No |
| v2 status snapshot | `GET /api/v2/Branch/GetBranchesWithStatus` | array filters for branch/name/POS/version; no body | `BranchStatusVm[]` | No |

The live local-branch v1 status GET returned 401 without an Authorization
header. The install/uninstall PUT contracts were not invoked. Swagger documents
no install-specific polling resource, cancellation route, idempotency key,
package response, or rollback behavior.

## POS API contract

| Operation | Method and route | Inputs/body | Documented response | State-changing |
| --- | --- | --- | --- | --- |
| Installed POS inventory | `GET /api/PosMachine/GetInstalledPosMachine` | `BranchCode` and `api-version` query; no body | `PosMachineUpdateDto[] { Id, Name, PosNumber }` | No |
| Mark POS installed | `PUT /api/PosMachine/InstallPos` | `BranchCode`, `PosNumber`, and `api-version` query; no body | `EmptyResponseDTO { Message, IsDone }` | Yes |
| Mark POS uninstalled | `PUT /api/PosMachine/UnInstalledPos` | `BranchCode`, `PosNumber`, and `api-version` query; no body | `EmptyResponseDTO` | Yes |
| POS status lookup | `GET /api/PosMachine/GetAllPosStatuses` | `api-version` query; no body | `PosStatusesViewModel[] { Value, Name }` | No |

The installed-POS and POS-status GETs returned 200 without an Authorization
header in the UPC runtime. Their response bodies were not retained. The PUTs
were not invoked. No POS install-specific progress, cancellation, idempotency,
package, or rollback contract is documented.

### Other maintenance-relevant read contracts

The v1 document also exposes typed read candidates for pending/failed branch
updates and POS participants under `/api/Updates/**`, plus pending migrations
under `/api/Setup/GetPendingMigrations`. They require individual security and
side-effect review before allow-listing. Several GET-named Setup/Update routes
appear capable of cache clearing, bulk data preparation, or package download;
HTTP GET alone does not make them safe. They were not called and must remain
excluded until proven side-effect-free.

## Repair-install strategy

The discovered contract supports a controlled combination, with local package
execution as the actual device repair mechanism and Main Server as a typed
status/profile integration:

| Stage | Local Agent/runtime | Main Server |
| --- | --- | --- |
| Health and diagnosis | Fixed RMS files, SCM, DB probes, logs/events, capacity | Allow-listed read-only status/update evidence when authenticated |
| Safety snapshot | Health, release/builds, service state, config fingerprints, support evidence, required DB backup | Record correlation locally; no remote mutation |
| Repair execution | Validated local payload/installer adapter; exact owned package; only required services; rollback manifest | No mutation unless the operator separately approves the exact typed call |
| Install-state acknowledgement | Verify files, release, builds, DBs, services, Main Server connectivity | Only then consider the exact Branch/POS `PUT` marker, bound to discovered local identity |
| Closure | Post-repair health, incident timeline, sanitized bundle/audit | Read-only status reconciliation |

Until an authorized live contract test proves otherwise, `InstalledBranch`,
`UnInstalledBranch`, `InstallPos`, and `UnInstalledPos` are treated as remote
installation-state markers, not remote package executors. Every future Main
Server mutation requires explicit owner approval before invocation even after
code exists.

## Product and UI rebaseline

The finished `/tools/pos-maintenance` workspace uses this hierarchy:

1. compact title and `Branch · POS · Release · Client` identity line;
2. peer operational status for Agent, Windows auth/authorization, Main Server,
   configuration consistency, and overall health;
3. one compact RMS installation overview;
4. two equal primary Branch/Cashier database cards with always-visible Backup,
   Restore, View Backups, and Health actions;
5. compact rows for the three real RMS services with state, Diagnose, and
   state-valid Start/Stop/Restart controls;
6. one maintenance area for Downloader, Deployment, Cleanup, Branch Reset,
   Repair Installation, and Guided Repair;
7. obvious Health Check, Service Failure Analyzer, Support Bundle, and Incident
   Timeline actions;
8. collapsible Advanced Diagnostics for versions, OS, detailed connectivity,
   safe configuration evidence, legacy SQL evidence, and Testing infrastructure.

Desktop database cards are peers; narrow layouts stack. Required review widths
are 1440, 1024, 768, and 390 pixels. Technical metadata and misleading secret
presence rows leave the primary operator view.

## Accelerated remaining roadmap

The security and contract findings do not support merging Slices A and B into
one execution session. Slice B adds process launch, log/event confinement,
remote credentials, external mutation contracts, installer coordination, and
package rollback; it needs Slice A's typed evidence model as a stable input.

- **Slice A — final operator workspace and diagnostic evidence:** authoritative
  release/client discovery, service-name correction, drift/health/capacity,
  failure analyzer, bounded logs/events, Support Bundle, Incident Timeline,
  existing Backup/Restore/Downloader/Cleanup/Reset integration, and responsive
  UI acceptance.
- **Slice B — typed repair/install vertical:** Agent-owned Main Server profiles,
  Branch/POS install-state operations, pre-maintenance snapshot, Safe Diagnostic
  Console Run, Deployment/Repair/Guided Repair, and the real Agent package,
  upgrade/uninstall/rollback boundary. Live Main Server mutation remains
  separately owner-approved.
- **Slice C — production/fleet hardening:** M-1 managed browser policy, M-2
  production certificate lifecycle, durable production audit, representative
  device proofs, Whites contract comparison, and independent Claude Opus 5 High
  security/readiness review.

## Unresolved facts and gates

- Whites contract equivalence is expected by the owner but not network-verified.
- Main Server credential provisioning and per-route authorization must be
  established without importing browser credentials or committing secrets.
- Branch/POS PUT side effects, retry/idempotency behavior, and error envelopes
  are not proven by OpenAPI and were not invoked.
- Supported local installer repair/silent switches and rollback semantics are
  not established.
- Safe Diagnostic Console Run still needs synthetic/Testing validation before
  any representative RMS launch.
- Non-destructive Branch/Cashier Backup persistence proof remains open; Restore
  is not part of that proof.
- M-1, M-2, durable Production audit, package/ACL ownership, and final
  independent review remain open Production gates.
