# Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.0.8.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## POS Agent contract types

The POS Agent owns the authoritative OpenAPI document at
`../pos/openapi/RmsSupportHub.Pos.Agent.json`. After building the Agent with its
deployment-owned `PosAgentSecurity:SupportHubOrigin` configuration, install the
locked frontend dependencies and regenerate the destination-owned TypeScript
types:

```bash
npm ci
npm ci --prefix ../tools/pos-agent-client-generator
npm run generate:pos-agent-client
```

The OpenAPI generator is installed from the isolated
`../tools/pos-agent-client-generator` workspace; it is not a frontend
dependency. The repository generation script uses its committed lockfile and
does not download a global or floating CLI.

The generated output is
`src/app/core/pos-agent/generated/pos-agent-api.generated.ts`; it is derived
output and must not be edited manually. The dedicated POS Agent transport uses
`HttpBackend` so the Support Hub API interceptor chain cannot rewrite its
credentialed direct-Agent requests.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
