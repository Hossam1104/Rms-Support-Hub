# POS Agent OpenAPI

`RmsSupportHub.Pos.Agent.json` is generated from the destination-owned Agent
endpoint composition during the Agent build. Do not edit it manually.

To regenerate locally, provide the deployment-owned exact Support Hub origin
configuration and build the Agent:

```powershell
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test:4443'
dotnet build pos/src/RmsSupportHub.Pos.Agent/RmsSupportHub.Pos.Agent.csproj -c Release
```

The Support Hub TypeScript contract is then regenerated separately. Install
the isolated generator workspace, then run the existing frontend convenience
command:

```powershell
npm ci --prefix tools/pos-agent-client-generator
npm run generate:pos-agent-client --prefix frontend
```

The generator is build-only tooling and is not part of the Angular
application dependency graph.

## Build prerequisite

`PosAgentSecurity__SupportHubOrigin` is required for every build or test
command that composes the Agent host, not only the single-project build shown
above. This includes the canonical full-solution command:

```powershell
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test:4443'
dotnet build pos/RmsSupportHub.Pos.slnx -c Release --nologo --warnaserror
```

Without it, `AgentSecurityOptions.Validate()` throws during the build-time
OpenAPI document generation step, which composes the Agent host. This is the
same exact Testing origin the provisioning scripts and Agent integration
tests use; CI configures the same value for its Windows/Agent/contract-
generation lanes.

## Scalar documentation boundary

The Agent uses the exact stable `Scalar.AspNetCore` package version `2.16.18`.
The package choice is pinned in the Agent project and is validated through the
normal restore/build vulnerability check. Scalar and runtime OpenAPI are
mapped only when the host environment is `Development` or the dedicated
`IntegrationTest` environment:

- `/openapi/{documentName}.json` serves the generated contract;
- `/scalar` redirects to `/scalar/`, which serves the local API reference;
- Scalar's AI Agent is disabled; and
- Scalar's default external font loading is disabled.

Production must not register either route. The OpenAPI document and generated
Support Hub client are derived artifacts; when endpoint metadata or schemas
change, regenerate both and run the drift check rather than editing either
file manually.
