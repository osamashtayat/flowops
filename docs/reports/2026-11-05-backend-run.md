# Backend run — 2026-11-05

Autonomous session: BE-04 → BE-05 → BE-07, each on its own branch from the latest `main`, merged by squash after build, full tests (real PostgreSQL via Testcontainers) and `/verify-feature`; BE-05 also had a `security-reviewer` pass. All three are merged; `docs/PHASE1_TASKS.md` marks them `Done`.

## Needs your decision

1. **Login lockout reveals which accounts exist (security review M2, open).** Only real, active accounts can ever reach `423 auth.locked_out`, so six wrong attempts per email separate existing accounts from unknown ones. The same lockout lets anyone who knows an email (e.g. the only admin) keep that account locked out, because there is no per-IP or global rate limit. Options:
   - (a) add ASP.NET Core rate limiting on `POST /api/auth/login` (no contract change; reduces both issues, doesn't remove the 423 signal);
   - (b) also track failures for unknown emails, so they lock out too;
   - (c) answer 401 instead of 423 while locked out — a **contract change** (API_CONTRACT §2/§3 list 423) and a UX change (the user isn't told they're locked).
   
   Recommendation: (a) now, and decide (c) together with the frontend login screen (FE-03).
2. **Explicit transaction in the bootstrap-admin seeder.** `.claude/rules/database.md` says "no explicit transactions … flag if you think you need one". Creating the user and assigning the Administrator role are two Identity saves; one transaction keeps a half-created admin from surviving a crash. Please confirm this exception, or say if you prefer a compensating delete.
3. **Logout ends all of the user's sessions.** To make logout revoke the session on the server (AUTHORIZATION.md §1.1), it rotates the security stamp. That also signs the user out on other devices within a minute. Keep it this way, or switch to an absolute session lifetime and leave other devices signed in.
4. **Session claims can be up to 1 minute out of date.** Role, manager, must-change and deactivation changes reach existing sessions at the next security-stamp check, every minute. During that window `/api/auth/me` (which reads the database) and the coarse policies (which read the cookie) can disagree. Resource checks always read the database. A shorter interval costs a few queries per request.

## BE-04 — Integration test harness

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/5 (merged)
- **Tests (at merge):** `dotnet test tests/backend/FlowOps.Domain.Tests` 84 passed / 0 failed; `dotnet test tests/backend/FlowOps.Api.IntegrationTests` 175 passed / 0 failed (whole integration run ~5 s wall after the image was pulled; AC < 30 s).
- **Review findings:** `/verify-feature` → meets spec with minor issues: `RecordingRealtimePublisher` waits for the `IRealtimePublisher` port (BE-14); `ClientFor`/builders were delivered in BE-05.
- **Decisions:** `TestClock` is a resettable wrapper over `FakeTimeProvider` (a fake can't move backwards), so every test starts at Mon 2026-03-02 09:00 Asia/Amman. The container image is `postgres:17-alpine`, the same as `docker-compose.yml`. Database tests share one `Database` collection and run sequentially; tests that need no database keep their own factories and run in parallel.
- **Contract changes:** none.

## BE-05 — Identity, cookie auth, antiforgery, bootstrap admin, auth endpoints

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/6 (merged)
- **Tests (at merge):** Domain 84 passed / 0 failed; Integration 224 passed / 0 failed.
- **Review findings:**
  - `/verify-feature` → meets spec with minor issues (manager-not-self constraint untested; its schema test landed in BE-07).
  - `security-reviewer`: no Critical/High.
  - **Fixed:** M1 timing parity for inactive or locked accounts; M3 logout didn't revoke the session server-side; L4 change-password guesses didn't count towards lockout; L5 "changing" to the same password; L6 stale comment, with claim staleness now documented; L7 test that every unsafe endpoint is an MVC action and only login skips antiforgery.
  - **Remaining:** M2 (see "Needs your decision" #1). Low/operational: two instances starting at the same moment could both try to create the bootstrap admin; the unique email makes one of them fail to start.
  - **Untested suggestions from the review:** a test that losing the Administrator role or the last report takes effect after re-validation. That behaviour follows from the claims rebuild, which the deactivation and other-session tests already exercise.
- **Decisions:**
  - Department table and user profile columns were mapped here, not in BE-07, because `/api/auth/me` returns department and manager.
  - Coarse policies (`Admin`, `CanSubmit`, `TasksView`) are capabilities from `CapabilityRules`, the single home shared with `/me`. `isManager` is a claim rebuilt at each security-stamp re-validation (every 1 minute).
  - XSRF failure → 403 `auth.forbidden`. TESTING.md allowed 400 or 403; 403 needs no new code. The check runs after authentication, so anonymous calls still get 401.
  - The session cookie is non-persistent (no `Expires`) and expires after 8 hours idle. There is no separate absolute cap.
  - Passwords: Identity's default complexity plus a 12-character minimum (the literal reading of "Identity defaults, min length 12"). Identity measures lockout with the system clock, not `TimeProvider`, so that test checks the stored lockout end.
  - Roles are seeded by migration with fixed ids, not at start-up. The bootstrap admin is created by a hosted service: once, only if no Administrator exists, with `MustChangePassword = true`. Without config it logs a warning and doesn't touch the database. Bad config stops start-up.
  - Logout rotates the security stamp (see #3). Change-password counts wrong current passwords towards lockout and rejects reusing the same password.
  - `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'` is sent outside Development (AUTHORIZATION.md §1.5).
  - Application now references `Microsoft.EntityFrameworkCore` (abstractions; allowed by ARCHITECTURE §3), and Infrastructure has a `FrameworkReference` to ASP.NET Core (`SignInManager`, hosted service).
  - New port: `IIdentitySignIn` (Application) with its Infrastructure implementation.
- **Contract changes:** none. The generated `contracts/openapi.json` is unchanged; the auth endpoints already had the final shapes from BE-03.

## BE-07 — Domain persistence mapping

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/7 (merged)
- **Tests (at merge):** Domain 92 passed / 0 failed; Integration 238 passed / 0 failed.
- **Review findings:** `/verify-feature` → meets spec with minor issues; the missing `Notification` round-trip test was added before merge. Nothing left open.
- **Decisions:**
  - The currency check is generated from `SupportedCurrencies.All`, and enum checks from the C# enums (`CheckConstraints`), so new values flow into the next migration.
  - Extra checks beyond DOMAIN_MODEL §6:
    - decision fields are set exactly when a task is Done;
    - the assignee role is a known role;
    - 0 < amount ≤ 1,000,000.000;
    - leave end ≥ start;
    - the purchase and leave details tables are keyed by `request_id`.
  - Only requests cascade to their tasks, activities and details; everything else is RESTRICT. `work_tasks.assignee_user_id` has only the open-task partial index (users are never deleted).
  - Domain types got private parameterless constructors for EF materialization (no EF attributes), plus `RequestNumber.Parse`. All of this is recorded under DOMAIN_MODEL §6 "As built".
- **Contract changes:** none.

## Not done / not verified

- The new migrations (`AuthAndOrganization`, `AddRequestsTasksActivitiesNotifications`) were applied to a fresh container on every test run but **not to your local dev database**. Run `dotnet ef database update --project src/backend/FlowOps.Infrastructure --startup-project src/backend/FlowOps.Api`. `AuthAndOrganization` inserts the five roles with fixed ids, so it will conflict if roles were created by hand in that database.
- There is no CI yet (OPS-02), so the PRs had no remote checks. Every result above comes from local runs against Docker 29.7.2.
- To try sign-in locally, set the bootstrap admin with `dotnet user-secrets` (README "Getting started").
