# Online Order Tool (.NET 10 + Angular 22)

An internal tool for building and sending pharmacy/e-commerce order and
invoice payloads to three client RMS APIs (**GHC E-Commerce**, **UPC
E-Commerce**, **GHC Uni-Commerce**), and for inspecting, cancelling and
resending what was actually sent, straight from the real `OrderRequests`
table. **OMS** and **Call Center** are registered placeholder modules, not
yet built.

Originally a Flask app; rewritten as a .NET 10 Web API + Angular 22 SPA,
then remediated across eleven sessions (see `.ai/HISTORY.md`; the detailed
audit plan is archived under `.ai/archive/`) to fix an invented payload
schema, invented SQL columns, a per-user draft that was actually shared
process-wide, and a design system that didn't match the approved
direction. This README describes the tool as it exists **after** that
remediation — the legacy Flask implementation has been removed from the
tree entirely (see git history before this commit if you need to compare
against it).

---

## Tech stack

- **Backend**: .NET 10 Web API (C#, `[ApiController]`), Dapper +
  `Microsoft.Data.SqlClient` against SQL Server, xUnit.
- **Frontend**: Angular 22 (standalone components, signals, Angular CDK for
  overlay/virtual-scroll/a11y), a hand-rolled **bold-gradient design
  system** (`frontend/src/styles/_tokens.css` + `_gradients.css` — see
  `docs/design-system.md`) with dark/light theme switching.
- **Architecture**: `OnlineOrderTool.Core` (domain/services, no external
  deps) → `OnlineOrderTool.Data` (Dapper repositories) → `OnlineOrderTool.Api`
  (controllers, DI composition root, references both). Frontend talks to
  the API exclusively through typed models in `frontend/src/app/core/models/`.

---

## Project layout

```
online_order_tool/
├── backend/
│   ├── OnlineOrderTool.slnx
│   ├── src/
│   │   ├── OnlineOrderTool.Api/         # Controllers, DI composition, middleware, guards
│   │   ├── OnlineOrderTool.Core/        # Domain models, modules/capabilities, payload builders, validators
│   │   └── OnlineOrderTool.Data/        # Dapper SQL Server repositories
│   └── tests/OnlineOrderTool.Tests/     # xUnit — 93 tests as of R10
│
├── frontend/                            # Angular 22 SPA
│   └── src/app/
│       ├── core/                        # ApiService, ModuleService, ThemeService, ToastService,
│       │                                #   typed models, HTTP interceptor, route guards
│       ├── shared/ui/                   # gradient-card, stat-tile, status-pill, json-tree, drawer,
│       │                                #   confirm-dialog, data-table, pagination, riyal, ...
│       ├── layout/                      # Navbar, Sidebar, Breadcrumb
│       └── features/
│           ├── landing/                 # Module picker
│           ├── flat-order/              # GHC/UPC E-Commerce order builder
│           ├── unicommerce/             # GHC Uni-Commerce invoice builder
│           ├── order-requests/          # Order Requests: list, filters, detail drawer, cancel/resend
│           ├── order-validation/        # UPC-only search/detail (superseded by order-requests; kept for now)
│           └── kitchen-sink/            # Dev-only /_kitchen-sink showcase of every shared/ui component
│
├── docs/
│   ├── request_examples/                # Reference JSON payloads -- the payload contract
│   ├── api-spec.md                      # REST API specification (kept in sync with the controllers)
│   ├── database-schema.md               # Verified SQL Server schema (the SQL contract)
│   └── design-system.md                 # Token catalogue and the "no raw hex" rule
│
├── scripts/
│   ├── dev.ps1                          # Runs the API and `ng serve` together
│   └── build.ps1                        # dotnet test + dotnet build (Release) + ng build (production)
│
├── docs/UI_Rework_Plan.md                # Active UI programme, U3-U8 only
├── docs/UI_Rework_Prompts.md             # Active execution prompts, U3-U8 only
└── .ai/HISTORY.md                        # Concise completed-milestone index
```

---

## Getting started

### 1. Prerequisites
- **.NET 10 SDK**
- **Node.js 18+** and npm

### 2. Configuration & secrets

**No connection string is ever committed to this repository.**
`appsettings.json` tracks four empty `ConnectionStrings` placeholders
(`GhcEcommerce`, `UpcEcommerceProd`, `UpcEcommerceTest`, `GhcUnicommerce`);
the API throws a `ConfigurationException` naming the exact missing key at
the point a database call needs one, instead of failing with an opaque
error inside Dapper.

**Development** — populate them with [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(already initialized for `OnlineOrderTool.Api`; the secrets live outside
the repo under your user profile, not in any tracked file):

```powershell
cd backend/src/OnlineOrderTool.Api
dotnet user-secrets set "ConnectionStrings:GhcEcommerce"     "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:UpcEcommerceProd" "Server=<host>;Database=RmsMainProd;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:UpcEcommerceTest" "Server=<host>;Database=RmsMainTest2;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:GhcUnicommerce"   "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets list   # confirm all four are set
```

**Production** — set the equivalent environment variables (double
underscore is ASP.NET Core's configuration-key separator):

```
CONNECTIONSTRINGS__GHCECOMMERCE=...
CONNECTIONSTRINGS__UPCECOMMERCEPROD=...
CONNECTIONSTRINGS__UPCECOMMERCETEST=...
CONNECTIONSTRINGS__GHCUNICOMMERCE=...
```

Real credentials that were previously hardcoded in source are already in
git history from before the remediation; rotating them is a decision for
whoever owns that SQL Server instance, not something this repository can
undo by deleting a file.

Outbound TLS certificate validation for calls to the client RMS APIs is
disabled by default (`Outbound:VerifyTls=false` in `appsettings.json`,
logged once at startup) — those hosts present self-signed certificates.
Set `Outbound:VerifyTls=true` (or the `Outbound__VerifyTls` environment
variable) once that's no longer the case.

### 3. Per-module credential status

| Module | `Capabilities.OrderRequests` | Notes |
|---|---|---|
| `upc_ecommerce` | `true` | Live against `RmsMainTest2`/`RmsMainProd` at `10.10.8.181`. |
| `ghc_ecommerce` | `false` | Item/consumer lookup and send/cancel work; the Order Requests page 501s pending confirmed GHC database credentials. |
| `ghc_unicommerce` | `false` | No confirmed API URL or database credentials yet; the module is registered but not live. |
| `oms`, `call_center` | — | Placeholder modules, not implemented. |

**The one-line GHC flip**, once GHC's `OrderRequests`/`RequestOrderHeaders`/
etc. tables are confirmed live the same way UPC's were (see
`docs/database-schema.md`): in
`backend/src/OnlineOrderTool.Core/Modules/GhcEcommerceModule.cs`, change

```diff
  public ModuleCapabilities Capabilities { get; } = new(
      ...
-     OrderRequests: false,
+     OrderRequests: true,
```

Nothing else keys off module identity for this feature — `OrderRequestsController`,
the frontend route guard, and the Order Requests UI all read this one
capability flag.

### 4. Run it

```powershell
.\scripts\dev.ps1
```
runs the API (`http://localhost:5200`) and `ng serve` (`http://localhost:4200`,
proxying `/api` to the API — see `frontend/proxy.conf.json`) together. Or
run them in separate terminals:

```bash
cd backend && dotnet run --project src/OnlineOrderTool.Api
cd frontend && npm install && npx ng serve
```

### 5. Test

```powershell
.\scripts\build.ps1
```
runs `dotnet test`, `dotnet build -c Release`, and `ng build --configuration production`
in sequence, or individually:

```bash
cd backend && dotnet test
cd frontend && npm run build
```

---

## Key features

1. **Module & environment selector** — loaded live from `GET /api/modules`
   (never hardcoded client-side), each with real `Capabilities` the
   frontend gates routes and UI on.
2. **Flat order builder (GHC & UPC)** — client identity block, branch-aware
   item lookup, consumer lookup prefilling name and address, live totals,
   and a compiled-payload preview that is the actual server-built payload
   (`GET .../export-json`), not an approximation.
3. **Uni-Commerce invoice builder** — invoice headers, consumer details,
   row items, `IsReturn`/parent-reference logic, live calculated totals.
4. **Order Requests** — reads the real `OrderRequests` table (not a local
   file): a filterable, paginated list with four live stat tiles, and a
   route-driven detail drawer (Overview / Request / Response / Line items /
   Payments / Invoice & lineage) with server-enforced cancel and resend.
5. **Bold-gradient design system** — a shared UI kit
   (`frontend/src/app/shared/ui/`) built on design tokens; see
   `docs/design-system.md`.
