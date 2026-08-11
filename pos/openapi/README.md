# POS Agent OpenAPI

`RmsSupportHub.Pos.Agent.json` is generated from the destination-owned Agent
endpoint composition during the Agent build. Do not edit it manually.

To regenerate locally, provide the deployment-owned exact Support Hub origin
configuration and build the Agent:

```powershell
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test'
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
