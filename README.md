# FlowOps

Internal requests, routed and tracked. Employees submit equipment-purchase and leave requests; FlowOps routes them through managers and finance/HR/procurement officers, records every decision, and keeps everyone notified in real time.

**Status:** Phase 1 architecture complete; implementation in progress — see [`docs/PHASE1_TASKS.md`](docs/PHASE1_TASKS.md).

| | |
|---|---|
| Backend | ASP.NET Core (.NET 10), EF Core, PostgreSQL, ASP.NET Core Identity (cookie auth), SignalR |
| Frontend | React, TypeScript (strict), Vite, Tailwind CSS v4, TanStack Query, React Router |
| Tests | xUnit + Testcontainers (PostgreSQL); Vitest + Testing Library |
| Architecture | Modular monolith, four backend layers — [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |

## Getting started

Prerequisites: .NET 10 SDK, Node.js ≥ 22, Docker.

```bash
cp .env.example .env                       # then edit the placeholder passwords
docker compose up -d db                    # PostgreSQL on 127.0.0.1:5432

# Backend. ASP.NET Core does not read .env: give the API its settings through user-secrets
# (or export .env into your shell first: set -a; source .env; set +a)
dotnet dev-certs https --trust
dotnet tool restore                        # installs the pinned dotnet-ef
dotnet user-secrets --project src/backend/FlowOps.Api set "ConnectionStrings:FlowOps" "Host=localhost;Port=5432;Database=flowops;Username=flowops;Password=<POSTGRES_PASSWORD from .env>"
# First administrator: created once at start-up, must change the password at first sign-in.
# Sign in with this email and password (the same values as FlowOps__BootstrapAdmin__Email / __Password in .env).
dotnet user-secrets --project src/backend/FlowOps.Api set "FlowOps:BootstrapAdmin:Email" "admin@flowops.local"
dotnet user-secrets --project src/backend/FlowOps.Api set "FlowOps:BootstrapAdmin:Password" "<12+ chars, upper, lower, digit, symbol>"
dotnet ef database update --project src/backend/FlowOps.Infrastructure --startup-project src/backend/FlowOps.Api
dotnet run --project src/backend/FlowOps.Api        # https://localhost:7180

# Frontend
cd src/frontend
npm install
npm run dev                                # http://localhost:5173 (proxies /api and /hubs to the API)
```

### Checks
```bash
dotnet test                                # backend (integration tests need Docker)
cd src/frontend && npm run typecheck && npm run lint && npm test && npm run build
```

## Documentation

| Doc | What it answers |
|---|---|
| [PRD](docs/PRD.md) | What we're building, for whom, acceptance criteria |
| [ARCHITECTURE](docs/ARCHITECTURE.md) | Layers, boundaries, what goes where |
| [DOMAIN_MODEL](docs/DOMAIN_MODEL.md) | Entities, statuses, invariants, transitions |
| [WORKFLOWS](docs/WORKFLOWS.md) | Purchase and leave processes, edge cases, concurrency |
| [AUTHORIZATION](docs/AUTHORIZATION.md) | Auth design, permission matrix, data visibility |
| [API_CONTRACT](docs/API_CONTRACT.md) | Endpoints, DTOs, errors, SignalR events |
| [UI_SPEC](docs/UI_SPEC.md) | Pages, routes, states, role-specific UX |
| [DESIGN_SYSTEM](docs/DESIGN_SYSTEM.md) | Beige design tokens, components, accessibility |
| [CODE_STANDARDS](docs/CODE_STANDARDS.md) | Separation of concerns, DRY, SOLID — with examples |
| [TESTING](docs/TESTING.md) | Test strategy, required scenarios, commands |
| [PHASE1_TASKS](docs/PHASE1_TASKS.md) | Implementation backlog |
| [ROADMAP](docs/ROADMAP.md) | Phase 2/3 evolution |
| [AGENT_HANDOFF](docs/AGENT_HANDOFF.md) | How coding sessions coordinate |
| [decisions/](docs/decisions/README.md) | Architecture Decision Records |

## Working with AI coding agents
`AGENTS.md` is the shared instruction file for any agent; `CLAUDE.md` imports it for Claude Code and adds `.claude/` rules, specialist agents and skills (`/implement-feature BE-01`, `/verify-feature`, `/review-change`, `/review-ui`).
