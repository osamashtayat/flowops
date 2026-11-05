# ADR-0007: EF Core via IAppDbContext and query services; no generic repositories

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
EF Core already implements unit-of-work and repository patterns. We need efficient read projections and clear persistence boundaries without controllers touching the database.

## Decision
Application defines `IAppDbContext` (DbSets + SaveChangesAsync) implemented by `AppDbContext`. Command services load aggregates and save once. Query services project directly to DTOs with `Select` (`AsNoTracking`). No `IRepository<T>`. Only Application services use `IAppDbContext`; controllers never do. Migrations live in Infrastructure; snake_case naming; enums as text with check constraints; constraints and indexes per DOMAIN_MODEL.md §6.

## Consequences
+ Efficient SQL, no N+1 from mapping entity graphs.
+ Less code; LINQ visible where it runs.
- Application references EF Core abstractions; swapping ORMs would be work (no requirement).
- Query discipline relies on review (projections, no client evaluation) — encoded in rules/database.md.

## Alternatives rejected
Generic repositories (duplicate EF, block projections); Dapper for reads (two data-access styles for little gain at this scale).
