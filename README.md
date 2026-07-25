# Online Order Tool (.NET 10 + Angular 22)

A enterprise order management application supporting multiple client modules (**GHC E-Commerce**, **UPC E-Commerce**, **GHC Uni-Commerce**, **OMS**, **Call Center**). Built with a **.NET 10 Web API** backend and an **Angular 22** frontend featuring a custom **Glassmorphism Design System**.

---

## Tech Stack Overview

- **Backend**: .NET 10 Web API (C#, Controller-based `[ApiController]`)
- **Data Access**: Dapper + Microsoft.Data.SqlClient (SQL Server)
- **Frontend**: Angular 22 (TypeScript, Standalone components, Signals, Angular CDK)
- **Styling**: Custom Glassmorphism CSS Design System with dark/light theme switching & micro-animations
- **Architecture**: Separated solution structure (`Api`, `Core`, `Data`, `Tests`)

---

## Project Layout

```
online_order_tool/
├── backend/
│   ├── OnlineOrderTool.slnx             # .NET 10 Solution
│   ├── src/
│   │   ├── OnlineOrderTool.Api/         # Controllers, DI configuration, Middleware
│   │   ├── OnlineOrderTool.Core/        # Domain models, serializers, validators, services
│   │   └── OnlineOrderTool.Data/        # Dapper SQL Server repositories
│   └── tests/
│       └── OnlineOrderTool.Tests/       # xUnit test suite (21 passing tests)
│
├── frontend/                            # Angular 22 application
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/                    # ApiService, ThemeService, ToastService, ModuleService
│   │   │   ├── shared/                  # Toast, Skeleton, JsonViewer, StatusBadge
│   │   │   ├── layout/                  # Navbar, Sidebar (collapsible), Breadcrumb
│   │   │   └── features/                # Landing, FlatOrder, Unicommerce, OrderRequests, OrderValidation
│   │   └── styles/                      # CSS custom properties, glassmorphism mixins, keyframes
│
├── docs/                                # Documentation & example payloads
│   ├── request_examples/                # Reference JSON payloads
│   ├── api-spec.md                      # REST API specification
│   └── database-schema.md               # SQL Server query schema
│
└── _legacy_flask/                       # Archived Python Flask reference implementation
```

---

## Getting Started

### 1. Prerequisites
- **.NET 10 SDK** (v10.0.100+)
- **Node.js** (v18+)

### 2. Configuration & secrets

**No connection string is ever committed to this repository.** `appsettings.json`
tracks four empty `ConnectionStrings` placeholders (`GhcEcommerce`,
`UpcEcommerceProd`, `UpcEcommerceTest`, `GhcUnicommerce`); the API throws a
`ConfigurationException` naming the exact missing key at the point a database
call needs one, instead of failing with an opaque error inside Dapper.

**Development** — populate them with [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(already initialized for `OnlineOrderTool.Api`; the secrets live outside the
repo under your user profile, not in any tracked file):

```bash
cd backend/src/OnlineOrderTool.Api
dotnet user-secrets set "ConnectionStrings:GhcEcommerce"    "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:UpcEcommerceProd" "Server=<host>;Database=RmsMainProd;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:UpcEcommerceTest" "Server=<host>;Database=RmsMainTest2;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:GhcUnicommerce"   "Server=<host>;Database=<db>;User Id=<user>;Password=<pwd>;TrustServerCertificate=True;"
dotnet user-secrets list   # confirm all four are set
```

**Production** — set the equivalent environment variables (double-underscore
is ASP.NET Core's configuration-key separator):

```
CONNECTIONSTRINGS__GHCECOMMERCE=...
CONNECTIONSTRINGS__UPCECOMMERCEPROD=...
CONNECTIONSTRINGS__UPCECOMMERCETEST=...
CONNECTIONSTRINGS__GHCUNICOMMERCE=...
```

GHC and Uni-Commerce credentials are not yet confirmed against a real
database — see `docs/database-schema.md` §3.1. Real credentials that were
previously hardcoded in source are already in git history; rotating them is
a decision for whoever owns that SQL Server instance, not something this
repository can undo by deleting the file.

### 3. Run Backend (.NET 10 Web API)
```bash
cd backend
dotnet run --project src/OnlineOrderTool.Api
```
The API server will launch on `http://localhost:5200` (see `backend/src/OnlineOrderTool.Api/Properties/launchSettings.json`).

### 4. Run Backend Unit & Integration Tests
```bash
cd backend
dotnet test
```

### 5. Run Frontend (Angular 22)
```bash
cd frontend
npm install
npx ng serve
```
Open `http://localhost:4200` in your browser.

---

## Key Features

1. **Module & Environment Selector**: Choose between GHC, UPC, and Uni-Commerce modules across Production and Testing environments.
2. **Glassmorphism UI**: Premium dark/light themes with backdrop-filter blur, smooth transitions, and visual badges.
3. **Flat Order Builder (GHC & UPC)**: Order info, client details, consumer lookup, product CRUD with in-modal UPC lookup, payment method rules.
4. **Uni-Commerce Invoice Builder**: Invoice headers, consumer details, row items table, `IsReturn` parent reference logic, live calculated totals.
5. **Order Requests History**: Dedicated per-module history tab displaying sent order accordion cards, relative timestamps, raw request/response JSON viewers, copy buttons, inline cancellation, and order re-sending.
6. **UPC Order Validation**: Multi-criteria database search, status decoding (1-9), resend eligibility checks, and detailed header/line-item/invoice inspection modals.
