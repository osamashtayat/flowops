# API Contract — FlowOps Phase 1

**Purpose:** the authoritative HTTP + SignalR contract between backend and frontend.
**Status:** v1 design. Once task BE-03 lands, `contracts/openapi.json` (generated from code) becomes the *executable* contract; this document stays the *design* contract. They must agree — see §8 for the change process.

Conventions: JSON camelCase; enums as strings; ids are UUID strings; timestamps ISO-8601 UTC (`2026-11-05T07:43:00Z`); dates `yyyy-MM-dd`; money `{ "amount": "1250.500", "currency": "JOD" }` with amount as a **string** (avoids JS float rounding).

All endpoints are under `/api` and require authentication unless marked **public**. All unsafe methods require the `X-XSRF-TOKEN` header (except login).

---

## 1. Common shapes

```ts
type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number };
// Query: ?page=1&pageSize=20   (page ≥ 1, 1 ≤ pageSize ≤ 100, default 20)

type Money = { amount: string; currency: "JOD" };

type UserRef = { id: string; displayName: string };

type RequestType   = "EquipmentPurchase" | "Leave";
type RequestStatus = "InReview" | "Approved" | "Completed" | "Rejected";
type RequestStage  = "ManagerApproval" | "FinanceApproval" | "HrConfirmation" | "ProcurementFulfillment" | "Closed";
type WorkTaskKind  = "Approval" | "Fulfillment";
type WorkTaskStatus  = "Open" | "Done";
type WorkTaskOutcome = "Approved" | "Rejected" | "Fulfilled";
type LeaveType     = "Annual" | "Sick" | "Unpaid" | "Other";
type Role          = "Administrator" | "Employee" | "FinanceOfficer" | "HrOfficer" | "ProcurementOfficer";
type Capability    = "requests.submit" | "tasks.view" | "admin.manage";
```

`version` fields are **opaque strings** (the server serialises PostgreSQL `xmin`). Clients echo them back unchanged; they never parse or compare them.

## 2. Errors (RFC 9457 ProblemDetails)

```json
{
  "type": "https://flowops.local/problems/task.already_decided",
  "title": "Task already decided",
  "status": 409,
  "detail": "This task was already handled by someone else.",
  "code": "task.already_decided",
  "traceId": "00-…"
}
```
Validation (400) adds `errors: { "fieldName": ["message"] }` with camelCase field paths (`estimatedCost.amount`).

| Status | When | `code` examples |
|---|---|---|
| 400 | Malformed body / DataAnnotations failure | `validation` |
| 401 | No/expired session | `auth.unauthenticated` |
| 403 | Authenticated but not permitted | `auth.forbidden`, `auth.password_change_required`, `task.not_eligible`, `task.self_decision` |
| 404 | Not found **or not visible** to caller | `not_found` |
| 409 | State conflict | `task.already_decided`, `task.not_current`, `task.invalid_action`, `request.closed`, `concurrency.conflict`, `department.duplicate`, `user.duplicate_email` |
| 422 | Valid input, violates business precondition | `request.manager_not_assigned`, `request.department_not_assigned`, `user.manager_cycle`, `user.last_admin`, `user.self_administration`, `department.inactive` |
| 429 | Too many login attempts from this address or for this email (login only); sends `Retry-After` (seconds) | `auth.rate_limited` |
| 500 | Unhandled | `server.error` (no internals in `detail`) |

The full code list lives in `FlowOps.Domain/Errors/ErrorCodes.cs` and `src/frontend/src/lib/api/problem.ts` maps codes to user messages. Adding a code = update both + this table.

---

## 3. Auth

| Method & path | Access | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/auth/login` | **public** | `{ email, password }` | 200 `CurrentUserDto` + session cookie + XSRF cookie | 400, 401 `auth.invalid_credentials` (also while the account is locked out), 429 `auth.rate_limited` |
| `POST /api/auth/logout` | auth | — | 204 | — |
| `GET /api/auth/me` | auth (allowed during must-change-password) | — | 200 `CurrentUserDto`; refreshes XSRF cookie | 401 |
| `POST /api/auth/change-password` | auth (allowed during must-change-password) | `{ currentPassword, newPassword }` | 204 | 400 `validation` (Identity password rules mapped to `errors.newPassword`) |

```ts
type CurrentUserDto = {
  id: string; email: string; displayName: string;
  department: { id: string; name: string } | null;
  manager: UserRef | null;
  roles: Role[];
  capabilities: Capability[];
  mustChangePassword: boolean;
};
```
Note: a locked-out account answers exactly like a wrong password (401 `auth.invalid_credentials`), so the response never reveals which emails belong to real accounts. The login screen's message for 401 mentions the 15-minute pause after repeated failures (AUTHORIZATION.md §1.6).

## 4. Requests

### `POST /api/requests/purchase` — policy `CanSubmit`
```ts
type SubmitPurchaseRequest = {
  itemName: string;        // 1–150
  description: string;     // 1–2000
  estimatedCost: Money;    // amount > 0, ≤ 1000000, ≤ 3 decimals; currency "JOD"
  justification: string;   // 1–2000
};
```
→ **201** `RequestDetailsDto`, `Location: /api/requests/{id}`. Errors: 400, 422 `request.manager_not_assigned` / `request.department_not_assigned`.
No `department`, `requesterId` or `submittedAt` field is accepted — unknown properties are ignored, and those values come from the server.

### `POST /api/requests/leave` — policy `CanSubmit`
```ts
type SubmitLeaveRequest = {
  startDate: string; endDate: string;  // yyyy-MM-dd; start ≥ today (company TZ); end ≥ start; days = end − start + 1 ≤ 90
  leaveType: LeaveType;
  reason?: string | null;              // ≤ 1000
};
```
→ **201** `RequestDetailsDto`. Errors as above.

As built (BE-10), for both submit endpoints:
- **Order of checks.**
  1. Input: field rules → 400.
  2. Department: none → 422 `request.department_not_assigned`.
  3. Manager: none, inactive, or the requester → 422 `request.manager_not_assigned`.

  Nothing is saved on any failure.
- **400 `errors` keys.** Rule failures that the domain finds (e.g. `estimatedCost.amount` > 1,000,000, `startDate` in the past, `endDate` beyond 90 days) also come back as `errors.{field}`, like DataAnnotations failures.
- **Leave length.** Days are counted inclusively: `days` = (end − start) + 1, so a same-day leave is 1 day. The maximum is 90 days (end − start ≤ 89); 91 or more gives 400 `errors.endDate`. "Today" is the date in `FlowOps:CompanyTimeZone`.
- **Number.** `FO-{company year}-{seq:000000}` from one database sequence. Failed submissions may leave gaps.
- **Title.** The item name for purchases. For leave, e.g. `Annual leave · 10–12 Mar` (`30 Nov – 2 Dec` across months, with years when they differ).
- **Notifications.** The manager gets one `TaskAssigned` notification. It names the number, type and title, never the reason.
- **No realtime push yet.** The realtime push arrives with BE-14.

### `GET /api/requests` — auth
Query: `scope=mine|visible` (default `mine`), `status?`, `type?`, `search?` (number prefix or item name, ≤ 100 chars), `page`, `pageSize`. Sorted `submittedAt desc`.
`scope=visible` returns everything the caller can see under `AUTHORIZATION.md` §4 (managers use it for "Team requests").
→ 200 `Paged<RequestSummaryDto>`

```ts
type RequestSummaryDto = {
  id: string; number: string; type: RequestType;
  title: string;                 // purchase: itemName; leave: "Annual leave · 12–14 Nov"
  requester: UserRef; department: { id: string; name: string };
  status: RequestStatus; stage: RequestStage;
  submittedAt: string; closedAt: string | null;
};
```

### `GET /api/requests/{id}` — auth + visibility
→ 200 `RequestDetailsDto` · 404 if not visible.
```ts
type RequestDetailsDto = RequestSummaryDto & {
  purchase: { itemName: string; description: string; estimatedCost: Money; justification: string } | null;
  leave: { startDate: string; endDate: string; days: number; leaveType: LeaveType;
           reason: string | null; reasonHidden: boolean } | null;
  tasks: WorkTaskDto[];           // chronological
  availableActions: { taskId: string; actions: ("approve"|"reject"|"fulfill")[]; version: string } | null;
  canComment: boolean;
};
type WorkTaskDto = {
  id: string; kind: WorkTaskKind; stage: RequestStage; status: WorkTaskStatus;
  assignee: { user: UserRef | null; role: Role | null };   // exactly one is non-null
  outcome: WorkTaskOutcome | null; decidedBy: UserRef | null; decidedAt: string | null;
  comment: string | null; createdAt: string;
};
```
`availableActions` is computed by the server with the same `TaskEligibility` used for decisions; the UI renders buttons from it and does not re-derive permissions.

### `GET /api/requests/{id}/activity` — auth + visibility
→ 200 `ActivityDto[]` (chronological, not paged; bounded by process length + comments, cap 500).
```ts
type ActivityDto = { id: string; type: "Submitted"|"TaskAssigned"|"Approved"|"Rejected"|"Fulfilled"|"Completed"|"CommentAdded";
  actor: UserRef | null; stage: RequestStage | null; body: string | null; occurredAt: string };
```

### `POST /api/requests/{id}/comments` — auth + visibility
Body `{ body: string }` (1–2000). → 201 `ActivityDto` · 400 · 404.
Comments are allowed on closed requests (follow-up notes), so there is no `request.closed` error here. Users who can't see the request (including admin-only users) get 404.

As built (BE-11):
- **List.**
  - `scope=mine` is the caller's own requests; `scope=visible` is everything §4 lets them see.
  - `search` matches a number prefix, case-insensitive (`fo-2026-0001`), or a substring of a purchase item name. Leave requests match by number only.
  - Sorted `submittedAt` desc, then `id`.
- **Details.**
  - `reason` comes back only under AUTHORIZATION §5 (`AccessRules.CanReadLeaveReasonAsync`).
  - `canComment` is always `true` on a details response, because anyone who can see a request may comment on it.
  - `availableActions` comes from `TaskEligibility`.
- **Activity.** Chronological, capped at 500. `actor` is `null` for system entries (`TaskAssigned`, `Completed`).
- **Comment.**
  - Returns 201 with `Location: /api/requests/{id}/activity`.
  - Notifies the requester, everyone who decided a step, and whoever may decide the open task (active users only, never the author).
  - The notification doesn't repeat the comment text.
- **Query parameter names** in `contracts/openapi.json` are now camelCase (`pageSize`, `scope`, `includeInactive`, …) as this document always said. The server accepts either spelling.

## 5. Tasks (inbox and decisions)

### `GET /api/tasks` — capability `tasks.view`
Query: `status=Open|Done` (default `Open`), `page`, `pageSize`.
- `Open`: tasks the caller is **eligible** to decide (assigned user or role queue, excluding own requests), oldest first.
- `Done`: tasks the caller decided, newest first.
→ 200 `Paged<InboxItemDto>`
```ts
type InboxItemDto = {
  taskId: string; kind: WorkTaskKind; stage: RequestStage; createdAt: string; version: string;
  status: WorkTaskStatus; outcome: WorkTaskOutcome | null; decidedAt: string | null;
  request: RequestSummaryDto & { keyDetail: string };   // "JOD 1,250.500" or "3 days · Annual"
};
```

### Decisions — auth + visibility + eligibility
| Method & path | Body | Success |
|---|---|---|
| `POST /api/tasks/{taskId}/approve` | `{ version: string; comment?: string \| null }` (comment ≤ 2000) | 204 |
| `POST /api/tasks/{taskId}/reject` | `{ version: string; reason: string }` (reason 1–2000, not whitespace) | 204 |
| `POST /api/tasks/{taskId}/fulfill` | `{ version: string; note?: string \| null }` | 204 |

Errors: 400, 403 `task.not_eligible` / `task.self_decision`, 404, 409 `task.already_decided` / `task.not_current` / `task.invalid_action` / `request.closed` / `concurrency.conflict`, 422 (next-step assignee unresolvable — not expected in Phase 1 since role queues always resolve).

Approve on an `HrConfirmation` task is the "Confirm" action in the UI.

## 6. Notifications

| Method & path | Notes | Success |
|---|---|---|
| `GET /api/notifications?unreadOnly=false&page&pageSize` | own only, newest first | 200 `Paged<NotificationDto>` |
| `GET /api/notifications/unread-count` | | 200 `{ count: number }` |
| `POST /api/notifications/{id}/read` | idempotent; others' ids → 404 | 204 |
| `POST /api/notifications/read-all` | | 204 |

```ts
type NotificationDto = { id: string; type: "TaskAssigned"|"RequestApproved"|"RequestRejected"|"RequestCompleted"|"StepApproved"|"CommentAdded";
  title: string; body: string; requestId: string | null; taskId: string | null; createdAt: string; readAt: string | null };
```

## 7. Dashboard

`GET /api/dashboard` — auth → 200 `DashboardDto`. Sections the caller lacks capability for are `null`.
```ts
type DashboardDto = {
  myRequests: { inReview: number; approved: number; completed: number; rejected: number; recent: RequestSummaryDto[] /* ≤5 */ };
  myTasks: { open: number; oldestOpenCreatedAt: string | null; decidedLast7Days: number } | null; // tasks.view
  recentActivity: (ActivityDto & { requestId: string; requestNumber: string })[];                // ≤10, on requests visible to me
  admin: { activeUsers: number; departments: number; stalledTasks: number } | null;               // admin.manage
};
```

## 8. Administration — policy `Admin`

### Departments
| Method & path | Body | Success | Errors |
|---|---|---|---|
| `GET /api/departments?includeInactive=false` | — | 200 `DepartmentDto[]` (any authenticated user may list active ones — used in filters) | |
| `POST /api/departments` | `{ name, code }` | 201 `DepartmentDto` | 409 `department.duplicate` |
| `PUT /api/departments/{id}` | `{ name, code, isActive }` | 200 `DepartmentDto` | 404, 409 |

`type DepartmentDto = { id; name; code; isActive; memberCount: number }`

As built (BE-08): `includeInactive=true` is honoured only for administrators; everyone else gets active departments. Sorted by name. `memberCount` counts **active** members. `code` is upper-cased. Duplicate name (case-insensitive) or code → 409 `department.duplicate`. Deactivating a department keeps its members; it just takes no new ones. `POST` returns `Location: /api/departments`, because there is no GET-by-id.

### Users
| Method & path | Body | Success | Errors |
|---|---|---|---|
| `GET /api/users?search&departmentId&role&includeInactive&page&pageSize` | — | 200 `Paged<UserDto>` | |
| `GET /api/users/{id}` | — | 200 `UserDto` | 404 |
| `POST /api/users` | `CreateUserRequest` | 201 `UserDto` + `Location` | 400 (password rules → `errors.initialPassword`; unknown department; unknown/inactive manager), 409 `user.duplicate_email`, 422 `department.inactive`, `user.self_administration` (caller as manager) |
| `PUT /api/users/{id}` | `UpdateUserRequest` | 200 `UserDto` | 400 (unknown department, unknown/inactive manager), 404, 409 `concurrency.conflict`, 422 `user.manager_cycle`, `user.last_admin` (deactivating last admin), `department.inactive` (moving into an inactive department), `user.self_administration` (changing own manager, making yourself someone's manager, reactivating someone who reports to you, or deactivating yourself) |
| `PUT /api/users/{id}/roles` | `{ roles: Role[] }` | 200 `UserDto` | 404, 409 `concurrency.conflict`, 422 `user.last_admin`, `user.self_administration` (own roles) |
| `POST /api/users/{id}/reset-password` | `{ newPassword }` | 204 (sets MustChangePassword, clears lockout, ends the user's sessions) | 400, 404, 409 `concurrency.conflict` |

```ts
type UserDto = { id; email; displayName; isActive: boolean; mustChangePassword: boolean;
  department: { id; name } | null; manager: UserRef | null; roles: Role[]; directReportCount: number; createdAt: string };
type CreateUserRequest = { email; displayName; initialPassword; departmentId: string | null; managerId: string | null; roles: Role[] };
type UpdateUserRequest = { displayName; departmentId: string | null; managerId: string | null; isActive: boolean };
```

As built (BE-08):
- **List.** `search` is a case-insensitive substring match on display name or email. Sorted by display name. Inactive users are excluded unless `includeInactive=true`.
- **Counts and roles.** `directReportCount` counts **active** direct reports, the same definition as `isManager`. `roles` is replaced as a set; duplicates are ignored.
- **Manager.** Must be an existing, active user (otherwise 400 `errors.managerId`). It is checked only when the manager changes.
- **Department.** An unknown department gives 400 `errors.departmentId`. An inactive one gives 422 `department.inactive`. Both are checked only when the department changes.
- **Last administrator.** "Last admin" means the last **active** user with the `Administrator` role.
- **No self-granted process authority (decided 2026-11-06).** An admin may not:
  - change their own roles (another admin must);
  - change their own manager link;
  - set themselves as anyone's manager, on create or update;
  - deactivate their own account (issue #27).

  Any of these gives 422 `user.self_administration`. The last-admin check runs first, so removing the last admin's own Administrator role, or deactivating the last admin, still says `user.last_admin`. Resetting **another** user's password stays allowed. Existing links aren't re-checked when other fields are edited, except that an admin can't reactivate a user who still reports to them.
- **Concurrent edits.** User edits carry no `version`, so between two administrators the last write wins. 409 `concurrency.conflict` appears only if the account changes during the request itself (e.g. the user changes their password at that moment).
- **Required fields.** `isActive` is required on user and department `PUT`, so a missing value can't deactivate by accident. Enum values must be strings; numbers such as `0` give 400.
- **Known races (accepted for Phase 1).** Two simultaneous demotions can remove the last administrator, and two simultaneous manager changes ("A reports to B" and "B reports to A") can create a cycle. Both rules are checked before acting, with no lock.
- **Audit.** Every user change, including a password reset, is logged with the acting admin's id and the target user's id (never the password).
- **Email.** It must also be a valid Identity user name: letters, digits and `-._@+`. Anything else gives 400 `errors.email`.

### Stalled tasks
`GET /api/admin/stalled-tasks?page&pageSize` → 200 `Paged<StalledTaskDto>`
`type StalledTaskDto = { taskId; requestId; requestNumber; requestType; stage; assignee: { user: UserRef | null; role: Role | null } /* exactly one non-null */; reason: "NoEligibleRoleMember" | "AssigneeInactive"; openSince: string }`
No request details (privacy): admins see metadata only.

## 9. Real-time (SignalR)

Hub: `/hubs/notifications` (cookie auth; same origin). Users are mapped to their own connection group by user id. Server → client only; clients never invoke hub methods in Phase 1.

| Event | Payload | Client reaction |
|---|---|---|
| `notificationCreated` | `{ notificationId: string; requestId: string \| null }` | invalidate `notifications.*`, `dashboard` |
| `requestChanged` | `{ requestId: string }` | invalidate `requests.detail(id)`, `requests.activity(id)`, `requests.lists`, `dashboard` |
| `tasksChanged` | `{}` | invalidate `tasks.*`, `dashboard` |

Payloads are **hints**: never render data from them. Sent only after the database commit. On reconnect the client invalidates everything listed above.

`requestChanged` goes to everyone who can currently see the request **and is connected**; `tasksChanged` goes to the decider, the next assignee(s), and members of affected role queues.

---

## 10. Changing this contract

1. The agent who needs the change edits this file **and** the DTO/controller in the same PR, regenerates `contracts/openapi.json` (`./scripts/export-openapi.sh`, created in BE-03), and regenerates frontend types (`npm run api:types` in `src/frontend`).
2. Breaking changes (rename/remove field, change type, new required input) require noting it in the PR description under **Contract change** and updating every frontend usage in the same PR, or splitting into additive-then-remove PRs.
3. Never add a field to the frontend types by hand. `schema.d.ts` is generated and lint-protected.
4. CI fails if `contracts/openapi.json` differs from what the API produces.
