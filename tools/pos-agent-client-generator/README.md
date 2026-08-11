# POS Agent client generator

This is destination-owned build tooling for the Support Hub's generated POS
Agent TypeScript contract. It is intentionally outside `frontend/` so the
Angular application's TypeScript 6 dependency graph does not share the
OpenAPI generator's TypeScript 5 peer dependency.

Install the exact locked toolchain before generation:

```powershell
npm ci --prefix tools/pos-agent-client-generator
```

Then run `npm run generate:pos-agent-client` from `frontend`, or invoke
`scripts/generate-pos-agent-client.ps1` from the repository root. The script
uses only this workspace's installed generator and the committed lockfile.
