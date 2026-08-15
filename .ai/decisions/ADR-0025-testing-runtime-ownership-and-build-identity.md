# ADR-0025: Testing runtime ownership and served build identity

- Status: Accepted
- Date: 2026-08-15
- Affected area: Testing provisioning scripts, secure `:4443` Support Hub
  runtime, POS Advanced Diagnostics, POS CI.

## Context

The Testing Support Hub at `https://support-hub.integration.test:4443` served a
stale Angular bundle from a previously provisioned runtime under
`C:\ProgramData\DBS\RmsSupportHub\Int13Testing`. Startup reported success
because `GET /` returned HTTP 200 and `<app-root>` was present, which says
nothing about which build is being served. Separately, a certificate check in
`scripts/PosSupportHubProvisioning.psm1` split a boolean across two physical
lines without a continuation. PowerShell parsed the second line as a command
named `-and`: the script parsed cleanly, the private-key provider condition
never contributed, and the failure appeared only at runtime.

## Decision

1. **A clean parse is not a sufficient PowerShell gate.**
   `scripts/PosScriptQuality.psm1` analyses every Git-tracked `.ps1`/`.psm1`
   with the PowerShell parser and rejects a parse error, a `CommandAst` whose
   command name is an operator, a line starting with an operator when the
   previous line has no continuation, and a backtick followed by whitespace.
   Comments and strings are excluded by token extent, not by line text.
   PSScriptAnalyzer is used only when already installed; it is never fetched.

2. **Certificate policy is one fail-closed expression.** Provider, export
   policy, and CNG-ness are decided by `Test-PosSupportHubPrivateKeyPolicy`, and
   the private-key file ACL is rejected when it grants Everyone, Authenticated
   Users, Users, Anonymous, or Guests. Any failure terminates provisioning, and
   no certificate or key material is printed.

3. **Self-elevation is confined.** `start-pos-agent-testing.ps1` re-launches
   only itself, only through a PowerShell host resolved inside `$PSHOME`, with
   only its known typed parameters and explicit quoting, and exits the
   unelevated parent after handoff. `-NoSelfElevate` is the CI opt-out. This
   boundary is Testing tooling only; Production onboarding remains Slice C.

4. **Runtime ownership is proved, never assumed.** Startup adopts or stops only
   the process whose PID, image, and command line it owns. An unowned `:4443`
   listener is neither killed nor adopted; a stale owned listener is replaced
   through the normal flow. State binds runtime root, owned PID, expected
   executable, content root, build identity, certificate thumbprint, host, and
   port; a stale or corrupt state file fails closed.

5. **Freshness is proved by identity, not by status code.**
   `frontend/scripts/build-identity.mjs` emits `/build-identity.json` carrying
   the commit, source state, a deterministic asset-manifest build id, asset
   count, build timestamp, and the index and main-bundle hashes. Startup fails
   closed unless the expected, staged, and served identities agree and the
   served index and main bundle hash as recorded.

## Consequences

- The defect class that shipped is now rejected repository-wide and in CI.
- A stale `:4443` frontend is a typed startup failure rather than a silent
  success, at the cost of a full frontend rebuild and publish on each startup.
- The identity document is non-secret by construction: it carries no filesystem
  path, hostname, or credential, so the browser-facing Advanced Diagnostics
  panel may display it.
- `ng serve` has no immutable identity, so the development placeholder reports
  `unknown` and the UI names it as a development bundle rather than implying a
  real build.
