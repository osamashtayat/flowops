# Architecture Decision Records

One file per decision: `NNNN-title.md` with Status (Proposed / Accepted / Superseded by ADR-XXXX), Context, Decision, Consequences, Alternatives rejected.

To change a decision, add a new ADR that supersedes the old one; never rewrite an accepted ADR's decision.

| ADR | Decision |
|---|---|
| [0001](0001-modular-monolith.md) | Modular monolith in a monorepo |
| [0002](0002-backend-layering.md) | Four backend projects: Domain, Application, Infrastructure, Api |
| [0003](0003-authentication.md) | Cookie authentication with ASP.NET Core Identity; resource checks in Application |
| [0004](0004-fixed-processes.md) | Fixed processes in code; no workflow engine in Phase 1 (**superseded by 0014** for Phase 2) |
| [0005](0005-domain-state-model.md) | Request/Stage/WorkTask state model with a single WorkTask entity |
| [0006](0006-concurrency.md) | Optimistic concurrency with PostgreSQL xmin and a partial unique index |
| [0007](0007-database-access.md) | EF Core via IAppDbContext and query services; no generic repositories |
| [0008](0008-frontend-state-and-contracts.md) | TanStack Query for server state; OpenAPI-generated types; contract-first skeleton |
| [0009](0009-notifications.md) | Notifications persisted in the business transaction; SignalR as post-commit hint |
| [0010](0010-background-processing.md) | No background job framework in Phase 1 |
| [0011](0011-design-system.md) | Token-first Tailwind v4 design system with disabled default palette |
| [0012](0012-two-person-admin-control.md) | Administrator role only on new accounts; own role change needs a second admin (#32) |
| [0013](0013-role-change-approver-account-age.md) | Role-change approver account must be a day older than the proposal (#48) |
| [0014](0014-configurable-workflows.md) | Phase 2 configurable workflows: versioned step lists with typed conditions; no engine; single-admin publish (supersedes 0004) |
