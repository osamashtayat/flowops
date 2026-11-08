# FlowOps

### Enterprise Workflow & Business Process Management Platform

**FlowOps** is a full-stack workflow and business process management platform designed to help organizations manage requests, coordinate tasks, track activities, and streamline internal operations.

The project aims to replace fragmented workflows, manual follow-ups, and disconnected tools with a centralized, structured, and scalable system.

Built with **ASP.NET Core (.NET 10)** and **React + TypeScript**, FlowOps emphasizes clean architecture, maintainability, separation of concerns, and real-world software engineering practices.

**Project Status:** 🚧 Active Development

---

## Overview

Organizations often rely on spreadsheets, email threads, and disconnected applications to manage everyday processes. This makes it difficult to track responsibilities, monitor progress, and maintain visibility across teams.

FlowOps addresses these challenges by providing a unified workspace where organizations can manage their operational workflows.

The platform is being developed incrementally, beginning with a functional business application and evolving toward a configurable workflow engine.

## Key Features

The following capabilities define the initial product scope and roadmap.

### Organization & User Management
- Organization-based user and data management.
- Secure authentication and authorization.
- Role-based access control.
- User identity and access management.

### Request Management
- Create, manage, and track business requests.
- Monitor request statuses and progress.
- Organize requests within their respective organizations.
- Maintain visibility into request lifecycles.

### Task Management
- Coordinate tasks associated with business operations.
- Track task assignments and progress.
- Support structured collaboration and accountability.

### Activity Tracking & Notifications
- Record important activities and operational changes.
- Maintain a history of relevant actions.
- Provide notifications for workflow-related events.

### Workflow Automation — Planned
- Configurable workflow definitions.
- Reusable workflow steps and transitions.
- Conditional routing and business rules.
- Automated task creation and execution.
- Extensible workflow processing.

*Features are being delivered progressively and may not all be available in the current development version.*

---

## Technology Stack

| Layer | Technologies |
|---|---|
| Backend | C#, ASP.NET Core Web API, .NET 10 |
| Frontend | React, TypeScript, Vite |
| Database | SQL Server, Entity Framework Core |
| Authentication | ASP.NET Core Identity, cookie sessions with antiforgery tokens |
| Data Access | Entity Framework Core |
| API Design | RESTful APIs, DTOs |
| State Management | TanStack Query, Zustand |
| Styling | Tailwind CSS |
| Infrastructure & Tools | Docker, Git, GitHub |

## Architecture & Engineering Principles

FlowOps is designed with an emphasis on code quality, maintainability, and extensibility.

### Separation of Concerns

Responsibilities are divided across clearly defined layers:

- **API Layer:** Handles HTTP requests, input validation, API contracts, and responses.
- **Application Layer:** Coordinates use cases and application-level operations.
- **Domain Layer:** Represents core business entities, rules, and concepts.
- **Infrastructure Layer:** Manages database persistence, identity integration, and external dependencies.
- **Frontend Layer:** Provides reusable UI components, application state, and API integration.

The objective is to keep controllers focused on HTTP concerns while placing business operations in appropriate application and domain services.

### Engineering Practices

- **SOLID Principles:** Promote modular, testable, and maintainable components.
- **DRY (Don't Repeat Yourself):** Extract shared logic into reusable components and utilities.
- **Dependency Injection:** Reduce coupling and enable flexible dependency management.
- **DTO-Based API Contracts:** Separate externally exposed data from internal domain models.
- **Asynchronous Programming:** Use async operations for I/O-bound backend workflows.
- **Consistent Error Handling:** Provide predictable errors across application boundaries.
- **Scalable Design:** Introduce abstractions where they support real requirements without unnecessary complexity.

---

## Getting Started (local development)

```bash
cp .env.example .env                 # then edit the placeholder values
docker compose up -d db              # PostgreSQL on localhost:5432
set -a; source .env; set +a          # or put the same values in dotnet user-secrets
dotnet tool restore
dotnet ef database update --project src/backend/FlowOps.Infrastructure --startup-project src/backend/FlowOps.Api
dotnet run --project src/backend/FlowOps.Api                  # API on https://localhost:7180
cd src/frontend && npm install && npm run dev                 # SPA on http://localhost:5173
```

### Full stack in Docker

```bash
cp .env.example .env                 # then edit the placeholder values
docker compose up -d --build         # db → migrate (one-shot) → api → web
```

Open **https://localhost:8443**. nginx serves the SPA over HTTPS with a self-signed certificate it creates for localhost
on first start, so your browser shows a warning; accept it, or mount a trusted certificate (for example from
[mkcert](https://github.com/FiloSottile/mkcert)) as `tls.crt` and `tls.key` at `/etc/nginx/certs`. HTTPS is required: the
session and antiforgery cookies are `Secure`. nginx proxies `/api` and `/hubs` to the API, which publishes no port of
its own. Sign in with the bootstrap administrator from `.env`.

To load the demo data (below) into the Docker database, run the API once in Development with the flag, and stop it with
Ctrl+C once it logs "Demo data seeded":

```bash
docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Development api --seed-demo
```

### Demo data (Development only)

Start the API once with `--seed-demo` to create a demo company:

```bash
dotnet run --project src/backend/FlowOps.Api -- --seed-demo
```

- It runs only when `ASPNETCORE_ENVIRONMENT=Development`; in any other environment the API refuses to start.
- It is idempotent: if the demo company already exists, nothing is added. Apply the migrations first.
- It creates 3 departments (Engineering, Finance, Operations), 11 users and 13 purchase and leave requests covering
  every state, including two stalled tasks and one pending administrator role change.

Every demo account uses the password **`FlowOps-Demo-2026!`**. This is a development-only test value, not a secret;
never seed a shared or production database.

| Email | Role(s) | Notes |
|---|---|---|
| `alice.admin@demo.flowops.test` | Administrator | Reviews Omar's pending role change |
| `omar.admin@demo.flowops.test` | Administrator | Has a pending change to his own roles |
| `dana.director@demo.flowops.test` | Employee | Operations; manages Samir, Farah, Hana, Yousef, Nour |
| `samir.manager@demo.flowops.test` | Employee | Engineering manager of Lina and Karim |
| `lina.dev@demo.flowops.test` | Employee | Engineering |
| `karim.dev@demo.flowops.test` | Employee | Engineering |
| `farah.finance@demo.flowops.test` | Employee, FinanceOfficer | The only finance officer |
| `hana.hr@demo.flowops.test` | Employee, HrOfficer | |
| `yousef.procurement@demo.flowops.test` | Employee, ProcurementOfficer | |
| `nour.ops@demo.flowops.test` | Employee | Her old purchase still waits for Rami |
| `rami.former@demo.flowops.test` | Employee | Inactive (left the company); cannot sign in |

---

## Development Roadmap

### Phase 1 — Core Business Application

Establish a functional, end-to-end business management platform.

- [ ] Complete authentication and authorization.
- [ ] Implement organization management.
- [ ] Complete request and task management.
- [ ] Integrate activity tracking and notifications.
- [ ] Connect backend services with the frontend.
- [ ] Validate core business workflows through integration testing.

### Phase 2 — Configurable Workflow Engine

Transform the application into a flexible workflow management platform.

- [ ] Introduce workflow definitions.
- [ ] Implement configurable steps and transitions.
- [ ] Support conditional routing.
- [ ] Implement workflow execution and state management.
- [ ] Support reusable workflow templates.

### Phase 3 — Enterprise Capabilities

Extend the platform for larger organizations and advanced operational requirements.

- [ ] Advanced permissions and access policies.
- [ ] Audit trails and operational monitoring.
- [ ] Analytics and reporting dashboards.
- [ ] Workflow versioning.
- [ ] Performance and reliability improvements.
- [ ] Production-oriented observability and deployment.

*The roadmap reflects planned development and is not a declaration of completed functionality.*

---

## Security Considerations

Security is an important part of the platform's architecture.

The intended security model includes:

- Authentication through ASP.NET Core Identity with a `__Host-` session cookie (HttpOnly, Secure, SameSite=Strict) and
  antiforgery tokens on every unsafe request (ADR-0003). There are no bearer tokens or JWTs.
- Role-based policies for coarse access, plus per-request visibility and decision rules enforced server-side
  (`AccessRules`, `TaskEligibility`; docs/AUTHORIZATION.md). Phase 1 serves one organisation; multi-tenancy is Phase 3.
- Two-person control for an administrator's own role changes (ADR-0012).
- Server-side input validation; login and comment rate limits; a per-user cap on real-time connections.
- Secrets only in environment variables or `dotnet user-secrets`, never in committed files.

Security-sensitive functionality requires testing and review before production deployment.

---

## Project Goals

FlowOps is being developed to achieve three primary objectives:

**1. Deliver a useful business application**

Build a platform that addresses practical operational challenges rather than serving only as a technical demonstration.

**2. Apply production-oriented software engineering**

Design and implement a maintainable full-stack application using established architectural principles, appropriate abstractions, and disciplined development practices.

**3. Establish a foundation for future extensibility**

Create an architecture capable of supporting increasingly complex business processes without requiring fundamental redesign.

---

## Contributing

FlowOps is currently under active development.

Feedback, suggestions, bug reports, and technical discussions are welcome through GitHub Issues.

## Author

**Osama Shtayat**

Computer Engineering | Software Engineering | Backend Development

[GitHub](https://github.com/osamashtayat)

---

**FlowOps — Bringing structure, visibility, and efficiency to business workflows.**
