# INT-13C disposable browser evidence

This task-scoped harness launches the installed `chrome` or `msedge` channel with a fresh persistent profile, never downloads or uses a bundled browser, and records only sanitized status evidence. It does not set cookies, inject credentials, disable web security, ignore certificate errors, or print response bodies, tokens, principals, service names, paths, or secrets.

Run `scripts/invoke-pos-browser-evidence.ps1` from an elevated Testing-machine PowerShell session. The launcher creates a one-shot Scheduled Task for the currently interactive user with the Windows `Interactive` logon type and `RunLevel Limited`; the child process verifies Medium/MediumPlus integrity before launching the visible browser. The task is removed in `finally` and the disposable profile is removed by the Node runner.

The start URL must use the configured exact HTTPS SupportHubOrigin. Protected reads are considered proven only when the page rendered the POS Maintenance surface, the session and all first-release protected reads returned success, and the authorization labels confirmed Windows authentication plus local Administrator authorization. The optional service action requires an opaque `svc-...` ID and an explicit `-AllowDisposableServiceAction`; it chooses one state-valid typed action, holds the token only in page memory, never retries an unknown outcome, and records only status fields.

For the repository's local Angular smoke path only, the launcher accepts
`http://localhost:4200` when `-AllowLocalhostDevTest` is explicitly supplied.
That mode checks page reachability and normal-user browser behavior only; it is
not an authenticated Agent-evidence mode and does not relax Agent HTTPS,
origin, CORS, or certificate checks.

Example (Testing only):

```powershell
.\scripts\invoke-pos-browser-evidence.ps1 `
  -IUnderstandTestingOnly `
  -Browser chrome `
  -SupportHubOrigin https://support-hub.integration.test:4443 `
  -StartUrl https://support-hub.integration.test:4443/tools/pos-maintenance
```

If the exact Support Hub origin is not serving the real application or no installed browser channel can be launched in the interactive session, the output is `blocked`; it must not be rewritten as a pass.
