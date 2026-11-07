# Testing — FlowOps

**Purpose:** what we test, at which level, with which tools, how to run it, and what "tested" means.
Rules for agents: `.claude/rules/testing.md`.

---

## 1. Strategy

| Level | Project / location | Tools | What it proves | Speed |
|---|---|---|---|---|
| Domain unit | `tests/backend/FlowOps.Domain.Tests` | xUnit v3, Shouldly (assertions) | Invariants and state transitions, in isolation | ms |
| API integration | `tests/backend/FlowOps.Api.IntegrationTests` | xUnit v3, `WebApplicationFactory`, Testcontainers.PostgreSql, Respawn | Real HTTP → real auth → real SQL: authorization, persistence, concurrency, contracts | seconds |
| Architecture | same integration project, `Architecture/` | reflection / NetArchTest | Layer dependencies (ARCHITECTURE.md §3), no controller uses DbContext | ms |
| Frontend unit/component | colocated `*.test.ts(x)` | Vitest, Testing Library, user-event, jest-dom, MSW | Components, hooks, forms, guards, error handling | ms |
| E2E smoke (optional, end of Phase 1) | `e2e/` | Playwright | Two happy paths in a real browser | minutes |

**No mocked-DbContext service tests.** Application behaviour is tested through the API against PostgreSQL; that is where authorization, SQL translation of `AccessRules`, constraints and `xmin` actually live. Mocks are used only for `TimeProvider` (`FakeTimeProvider`) and the realtime publisher (recording fake).

Why Shouldly rather than FluentAssertions: FluentAssertions v8 moved to a commercial licence. Plain `Assert` is also acceptable.

## 2. Test infrastructure (built once, in BE-04)

All of it lives in `tests/backend/FlowOps.Api.IntegrationTests/Harness/`.

- `FlowOpsApiFactory : WebApplicationFactory<Program>` — one PostgreSQL Testcontainer (`postgres:17-alpine`, same as `docker-compose.yml`) per test *collection*; migrations applied once; **Respawn** resets data between tests (keeps `roles` and `__EFMigrationsHistory`). `CreateHttpsClient()` gives an HTTPS client that never follows redirects (so a 302 can't hide behind a 200).
- Database tests derive from `DatabaseTest(factory)`: it puts the class in the shared `Database` collection (classes in it run sequentially) and resets data + clock before each test. Tests that need no database (contract, error pipeline, architecture) keep their own lightweight factories and run in parallel.
- `TestClock` registered as `TimeProvider`: a resettable wrapper over `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) — a fake can't move backwards, so each reset swaps in a fresh one at `TestClock.Start` (Mon 2026-03-02 09:00 Asia/Amman). Tests move time with `Factory.Clock.Advance(...)`.
- `Factory.Realtime` (`RealtimeRecorder`, fed by `RecordingRealtimePublisher`) records every hint the app publishes and forwards it to the same host's real SignalR publisher, so hub tests still receive it; `FailPublishing = true` simulates a hub failure. `Factory.SaveFailures.FailNextSave()` makes the next `SaveChanges` throw after the domain call (§3.5). Both reset with the database. The test host sets `FlowOps:Realtime:CloseOnAuthenticationExpiration=false`: cookies are issued by the fake clock, but SignalR checks their expiry against real time and would close every hub connection.
- **Builders** (`Given.User("lina").InDepartment(sales).ManagedBy(karim).WithRoles(Employee)`), **authenticated clients** (`await factory.ClientFor(lina)` logs in through the real login endpoint and handles the XSRF header). No test talks to `DbContext` to *arrange* state that an API call could create, except bulk org setup. Today: `Given.User(name)` with `WithRoles`/`ManagedBy`/`InDepartment`/`MustChangePassword`/`Inactive`, `Given.DepartmentAsync`, and `factory.ClientFor(user)`; they write through `UserManager`/`AppDbContext` as bulk org setup. Since BE-08 the admin API exists too: tests about administration arrange through it (`Organization/*Tests`); other tests keep the builders, so they don't need an admin session.
- Database-free factories (`ContractApiFactory`, `ErrorPipelineFactory`) skip real sign-in: the contract factory uses a header-driven test scheme and accepts any XSRF token, so it isolates routes, policies and validation. Real cookies and XSRF are tested in `Auth/`.
- `ProblemAssert.HasCode(response, 409, "task.already_decided")` — asserts status, `application/problem+json` and `code`; returns the body for further checks.

Frontend: `src/test/setup.ts` (jest-dom, MSW server), `renderWithProviders()` (QueryClient with retries off + MemoryRouter + session), MSW handlers typed from `schema.d.ts`.

## 3. Required scenarios (Phase 1 Definition of Done)

### 3.1 Domain (unit)
- Purchase: submit → manager approve → finance approve → status `Approved`, stage `ProcurementFulfillment` → fulfill → `Completed`.
- Leave: submit → manager approve → HR approve → `Completed`.
- Reject at each approval step → `Rejected`/`Closed`, no next task, `ClosedAt` set.
- Reject without reason → error.
- Decide a Done task → `task.already_decided`.
- Decide any task on a closed request → `request.closed`.
- Decide a non-current task → `task.not_current`.
- `reject` on fulfillment / `fulfill` on approval → `task.invalid_action`.
- Requester deciding own task → `task.self_decision`.
- Every transition appends exactly the expected activities with actor/time.
- `Money`: rejects ≤ 0, > max, > 3 decimals, unsupported currency.
- Leave dates: end < start, > 90 days, start in past (using company time zone boundary at 00:00 Asia/Amman).

### 3.2 Workflows through the API (integration)
- Both happy paths end to end via HTTP, asserting status/stage after every step, tasks visible in the right inboxes, and notifications created for the right recipients (and **not** for the requester's role-queue membership).
- Submit with no manager → 422 `request.manager_not_assigned`; nothing persisted.
- Server ignores client-supplied `department`, `requesterId`.
- Role queue with only the requester as member → task appears in admin stalled list; adding a second officer makes it decidable.
- Comments: visible participants can comment; closed requests accept comments.

### 3.3 Authorization (integration) — one test per row/cell that matters
- Employee cannot GET another employee's request → **404**.
- Manager can GET direct report's request; cannot GET a non-report's → 404.
- Finance cannot see leave requests; HR cannot see purchase requests → 404.
- Procurement cannot see purchase still at finance stage → 404.
- Non-assignee manager approving → 403/404 as per AUTHORIZATION.md §4.
- Finance officer approving own purchase at finance stage → 403 `task.self_decision`.
- Admin (without other roles) cannot see request details or decide tasks → 404.
- Admin endpoints with non-admin → 403; unauthenticated anything → 401 (never 302).
- Unsafe request without XSRF header → 400/403 (antiforgery failure).
- Leave `reason`: the requester, direct manager and HR receive it; a **second-level manager** (manager's manager) gets 404 for the request (not a direct report), and any viewer outside R1/R2/HR receives `reason: null, reasonHidden: true`.
- `mustChangePassword` user gets 403 `auth.password_change_required` on non-auth endpoints.
- Cannot remove last Administrator; cannot create manager cycle.

### 3.4 Concurrency (integration)
- Two finance officers approve the same task concurrently (`Task.WhenAll` on two clients, repeated 20×): exactly one 204, the other 409; exactly one procurement task exists; activities contain exactly one `Approved` for that step.
- Approve with a stale `version` → 409 `concurrency.conflict` and no state change.
- Approve and reject the same task concurrently → one wins; request state matches the winner.

### 3.5 Notifications & realtime (integration)
- Notification rows exist only if the transaction committed (force a failure after domain call → no rows).
- Publisher called after commit with the expected events; not called on failure.
- Read/unread endpoints only touch the caller's notifications.

### 3.6 Frontend
- Session: 401 → redirect to login with `returnTo`; `mustChangePassword` → forced route.
- Navigation renders items per capability set.
- Purchase and leave forms: client validation messages, server 400 mapping onto fields, 422 banner, success navigation.
- DecisionDialog: reason required for reject; 409 shows mapped message and invalidates queries.
- DataTable/Pagination: loading, empty, error states; URL state round-trips.
- StatusBadge renders label + tone for every status value (exhaustive).
- Token lint (`npm run lint:tokens`) passes — no raw colours.

## 4. Commands

```bash
# Backend (from repo root)
dotnet test tests/backend/FlowOps.Domain.Tests              # fast, no Docker
dotnet test tests/backend/FlowOps.Api.IntegrationTests      # needs Docker running
dotnet test                                                 # everything

# Frontend (from src/frontend)
npm run typecheck
npm run lint            # oxlint (incl. no-fetch-outside-lib/api, no-danger, no-any) + design-token check
npm test                # Vitest (run mode)
npm run build
```

CI (OPS-02) runs all of the above plus the OpenAPI drift check on every PR.

## 5. Quality bar
- Every bug fix starts with a failing test that reproduces it.
- Test names state behaviour: `Approve_WhenTaskAlreadyDecided_Returns409`.
- No `Thread.Sleep`/arbitrary waits; use `FakeTimeProvider` and awaited calls.
- No coverage percentage target; the scenario lists above are the target. Reviewers check that changed behaviour has tests for success **and** failure paths.

## 6. Honest reporting
Agents report test results exactly as observed: the command run, pass/fail counts, and any skipped tests. "Should pass" or "tests written but not run" must be stated as such. If Docker is unavailable, say integration tests were not run.
