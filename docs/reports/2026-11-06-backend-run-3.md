# Backend run 3 — 2026-11-06

The brief asked for BE-08b (admin self-authority), BE-10 and BE-11. When the run started, `main` already had BE-10 (#21), BE-11 (#22) and most of the self-authority rule (#24). So, after checking with you at the start, this run only closed the gaps between the brief and what was merged:
- the existing error code `user.self_administration` stays (the frontend already maps it), with **no rename** to `user.self_change_forbidden`;
- BE-10 and BE-11 get small follow-up branches (BE-10b, BE-11b) instead of being re-implemented.

Each follow-up had one branch in its own worktree and was rebased on the latest `main`. Each was merged by squash after a full build, both test suites on real PostgreSQL (Testcontainers, Docker 29.7.2) and `/verify-feature`. BE-08b and BE-11 had a `security-reviewer` pass. Neither found anything Critical or High, and every in-scope finding was fixed.

**BE-12 was not started**, as instructed. No ADR change was needed.

## Update — your decisions on #31, #32, #37 (same day)

| Issue | Decision | Result |
|---|---|---|
| #31 | Option 1: forbid self password reset | **Done.** https://github.com/osamashtayat/flowops/pull/41 (merged, closes #31). `POST /users/{own id}/reset-password` → 422 `user.self_administration`. Tests: Domain 103/103, Integration 433/433. |
| #37 | Option 1 + comment rate limit | **Done.** https://github.com/osamashtayat/flowops/pull/45 (merged, closes #37). Activity returns the newest 500, oldest first. Comments are limited to 10 per minute per user (`FlowOps:CommentRateLimit`) → 429 `request.comment_rate_limited` + `Retry-After`. Rate-limiter middleware now runs after authentication. Limit options are validated at start-up. Contract: new code, 429 on comments, `openapi.json` and `schema.d.ts` regenerated, `problem.ts` message added. Tests after rebasing on #42: Domain 103/103, Integration 442/442, frontend 451/451 plus typecheck/lint/build. `security-reviewer`: no Critical, High or Medium. |
| #32 | Option 3: two-person control | **Proposed, not implemented.** It needs a new ADR, so I stopped as the brief says. ADR-0012 (Proposed) is in https://github.com/osamashtayat/flowops/pull/44 (open). **Merge it to accept, or comment.** Its open questions: Phase 1 or 2; whether Employee-only creates need approval; 72 h expiry; the bootstrap exception for the second admin. It changes `POST /users` (generated passwords) and returns 202 on four admin endpoints, so FE-13 changes too. |

## Needs your decision (original list; #31 and #37 are now resolved, #32 is waiting on ADR-0012)

1. **#31: may an admin reset their own password through `POST /api/users/{id}/reset-password`?**
   - Today they can, without the current password that `change-password` requires.
   - A hijacked admin session could use this to lock out the real owner.
   - Recommendation: forbid it with 422 `user.self_administration`, so admins use change-password instead.
2. **#32: one admin can still get process authority through a second account.**
   - They can create a user with a process role and a password they choose, or reset someone else's password, then sign in as that account.
   - The audit log is the only control; this is the password-reset impersonation gap from run 2.
   - Options:
     - accept it for Phase 1 and soften AUTHORIZATION §2;
     - forbid resetting passwords of users who hold a process or admin role;
     - two-person control (Phase 2, needs an ADR).
3. **#37: the activity cap and comment volume.**
   - `GET /activity` keeps the **oldest** 500 entries, so on a request flooded with comments the newest decisions disappear.
   - Comments are unlimited, and each one notifies every eligible decider.
   - Recommendation: keep the newest 500, and either add a comment cap or rate-limit comments. Both are contract changes.
4. Still open from run 2: the accepted races (simultaneous last-admin demotions, manager cycles) and the login rate-limit numbers. Nothing changed there.

## BE-08b — Admin self-authority (follow-up to BE-08)

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/33 (merged) · **Closes:** #27
- **Changes:**
  - **Application.** `UserAdministrationService.UpdateAsync` returns 422 `user.self_administration` when an admin:
    - deactivates themselves (the last-admin check still runs first);
    - reactivates a user whose manager is the admin. This was a security-review finding: it silently made the admin a manager again.
  - **Audit log.** The create audit line now records department and manager. Password resets on other users stay allowed and were already logged with the actor id; this is now tested.
  - **Tests.**
    - `LogSink` (test harness only) captures the app's logs: messages, structured values, scopes and exceptions.
    - New `SelfAdministrationTests`:
      - self-deactivation;
      - self-deactivation combined with a rename (saves nothing);
      - reactivating your own report;
      - another admin deactivating or reactivating (allowed);
      - deactivation audit;
      - password reset on another user is allowed and logged with `ActorId`/`UserId`, and the password appears in no log entry.
- **Already in place from #24 (re-verified):** own roles, own manager link, and self as manager on create or update all give 422, each with a test. The other-admin cases are allowed and tested.
- **Contract change:** none in `openapi.json` (regenerated, no diff). API_CONTRACT §8 now lists the new 422 cases on `PUT /api/users/{id}`. AUTHORIZATION §2 and the §3 matrix have a self-administration row. `ErrorCodes.UserSelfAdministration` and API_CONTRACT §2 already had the code.
- **Reviews:**
  - `security-reviewer`: no Critical or High.
    - **Fixed:** M1 (reactivation bypass), L4 (create audit), L5 (log-leak assertion coverage).
    - **Raised:** M2 → #31, M3 → #32.
  - `/verify-feature`: meets spec. `UpdateAsync` was split back under about 30 lines (`EnsureNoSelfManagerLink`).
- **Tests at merge:** build 0 warnings / 0 errors; Domain **100 passed / 0 failed**; Integration **418 passed / 0 failed**. Re-run after rebasing on the frontend PRs #28/#30 gave the same counts.
- **AGENT_HANDOFF §6:** the table was already empty. The item it used to hold had been filed as #27 by the frontend run, and #27 is now closed.

## BE-10b — Leave length (follow-up to BE-10)

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/35 (merged)
- **Finding:** the code already counted days inclusively, with `RequestLimits.LeaveMaxDays = 90` (`RequestLimits.cs:15`, `LeaveDetails.cs:26,56`). Nothing in the behaviour changed.
- **Changes:**
  - **Domain tests:** same day = 1; 89 days accepted; the limit read from `RequestLimits`. The 90 and 91 cases already existed.
  - **API test** `SubmitLeave_DateRules`: adds an 89-day case and asserts the returned `leave.days` (1 / 89 / 90); 91 gives 400 `errors.endDate`.
  - **Docs:** API_CONTRACT §4 (`days = (end − start) + 1`, max 90, end − start ≤ 89, same day = 1) and a derived `Days` row in DOMAIN_MODEL §2.3.
- **Contract change:** none (`openapi.json` regenerated, no diff).
- **Tests at merge:** build 0 warnings / 0 errors; Domain **103 passed / 0 failed**; Integration **419 passed / 0 failed**.

## BE-11b — Security review of BE-11

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/39 (merged)
- **`security-reviewer` on BE-11:** no Critical or High, four Lows.
  - **L3, fixed:** an HR officer who had decided the manager step as a former manager could still read the leave reason, which contradicts AUTHORIZATION §4.
    - `CanReadLeaveReasonAsync` now requires HrOfficer **and** an HrOfficer queue task on the request.
    - The test was written first and failed before the fix.
  - **L1, fixed:** `page=2147483647` overflowed the offset and returned a 500.
    - `PageRequest.MaxPage = 10_000` applies to every paged endpoint, so out-of-range pages give 400.
  - **L2:** raised as #37 (needs your decision).
  - **L4:** raised as #38. Decision comments aren't masked like the reason, so the frontend should show a hint; AUTHORIZATION §5 now says this.
- **Negative tests added:**
  - the reason never appears in list items or activity bodies;
  - `GET /activity` gives 404 for an admin, an inactive user and a second-level manager;
  - paging bounds give 400;
  - the last allowed page returns an empty list.
- **Contract change:** `page` maximum 2147483647 → 10000 in `openapi.json` (regenerated) and API_CONTRACT §1. I regenerated the frontend types with the pinned `openapi-typescript@7.13.0`; there is **no diff** in `schema.d.ts`, so the frontend has nothing to do.
- **Tests at merge:** build 0 warnings / 0 errors; Domain **103 passed / 0 failed**; Integration **432 passed / 0 failed**. Re-run after rebasing on #34/#36 gave the same counts.

## Issues

| # | Title | Labels | State |
|---|---|---|---|
| #27 | Self-deactivation and error code name differ from owner decision | backend, contract, decision-needed | **closed** by #33 (filed earlier by the frontend run) |
| #31 | Admin can reset their own password via the admin endpoint | backend, decision-needed, security | opened |
| #32 | One admin can gain process authority through a second account | backend, decision-needed, security | opened |
| #37 | Activity cap hides newest entries; comments are unlimited | backend, decision-needed, contract | opened |
| #38 | Warn that leave decision comments are visible to all participants | frontend, security | opened |

## Decisions taken

- **Error code.** Kept `user.self_administration` for every self-change, as you chose at the start of the run.
- **Reactivation counts as self-management.** Reactivating a user who reports to the caller counts as "making yourself someone's manager". Other edits still don't re-check existing links.
- **HR reason access is tied to the HR queue task,** not to any participation.
- **`MaxPage` = 10,000.** It is far above real use, keeps `OFFSET` at most about 1M rows, and is defined once on `PageRequest`.

## Not done / not verified

- BE-12 was not started.
- No CI exists yet (OPS-02), so all results come from local runs.
- `src/backend/FlowOps.Api/Controllers/NotificationsController.cs` has an uncommitted change in the main checkout (renames the constant `Task` to `_task`). It isn't from this run, so I left it alone.
- Stale worktrees from earlier runs still exist under `.claude/worktrees/` (`backend-run`, `backend-run-2`, `frontend-run`, `FE-02-05-06-ui-primitives`). I didn't remove them.
