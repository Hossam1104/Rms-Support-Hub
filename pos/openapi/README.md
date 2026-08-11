# POS Agent OpenAPI

`RmsSupportHub.Pos.Agent.json` is generated from the destination-owned Agent
endpoint composition during the Agent build. Do not edit it manually.

To regenerate locally, provide the deployment-owned exact Support Hub origin
configuration and build the Agent:

```powershell
$env:PosAgentSecurity__SupportHubOrigin = 'https://support-hub.integration.test'
dotnet build pos/src/RmsSupportHub.Pos.Agent/RmsSupportHub.Pos.Agent.csproj -c Release
```

The Support Hub TypeScript contract is then regenerated separately with
`npm run generate:pos-agent-client` from `frontend`.
