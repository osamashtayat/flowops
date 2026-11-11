# UI Specification — FlowOps Phase 1 and Phase 2a

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
| `/admin/role-changes` | RoleChangesPage | `admin.manage` | `GET /admin/role-changes` |
| `/admin/stalled-tasks` | StalledTasksPage | `admin.manage` | `GET /admin/stalled-tasks` |
| `/admin/workflows` *(Phase 2a, P2-FE-03)* | WorkflowsPage | `workflows.manage` | `GET /admin/workflows` |
| `/admin/workflows/:workflowId` *(Phase 2a)* | WorkflowEditorPage (Draft editor + version history) | `workflows.manage` | `GET …/versions`, `GET …/versions/:id`, `POST …/draft`, `PUT …/versions/:id`, `…/validate`, `…/preview`, `…/publish` |
| `/forbidden` | ForbiddenPage (403) | — | — |
| `*` | NotFoundPage | — | — |

Guards read `capabilities` from the session (`useSession()`), never role names. A failed guard routes to `/forbidden`; the API remains the real enforcement.

Filters, search, tab and page live in the **URL query string** so views are shareable and the back button works.

## 2. Session behaviour
- App start: `GET /auth/me`. 401 → `/login?returnTo=…`. `mustChangePassword` → `/change-password` (only route allowed).
- Any API 401 mid-session → clear query cache → `/login?returnTo=<current>` with toast "Your session has ended. Please sign in again."
- Any API 403 on a page load → ForbiddenPage; on an action → error toast with the mapped message.
- Logout → `POST /auth/logout` → clear cache → `/login`.

### 2.1 Connection lost (FE-15)
- One connectivity state for the whole app (`lib/connectivity`). It counts as lost when the browser fires `offline` (→ *offline*) or any query or mutation fails without reaching the API: a network failure, or a 502/503/504 from the reverse proxy (→ *server unreachable*). Any successful call, or a passing `GET /api/health`, makes it online again.
- While lost, TanStack Query is told it's offline: queries pause instead of failing (cached data stays on screen, a page with nothing cached keeps its skeleton) and refetch when the connection is back. No stream of failing requests; the realtime hub stops retrying and reconnects once the connection is back.
- While the server is unreachable, `GET /api/health` is checked after 2 s, 5 s, 15 s, then every 30 s. With the browser offline, nothing is polled; the check runs on the browser's `online` event.
- `ConnectionBanner` (sticky, top of every page, signed in or not): "You're offline. Changes can't be saved until your connection is back." / "Can't reach FlowOps right now. Changes can't be saved until the connection is back." + **Try again** (checks at once, spinner while checking). Back online → success toast "You're back online."
- Mutations never wait for the connection (`networkMode: 'always'`): an action taken while lost fails at once with "Can't reach the server. Check your connection and try again." and is never sent later on its own (a decision must not be replayed silently).
- Dev server only: when the local API is down, Vite's proxy answers 500, which reads as a server error rather than "unreachable". nginx (Docker/production) answers 502 and shows the banner.

## 3. Navigation by capability

| Item | Shown when |
|---|---|
| Dashboard, My requests, Notifications | always |
| New request (primary button in sidebar) | `requests.submit` |
| Inbox (with open count) | `tasks.view` |
| Team requests | `tasks.view` |
| Administration ▸ Users / Departments / Role changes / Stalled tasks | `admin.manage` |
| Administration ▸ Workflows *(Phase 2a)* | `workflows.manage` |

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
Actions: buttons from `availableActions` → ConfirmDialog → decision mutation with `version`. 409 → toast with mapped message + refetch. If a refresh removes the task (or the open action) while its dialog is open and nothing was sent yet, the dialog closes with an info toast "FO-… is no longer waiting for your decision, so the dialog was closed." (same in the inbox and the role-changes list; `useClosedByRefreshNotice`).
Leave requests only: the comment box and the decision dialog's comment / reason field say "Visible to everyone who can see this request. Don't repeat the leave reason." (free text isn't masked, the leave reason is; AUTHORIZATION §5). Same dialog from the inbox. The text lives in `lib/format/visibility.ts`.
States: skeleton; 404 → NotFound content ("This request doesn't exist or you don't have access."); error with retry.

### 4.6 Inbox
Tabs Open / Completed (URL `?status=Open|Done`). List of InboxItem rows: request number + title, requester, department, `keyDetail`, stage, age ("2 d"), actions inline (Approve / Reject / Fulfill / Confirm per kind+stage) and a "View" link. Bulk actions: none in Phase 1.
After any decision: invalidate tasks + request detail + dashboard; the item leaves the Open list.
Empty Open: "You're all caught up." Completed tab shows outcome badge and decided date.

### 4.7 Notifications
Bell in TopBar with unread count (`aria-label="Notifications, 3 unread"`); click opens a popover with the latest 8 and "View all". Full page: list with unread highlighted (`bg-primary-soft` + dot), "Mark all as read". Clicking an item marks it read (optimistic) and navigates to the request.

### 4.8 Administration
- **Users:** table (name, email, department, manager, roles as small neutral chips, status), search + department + role filters, "New user". UserFormPage: display name, email (create only), initial password (create only), department (select), manager (searchable select of active users, excluding self), roles (checkbox group), active toggle (edit only). "Reset password" in a secondary section with confirm. Errors `user.manager_cycle`, `user.last_admin`, `user.self_administration` shown as banner messages. On the signed-in administrator's own record the manager and the active toggle are read-only with a short explanation (own roles go through a role change, below), and the manager picker never offers the signed-in administrator.
- **Departments:** table (name, code, members, status) + create/edit dialog.
- **Role changes** (`/admin/role-changes`, ADR-0012 §8): filter by status (default Pending); table of administrator, proposed roles with "Now: …", proposed time, status badge with decision details (and the rejection reason) and Approve / Reject / Cancel buttons shown only from the server's `availableActions`, each behind a confirmation dialog (reject needs a reason). A 409/422 shows the mapped message in the dialog and refetches the list. The edit-user form never offers to *add* Administrator to an existing user (create still does); on your own record, saving changed roles shows "Role change sent. Waiting for another administrator." and opens this list. Manager and Active stay read-only on your own record.
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
Defaults: `staleTime` 30 s, refetch on window focus, retry 1 for queries (never for 4xx), 0 for mutations; queries pause while the connection is lost and mutations fail at once (`networkMode: 'always'`), see §2.1.

---

## 7. Phase 2a: workflows, route and skipped steps (ADR-0014)

Status: **designed** (P2-DOC-01). Built in P2-FE-01 to P2-FE-03 against the contract in API_CONTRACT §4 and §10. Until the backend lands, MSW mocks are generated from `contracts/openapi.json`, as in Phase 1. Built only from `components/ui` and `components/layout`, with theme tokens only.

### 7.1 Steps instead of stages (P2-FE-01)
- Everywhere a stage is shown today (lists' Stage column, "Waiting for: …", the inbox, the approval-steps card, timeline sentences, stalled tasks), show `step.name` from the server. A closed request (`step: null`) shows its status only.
- The frontend has **no step names or keys of its own**. `requestStageLabel` and the stage switches in `lib/format/status.ts`, `features/tasks/decisions.ts` and the mocks go away. Logic that needs the kind of task (Approve/Reject vs Fulfill, "Confirm") uses `kind` and `availableActions`, never a key.
- The "Confirm" label for HR approval (WORKFLOWS §3.1) becomes plain "Approve". Its only source was the hardcoded `HrConfirmation` stage. The step's name already says "HR confirmation".

### 7.2 Planned route and skipped steps (P2-FE-02)
- **Request details, "Route" card** (replaces the approval-steps stepper; it keeps its per-task detail). One row per `route` entry, in order:
  - `Done`: check icon, the task's outcome, decider and time;
  - `Current`: highlighted with `bg-primary-soft` and `aria-current="step"`, with "Waiting for …";
  - `Planned`: muted text, "Next" on the first one;
  - `Skipped`: muted, skip icon, "Skipped", and the condition text from the matching `StepSkipped` activity.

  Screen readers get the state in words ("Finance approval, skipped"), not only by colour or icon.
- **Timeline.** `StepSkipped` reads "The Finance approval step was skipped", with the condition as the entry's body.
- **New request forms.** Under the form, "What happens next" shows the route from `POST /api/requests/route-preview`, e.g. "Manager approval → Procurement fulfilment". It updates (debounced 300 ms) when the amount, leave type or dates change and are valid. It is hidden while they are invalid; on error it says "Route preview unavailable", which never blocks submitting. The success toast says "Request FO-… submitted." The "Your manager has been notified" part is dropped, because the first step is the workflow's.

### 7.3 Workflow editor (P2-FE-03)
- **WorkflowsPage.** A table with one row per request type: name, the Published version's number and "published by … on …", a "Draft in progress" badge when there is one, and an "Edit" link.
- **WorkflowEditorPage** has two tabs, *Draft* and *History*.
  - **No draft:** a read-only view of the Published steps and an "Edit workflow" button (`POST …/draft`).
  - **Draft step list:** one card per step. Each card has:
    - name (Input) and key (Input, filled from the name until edited; monospace);
    - kind (Select: Approval / Fulfillment);
    - "Who approves" (Select of `catalogue.assigneeRules`, then a role, department or user picker as the rule needs; Administrator never offered);
    - an optional "Only if" row: fact, operator and value Selects from `catalogue`, with a money input for `Money` facts and a multi-select for `IsOneOf`. The first step has no "Only if" row, with the helper text "The first step always runs";
    - "Can read restricted details" (toggle). Switching it on opens a warning: "People assigned to this step will see leave reasons on requests that use this version."
    - Move up / Move down buttons (keyboard reordering, WCAG; drag is optional) and Remove.

    Under the list are "Add step", "Save draft" (`PUT`, sends `version`), "Discard draft" (ConfirmDialog) and "Check" (`validate`).
  - **Issues panel.** Errors (danger) and warnings (warning) from `WorkflowValidationDto`, each linked to its step card (`stepKey`) and field; the cards show the same messages inline. Publish is disabled while errors exist.
  - **Preview panel.** Sample-fact inputs for the request type (amount; leave type and days), then "Preview", which shows the route like §7.2, skipped steps included.
  - **Publish.** "Publish version N" opens a ConfirmDialog: "New requests will use version N. Requests already in progress keep the version they started with. This is recorded with your name." On success, a toast and the History tab.
  - **History tab.** Every version (number, status badge, created by/at, published by/at, retired at). Any version can be opened read-only.
- **Errors.** 409 `concurrency.conflict` on save or publish: "Someone else changed this draft. Reload to see their changes", with a Reload button (no silent overwrite). 409 `workflow.draft_exists` opens the existing draft. 422 `workflow.invalid` re-runs Check and shows the issues.
- **States:** skeletons, ErrorState with retry, and EmptyState for a version history of one.
- **Permissions** come from `workflows.manage` only, never from role names.
