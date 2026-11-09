# Authentication & Authorization — FlowOps Phase 1

**Purpose:** how users sign in, what each user may see and do, and where each check lives.
Decision record: ADR-0003 (cookie auth). Domain terms: `DOMAIN_MODEL.md`.

---

## 1. Authentication

### 1.1 Choice: ASP.NET Core Identity + HttpOnly session cookie (not JWT)

FlowOps is a first-party SPA served from the **same site** as the API (Vite dev proxy locally; one reverse-proxied origin in deployment). For that topology a cookie is strictly simpler and safer:

| Concern | Cookie (chosen) | JWT in browser |
|---|---|---|
| Token theft via XSS | HttpOnly — JS can't read it | Token in memory/localStorage is readable by injected script |
| Revocation / logout | Server-side: security stamp + sign-out | Needs a denylist or short expiry + refresh tokens |
| CSRF | Must be handled (below) | Not applicable if sent as header |
| Complexity | Built into Identity | Refresh flow, rotation, storage decisions |

JWT is reconsidered only if a third-party or mobile client needs the API (Phase 3, `ROADMAP.md`).

### 1.2 Cookie configuration

| Setting | Value |
|---|---|
| Name | `__Host-flowops` (requires Secure, Path=/, no Domain) |
| HttpOnly | true |
| Secure | Always (dev uses HTTPS via `dotnet dev-certs`; the Vite proxy targets HTTPS) |
| SameSite | Strict |
| Lifetime | 8 h sliding expiration (`ExpireTimeSpan` = 8 h idle; then re-login). Session cookie (no `Expires`): closing the browser ends it |
| Re-validation | Security stamp checked every **1 min**: role, manager-flag and must-change changes, password changes elsewhere and deactivation reach existing sessions within a minute. Until then the coarse policies use the session's claims while `/api/auth/me` reads the database, so the two can disagree for up to a minute (resource checks always use the database) |
| Logout | Rotates the security stamp, then deletes the cookie: a copied cookie stops working at the next re-validation, and **the user's other sessions end too** (signing out signs out every device within a minute). Decided 2026-11-06: kept deliberately — server-side revocation matters more than staying signed in elsewhere |
| Unauthenticated API call | **401 ProblemDetails**, never a 302 redirect (override `OnRedirectToLogin` / `OnRedirectToAccessDenied`) |

Implementation (BE-05): Identity owns the scheme (`Infrastructure/Identity/IdentitySetup.cs`); the Api configures the cookie, antiforgery and policies (`Api/Auth/AuthorizationSetup.cs`). The session principal carries Identity's id/role/stamp claims plus `flowops:is_manager` and `flowops:must_change_password` (`AppClaimTypes`), rebuilt on every re-validation.

### 1.3 CSRF

SameSite=Strict blocks cross-site cookie sending in modern browsers; ASP.NET Core antiforgery is added as defence in depth:
- After login and on `GET /api/auth/me`, the API issues a readable `XSRF-TOKEN` cookie (antiforgery request token).
- The frontend API client copies it into the `X-XSRF-TOKEN` header on every non-GET request.
- A global filter validates antiforgery on all unsafe methods except `POST /api/auth/login` (no session yet). The SignalR hub (`/hubs/notifications`, BE-14) is outside MVC: it needs the session cookie but takes no antiforgery token (browsers can't add a header to a WebSocket, and clients can't invoke anything on it). Because SameSite=Strict only stops cross-*site* requests, `SameOriginHubGuard` refuses a `/hubs` request whose `Origin` host differs from the API's host (403), which blocks a sibling subdomain from opening the user's event stream. Ending a user's sessions (logout, password change or reset, deactivation) also closes their open hub connections, which cookie re-validation alone would not reach.
- The antiforgery *cookie token* lives in `__Host-flowops-af` (HttpOnly, Secure, Strict); `XSRF-TOKEN` (Secure, Strict, readable) carries the request token, bound to the signed-in user. Missing/invalid header → **403 `auth.forbidden`** (checked after authentication, so an anonymous call still gets 401).

### 1.4 CORS
Disabled. Same origin in all environments. If an environment ever needs cross-origin, it must be an ADR, not a config tweak.

### 1.5 XSS
- React escapes by default; `dangerouslySetInnerHTML` is **forbidden** (lint rule).
- All user text (comments, reasons, descriptions) is stored and returned as plain text.
- Response header `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'` in non-dev environments.

### 1.6 Credentials & accounts
- Passwords: Identity defaults (PBKDF2; digit, upper, lower and non-alphanumeric required), min length 12, lockout after 5 failed attempts for 15 min (Identity measures lockout with the system clock, not `TimeProvider`).
- Sign-in failures for unknown email, wrong password, inactive account and **locked-out account** are indistinguishable (`401 auth.invalid_credentials`, same message, same hashing work). There is no distinct lockout response (decided 2026-11-06; it used to be 423 and told an attacker which emails exist).
- **Login rate limiting** (ASP.NET Core rate limiting, `Api/Auth/LoginRateLimiting.cs`), configurable under `FlowOps:LoginRateLimit`: per client IP 30 attempts / 1 min (rate-limiter middleware, before the body is read); per email 10 attempts / 15 min, counted for every email whether or not an account exists (action filter after binding). Over either limit → `429 auth.rate_limited` with `Retry-After`. Limits are in memory per instance. Behind a reverse proxy the per-IP limit needs forwarded headers configured, otherwise every client shares the proxy's address. The Docker stack (OPS-01) does this: nginx overwrites `X-Forwarded-For`, and the API trusts it (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`) only because nginx is its sole caller. The API publishes no port. Residual risk: anyone who knows an email can still trigger that account's 15-minute lockout with 5 wrong passwords; the limits slow this down but cannot prevent it.
- **Comment rate limiting** (#37, `Api/Requests/CommentRateLimiting.cs`), configurable under `FlowOps:CommentRateLimit`: 10 comments / 1 min per signed-in user, across all requests → `429 request.comment_rate_limited` with `Retry-After`. In memory per instance. The rate-limiter middleware runs after authentication so it can partition by user id.
- Change-password: wrong current passwords count towards lockout; the new password must differ from the current one.
- **No public registration.** Accounts are created by administrators only.
- **Bootstrap admin:** on startup in any environment, if no Administrator exists, the app creates one from `FlowOps:BootstrapAdmin:Email` / `Password` (env vars / user-secrets; absent in production → startup logs a warning and creates nothing). `MustChangePassword = true`. Invalid configuration (weak password, email already used, or the password still the public placeholder from `.env.example`, #79) stops start-up. The five roles are seeded by migration with fixed ids, not at start-up.
- **Employee creation:** admin creates the user with an initial password → `MustChangePassword = true` → on first login every endpoint except `/api/auth/*` returns 403 `auth.password_change_required` until changed.
- Email delivery (invites, reset links) is **not** in Phase 1.

---

## 2. Roles and derived capabilities

| Role (Identity) | Grants |
|---|---|
| `Employee` | Submit requests, see own requests |
| `FinanceOfficer` | Eligible for `FinanceApproval` role-queue tasks |
| `HrOfficer` | Eligible for `HrConfirmation` role-queue tasks |
| `ProcurementOfficer` | Eligible for `ProcurementFulfillment` role-queue tasks |
| `Administrator` | Manage users, departments, roles, manager links; view stalled-task list |

| Derived capability | Computed as |
|---|---|
| `isManager` | user has ≥ 1 active direct report |

Users may hold several roles. **Administrator grants no request visibility and no process authority.** Admin powers can't be used to get it either (decided 2026-11-06): an admin can't change their own manager link, make themselves anyone's manager, deactivate themselves, or reset their own password through the admin endpoint (422 `user.self_administration`; they use change-password). Another administrator has to make those changes. An admin's own **role** change is recorded as pending (202) and applies only when a different, active administrator approves it within 72 hours. The approver's account must have been created at least 24 hours before the proposal (422 `role_change.approver_too_new`, ADR-0013). With no such admin the proposal is refused (422 `role_change.no_reviewer`). The `Administrator` role can only be given when a new account is created (422 `user.admin_role_on_create_only` otherwise). See ADR-0012. Resetting another user's password stays allowed; every admin change, password resets and role-change decisions included, is logged with the actor's id.

The server returns the capability set from `GET /api/auth/me` (`capabilities: string[]`). The frontend uses that list to show navigation and buttons; it never derives permissions from role names itself.

Capabilities: `requests.submit`, `tasks.view` (any officer role or isManager), `admin.manage`.

---

## 3. Permission matrix

✅ allowed · ⛔ forbidden · ⚙ allowed only if the resource rule in §4 holds.

| Action | Employee | Manager (derived) | Finance | HR | Procurement | Admin |
|---|---|---|---|---|---|---|
| Sign in / view own profile | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Submit purchase/leave request | ✅ | ✅ (needs Employee role) | ✅ (needs Employee role) | ✅ (needs Employee role) | ✅ (needs Employee role) | ⛔ unless also Employee |
| List/view own requests | ✅ | ✅ | ✅ | ✅ | ✅ | own only |
| View direct reports' requests | ⛔ | ⚙ R2 | ⛔ | ⛔ | ⛔ | ⛔ |
| View purchase requests at/after finance step | ⛔ | ⛔ | ⚙ R3 | ⛔ | ⛔ | ⛔ |
| View leave requests at/after HR step | ⛔ | ⛔ | ⛔ | ⚙ R3 | ⛔ | ⛔ |
| View purchase requests at fulfillment | ⛔ | ⛔ | ⛔ | ⛔ | ⚙ R3 | ⛔ |
| Decide manager-approval task | ⛔ | ⚙ T1 | ⛔ | ⛔ | ⛔ | ⛔ |
| Decide finance task | ⛔ | ⛔ | ⚙ T2 | ⛔ | ⛔ | ⛔ |
| Decide HR task | ⛔ | ⛔ | ⛔ | ⚙ T2 | ⛔ | ⛔ |
| Fulfill procurement task | ⛔ | ⛔ | ⛔ | ⛔ | ⚙ T2 | ⛔ |
| Comment on a request | ⚙ R1 | ⚙ R2 | ⚙ R3 | ⚙ R3 | ⚙ R3 | ⛔ |
| Read leave `Reason` | own | ⚙ R2 | ⛔ | ⚙ R3 | — | ⛔ |
| Manage users / departments / roles / managers | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ✅ |
| …on **their own** account: change their manager link, make themselves anyone's manager (including by reactivating a report), deactivate themselves, reset their own password via the admin endpoint | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ 422 `user.self_administration` (another admin must) |
| …change **their own** roles | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ⏳ 202 pending; another active admin approves (ADR-0012) |
| …add `Administrator` to an **existing** user | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ 422 `user.admin_role_on_create_only` (only on create) |
| Approve or reject **another** admin's own role change | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ✅ (not your own: 422 `role_change.self_approval`) |
| View stalled tasks (metadata only: the `StalledTaskDto` fields, see below) | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ✅ |
| Own notifications | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

**Stalled tasks (#58, decided 2026-11-09).** The list returns exactly the fields of `StalledTaskDto` (API_CONTRACT.md §8): task and request ids, request number, type, stage, the assignee (user `UserRef` or role), the reason (`AssigneeInactive` / `NoEligibleRoleMember`) and the open-since time. It never returns request details: purpose, item, cost, dates, leave reason or comments. The request id does not grant access: `GET /api/requests/{id}` stays 404 for administrators.

These fields allow some inference, which is accepted. `AssigneeInactive` with a named manager says that one of that manager's reports has an open request of that type. `NoEligibleRoleMember` on a `Leave` row at `HrConfirmation` says that the only HR officer applied for leave. The admin needs the assignee and the reason to fix the stall (UI_SPEC §4.8: "add an active Finance officer", "reassign the manager"), and already sees user names and roles in `/api/users`.

---

## 4. Resource rules (single implementation)

All resource rules live in **one** Application class, `AccessRules`. Its core method is `IQueryable<Request> VisibleTo(IQueryable<Request> requests, AccessContext user)`, an EF-translatable filter (it joins the users read model for R2). The *same* method backs list queries (`VisibleTo(db.Requests, me).Where(...)`) and single-item checks (`VisibleTo(...).AnyAsync(r => r.Id == id)`), so the two can never diverge. There is no second copy in controllers, services or the frontend.

`AccessContext` = `{ UserId, Roles, IsActive }`, built once per request by `AccessRules.ForCurrentUserAsync` from `ICurrentUser`'s id and **the database** (not session claims).

**Visibility — user U can see request Q iff any of:**
- **R1** `Q.RequesterId == U.Id`
- **R2** `Q.Requester.ManagerId == U.Id` (current direct manager)
- **R3** some task T on Q has `T.AssigneeUserId == U.Id`, or `T.AssigneeRole ∈ U.Roles`, or `T.DecidedById == U.Id`

(R3 naturally limits finance to purchases, HR to leave, procurement to fulfilment-stage purchases, because only those tasks carry those roles.)

**Eligibility — user U may decide task T iff all of:**
- **T1/T2** (`T.AssigneeUserId == U.Id`) or (`T.AssigneeRole ∈ U.Roles`)
- U is active
- U is not the requester (I2)

**As built (BE-09)** — `Application/Access/`:
- **`AccessRules`** (scoped):
  - **`ForCurrentUserAsync()`** builds the caller's `AccessContext` from the database in one query. `AccessContext` can't be built anywhere else: its constructor is internal, and tests use an internal `ContextForAsync(userId)` seam.
  - **`VisibleTo(requests, me)`** is one SQL statement (`EXISTS` subqueries for R2/R3). It works on tracked or untracked queries, with or without includes. An inactive or unknown user sees nothing.
  - **`CanViewAsync(id, me)`** is `VisibleTo(...).AnyAsync(id)`.
  - **Commands load the aggregate through `VisibleTo`**, so an invisible request is simply "not found" (404) before any eligibility check.
- **`TaskEligibility`** (static):
  - **`AssignedTo(me)`** is the T1/T2 expression. The in-memory check uses the same expression, compiled once per context.
  - **`DecidableBy(requests, me)`** yields `DecidableTask { Request, Task }`: open tasks the caller may decide now, excluding their own requests. The inbox projects both in one query.
  - **`FailureCode` / `EnsureCanDecide(requesterId, task, me)`**: not assigned or inactive → `task.not_eligible`; then the requester → `task.self_decision`; then someone who decided another task of the request → `task.already_participated` (**T3, separation of duties**, #50, decided 2026-11-09: every step is decided by a different person; a step whose only eligible members already took part shows as stalled).
  - **`AvailableActions(requesterId, openTask, me)`** takes only what a projection supplies. It returns the task's `WorkTask.AllowedActions` plus its `version`, or `null` for a decided task or a non-eligible caller.
  - **Open/current/already-decided stay the domain's 409s**: a late decider gets `task.already_decided`, not 403.
- **Administrator never gets a task.** The domain refuses Administrator as a role queue (`AssigneeRule`/`Assignee.ForRole`), which is what makes "admins see nothing" hold.
- **Leave reason (BE-10/11).** `AccessRules.CanReadLeaveReasonAsync` is composed from the same R1/R2/R3 predicates as `VisibleTo`: R1, or R2, or HrOfficer + R3 **through the HR task** (the request has an `HrOfficer` queue task). A former manager who decided the step but is no longer the direct manager sees the request (R3) but not the reason, even if they also hold `HrOfficer` (BE-11b).
- **Commenting = visibility** (§3). There is no separate rule: `canComment` is `true` on every request the caller can see.
- **`TaskEligibility.EligibleDeciders`** applies the same three conditions to address notifications: active, assigned, not the requester.

**Response codes**
- Request not visible → **404** (do not reveal existence).
- Visible but action not permitted → **403** with a `code`.
- Unauthenticated → **401**.

---

## 5. Sensitive data

| Data | Rule |
|---|---|
| Leave `Reason` | Returned only when the caller satisfies R1, R2, or holds `HrOfficer` and the request has an HR task (R3 through the HR queue). Decision comments, rejection reasons and request comments are **not** masked: they are visible to everyone who can see the request. `LeaveDetailsDto.Reason` is `null` otherwise; a boolean `reasonHidden` tells the UI to show "Restricted". |
| Leave in notifications | Title/body mention "leave request FO-2026-000123" and dates only. |
| Password hashes, security stamps, lockout data | Never in any DTO. Users endpoints return `UserDto` (explicit fields). |
| Logs | Never log request bodies, reasons, comments or passwords. Log ids and outcomes. |

---

## 6. Where each check lives

| Check | Location |
|---|---|
| Authenticated? role-level gate (e.g. admin endpoints) | ASP.NET Core **policies** on controllers: `Policies.Authenticated` (fallback), `Policies.Admin`, `Policies.CanSubmit` |
| Visibility of a request | `AccessRules.VisibleTo` / `CanViewAsync` (Application) — called by query and command services |
| Eligibility to decide a task | `TaskEligibility.FailureCode` / `EnsureCanDecide` / `DecidableBy` (Application), after the visibility check |
| Self-decision | Domain (`Request.DecideTask`) **and** `TaskEligibility` (fail fast with clear code) |
| Actor identity | `ICurrentUser` (Api implementation reads `HttpContext.User`); services never accept a user id from a DTO |
| Must-change-password gate | Middleware in Api |

**Claim staleness (accepted 2026-11-06).** The coarse policies read the session's claims, which are rebuilt at the security-stamp re-validation every 1 minute (§1.2). So for up to a minute after a role, manager-link, must-change or deactivation change, a session may still pass (or fail) a policy by its old claims, and `/api/auth/me` (database) and the policies (cookie) may disagree. Resource checks (`AccessRules`, `TaskEligibility`) always read the database and are never stale. Two writes don't rely on the stale claims either: every admin write re-checks that the caller is an active administrator in the database (`RoleAssignmentRules.EnsureActiveAdministratorAsync`, #76), and submitting a request re-checks that the caller is active and holds `Employee` (#77). A shorter interval would cost extra queries on every request.

Resource checks are not implemented as ASP.NET `IAuthorizationHandler`s because that would force controllers to load entities before calling services (fat controllers). ADR-0003 records this.

---

## 7. Required security tests
See `TESTING.md` §3.3. Minimum: every row of §3 with a ⛔ for a ⚙/✅ neighbour has an integration test proving the 403/404.
