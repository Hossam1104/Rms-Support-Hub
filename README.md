# RMS+ Support Hub

An internal QA engineering workspace: a .NET 10 Web API and an Angular 22 SPA
hosting the QA tools used day to day.

| Tool | Route | State |
| --- | --- | --- |
| **QA Prompt Studio** | `/tools/prompt-studio` | Available |
| **Online Order Tool** | `/tools/online-orders` | Available |
| **POS Maintenance Tool** | `/tools/pos-maintenance` | Available (secure Testing Agent) |

**QA Prompt Studio** turns rough notes into structured, paste-ready QA prompts —
Bug Refinement, Story Refinement, and Test Case Generation — with deterministic
builders, advisory prompt-quality feedback, and a local ten-record history.
Generation is entirely client-side: nothing is sent to an external provider and
attachment contents are never stored.

**Online Order Tool** builds and sends order and invoice payloads to three
client RMS APIs (GHC E-Commerce, UPC E-Commerce, GHC Uni-Commerce), and
inspects, cancels, and resends what was actually sent, straight from the real
`OrderRequests` table. OMS and Call Center are registered placeholder modules.

**POS Maintenance Tool** is a Support Hub-owned operations console backed by
the separate Windows `RmsSupportAgent` service. The browser uses the exact
secure Testing origin and the Agent owns privileged POS work; the Hub API is
not a privileged relay. The current implementation boundary, migration plan,
and evidence gates are recorded in
[docs/POS_SLICE_C_IMPLEMENTATION.md](docs/POS_SLICE_C_IMPLEMENTATION.md).
Historical `RmsSupportHub.Pos.Agent` and `RmsSupportHub.Pos.Int13.TestService`
names remain migration inputs only.

---

## Tech stack

- **Backend** — .NET 10 Web API (`[ApiController]`), Dapper +
  `Microsoft.Data.SqlClient` against SQL Server, xUnit.
- **Frontend** — Angular 22 standalone components with signals, Angular CDK for
  overlay/virtual-scroll/a11y, a hand-rolled token-based design system with
  dark/light themes, and Three.js for one decorative, lazy-loaded Hub scene.
- **Architecture** — `RmsSupportHub.Core` (domain, module capabilities,
  payload builders, validators; no external dependencies) →
  `RmsSupportHub.Data` (Dapper repositories) → `RmsSupportHub.Api`
  (controllers, middleware, DI composition root). The frontend talks to the API
  only through typed models in `frontend/src/app/core/models/`.
- **Feature routing** — every tool is a lazy Angular route with typed
  `ToolRouteData`. Pre-hub Online Order URLs (`/modules/:key/...`) still
  resolve through a compatibility mount.

The full layout, and where new work belongs, is in
[docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md).

---

## Getting started

### 1. Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm

### 2. Configuration and secrets

**No connection string is ever committed.** `appsettings.json` tracks three
empty `ConnectionStrings` placeholders (`GhcEcommerce`, `UpcEcommerceTest`,
`GhcUnicommerce`); the API throws a
`ConfigurationException` naming the exact missing key when a database call
needs one, instead of failing opaquely inside Dapper.

UPC Testing and UPC Production are both supported. Production reuses the
server-owned `UpcEcommerceTest` connection details and changes only the
approved database catalog to `RmsMainProd`; the browser cannot supply a server,
catalog, credentials, or connection string. Production database checks are
read-only, and no Production order mutation is part of local validation.

**Development** — use [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(already initialized for `RmsSupportHub.Api`; secrets live outside the repo):

```powershell
cd backend/src/RmsSupportHub.Api
dotnet user-secrets set "ConnectionStrings:GhcEcommerce"     "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:UpcEcommerceTest" "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:GhcUnicommerce"   "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets list   # confirm all three are set
```

**Deployed** — set the equivalent environment variables (double underscore is
ASP.NET Core's configuration-key separator):

```
CONNECTIONSTRINGS__GHCECOMMERCE=...
CONNECTIONSTRINGS__UPCECOMMERCETEST=...
CONNECTIONSTRINGS__GHCUNICOMMERCE=...
```

Outbound TLS certificate validation for calls to the client RMS APIs is
disabled by default (`Outbound:VerifyTls=false`, logged once at startup)
because those hosts present self-signed certificates. Set
`Outbound:VerifyTls=true` (or `Outbound__VerifyTls`) once that changes.

### 3. Per-module credential status

| Module | `Capabilities.OrderRequests` | Notes |
|---|---|---|
| `upc_ecommerce` | `true` | Configured for Testing/Production; live availability must be verified in the target environment. |
| `ghc_ecommerce` | `false` | Lookup and send/cancel are configured; Order Requests returns 501 pending confirmed GHC database credentials. |
| `ghc_unicommerce` | `false` | No confirmed API URL or database credentials yet; registered but not live. |
| `oms`, `call_center` | — | Placeholder modules, not implemented. |

**The one-line GHC flip**: once GHC's `OrderRequests`/`RequestOrderHeaders`
tables are confirmed live the way UPC's were (see
[docs/database-schema.md](docs/database-schema.md)), change
`OrderRequests: false` to `true` in
`backend/src/RmsSupportHub.Core/Modules/GhcEcommerceModule.cs`. Nothing else
keys off module identity — the controller, the route guard, and the UI all read
that one capability flag.

### 4. Run

```powershell
.\scripts\dev.ps1
```

starts the API on `http://localhost:5200` and `ng serve` on
`http://localhost:4200` (proxying `/api`, see `frontend/proxy.conf.json`). Or
run them separately:

```bash
cd backend && dotnet run --project src/RmsSupportHub.Api
cd frontend && npm install && npx ng serve
```

### 5. Test and build

```powershell
.\scripts\build.ps1
```

runs `dotnet test`, `dotnet build -c Release`, and
`ng build --configuration production` in sequence. Individually:

```bash
cd backend  && dotnet test
cd frontend && npm test -- --watch=false
cd frontend && npm run build -- --configuration production
cd frontend && npm run test:riyal-asset    # approved currency asset provenance
```

A `production-offline` build configuration exists for reproducible builds when
Google Fonts is unreachable.

---

## Key behavior

1. **Modules and environments** load live from `GET /api/modules`, never
   hardcoded client-side, and carry real `Capabilities` that the frontend gates
   routes and UI on.
2. **Flat order builder (GHC & UPC)** — client identity, branch-aware item
   lookup, consumer lookup, server-owned live totals, and a compiled-payload
   preview that is the actual server-built payload, not an approximation. An
   empty payment list is the verified Cash on Delivery state, not an error.
3. **Uni-Commerce invoice builder** — invoice headers, consumer details, row
   items, `IsReturn`/parent-reference logic, and calculated totals.
4. **Order Requests** — an explicit-apply, paginated filter workbench over the
   real `OrderRequests` table, with exact or escaped partial order search,
   last-nine-digit phone search, live stat tiles, and a route-level detail page
   with server-enforced cancel and same-number resend.
5. **Prompt Studio** — deterministic builders with detail levels and
   Generic/Jira/Azure DevOps output formats, draft persistence, copy and
   Markdown/plain-text export, and `Ctrl`/`Cmd`+`Enter` generation.
6. **Design system** — one token set, one card contract, one theme service and
   one motion service; see [docs/design-system.md](docs/design-system.md).

## Documentation

Start at [docs/README.md](docs/README.md). The contracts that must never be
guessed at are `docs/api-spec.md`, `docs/database-schema.md`, and
`docs/request_examples/`. Release status and deferred items are in
[docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md](docs/RMS_SUPPORT_HUB_RELEASE_READINESS.md).
