# Repository Structure

Where things live, where new work belongs, and what to read first.

## Layout

```
Rms-Support-Hub/
├── AGENTS.md                  # Canonical AI operating contract (CLAUDE.md just includes it)
├── TASK.md                    # Current mode and standing constraints
├── README.md                  # Product overview, setup, build, and run
│
├── backend/
│   ├── RmsSupportHub.slnx
│   ├── src/
│   │   ├── RmsSupportHub.Core/   # Domain models, module capabilities, payload builders, validators
│   │   ├── RmsSupportHub.Data/   # Dapper SQL Server repositories
│   │   └── RmsSupportHub.Api/    # Controllers, middleware, guards, DI composition root
│   └── tests/RmsSupportHub.Tests/
│
├── pos/
│   ├── RmsSupportHub.Pos.slnx
│   ├── src/
│   │   ├── RmsSupportHub.Pos.Domain/
│   │   ├── RmsSupportHub.Pos.Application/
│   │   ├── RmsSupportHub.Pos.Contracts/
│   │   ├── RmsSupportHub.Pos.Infrastructure/
│   │   ├── RmsSupportHub.Pos.Agent/
│   │   └── PosAdminTool.WinUI/
│   └── tests/
│       ├── RmsSupportHub.Pos.Domain.Tests/
│       ├── RmsSupportHub.Pos.Application.Tests/
│       └── RmsSupportHub.Pos.Infrastructure.Tests/
│
├── frontend/src/
│   ├── app/
│   │   ├── core/              # Guards, interceptors, typed models, global services, utilities
│   │   ├── shared/
│   │   │   ├── ui/            # Design-system primitives, exported through index.ts
│   │   │   ├── components/    # Older shared components (status badge, toast)
│   │   │   └── directives/
│   │   ├── layout/            # Navbar, sidebar, breadcrumb
│   │   └── features/
│   │       ├── hub/           # RMS+ Support Hub landing + hub-scene (Three.js)
│   │       ├── prompt-studio/ # Landing, three generators, builders, models, services
│   │       ├── landing/       # Online Order module picker
│   │       ├── module-shell/  # Online Order workspace shell
│   │       ├── flat-order/    # GHC/UPC order builder
│   │       ├── unicommerce/   # GHC Uni-Commerce invoice builder
│   │       ├── order-requests/# Request history, detail, cancel, resend
│   │       ├── pos-maintenance/ # Coming Soon informational page
│   │       └── kitchen-sink/  # Dev-only /_kitchen-sink primitive showcase
│   ├── styles/                # _tokens.css, _gradients.css, _typography.css, _animations.css
│   └── environments/
│
├── docs/                      # See docs/README.md for the index
├── scripts/                   # dev.ps1, build.ps1, verify-riyal-asset.ps1
└── .ai/                       # Agent working set: PROJECT, STATE, DECISIONS, decisions/, HANDOFF, HISTORY, scripts
```

INT-03 established the isolated POS build boundary with the approved portable,
Windows Infrastructure, and retained WinUI source snapshot. Its current
destination is:

```text
/pos
  /src
    RmsSupportHub.Pos.Domain
    RmsSupportHub.Pos.Application
    RmsSupportHub.Pos.Contracts
    RmsSupportHub.Pos.Infrastructure
    RmsSupportHub.Pos.Agent
    PosAdminTool.WinUI
  /tests
    RmsSupportHub.Pos.Domain.Tests
    RmsSupportHub.Pos.Application.Tests
    RmsSupportHub.Pos.Infrastructure.Tests
```

Portable POS projects remain `net10.0`; Windows Infrastructure, Agent, and
retained WinUI projects are Windows-targeted. Domain/Application/Contracts,
Infrastructure, Infrastructure tests, and WinUI contain the namespace-reconciled
source from provenance `25922b499d33bd73f241ffc26c212dd000e81433`, and all three
POS test suites are destination-owned. The Agent remains inert, with no POS
Angular workspace, runtime host, or privileged execution path imported. Existing
Support Hub `Core`, `Data`, and `Api` remain portable. The Agent owns privileged
POS operations and the Support Hub general API is not their proxy or execution
path.

## Route topology

Every tool is a lazy route with typed `ToolRouteData`, declared in
`frontend/src/app/app.routes.ts`:

```text
/                                                       Hub
/tools/prompt-studio                                    Prompt Studio landing
/tools/prompt-studio/bugs                               Bug Refinement
/tools/prompt-studio/stories                            Story Refinement
/tools/prompt-studio/test-cases                         Test Case Generation
/tools/online-orders                                    Online Order module picker
/tools/online-orders/modules/:key/order                 Flat order builder
/tools/online-orders/modules/:key/unicommerce           Uni-Commerce invoice builder
/tools/online-orders/modules/:key/order-requests        Request history
/tools/online-orders/modules/:key/order-requests/:orderId   Request detail
/modules/:key/...                                       Pre-hub Online Order compatibility mount
/tools/pos-maintenance                                  POS Coming Soon (informational)
/_kitchen-sink                                          Dev-only; tree-shaken from production
```

## Where new work belongs

| Change | Location |
| --- | --- |
| New Support Hub API endpoint | Controller in `RmsSupportHub.Api`, logic in `Core`, SQL in `Data`. Dependencies flow Core → Data → API only. Privileged POS endpoints belong to the future isolated Agent, not this API. |
| Module behavior difference | A flag on `IOrderModule.Capabilities`. Never a module-key string comparison. |
| New shared UI primitive | `frontend/src/app/shared/ui/<name>/`, exported from `shared/ui/index.ts`, demonstrated in the kitchen sink. |
| New feature screen | `frontend/src/app/features/<feature>/`, lazy-loaded from `app.routes.ts` with typed route data. The future POS screen remains a Support Hub-owned consumer of the separate Agent contract. |
| POS destination project | `pos/src/<project>/` and `pos/tests/<project>/`; the separate `pos/RmsSupportHub.Pos.slnx` is the POS build boundary. Portable projects must not reference Windows-targeted POS projects. |
| Card surface of any kind | Consume the `--card-*` tokens; do not introduce new radii, padding, or hover values. See `docs/design-system.md`. |
| Any color | A semantic token. Raw hex is allowed only in `styles/_tokens.css` and `styles/_gradients.css`. |
| A lasting technical decision | One ADR in `.ai/decisions/`, one row in `.ai/DECISIONS.md`. |

## Documentation rules

- Documents describe the repository as it is **today**. Plans, session prompts,
  and progress logs do not live in the tree after their work lands — Git
  history and `.ai/HISTORY.md` cover that.
- One topic, one document. If a new file would overlap an existing one, extend
  the existing one instead.
- `docs/api-spec.md`, `docs/database-schema.md`, and `docs/UI_Rework_Plan.md`
  are cited **by filename** from source comments. Do not rename or move them
  without updating every citation.
- Keep the `.ai/` working set inside the budgets enforced by
  `python .ai/scripts/check_memory.py`.

## Read first

1. `TASK.md` — current mode and constraints
2. `.ai/STATE.md` — durable current facts
3. `python .ai/scripts/context.py` — branch, HEAD, changed files
4. This file plus `docs/README.md` — placement and contracts
5. Only then the specific source, tests, and documents your task names
