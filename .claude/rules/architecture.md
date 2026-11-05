# Architecture rules (always apply)

Full rationale: `docs/ARCHITECTURE.md`, ADRs in `docs/decisions/`.

## Layers and dependency direction
- `FlowOps.Domain` → BCL only. No EF Core, ASP.NET, Identity, logging, async I/O, `DateTime.Now`.
- `FlowOps.Application` → Domain + EF Core abstractions (`IAppDbContext`) + `Microsoft.Extensions.*` abstractions. Never Npgsql, ASP.NET Core, SignalR, Infrastructure, Api.
- `FlowOps.Infrastructure` → Application + Domain + technical packages. Never Api.
- `FlowOps.Api` → Application; Infrastructure **only** in the composition root (`Program.cs`, DI extension calls).
- Adding a project reference outside this table is an architecture change: stop and flag it.

## What goes where
- **Controller:** routes, binding, `[Authorize(Policy=…)]`, one service call, HTTP result. ≤ ~10 lines per action.
- **Application service:** a use case — load, check access, call domain, create notifications, `SaveChangesAsync` once, publish realtime after commit. Returns DTOs.
- **Domain:** invariants, state transitions, process step order. Throws `DomainException(code)`.
- **Infrastructure:** EF configurations/migrations, Identity, SignalR, sequences.
- **Frontend page:** composes feature components + hooks; no fetch logic, no permission algorithms, no business calculations.

## Boundaries
- Use-case services must not call other use-case services. Share knowledge through helpers that never save (`AccessRules`, `TaskEligibility`, `TaskAssigneeResolver`, `NotificationFactory`).
- No circular dependencies between feature folders; `Common/` and `Access/` may be used by all features, features don't use each other's services.
- Entities never cross the API boundary; DTOs only.

## Avoid overengineering
- Phase 1 only. No workflow engine, rule DSL, plugin registry, outbox, background jobs, CQRS/MediatR, generic repositories, AutoMapper.
- Interfaces only for real boundaries (I/O, identity, time, realtime) or test seams — not one interface per service.
- New shared abstraction requires ≥ 2 current callers and a one-line justification in your report.

## Extension point
New request type = `RequestType` value + details type + `FixedProcesses` entry + submit endpoint + form. If you must change decision, access, inbox or notification code to add one, flag it — the design has leaked.
