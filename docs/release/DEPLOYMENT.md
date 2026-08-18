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
`release-manifest.json`, `file-integrity.sha256`, a sanitized root
`appsettings.json`, and this deployment folder.

The release manifest records the exact .NET SDK, Node.js, and npm versions used
to build the artifact. Byte identity is verified for repeated builds from the
same source commit using the recorded toolchain in an equivalent build
environment, including checkout byte materialization; cross-environment byte
identity is not guaranteed.

## Prerequisites

- Install the compatible .NET 10 ASP.NET Core Hosting Bundle, including the IIS
  ASP.NET Core Module v2.
- Use an IIS application pool configured for **No Managed Code** and the
  **Integrated** pipeline.
- Grant the IIS application-pool identity **Modify** access to
  `var/drafts`. This is runtime-owned storage; it is intentionally absent from
  the package and must be created on the target.
- The packaged `appsettings.json` is a deterministic, byte-identical copy of
  `deployment/appsettings.Testing.template.json`. It is fail-safe Testing
  configuration: `SupportHub:DeploymentTier` is `Testing`, custom endpoints are
  disabled, every Production registration is disabled, endpoint values are
  placeholders only, and Production database overrides are absent.
- Keep connection strings, authorized Testing gateway values, and all other
  server-owned environment overrides outside Git and outside the ZIP. Inject
  them through the approved server configuration source after extraction; the
  package never contains customer Testing secrets or Production configuration.
- A Testing host rejects Production operations before downstream calls. This
  package is not Production configuration or Production acceptance evidence.

## Authorized operator sequence

1. Confirm the release manifest source commit, build ID, configuration schema
   identity, and ZIP sidecar hash before opening the deployment window.
2. Stop the approved IIS application pool and make a recoverable backup of the
   current application directory.
3. Create `var/drafts`, apply the ACL requirement, and preserve the server-owned
   configuration/secret source.
4. Extract the ZIP contents into the application directory.
5. Preserve the sanitized packaged defaults and inject only the authorized
   server-owned Testing configuration source; do not replace the package with
   Production topology or commit credentials.
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

The offline verifier scans emitted HTML, CSS, JavaScript/module, JSON, SVG, web
manifest, and XML text assets. It rejects absolute and resource-bearing
protocol-relative external URLs, including CDN/font references. Only the exact
POS origins and exact current framework namespace/license metadata documented in
the verifier are permitted.
