# UI Specification — FlowOps Phase 1

**Purpose:** pages, routes, navigation, page composition, forms, interaction states, and role-specific experience.
Visual rules: `DESIGN_SYSTEM.md`. Data: `API_CONTRACT.md` (every field shown here exists there — do not invent fields).

---

## 1. Routes

| Path | Page | Guard | Primary API |
|---|---|---|---|
| `/login` | LoginPage | anonymous only | `POST /auth/login` |
| `/change-password` | ChangePasswordPage | auth (also forced when `mustChangePassword`) | `POST /auth/change-password` |
| `/` | DashboardPage | auth | `GET /dashboard` |
| `/requests` | MyRequestsPage | auth | `GET /requests?scope=mine` |
| `/requests/team` | TeamRequestsPage | `isManager` or any officer role (`tasks.view`) | `GET /requests?scope=visible` |
| `/requests/new` | NewRequestPage (type picker) | `requests.submit` | — |
| `/requests/new/purchase` | NewPurchasePage | `requests.submit` | `POST /requests/purchase` |
| `/requests/new/leave` | NewLeavePage | `requests.submit` | `POST /requests/leave` |
| `/requests/:id` | RequestDetailsPage | auth (404 from API → NotFound) | `GET /requests/:id`, `/activity` |
| `/inbox` | InboxPage (tabs: Open / Completed) | `tasks.view` | `GET /tasks` |
| `/notifications` | NotificationsPage | auth | `GET /notifications` |
| `/admin/users` | UsersPage | `admin.manage` | `GET /users` |
| `/admin/users/new`, `/admin/users/:id` | UserFormPage | `admin.manage` | `POST/PUT /users`, `/roles` |
| `/admin/departments` | DepartmentsPage (inline create/edit dialogs) | `admin.manage` | `/departments` |
| `/admin/stalled-tasks` | StalledTasksPage | `admin.manage` | `GET /admin/stalled-tasks` |
| `/forbidden` | ForbiddenPage (403) | — | — |
| `*` | NotFoundPage | — | — |

Guards read `capabilities` from the session (`useSession()`), never role names. A failed guard routes to `/forbidden`; the API remains the real enforcement.

Filters, search, tab and page live in the **URL query string** so views are shareable and the back button works.

## 2. Session behaviour
- App start: `GET /auth/me`. 401 → `/login?returnTo=…`. `mustChangePassword` → `/change-password` (only route allowed).
- Any API 401 mid-session → clear query cache → `/login?returnTo=<current>` with toast "Your session has ended. Please sign in again."
- Any API 403 on a page load → ForbiddenPage; on an action → error toast with the mapped message.
- Logout → `POST /auth/logout` → clear cache → `/login`.

## 3. Navigation by capability

| Item | Shown when |
|---|---|
| Dashboard, My requests, Notifications | always |
| New request (primary button in sidebar) | `requests.submit` |
| Inbox (with open count) | `tasks.view` |
| Team requests | `tasks.view` |
| Administration ▸ Users / Departments / Stalled tasks | `admin.manage` |

## 4. Pages

Each page = `PageHeader` + content built from shared components. States listed are **required**, not optional.

### 4.1 Login
Centered card on canvas, product name, email, password, "Sign in" (primary, full width). Errors inline above the form: invalid credentials (generic; also covers a locked-out account, so the message mentions the 15-minute pause) and rate limited (429: "Too many sign-in attempts. Please wait a few minutes and try again."). No "register" link. Submit on Enter. Autocomplete attributes set.

### 4.2 Dashboard (role-aware)
Order = what needs attention first:
1. **Tasks card** (if `myTasks`): "3 tasks waiting for you · oldest 2 days" → button "Open inbox".
2. **My requests summary**: four stat tiles (Pending review / Approved / Completed / Rejected), each linking to `/requests?status=…`.
3. **Recent requests** (≤ 5 rows, compact table) + "View all".
4. **Recent activity** (≤ 10 timeline entries with request numbers linked).
5. **Admin card** (if `admin`): users, departments, stalled tasks (warning tone if > 0).
States: skeleton tiles; per-card error with retry (one failing card doesn't blank the page — the endpoint is one call, so error is page-level ErrorState); employee with zero requests sees EmptyState with "New request".

### 4.3 My requests / Team requests
FilterBar (status select, type select, search input with 300 ms debounce) → DataTable columns: Number · Title · Type · Status (badge) · Stage · Submitted · (Team only) Requester.
States: loading skeleton rows; empty ("No requests match these filters" + Clear filters, or first-time empty with New request); error with retry. Pagination below.

### 4.4 New request
Type picker: two large selectable cards ("Equipment purchase — Laptops, monitors, tools", "Leave — Annual, sick, unpaid") → form page.

**Purchase form** — read-only "Requested by" and "Department" rows from session (if department/manager missing: blocking banner explaining to contact admin, form disabled). Fields: Item name, Description (textarea), Estimated cost (numeric input with "JOD" suffix, 3 decimals), Business justification (textarea). Buttons: Submit request (primary), Cancel (ghost, back).
**Leave form** — Start date, End date (min = start), computed "N calendar days" hint, Leave type (select), Reason (optional textarea, helper: "Visible only to your manager and HR").
Validation: zod schemas in `features/requests/schemas.ts` mirroring `API_CONTRACT.md` limits; on submit errors focus the first invalid field. Server 400 `errors` are mapped onto fields; 422 shown as a banner.
Success: navigate to `/requests/:id`, toast "Request FO-… submitted. Your manager has been notified."

### 4.5 Request details
Header: "FO-2026-000123 · Dell XPS 15" + StatusBadge + stage text ("Waiting for: Finance approval").
Layout ≥ 1024: two columns — left (2/3): Details card (fields by type; leave reason shows "Restricted" when `reasonHidden`), Approval steps card (WorkTaskDto list as a stepper: each step with assignee, outcome, decided by/at, comment); right (1/3): Summary card (requester, department, submitted, closed), Actions card (only if `availableActions`), Activity timeline + comment box (if `canComment`).
Actions: buttons from `availableActions` → ConfirmDialog → decision mutation with `version`. 409 → toast with mapped message + refetch.
States: skeleton; 404 → NotFound content ("This request doesn't exist or you don't have access."); error with retry.

### 4.6 Inbox
Tabs Open / Completed (URL `?status=Open|Done`). List of InboxItem rows: request number + title, requester, department, `keyDetail`, stage, age ("2 d"), actions inline (Approve / Reject / Fulfill / Confirm per kind+stage) and a "View" link. Bulk actions: none in Phase 1.
After any decision: invalidate tasks + request detail + dashboard; the item leaves the Open list.
Empty Open: "You're all caught up." Completed tab shows outcome badge and decided date.

### 4.7 Notifications
Bell in TopBar with unread count (`aria-label="Notifications, 3 unread"`); click opens a popover with the latest 8 and "View all". Full page: list with unread highlighted (`bg-primary-soft` + dot), "Mark all as read". Clicking an item marks it read (optimistic) and navigates to the request.

### 4.8 Administration
- **Users:** table (name, email, department, manager, roles as small neutral chips, status), search + department + role filters, "New user". UserFormPage: display name, email (create only), initial password (create only), department (select), manager (searchable select of active users, excluding self), roles (checkbox group), active toggle (edit only). "Reset password" in a secondary section with confirm. Errors `user.manager_cycle`, `user.last_admin`, `user.self_administration` shown as banner messages. On the signed-in administrator's own record roles, manager and the active toggle are read-only with a short explanation, and the manager picker never offers the signed-in administrator.
- **Departments:** table (name, code, members, status) + create/edit dialog.
- **Stalled tasks:** table (request number, type, stage, assignee, reason, open since) with guidance text per reason ("Add an active Finance officer").

### 4.9 401/403/404
Simple centered panels with an explanation and a link to Dashboard. No technical detail.

## 5. Shared frontend building blocks (build once)
`AppShell`, `Sidebar`, `TopBar`, `PageHeader`, `Button`, `Input`, `Textarea`, `Select`, `DateInput`, `MoneyInput`, `FormField`, `Card`, `StatusBadge`, `DataTable`, `Pagination`, `FilterBar`, `Dialog`, `ConfirmDialog`, `DecisionDialog` (wraps ConfirmDialog with comment/reason field and decision mutation), `Toast`, `EmptyState`, `ErrorState`, `Skeleton`, `ActivityTimeline`, `RequestSummaryRow`, `NotificationBell`.
Hooks: `useSession`, `useCapability`, `useUrlState` (filters/page in URL), `useDecideTask` (one hook for approve/reject/fulfill).
Utilities: `lib/format/{date,money,status}.ts`, `lib/api/problem.ts` (`toUserMessage(problem)`).

## 6. Query keys (central, `lib/api/queryKeys.ts`)
```ts
session:        ['session']
dashboard:      ['dashboard']
requests.list:  ['requests', 'list', params]
requests.detail:['requests', 'detail', id]
requests.activity: ['requests', 'activity', id]
tasks.list:     ['tasks', 'list', params]
notifications.list: ['notifications', 'list', params]
notifications.unread: ['notifications', 'unread']
admin.users / admin.user(id) / admin.departments / admin.stalled
```
Defaults: `staleTime` 30 s, refetch on window focus, retry 1 for queries (never for 4xx), 0 for mutations.
