# 🚀 Quick Start & Command Cheat Sheet

> [!NOTE]
> Detailed command reference for running, testing, building, and troubleshooting the **Online Order Tool** (.NET 10 Web API + Angular 22).

---

## ⚙️ Prerequisites Check

Ensure you have the required runtimes installed before running the project:

| Tool | Minimum Version | Command to Check |
| :--- | :--- | :--- |
| **.NET SDK** | `.NET 10.0+` | `dotnet --version` |
| **Node.js** | `v18.0+` | `node -v` |
| **npm** | `v9.0+` | `npm -v` |

---

## 🔌 1. Running the Application

### ⚡ Option A: Separate Development Terminals (Recommended)

#### Terminal 1 — Backend (.NET 10 API)
```bash
cd backend
dotnet run --project src/OnlineOrderTool.Api
```
> 📍 **API Endpoint**: `http://localhost:5200/api` (see `Properties/launychSettings.json`)
> 📖 **OpenAPI Specs**: `http://localhost:5200/openapi/v1.json`

#### Terminal 2 — Frontend (Angular 22)
```bash
cd frontend
npm install
npx ng serve --open
```
> 🌐 **App URL**: `http://localhost:4200`
> 🔀 `ng serve` proxies `/api` to `http://localhost:5200` via `proxy.conf.json` — no CORS needed in development.

---

### ⚡ Option B: Single Command (PowerShell)

Run both Backend and Frontend in one PowerShell window:

```powershell
Start-Process powershell -ArgumentList "-NoExit -Command cd '$PWD\backend'; dotnet run --project src/OnlineOrderTool.Api"; Start-Process powershell -ArgumentList "-NoExit -Command cd '$PWD\frontend'; npx ng serve --open"
```

---

## 🧪 2. Running Test Suites

### Backend Unit & Integration Tests (xUnit)
```bash
cd backend
dotnet test --verbosity normal
```

### Frontend Angular Unit Tests
```bash
cd frontend
npx ng test --watch=false
```

---

## 📦 3. Production Build & Deployment

### Build Backend Assembly
```bash
cd backend
dotnet build --configuration Release
```

### Build Frontend Production Bundle
```bash
cd frontend
npx ng build --configuration production
```
> 📁 Output directory: `frontend/dist/frontend/`

---

## 🛠️ 4. Useful Maintenance Commands

| Task | Shell Command |
| :--- | :--- |
| **Restore .NET Dependencies** | `cd backend && dotnet restore` |
| **Clean .NET Build Artifacts** | `cd backend && dotnet clean` |
| **Install Frontend Packages** | `cd frontend && npm install` |
| **Audit Frontend Vulnerabilities** | `cd frontend && npm audit` |
| **Check Port 5200 Usage (Windows)** | `netstat -ano \| findstr :5200` |
| **Check Port 4200 Usage (Windows)** | `netstat -ano \| findstr :4200` |

---

> 💡 **Need More Context?**  
> Check the full documentation in [`docs/api-spec.md`](file:///d:/AI%20Tools/DBS/online_order_tool/docs/api-spec.md) and [`README.md`](file:///d:/AI%20Tools/DBS/online_order_tool/README.md).