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
| Re-validation | Security stamp checked every **1 min**: role, manager-flag and must-change changes, password changes elsewhere and deactivation reach existing sessions within a minute |
| Unauthenticated API call | **401 ProblemDetails**, never a 302 redirect (override `OnRedirectToLogin` / `OnRedirectToAccessDenied`) |

Implementation (BE-05): Identity owns the scheme (`Infrastructure/Identity/IdentitySetup.cs`); the Api configures the cookie, antiforgery and policies (`Api/Auth/AuthorizationSetup.cs`). The session principal carries Identity's id/role/stamp claims plus `flowops:is_manager` and `flowops:must_change_password` (`AppClaimTypes`), rebuilt on every re-validation.

### 1.3 CSRF

SameSite=Strict blocks cross-site cookie sending in modern browsers; ASP.NET Core antiforgery is added as defence in depth:
- After login and on `GET /api/auth/me`, the API issues a readable `XSRF-TOKEN` cookie (antiforgery request token).
- The frontend API client copies it into the `X-XSRF-TOKEN` header on every non-GET request.
- A global filter validates antiforgery on all unsafe methods except `POST /api/auth/login` (no session yet).
- The antiforgery *cookie token* lives in `__Host-flowops-af` (HttpOnly, Secure, Strict); `XSRF-TOKEN` (Secure, Strict, readable) carries the request token, bound to the signed-in user. Missing/invalid header → **403 `auth.forbidden`** (checked after authentication, so an anonymous call still gets 401).

### 1.4 CORS
Disabled. Same origin in all environments. If an environment ever needs cross-origin, it must be an ADR, not a config tweak.

### 1.5 XSS
- React escapes by default; `dangerouslySetInnerHTML` is **forbidden** (lint rule).
- All user text (comments, reasons, descriptions) is stored and returned as plain text.
- Response header `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'` in non-dev environments.

### 1.6 Credentials & accounts
- Passwords: Identity defaults (PBKDF2; digit, upper, lower and non-alphanumeric required), min length 12, lockout after 5 failed attempts for 15 min (Identity measures lockout with the system clock, not `TimeProvider`).
- Sign-in failures for unknown email, wrong password and inactive account are indistinguishable (`401 auth.invalid_credentials`, same hashing work).
- **No public registration.** Accounts are created by administrators only.
- **Bootstrap admin:** on startup in any environment, if no Administrator exists, the app creates one from `FlowOps:BootstrapAdmin:Email` / `Password` (env vars / user-secrets; absent in production → startup logs a warning and creates nothing). `MustChangePassword = true`. Invalid configuration (weak password, email already used) stops start-up. The five roles are seeded by migration with fixed ids, not at start-up.
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

Users may hold several roles. **Administrator grants no request visibility and no process authority.**

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
| View stalled tasks (number, type, stage, age only) | ⛔ | ⛔ | ⛔ | ⛔ | ⛔ | ✅ |
| Own notifications | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## 4. Resource rules (single implementation)

All resource rules live in **one** Application class, `AccessRules`. Its core method is `IQueryable<Request> VisibleTo(IQueryable<Request> requests, AccessContext user)`, an EF-translatable filter (it joins the users read model for R2). The *same* method backs list queries (`VisibleTo(db.Requests, me).Where(...)`) and single-item checks (`VisibleTo(...).AnyAsync(r => r.Id == id)`), so the two can never diverge. There is no second copy in controllers, services or the frontend.

`AccessContext` = `{ UserId, Roles }`, built once per request from `ICurrentUser`.

**Visibility — user U can see request Q iff any of:**
- **R1** `Q.RequesterId == U.Id`
- **R2** `Q.Requester.ManagerId == U.Id` (current direct manager)
- **R3** some task T on Q has `T.AssigneeUserId == U.Id`, or `T.AssigneeRole ∈ U.Roles`, or `T.DecidedById == U.Id`

(R3 naturally limits finance to purchases, HR to leave, procurement to fulfilment-stage purchases, because only those tasks carry those roles.)

**Eligibility — user U may decide task T iff all of:**
- **T1/T2** (`T.AssigneeUserId == U.Id`) or (`T.AssigneeRole ∈ U.Roles`)
- U is active
- U is not the requester (I2)

**Response codes**
- Request not visible → **404** (do not reveal existence).
- Visible but action not permitted → **403** with a `code`.
- Unauthenticated → **401**.

---

## 5. Sensitive data

| Data | Rule |
|---|---|
| Leave `Reason` | Returned only when the caller satisfies R1, R2, or holds `HrOfficer` with R3. `LeaveDetailsDto.Reason` is `null` otherwise; a boolean `reasonHidden` tells the UI to show "Restricted". |
| Leave in notifications | Title/body mention "leave request FO-2026-000123" and dates only. |
| Password hashes, security stamps, lockout data | Never in any DTO. Users endpoints return `UserDto` (explicit fields). |
| Logs | Never log request bodies, reasons, comments or passwords. Log ids and outcomes. |

---

## 6. Where each check lives

| Check | Location |
|---|---|
| Authenticated? role-level gate (e.g. admin endpoints) | ASP.NET Core **policies** on controllers: `Policies.Authenticated` (fallback), `Policies.Admin`, `Policies.CanSubmit` |
| Visibility of a request | `AccessRules.CanView` — called by query and command services |
| Eligibility to decide a task | `TaskEligibility.Check` (Application) — uses `AccessRules` + roles |
| Self-decision | Domain (`Request.DecideTask`) **and** `TaskEligibility` (fail fast with clear code) |
| Actor identity | `ICurrentUser` (Api implementation reads `HttpContext.User`); services never accept a user id from a DTO |
| Must-change-password gate | Middleware in Api |

Resource checks are not implemented as ASP.NET `IAuthorizationHandler`s because that would force controllers to load entities before calling services (fat controllers). ADR-0003 records this.

---

## 7. Required security tests
See `TESTING.md` §3.3. Minimum: every row of §3 with a ⛔ for a ⚙/✅ neighbour has an integration test proving the 403/404.
