# RMS+ Support Hub Testing/Staging Release Candidate

This directory is packaged documentation for an operator-authorized IIS
deployment. The build pipeline only creates and verifies the package; it does
not connect to IIS, copy files to a server, start an application pool, contact
Production, or mutate Testing data.

## Package shape

The ZIP root is the IIS application root. Extract the contents directly into
the approved application directory; do not create an extra nested package
directory. The package contains the framework-dependent .NET 10 publish output,
`web.config`, Angular assets under `wwwroot`, `build-identity.json`,
`release-manifest.json`, `file-integrity.sha256`, and this deployment folder.

## Prerequisites

- Install the compatible .NET 10 ASP.NET Core Hosting Bundle, including the IIS
  ASP.NET Core Module v2.
- Use an IIS application pool configured for **No Managed Code** and the
  **Integrated** pipeline.
- Grant the IIS application-pool identity **Modify** access to
  `var/drafts`. This is runtime-owned storage; it is intentionally absent from
  the package and must be created on the target.
- Keep connection strings and any server-owned environment overrides outside
  Git and outside the ZIP. `appsettings.Testing.template.json` contains names
  and placeholders only.
- Configure `SupportHub:DeploymentTier` as `Testing` unless an explicitly
  approved server configuration selects another supported tier. A Testing host
  rejects Production operations before downstream calls.

## Authorized operator sequence

1. Confirm the release manifest source commit, build ID, configuration schema
   identity, and ZIP sidecar hash before opening the deployment window.
2. Stop the approved IIS application pool and make a recoverable backup of the
   current application directory.
3. Create `var/drafts`, apply the ACL requirement, and preserve the server-owned
   configuration/secret source.
4. Extract the ZIP contents into the application directory.
5. Restore or inject server-owned configuration without adding credentials to
   the package.
6. Start the application pool and run the checks in `SMOKE.md`.
7. Record the observed build identity and integrity result with the release
   evidence.

The sequence above is documentation only for this repository task. No IIS
deployment is performed by the release-candidate scripts or validation.

## Internal RMS gateway boundary

RMS gateway URLs are explicit server configuration under the named
`ModuleEndpoints` and `ModuleCancelEndpoints` keys. They are approved internal
application dependencies, not public Internet/CDN dependencies. The browser
does not receive those endpoint values; the API owns the configured authority.
