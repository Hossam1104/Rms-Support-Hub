# Packaged runtime smoke checks

These checks run against the extracted package root. The automated harness is
`scripts/smoke-test-release-candidate.ps1`; it starts the packaged framework-
dependent API on a loopback port, performs the checks, and stops that temporary
process. It does not probe RMS gateways or databases.

The packaged root `appsettings.json` is the sanitized Testing template copy.
It has Testing authority, disabled Production registrations, no Production
database override, no concrete gateway addresses, and no embedded secrets.
Authorized customer Testing endpoints and secrets are external server-owned
configuration and are not required for this offline smoke run.

## Required checks

- `GET /` returns the packaged Angular index and `<app-root>`.
- `GET /api/health/live` returns a healthy local process response.
- `GET /api/health/ready` returns ready, reports the expected deployment tier,
  and proves `var/drafts` can be written.
- `GET /api/modules` returns the module catalogue without requiring a browser
  supplied endpoint or connection string.
- `GET /tools/online-orders` returns the Angular index through SPA fallback.
- `GET /build-identity.json` reports the source commit/build ID in the release
  manifest.
- The hashed main JavaScript bundle and representative local assets return
  successfully and retain their expected hashes.

Example after a fresh extraction:

```powershell
.\scripts\smoke-test-release-candidate.ps1 `
  -PackageRoot .\verification\RmsSupportHub-IIS
```

The smoke run is a Testing/offline package check. It is not Production
acceptance, database acceptance, or IIS deployment evidence.
