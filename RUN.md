# Run RMS+ Support Hub locally

These commands run the backend and frontend locally. The API process uses the
`Development` ASP.NET environment so it loads the connection strings stored in
local .NET user-secrets; the selected RMS module environment remains **Testing**.
Use two PowerShell windows so both processes stay running.

## Backend — PowerShell window 1

```powershell
cd "D:\AI Tools\DBS\Rms-Support-Hub\backend"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\src\RmsSupportHub.Api --no-launch-profile --urls "http://localhost:5200"
```

The API will be available at `http://localhost:5200`.

## POS Maintenance console

The POS route is available only through the exact secure Testing handoff. The
local HTTP development origin is intentionally not an Agent origin. On an
authorized Testing machine, use the bounded provisioning/runtime scripts:

```powershell
cd "D:\AI Tools\DBS\Rms-Support-Hub"
.\scripts\bootstrap-rms-support-agent.ps1 -Status
.\scripts\bootstrap-rms-support-agent.ps1 -PlanOnly -Channel Testing
```

The first command is read-only. The second prints the package, browser,
certificate, migration, and lifecycle plan without writing SCM, registry,
certificate, or RMS state. Live Testing provisioning requires the explicit
Testing-only flow and elevation; never run it against Production.

## Frontend — PowerShell window 2

```powershell
cd "D:\AI Tools\DBS\Rms-Support-Hub\frontend"
# Run this once if frontend\node_modules does not exist:
npm ci
npm start -- --host localhost --port 4200 --prebundle=false
```

Open `http://localhost:4200`. Angular proxies `/api` requests to the backend
through `frontend/proxy.conf.json`.

## Quick API check

With the backend running, use:

```powershell
Invoke-RestMethod "http://localhost:5200/api/modules"
```

Press `Ctrl+C` in each window to stop the backend and frontend.
