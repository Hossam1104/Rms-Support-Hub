# Manual IIS Deployment

Temporary manual publish/deploy workflow for RMS+ Support Hub, until a CI/CD
pipeline exists. Produces a single package that combines the Angular
production build and the .NET Release publish, ready to extract directly into
the existing IIS site.

## Build

From the repository root:

```powershell
.\scripts\publish-iis.ps1
```

The script is deterministic and non-interactive: it cleans previous output,
installs frontend dependencies with `npm ci`, builds Angular with the
`production` configuration, publishes the .NET API in Release, stages the
Angular build into the API's `wwwroot`, verifies the required artifacts, and
zips the result. It exits non-zero on the first failed stage (build, publish,
copy, or verification).

## Result

```text
publish/
├── RmsSupportHub-IIS/       staged application files
└── RmsSupportHub-IIS.zip    deployable archive
```

`RmsSupportHub-IIS/` contains the API assembly, its dependencies,
`web.config`, `appsettings.json`, and `wwwroot/` with the Angular production
build (`index.html`, hashed `.js`/`.css`, `assets/`).

**The ZIP's root is the application itself** — there is no extra
`RmsSupportHub-IIS/` folder inside it. Extracting it into a directory places
`RmsSupportHub.Api.dll`, `web.config`, `wwwroot/`, etc. directly in that
directory.

## Server deployment

The IIS Application Pool `online order tool` already exists and is not
touched by this script. The physical directory is whatever that site/app
already points at.

```text
1. Stop IIS Application Pool: online order tool
2. Backup the current application directory (copy it aside; do not delete)
3. Extract RmsSupportHub-IIS.zip directly into that physical directory
4. Preserve or restore server-owned configuration (see "Secrets" below)
5. Start IIS Application Pool: online order tool
6. Verify: / loads the Angular app, /api/modules responds, a deep link
   (e.g. /tools/online-orders) loads directly and survives a refresh
```

### Important — extract into the directory, not next to it

If the IIS physical path is e.g. `C:\inetpub\wwwroot\OnlineOrderTool`, the
correct result is:

```text
C:\inetpub\wwwroot\OnlineOrderTool\
├── RmsSupportHub.Api.dll
├── RmsSupportHub.Core.dll
├── RmsSupportHub.Data.dll
├── web.config
├── appsettings.json
└── wwwroot\
    ├── index.html
    ├── *.js
    ├── *.css
    └── assets\
```

**Not** a nested `C:\inetpub\wwwroot\OnlineOrderTool\RmsSupportHub-IIS\...`.
Most extraction tools create that nested folder by default when you
right-click → Extract Here on a ZIP whose top-level entries are the files
themselves only if you extract *into* the target directory, not next to it —
confirm the destination before extracting, and delete any accidental nested
folder.

## Secrets and server-owned configuration

The publish script never writes secrets into the package. `appsettings.json`
in source control only ever contains empty placeholder connection strings
(see the `_comment_ConnectionStrings` note in that file); no
`appsettings.Production.json` is tracked in this repository.

Two supported ways to supply the real connection strings on the server,
neither of which this script touches:

- **Environment variables** (preferred) — `CONNECTIONSTRINGS__GHCECOMMERCE`,
  `CONNECTIONSTRINGS__UPCECOMMERCETEST`, `CONNECTIONSTRINGS__GHCUNICOMMERCE`,
  set at the IIS Application Pool / server level.
- **A server-owned `appsettings.Production.json`** placed directly in the
  IIS physical directory, outside Git, if that is how the server is
  currently configured.

If the server uses a server-owned `appsettings.Production.json`, back it up
in step 2 above and restore it after extracting the ZIP in step 3 — the ZIP
does not contain one, so extraction will not overwrite it, but a from-scratch
directory wipe would remove it.

## .NET 10 hosting requirements (server-side, not automated here)

Framework-dependent publish (this script does not target a specific RID), so
the server needs the ASP.NET Core Hosting Bundle for .NET 10 installed.
Verify on the server:

```powershell
dotnet --list-runtimes
```

Expect to see compatible `Microsoft.NETCore.App 10.x` and
`Microsoft.AspNetCore.App 10.x` entries. The IIS Application Pool `online
order tool` should be configured with:

```text
.NET CLR Version: No Managed Code
Managed Pipeline Mode: Integrated
```

This script does not change the Application Pool, create a second IIS site,
or otherwise touch IIS — packaging only.

## Rollback

Retain the backup made in deployment step 2. To roll back: stop the pool,
restore the backed-up directory contents, restart the pool, and re-verify.

## Boundaries

- This workflow is manual and local-build only; no CI/CD pipeline is created
  by it, and no remote/server modification (PowerShell remoting, Web Deploy,
  FTP, SSH) is performed by `publish-iis.ps1`.
- Production database access, live acceptance testing, and Production
  deployment execution remain out of scope — see
  [RMS_SUPPORT_HUB_RELEASE_READINESS.md](RMS_SUPPORT_HUB_RELEASE_READINESS.md)
  for the full deployment-preconditions and rollback-expectations record.
