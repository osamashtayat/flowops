# Phase 1 Backlog — FlowOps

**Purpose:** the ordered, dependency-aware implementation backlog. Each task is sized for one focused coding session (roughly ½–1 day for a person).
**How to use:** pick the lowest-numbered task in your lane whose dependencies are **Done**; run the `implement-feature` skill with its ID; update its status line here in the same PR.

Status values: `Todo` · `In progress` · `Done` · `Blocked (reason)`.

Every task inherits the **Definition of Done** (`AGENTS.md` §8). "Tests" lists the minimum; add more when behaviour warrants.

---

## Lanes and order

```mermaid
flowchart LR
  BE01[BE-01 solution] --> BE02[BE-02 persistence+errors] --> BE03[BE-03 contract skeleton]
  BE01 --> BE06[BE-06 domain core]
  BE02 --> BE04[BE-04 test harness] --> BE05[BE-05 auth]
  BE06 --> BE07[BE-07 domain mapping]
  BE05 --> BE08[BE-08 org admin]
  BE07 --> BE09[BE-09 access rules]
  BE08 --> BE10
  BE09 --> BE10[BE-10 submission] --> BE11[BE-11 request queries] --> BE12[BE-12 decisions]
  BE12 --> BE13[BE-13 notifications] --> BE14[BE-14 realtime]
  BE12 --> BE15[BE-15 dashboard]
  BE03 --> FE01[FE-01 api client]
  FE00[FE-00 scaffold ✔] --> FE02[FE-02 form primitives]
  FE00 --> FE05[FE-05 data display]
  FE00 --> FE06[FE-06 dialogs/toasts]
  FE01 --> FE03[FE-03 session] --> FE04[FE-04 shell+nav]
  FE04 --> FE07 & FE08 & FE10 & FE13
```

**Parallel lanes once BE-03 is Done:** Backend (BE-04…BE-16) ‖ Frontend (FE-01…FE-13 against MSW mocks generated from `contracts/openapi.json`). Frontend features need only the *contract* (BE-03), not the backend implementation; integration with the real API happens when the matching BE task is Done.

**Changes vs. the original suggested order:** the API contract skeleton (BE-03) is moved right after scaffolding, so frontend work doesn't wait for backend features; the domain core (BE-06) is pure C# and starts in parallel with infrastructure; notifications are split into persistence (with decisions) and realtime (later), because notification rows must be written in the decision transaction.

---

## Foundation

### FE-00 — Frontend scaffold, theme tokens, first primitives · `Done` (architecture session)
Vite + React + TS strict, Tailwind v4 with `theme.css` tokens (default palette disabled), `Button`, `StatusBadge`, `AppShell` placeholder, Vitest + Testing Library, token lint. Verified: typecheck, lint, test, build.

### BE-01 — Backend solution scaffold · `Done`
- **Objective:** create the solution exactly as `ARCHITECTURE.md` §2–3.
- **Deps:** none. **Modules:** `src/backend/*`, `tests/backend/*`, root `FlowOps.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
- **Steps (exact):**
  ```bash
  dotnet new sln -n FlowOps --format slnx
  dotnet new classlib -n FlowOps.Domain         -o src/backend/FlowOps.Domain
  dotnet new classlib -n FlowOps.Application    -o src/backend/FlowOps.Application
  dotnet new classlib -n FlowOps.Infrastructure -o src/backend/FlowOps.Infrastructure
  dotnet new webapi   -n FlowOps.Api --use-controllers -o src/backend/FlowOps.Api
  dotnet new install xunit.v3.templates
  dotnet new xunit3 -n FlowOps.Domain.Tests -o tests/backend/FlowOps.Domain.Tests
  dotnet new xunit3 -n FlowOps.Api.IntegrationTests -o tests/backend/FlowOps.Api.IntegrationTests
  # add all to sln; add project references per ARCHITECTURE.md §3
  ```
  Enable central package management; `Directory.Build.props`: `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`.
  Api: set `launchSettings.json` HTTPS URL to `https://localhost:7180` (the Vite proxy and docs assume it); remove WeatherForecast; add `GET /api/health` (anonymous, 200 `{status:"ok"}`); JSON console logging; `AddProblemDetails()`.
- **AC:** `dotnet build` with 0 warnings; `dotnet test` runs (one placeholder test per project); references match §3 exactly.
- **Tests:** architecture test asserting the dependency table (can use reflection over referenced assemblies).
- **Parallel:** with BE-06 once projects exist.

### BE-02 — Persistence foundation and error pipeline · `Done`
- **Objective:** `AppDbContext` (Identity), Npgsql, snake_case naming, migrations workflow, `IAppDbContext`, global exception→ProblemDetails handler, `ICurrentUser`, `TimeProvider`.
- **Deps:** BE-01. **Modules:** Infrastructure/Persistence, Application/Common, Api/Errors, Api/Auth.
- **AC:** `docker compose up -d db` + `dotnet ef database update` creates schema; exception types map to the statuses in `API_CONTRACT.md` §2 with `code` and `traceId`; unhandled → 500 without internals; connection string from env/user-secrets only.
- **Tests:** unit tests for the exception→status mapping table.
- **Parallel:** BE-06.

### BE-03 — API contract skeleton + OpenAPI export · `Done`
- **Objective:** make `API_CONTRACT.md` executable: every request/response DTO and every controller action with correct routes, `[ProducesResponseType]`s, and policies, each action returning `501 Not Implemented` until its feature task lands. Export `contracts/openapi.json` via build-time generation (`Microsoft.Extensions.ApiDescription.Server`) and a `scripts/export-openapi.sh` wrapper.
- **Deps:** BE-02. **Modules:** Api/Controllers, Application/*/Dtos.
- **AC:** `contracts/openapi.json` contains all endpoints in `API_CONTRACT.md` §3–8 with enums as strings and `version`/money amount as strings; script is deterministic (re-run gives identical file).
- **Tests:** a test that the generated document equals the committed file (drift check).
- **Parallel:** BE-06/BE-07.

### BE-04 — Integration test harness · `Done`
- **Objective:** everything in `TESTING.md` §2 for the backend.
- **Deps:** BE-02. **AC:** a sample test spins PostgreSQL via Testcontainers, applies migrations, hits `/api/health`; Respawn reset works between tests; suite runs in < 30 s locally after image pull.
- **Parallel:** BE-06.

### BE-05 — Identity, cookie auth, antiforgery, bootstrap admin · `Done`
- **Objective:** `AUTHORIZATION.md` §1–2 and §6 (policies, must-change-password middleware); `POST /auth/login`, `/logout`, `GET /auth/me` (with capabilities), `POST /auth/change-password`.
- **Deps:** BE-03, BE-04. **Modules:** Api/Auth, Infrastructure/Identity, Application/Organization (capabilities).
- **AC:** 401 JSON (no redirects); lockout 5/15 min; XSRF cookie issued + enforced on unsafe methods; bootstrap admin created once from config; roles seeded.
- **Tests:** login success/failure/lockout; 401 shape; XSRF missing → rejected; must-change-password gate; capabilities per role combo (incl. derived `isManager`→`tasks.view`).

### BE-06 — Domain core: Request aggregate & fixed processes · `Done`
- **Objective:** everything in `DOMAIN_MODEL.md` §2–5 as pure C# (no EF attributes).
- **Deps:** BE-01. **Modules:** FlowOps.Domain only.
- **AC:** all invariants I2–I9 enforced in the aggregate; no public setters on state; `FixedProcesses` is the only place step order exists.
- **Tests:** all of `TESTING.md` §3.1.
- **Parallel:** BE-02…BE-05 (no shared files).

### BE-07 — Domain persistence mapping · `Done`
- **Objective:** EF configurations for Request/details/WorkTask/Activity/Notification (Department and the user profile columns landed early in BE-05, which `/api/auth/me` needed); `xmin` tokens; constraints and indexes from `DOMAIN_MODEL.md` §6; request-number sequence; migration.
- **Deps:** BE-02, BE-06. **AC:** migration applies cleanly; partial unique index and check constraints exist (assert via `information_schema`/`pg_indexes` in a test).
- **Tests:** round-trip persistence of a full purchase aggregate; inserting a second open task fails at DB level.

## Organization

### BE-08 — Departments & user administration API · `Done`
- **Objective:** `API_CONTRACT.md` §8 (departments, users, roles, reset password) via `DepartmentService`, `UserAdministrationService`, `ManagerAssignmentRules`.
- **Deps:** BE-05, BE-07. **AC:** cycle/self-manager prevention (I13); last-admin protection; duplicate email/department → 409; inactive department can't receive members.
- **Tests:** each error code; non-admin → 403; paging/search.

### BE-08b — Admin self-authority (follow-up) · `Done`
- **Objective:** an admin can't change their own roles or manager link, make themselves anyone's manager (incl. by reactivating a report) or deactivate themselves → 422 `user.self_administration`; resetting another user's password stays allowed and is audit-logged with the actor id. #24, #27. Self password reset via the admin endpoint → 422 too (BE-08c, #31). #32 → ADR-0012 (accepted), implemented by BE-08d.

### BE-08d — Role-change rules (ADR-0012) · `Done`
- **Objective:** `Administrator` can't be added by `PUT /users/{id}/roles` (422 `user.admin_role_on_create_only`); an admin's own role change becomes a pending `RoleChange` (202) that another active admin approves, rejects or the proposer cancels; 72 h expiry; `RoleChangePending` notifications. ADR-0012 §1–7.
- **Deps:** BE-08. (Notifications are written with the existing `Notification` entity; users read them once BE-13 lands.) **AC:** approver checked against the database; stale and expired changes refused; one `SaveChangesAsync` per use case; contract and openapi updated.
- **Tests:** each new error code; self-approval; only-admin refusal; stale after a direct change; expiry with `FakeTimeProvider`; approve/cancel race (≥ 20 runs); notifications.

## Requests and process

### BE-09 — Access rules & task eligibility · `Done`
- **Objective:** `AccessRules.VisibleTo` and `TaskEligibility` (`AUTHORIZATION.md` §4) as the single implementation.
- **Deps:** BE-07. **AC:** translates to one SQL query (no client evaluation); used by no code yet except tests.
- **Tests:** table-driven integration tests over a seeded org for R1/R2/R3 and T1/T2 incl. self-decision and inactive users.

### BE-10 — Submit purchase & leave · `Done`
- **Objective:** `RequestSubmissionService`, `TaskAssigneeResolver`, `NotificationFactory` (TaskAssigned), endpoints `POST /requests/purchase|leave`.
- **Deps:** BE-08, BE-09. **AC:** server-side requester/department; 422s for missing manager/department; one transaction; returns `RequestDetailsDto` 201 with `Location`.
- **Tests:** `TESTING.md` §3.2 submission rows; validation limits (boundary values); company-TZ "today".
- **Follow-up BE-10b (`Done`):** leave days counted inclusively, `days = (end − start) + 1`, max `RequestLimits.LeaveMaxDays` = 90; tests at 89/90/91 days and same-day = 1 (domain and API).

### BE-11 — Request queries & comments · `Done`
- **Objective:** `GET /requests`, `/requests/{id}`, `/activity`, `POST /comments`; `availableActions` via `TaskEligibility`; leave-reason masking.
- **Deps:** BE-10. **AC:** projections only (no entity graphs to DTO in memory); filters/search/paging; 404 for invisible.
- **Tests:** `TESTING.md` §3.3 visibility rows; reason masking; comment notifications.
- **Follow-up BE-11b (`Done`, security review):** HR reads a leave reason only through the HR task (a former manager who also holds HrOfficer doesn't); `page` capped at 10,000 (was a 500 on overflow); extra negative tests. Activity returns the newest 500 entries and comments are rate-limited per user (BE-11c, #37). Open: #38 (frontend hint).

### BE-12 — Inbox & decisions (approve/reject/fulfill) · `Done`
- **Objective:** `TaskQueryService` (`GET /tasks`), `TaskDecisionService` (one pipeline, `CODE_STANDARDS.md` §2.4), concurrency mapping, decision notifications.
- **Deps:** BE-11. **AC:** `WORKFLOWS.md` §2–5 behaviour exactly.
- **Tests:** `TESTING.md` §3.2 full paths, §3.3 decision rows, §3.4 all concurrency tests.
- **Contract:** `InboxItemDto.actions` is in the contract (DTO, openapi, API_CONTRACT §5). `GET /tasks` must fill it from the same eligibility source as `availableActions.actions` (`[]` for Done items), with a test.
- **Note:** highest-risk task — request an `architecture-reviewer` and `security-reviewer` pass before merging.

### BE-13 — Notifications API · `Done`
- **Objective:** `API_CONTRACT.md` §6. **Deps:** BE-12. **Tests:** own-only access; read-all; unread count.

### BE-14 — Real-time hub & post-commit publishing · `Done`
- **Objective:** `/hubs/notifications`, user groups, `SignalRRealtimePublisher`; services publish after `SaveChangesAsync`.
- **Deps:** BE-13. **AC:** events and recipients per `API_CONTRACT.md` §9; publisher failures are logged, never fail the request.
- **Tests:** `TESTING.md` §3.5; one hub connection test with cookie auth.

### BE-15 — Dashboard & stalled tasks · `Done`
- **Objective:** `GET /dashboard`, `GET /admin/stalled-tasks`. **Deps:** BE-12.
- **AC:** dashboard is ≤ 4 SQL round-trips; capability-gated sections are `null`.
- **Tests:** per-capability shapes; stalled reasons both detected.

### BE-16 — Development seed data · `Todo`
- **Objective:** `--seed-demo` option creating a demo company (3 departments, ~10 users across all roles, sample requests in every state). Dev only.
- **Deps:** BE-12. **AC:** idempotent; documented credentials in README; never runs in Production.

## Frontend

### FE-01 — Typed API client & problem handling · `Done`
- **Objective:** `openapi-typescript` generation from `contracts/openapi.json` (`npm run api:types`), `lib/api/client.ts` (`openapi-fetch`, credentials, XSRF header, `ApiError`), `problem.ts` (code→message), `queryKeys.ts`, QueryClient defaults, MSW setup with typed handlers.
- **Deps:** BE-03. **AC:** no hand-written API types (replace the local unions in `lib/format/status.ts` with generated ones); the existing oxlint rule forbidding `fetch` outside `lib/api` stays green; add a 4xx-never-retry rule to `lib/queryClient.ts`.
- **Tests:** client attaches XSRF on unsafe methods only; ApiError parsing; `toUserMessage` for every known code.

### FE-02 — Form primitives · `Done`
- **Objective:** `Input`, `Textarea`, `Select`, `DateInput`, `MoneyInput`, `FormField`, `Card`, `PageHeader` per `DESIGN_SYSTEM.md` §5; react-hook-form + zod wiring helper.
- **Deps:** FE-00. **Tests:** label/error association (`aria-describedby`, `aria-invalid`); MoneyInput 3-decimal handling.
- **Parallel:** FE-05, FE-06, any backend task.

### FE-03 — Session, login, change password, guards, 401/403 · `Done`
- **Objective:** `UI_SPEC.md` §2, §4.1, §4.9; `useSession`, `useCapability`, `RequireCapability`.
- **Deps:** FE-01, FE-02. **Tests:** `TESTING.md` §3.6 session rows.

### FE-04 — App shell & capability navigation · `Done`
- **Objective:** real `AppShell`, `Sidebar` (+ drawer < 1024 px), `TopBar` (bell placeholder, user menu, logout), skip link.
- **Deps:** FE-03. **Tests:** nav items per capability set; keyboard navigation of drawer.

### FE-05 — Data display components · `Done`
- **Objective:** `DataTable` (loading/empty/error, stacked < 640 px), `Pagination`, `FilterBar`, `useUrlState`, `EmptyState`, `ErrorState`, `Skeleton`, `lib/format/{date,money}`.
- **Deps:** FE-00. **Tests:** states; URL round trip; money/date formatting in Asia/Amman.

### FE-06 — Dialogs & toasts · `Done`
- **Objective:** `Dialog`, `ConfirmDialog`, `Toast` provider (accessible, focus management).
- **Deps:** FE-00. **Tests:** focus trap/return, Esc, roles for toasts.

### FE-07 — New request flow · `Done` (real API since BE-10; checked in the browser 2026-11-06)
- **Objective:** `UI_SPEC.md` §4.4. **Deps:** FE-04, FE-02, FE-06 (+ BE-10 for real integration).
- **Tests:** `TESTING.md` §3.6 form rows.

### FE-08 — My requests & Team requests · `Done` (real API since BE-11; checked in the browser 2026-11-06)
- **Objective:** `UI_SPEC.md` §4.3. **Deps:** FE-04, FE-05 (+ BE-11).

### FE-09 — Request details · `Done` (details, activity and comments on the real API since BE-11; decisions wait for BE-12)
- **Objective:** `UI_SPEC.md` §4.5 incl. `ActivityTimeline`, comment box, `DecisionDialog` + `useDecideTask`.
- **Deps:** FE-08, FE-06 (+ BE-11/12). **Tests:** actions only from `availableActions`; 409 path; restricted reason display.

### FE-10 — Inbox · `Done` (against MSW mocks; real integration waits for BE-12)
- **Objective:** `UI_SPEC.md` §4.6 reusing `DecisionDialog`/`useDecideTask` from FE-09. **Deps:** FE-09 (+ BE-12).
- **Notes:** inline actions come from `InboxItemDto.actions` (contract since #29). Leave decision dialogs carry the visibility note (#38).

### FE-11 — Notifications & realtime · `Done` (notifications on the real API, checked in the browser 2026-11-07; the SignalR client is built against the contract but the hub isn't on `main` until BE-14, see #53)
- **Objective:** `NotificationBell`, notifications page, `lib/realtime` (SignalR client → query invalidation per `API_CONTRACT.md` §9, reconnect handling).
- **Deps:** FE-04 (+ BE-13/14). **Tests:** invalidation mapping per event; optimistic mark-read rollback on error.
- **Notes:** role-change notifications (no request/task id) open `/admin/role-changes` (the screen itself is FE-13b). The realtime client retries a missing hub quietly and is off in unit tests and while the dev API is mocked. Verify live updates once BE-14 is merged.

### FE-12 — Dashboard · `Done` (against MSW mocks; real integration waits for BE-15)
- **Objective:** `UI_SPEC.md` §4.2. **Deps:** FE-05, FE-08 (+ BE-15).

### FE-13 — Administration screens · `Done` (users and departments on the real API, checked in the browser 2026-11-06; stalled tasks stay on MSW until BE-15)
- **Objective:** `UI_SPEC.md` §4.8. **Deps:** FE-04, FE-05, FE-06 (+ BE-08, BE-15 for stalled).
- **Notes:** on the admin's own record roles, manager and Active are read-only with an explanation, and the admin is not offered as a manager (see #27 for the open question on self-deactivation and the error code name).

### FE-13b — Role-change screens (ADR-0012) · `Todo`
- **Objective:** ADR-0012 §8: the edit-roles form can't add `Administrator`; "Waiting for another administrator" after a 202 on your own roles; a pending/decided "Role changes" list with approve, reject and cancel from `availableActions`. **Deps:** FE-13, BE-08d (contract).

## Operations & quality

### OPS-01 — Full-stack Docker Compose · `Todo`
Add `api` (multi-stage Dockerfile) and `web` (build → nginx serving SPA, proxying `/api` and `/hubs`) services; one command brings up the app. **Deps:** BE-05, FE-04.

### OPS-02 — CI pipeline · `Todo`
GitHub Actions: backend build + domain tests + integration tests (Docker available on ubuntu runners), OpenAPI drift check, frontend typecheck/lint/test/build. **Deps:** BE-04, BE-03.

### QA-01 — Security review pass · `Todo` — `security-reviewer` agent over auth, access rules, decisions, admin. **Deps:** BE-12, BE-08, BE-08d.
### QA-02 — Architecture review pass · `Todo` — `architecture-reviewer` agent over whole backend + frontend structure. **Deps:** BE-15, FE-10.
### QA-03 — UI/UX review pass · `Todo` — `review-ui` skill over all pages at 360/768/1280 px. **Deps:** FE-13.
### QA-04 — E2E smoke (optional) · `Todo` — Playwright: purchase happy path across 4 users; leave rejection. **Deps:** OPS-01.

---

## First tasks to start
- **Backend:** BE-01 (then BE-06 in parallel with BE-02).
- **Frontend:** FE-02 (form primitives) — independent of the backend; FE-01 waits on BE-03.
