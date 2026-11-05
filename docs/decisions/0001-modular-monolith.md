# ADR-0001: Modular monolith in a monorepo

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
FlowOps is a small-team product with one database, strong transactional needs (decision + next task + audit + notifications commit together) and no independent scaling requirements.

## Decision
One deployable ASP.NET Core API + one React SPA + one PostgreSQL database, in one repository. Modules are feature folders inside layered projects (Requests, Tasks, Notifications, Organization, Dashboard), not separate services.

## Consequences
+ Single transaction for every use case; no distributed consistency.
+ One build, one deploy, simple local setup.
+ Frontend and backend contracts evolve in the same PR.
- Module boundaries are enforced by convention and tests, not process isolation.
- Extracting a service later requires real work (accepted; no requirement exists).

## Alternatives rejected
Microservices (operational cost, distributed transactions for no benefit); separate repos for frontend/backend (contract drift).
