# Manual IIS package entry point

The supported local package command is now the deterministic Testing/Staging
release-candidate pipeline:

```powershell
.\scripts\build-release-candidate.ps1
```

The compatibility command below delegates to the same pipeline:

```powershell
.\scripts\publish-iis.ps1
```

The result is written under `publish/`:

```text
publish/
├── RmsSupportHub-IIS/       staged application files
├── RmsSupportHub-IIS.zip    deterministic deployment archive
├── RmsSupportHub-IIS.zip.sha256
└── verification/            fresh extraction used by verification
```

The ZIP root is the application itself. It contains the framework-dependent
.NET 10 API publish output, `web.config`, Angular assets under `wwwroot`, a
Testing build identity, `release-manifest.json`, `file-integrity.sha256`, and
deployment/configuration documentation. It excludes secrets, local settings,
certificates/private keys, source maps, compiler symbols, and runtime `var`.

The package builder installs frontend dependencies with `npm ci`, builds the
Angular production bundle, publishes the API in Release, finalizes identity
from the source commit and a commit-derived UTC timestamp, scans HTML/CSS/JS
for unexpected public runtime URLs, writes integrity hashes, creates a sorted
fixed-timestamp ZIP, and verifies a fresh extraction.

The scripts only build and verify files. They do not deploy IIS, contact
Production, mutate Testing data, or send/cancel/resend orders. For the
operator-only deployment, rollback, prerequisite, and smoke instructions,
see the packaged files in [`docs/release`](release/).
