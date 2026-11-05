# ADR-0002: Four backend projects: Domain, Application, Infrastructure, Api

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
We need business rules isolated from HTTP and EF Core so they're testable and so controllers stay thin, without the ceremony of many layers.

## Decision
Projects and allowed references as in ARCHITECTURE.md §3. Application uses EF Core *abstractions* through `IAppDbContext` (not Npgsql). `ApplicationUser : IdentityUser<Guid>` lives in Infrastructure; Domain/Application refer to users by id and through a `UserReadModel` projection. An architecture test enforces references. Two test projects: fast domain unit tests and Docker-backed API integration tests.

## Consequences
+ Domain is pure and fast to test.
+ Controllers can't reach the database (no reference path to concrete DbContext in Api controllers by convention + test).
- Application depending on EF Core abstractions is less "pure" than a repository layer; accepted for direct, efficient projections (see ADR-0007).
- Identity user outside Domain means user-related rules live in Application (ManagerAssignmentRules).

## Alternatives rejected
Single project with folders (boundaries too easy to violate); Clean Architecture with repositories + MediatR + AutoMapper (indirection, and MediatR/AutoMapper are now commercially licensed); Domain referencing Identity (leaks infrastructure into the core).
