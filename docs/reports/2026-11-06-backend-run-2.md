# Backend run 2 — 2026-11-06

Autonomous session in its own worktree, in this order:
- the decisions on the 2026-11-05 report (BE-05 follow-up), then BE-08 → BE-09 → BE-10 → BE-11;
- each task on its own branch from the latest `main`, merged by squash after a full build, all tests on real PostgreSQL (Testcontainers) and `/verify-feature`;
- BE-08 and BE-09 also had `security-reviewer` and `architecture-reviewer` passes, and every Critical/High finding was fixed before merge.

The run stopped at BE-12 as instructed. `docs/PHASE1_TASKS.md` marks BE-08 to BE-11 `Done`.

## Needs your decision

1. **Can administrators give themselves process authority? (BE-08 security review M1, open, in AGENT_HANDOFF §6.)** Admin powers alone let an admin:
   - grant themselves `FinanceOfficer` or `HrOfficer`;
   - make themselves someone's manager, then read that person's leave reasons and decide their requests;
   - reset a user's password and sign in as them.

   This goes against the spirit of "Administrator grants no process authority" (AUTHORIZATION §2). Every admin change is now logged with the actor's id; nothing is blocked. FE-13 (admin screens) needs the answer. Options:
   - **(a)** forbid an admin changing their own roles or manager link, and assigning themselves as anyone's manager (new 422 code);
   - **(b)** (a), plus forbid giving process roles to accounts that hold Administrator;
   - **(c)** accept it and rely on the audit log.

   Recommendation: (a). It is small, testable and closes the quiet path. Password-reset impersonation remains, and only an audit trail or a second approver can address it.
2. **Accepted check-then-act races (please confirm).** Two rules are checked before acting, with no lock:
   - two simultaneous demotions can leave no active administrator;
   - two simultaneous manager changes ("A reports to B" and "B reports to A") can create a cycle.

   Closing them needs a lock (`pg_advisory_xact_lock`), and `.claude/rules/database.md` requires an ADR for that. Both races are documented in API_CONTRACT §8 "As built".
3. **Login rate-limit numbers.**
   - Defaults are 30 attempts per minute per IP and 10 per 15 minutes per email, configurable under `FlowOps:LoginRateLimit`.
   - Limits are in memory per instance.
   - Behind a reverse proxy, every client shares the proxy's IP until forwarded headers are configured (OPS-01).
   - Anyone who knows an email can still trigger that account's 15-minute lockout with 5 wrong passwords. The limits slow this down but can't prevent it.

   Tell me if the office NAT needs a higher per-IP limit.

## BE-05 follow-up — decisions from the 2026-11-05 report

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/12 (merged)
- **Tests (at merge):** Domain 92 passed / 0 failed; Integration 245 passed / 0 failed.
- **Changes:**
  1. **Login lockout.**
     - ASP.NET Core rate limiting on `POST /api/auth/login`: per IP in the middleware, per email in an action filter, counting unknown emails too. Over a limit → `429 auth.rate_limited` + `Retry-After`.
     - A locked-out account answers `401 auth.invalid_credentials` with the same message as a wrong password.
     - `auth.locked_out` and 423 are removed from the code, API_CONTRACT §2/§3 and AUTHORIZATION.md, and `openapi.json` is regenerated.
     - AGENT_HANDOFF §6 entry for the frontend; the frontend then did it in #14 and removed the entry.
  2. **Bootstrap-admin transaction** recorded as the allowed exception in `.claude/rules/database.md`.
  3. **Logout signs out all devices:** documented as a decision in AUTHORIZATION §1.2.
  4. **1-minute claim staleness:** documented in AUTHORIZATION §6.
  5. **`.env.example`, logging and README.**
     - `.env.example` quotes `ConnectionStrings__FlowOps`.
     - EF command logging is at `Warning` in `appsettings.Development.json`.
     - README "Getting started": ASP.NET doesn't read `.env` (use user-secrets, or export the file); `dotnet tool restore` is added; it says you sign in with the `FlowOps__BootstrapAdmin__Email/Password` values.
- **Found on the way:** the integration-test host read the developer's `dotnet user-secrets`, so a configured bootstrap admin tried to seed before migrations and 57 tests failed on this machine. `FlowOpsApiFactory` now blanks those keys.
- **Contract changes:** 423 removed, 429 `auth.rate_limited` added on login.

## BE-08 — Departments & user administration API

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/16 (merged)
- **Tests (at merge):** Domain 95 passed / 0 failed; Integration 301 passed / 0 failed.
- **Review findings:**
  - `/verify-feature`: meets spec.
  - `architecture-reviewer`:
    - **High, fixed:** user creation skipped Identity's user validators, so `o'neil@…` became an account that could never change its password. Test: `Create_EmailIdentityRejectsAsUserName_Returns400OnEmail`.
    - **Mediums and Lows fixed:**
      - index names are constants;
      - last-write-wins is documented;
      - `CancellationToken` on the port;
      - `CapabilityRules.CanAdminister`.
  - `security-reviewer`: no Critical or High.
    - **Fixed:**
      - M2: integer enums such as `roles: [99]` gave a 500; enums are now strings only.
      - L1: a missing `isActive` deactivated the user; it is now required.
      - L2: concurrent role edits gave a 500; they now give 409.
      - L4: deactivation now rotates the security stamp.
    - **Open:** M1 (Needs your decision #1); L3 (Needs your decision #2).
- **Decisions:**
  - **`IIdentityAdmin` port.** Planned in ARCHITECTURE §4, it stages Identity writes on the shared `AppDbContext`, so each use case still calls `SaveChangesAsync` once. It uses Identity's normalizer, validators and hasher.
  - **Duplicates are judged by the database.** Unique-index violations become 409 by index name in `AppDbContext`, so there is no check-then-insert race and Npgsql stays out of Application.
  - **Departments.** `includeInactive` is honoured for admins only. `memberCount` and `directReportCount` count active users.
  - **Validation.** An unknown department or an unknown/inactive manager gives 400 on the field; an inactive department gives 422.
  - **Password reset** clears lockout and ends the user's sessions.
  - **New shared code:** `Paging.ToPagedResultAsync` (the CODE_STANDARDS §2.3 home) and `BusinessRuleException` (422).
- **Contract changes:**
  - 409 `concurrency.conflict` on user `PUT`, `/roles` and `/reset-password`;
  - `isActive` required on user and department `PUT`;
  - enums must be strings;
  - the unreachable 422 `user.manager_cycle` is dropped on `POST /users`.

  All are documented under "As built" in API_CONTRACT §8.

## BE-09 — Access rules & task eligibility

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/20 (merged)
- **Tests (at merge):** Domain 97 passed / 0 failed; Integration 324 passed / 0 failed.
- **Review findings:**
  - `/verify-feature`: meets spec.
  - `architecture-reviewer`:
    - **High, fixed:** `AvailableActions` needed a loaded aggregate, which conflicts with BE-11's "projections only". It now takes `(requesterId, openTask, me)`.
    - **Mediums fixed:**
      - Access→Requests folder cycle: the DTOs moved to `Access`;
      - inbox shape: `DecidableBy` returns `{ Request, Task }`;
      - per-call expression compile;
      - inconsistent stage check;
      - stale AUTHORIZATION text.
  - `security-reviewer`: no Critical or High.
    - **Medium, fixed:** `AccessContext` could be built from stale claims or a client id. It is now internal, built from the database for `ICurrentUser` only.
    - **Lows fixed:** visibility-before-eligibility documented, and commands load through `VisibleTo`; "Administrator is never a role queue" enforced in the domain, with a test; doc contradictions.
    - All of its missing negative tests were added.
- **Decisions:**
  - Resource checks read roles and the active flag from the database, never claims, so they are not affected by the 1-minute claim staleness.
  - An inactive user sees nothing.
  - Eligibility order: not assigned or inactive → `task.not_eligible`; then the requester → `task.self_decision`.
  - Open, current and already-decided stay the domain's 409s.
- **Contract changes:** none.

## BE-10 — Submit purchase & leave

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/21 (merged)
- **Tests (at merge):** Domain 97 passed / 0 failed; Integration 357 passed / 0 failed.
- **Review findings:** `/verify-feature` → meets spec with minor issues:
  - no realtime push (the `IRealtimePublisher` port is BE-14);
  - the full HTTP happy paths need BE-12's decision endpoints.
- **Decisions:**
  - Check order: input (400) → department (422) → manager (422). The request number is taken last, so invalid input doesn't burn numbers.
  - **The 90-day leave limit counts both ends,** as the frontend run asked us to confirm. "Today" and the number's year are company-time-zone dates. `FlowOps:CompanyTimeZone` is validated at start-up.
  - **Domain rule failures now name their field** (`DomainException.Field`), so 400s carry `errors.{field}` as the contract promises, e.g. `estimatedCost.amount`, `startDate`, `endDate`.
  - **`RequestDetailsReader`** is a shared helper (submission and GET). `CanReadLeaveReasonAsync` is composed from the same R1/R2/R3 predicates as `VisibleTo`.
  - **Test harness:** the request-number sequence restarts on every reset (Respawn doesn't reset sequences).
- **Contract changes:** none in `openapi.json`. API_CONTRACT §4 has "As built" notes.

## BE-11 — Request queries & comments

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/22 (merged)
- **Tests (at merge):** Domain 100 passed / 0 failed; Integration 400 passed / 0 failed. The ordering-sensitive classes were re-run twice more: 82/82 both times.
- **Review findings:** `/verify-feature` → meets spec with minor issues. The details projection materialises untracked `WorkTask` rows for `AvailableActions`, which is the signature the BE-09 review settled on.
- **Decisions:**
  - `canComment` = visibility; there is no separate rule.
  - Comment notifications go to the requester, everyone who decided a step, and whoever may decide the open task: active users only, never the author, never repeating the text.
  - `search` = number prefix (any case) or purchase item name.
  - **Ids are now monotonic** (`Domain/Common/SequentialGuid`). `Guid.CreateVersion7` randomises within a millisecond, so a request's `Submitted` and first `TaskAssigned` (same instant) could be listed in either order; this would have happened in production too. It is recorded in DOMAIN_MODEL §6 "As built".
- **Contract changes:** query parameter names in `openapi.json` are now camelCase (`pageSize`, `scope`, …), as API_CONTRACT always said. The frontend run had reported the capitalised names. The server accepts both spellings. AGENT_HANDOFF §6 tells the frontend to regenerate types.

## Not done / not verified

- **BE-12 not started**, as instructed. Decisions and the inbox will use `TaskEligibility.DecidableBy`/`EnsureCanDecide`, `TaskAssigneeResolver.Resolve`, `NotificationFactory` and `EligibleDeciders`. The full HTTP happy paths and the concurrency tests (TESTING §3.2, §3.4) belong there.
- **No realtime pushes yet.** `IRealtimePublisher` is BE-14.
- **The frontend still has to regenerate `schema.d.ts`** for BE-08 (if not done already) and BE-11's camelCase query names (AGENT_HANDOFF §6). I didn't touch `src/frontend`.
- **No new migrations this run,** so your local dev database needs nothing beyond the `dotnet ef database update` from the last report.
- **No CI yet** (OPS-02), so the PRs had no remote checks. All results above come from local runs against Docker 29.7.2.
