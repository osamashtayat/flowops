# Product Requirements — FlowOps

**Purpose:** what FlowOps does and for whom, Phase 1 scope, and acceptance criteria. Technical detail lives elsewhere; this document says *what* and *why*.

---

## 1. Problem

In small and medium-sized companies, internal requests (buy a laptop, take leave) travel through email, chat and spreadsheets. Employees can't see where a request is; managers lose approvals in their inboxes; departments have no queue; administrators have no overview.

## 2. Product

FlowOps centralises internal requests into tracked processes. An employee submits a request once; FlowOps routes it through the required approvals, tells each approver what is waiting for them, records every decision, and tells the employee the outcome.

Phase 1 ships two fixed processes (equipment purchase, leave). Later phases let administrators define their own (`ROADMAP.md`).

## 3. Users

| Persona | Goal | Primary screens |
|---|---|---|
| **Employee** (Lina, sales) | Submit a request in under 2 minutes and know its status without asking anyone | New request, My requests, Request details |
| **Manager** (Karim, sales lead, 6 reports) | Clear pending approvals fast, with enough context to decide | Approval inbox, Request details, Dashboard |
| **Finance officer** | Review purchase costs after manager sign-off | Approval inbox |
| **HR officer** | Confirm approved leave | Approval inbox |
| **Procurement officer** | Know what to buy and mark it done | Approval inbox (fulfillment tasks) |
| **Administrator** (IT/ops) | Keep users, departments and reporting lines correct | Administration |

## 4. Phase 1 scope

**In:** sign-in, admin-created accounts, departments, reporting lines, roles; equipment purchase and leave processes; approval inbox; approve/reject/fulfill with reasons; comments; activity history; persistent + live notifications; role-aware dashboard; stalled-task list for admins; Docker-based local setup; automated tests.

**Out (explicitly):** configurable workflows, form builder, request drafts, withdrawal/cancellation, task reassignment, delegation/out-of-office, escalations and reminders, email/SMS, file attachments, leave balances, payroll, vendors/payments/inventory, multi-company, dark mode, mobile apps, SSO.

## 5. User stories and acceptance criteria

IDs map to backlog tasks in `PHASE1_TASKS.md`.

### Authentication
- **US-01** As any user I sign in with email and password.
  - AC: wrong credentials → generic "Email or password is incorrect" (no user enumeration); 5 failures → 15-minute lockout; session survives page reload; sign-out ends it server-side.
- **US-02** As a user created by an admin, I must set my own password at first sign-in.
  - AC: until changed, only the change-password screen is reachable.

### Submitting
- **US-10** As an employee I submit an equipment purchase (item, description, estimated cost in JOD, justification).
  - AC: department and submitter are filled in automatically and shown read-only; cost accepts up to 3 decimals; on success I land on the request details page showing "Pending review — Manager approval"; my manager gets a notification.
  - AC: if I have no manager, I see "You don't have a manager assigned. Contact your administrator." and nothing is created.
- **US-11** As an employee I submit a leave request (start, end, type, optional reason).
  - AC: end ≥ start; start not in the past; max 90 days; reason is visible only to me, my manager and HR.

### Tracking
- **US-20** I see my requests in a list with status, type, stage, submission date; I can filter by status/type, search by number or item name, and page.
- **US-21** I open a request and see its details, current stage, a timeline of every action (who, when, outcome, comment) and any rejection reason.
- **US-22** I can add a comment to a request I can see; participants are notified.

### Approving
- **US-30** As an approver I see an inbox of open tasks assigned to me or my role queue, oldest first, with request summary (number, type, requester, department, key detail, age).
  - AC: I can approve with an optional comment, or reject with a mandatory reason, via a confirmation dialog; the task disappears from my open list immediately.
  - AC: if someone else already decided it, I see "This task was already handled by someone else" and the inbox refreshes.
- **US-31** As a procurement officer I mark an approved purchase as fulfilled with an optional note.
- **US-32** As an approver I see my recently completed tasks.
- **US-33** I can never decide a task on my own request.

### Notifications
- **US-40** I see an unread count that updates live; I open a list, click a notification to go to the request, and mark one or all as read. Notifications persist across sessions.

### Dashboard
- **US-50** On sign-in I land on a dashboard showing what needs my attention first: open tasks count (if I approve), my requests by status, my 5 most recent requests, recent activity on my requests.
- **US-51** Administrators see counts of users, departments, and stalled tasks with a link to the stalled list.

### Administration
- **US-60** Admin creates, edits, deactivates users; sets department, manager and roles; sets an initial password.
  - AC: cannot set a user as their own manager or create a reporting cycle; cannot remove the last active Administrator; deactivated users cannot sign in.
- **US-61** Admin creates, renames, deactivates departments.
- **US-62** Admin views stalled tasks (role queues with no eligible member; tasks assigned to inactive managers).

## 6. Non-functional requirements

| Area | Requirement |
|---|---|
| Security | OWASP ASVS L1 basics; server-side authorization on every endpoint; see `AUTHORIZATION.md` |
| Accessibility | WCAG 2.2 AA for text contrast, focus visibility, keyboard operation, form labelling |
| Performance | p95 < 300 ms for list/detail endpoints with 10k requests on a laptop-class DB; lists always paged (max 100) |
| Reliability | No state held in memory; restart at any time loses nothing |
| Auditability | Every decision and comment recorded with actor and timestamp; activities are immutable |
| Browser support | Last 2 versions of Chrome, Edge, Firefox, Safari; layouts usable from 360 px width |
| Localisation | English UI; dates displayed in the company time zone (`Asia/Amman` default); currency JOD with 3 decimals |

## 7. Success criteria for Phase 1
- Both processes run end to end in the demo seed, including rejection paths.
- All `TESTING.md` §3 scenarios pass in CI.
- A new employee can submit a request with no instructions (hallway test with 3 people).
- An architecture review finds no business logic in controllers and no duplicated approval rules.

## 8. Open questions (owner: product)
1. Should requesters be able to withdraw a pending request? (Not in Phase 1.)
2. What happens when procurement cannot buy the item? Currently fulfillment cannot fail.
3. Should finance approval be skipped below a cost threshold? (Phase 2 conditional branch.)
4. Should managers see their reports' requests that they did not approve (read-only)? Currently yes (rule R2).
