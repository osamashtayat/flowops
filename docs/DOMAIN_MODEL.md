# Domain Model — FlowOps Phase 1

**Purpose:** the single source of truth for entities, fields, statuses, invariants and state transitions.
If code and this document disagree, the code is wrong or this document must be updated in the same change (see `AGENT_HANDOFF.md`).

Related: `WORKFLOWS.md` (process behaviour), `AUTHORIZATION.md` (who may see/do what), ADR-0005 (state model), ADR-0006 (concurrency).

---

## 1. Vocabulary (use these names exactly)

| Term | Meaning |
|---|---|
| **Request** | A business request submitted by an employee (equipment purchase or leave). The aggregate root. |
| **Requester** | The user who submitted the request. Always the authenticated user at submission time. |
| **Process** | The fixed, ordered list of steps a request type goes through. Phase 1 has two, defined in code (`FixedProcesses`). |
| **Step / Stage** | One position in a process (e.g. `FinanceApproval`). `Request.Stage` is the *current* step. |
| **WorkTask** | A unit of work someone must act on: an approval decision or a fulfillment action. One per step. |
| **Assignee** | Who may act on a task: a *specific user* (manager step) or a *role queue* (officer steps). |
| **Decision** | The outcome recorded on a WorkTask (`Approved`, `Rejected`, `Fulfilled`) plus actor, time, comment. |
| **Activity** | An append-only audit record on a request (submitted, task created, decided, commented, …). |
| **Notification** | A persisted message to one user, optionally linked to a request/task. |

Avoid synonyms in code: not "ticket", "case", "approval item", "job".

---

## 2. Entities

All ids are `Guid` (UUIDv7 via `Guid.CreateVersion7()` for index locality). All timestamps are `DateTimeOffset` in UTC (`timestamptz`), obtained from the injected `TimeProvider`.

### 2.1 Department

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| Name | string(100) | required, unique (case-insensitive) |
| Code | string(16) | required, unique, upper-case `A-Z0-9-` |
| IsActive | bool | inactive departments cannot receive new members |
| CreatedAt | DateTimeOffset | |

### 2.2 ApplicationUser (extends `IdentityUser<Guid>`)

| Field | Type | Rules |
|---|---|---|
| DisplayName | string(100) | required |
| Email / UserName | string(256) | Identity; username = email |
| DepartmentId | Guid? | required before the user can submit requests |
| ManagerId | Guid? | self-reference; **no self-management, no cycles** |
| IsActive | bool | inactive users cannot sign in and are never assigned tasks |
| MustChangePassword | bool | set when an admin creates/resets the account |
| CreatedAt | DateTimeOffset | |

**Decision:** one department per user in Phase 1. The prompt's `EmployeeDepartmentMembership` join entity is not introduced (YAGNI); a membership table is a Phase 3 multi-company concern. See `ROADMAP.md`.

**Roles** (ASP.NET Core Identity roles, constants in `FlowOps.Domain.Organization.Roles`):
`Administrator`, `Employee`, `FinanceOfficer`, `HrOfficer`, `ProcurementOfficer`.

**"Manager" is not a role.** A user *is a manager of X* iff `X.ManagerId == user.Id`. The capability `isManager` (has ≥ 1 active direct report) is computed, never assigned. Rationale: approval routing uses the relationship, so a separate role could only ever disagree with it.

### 2.3 Request (aggregate root)

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| Number | string(20) | human-readable, unique: `FO-{yyyy}-{seq:000000}` from a PostgreSQL sequence |
| Type | `RequestType` | `EquipmentPurchase` \| `Leave` |
| RequesterId | Guid | from authenticated context only |
| DepartmentId | Guid | **snapshot** of the requester's department at submission (server-side) |
| Status | `RequestStatus` | see §3.1 |
| Stage | `RequestStage` | see §3.2 |
| SubmittedAt | DateTimeOffset | |
| ClosedAt | DateTimeOffset? | set when Status becomes `Completed` or `Rejected` |
| Version | uint (`xmin`) | optimistic concurrency token |
| Purchase | `PurchaseDetails?` | present iff Type = EquipmentPurchase |
| Leave | `LeaveDetails?` | present iff Type = Leave |
| Tasks | `List<WorkTask>` | owned by the aggregate |
| Activities | `List<RequestActivity>` | append-only, owned by the aggregate |

#### PurchaseDetails (1:1, table `purchase_request_details`)

| Field | Type | Rules |
|---|---|---|
| ItemName | string(150) | required |
| Description | string(2000) | required |
| EstimatedCost | `Money` | Amount > 0 and ≤ 1,000,000.000; Currency ∈ {`JOD`} in Phase 1 |
| Justification | string(2000) | required |

#### LeaveDetails (1:1, table `leave_request_details`)

| Field | Type | Rules |
|---|---|---|
| StartDate | DateOnly | required, ≥ today (company time zone, see below) |
| EndDate | DateOnly | required, ≥ StartDate, span ≤ 90 calendar days |
| Days (derived) | int | counted inclusively: `Days = (EndDate − StartDate) + 1`, so a same-day leave is 1 day; at most `RequestLimits.LeaveMaxDays` = 90 (EndDate − StartDate ≤ 89) |
| LeaveType | `LeaveType` | `Annual` \| `Sick` \| `Unpaid` \| `Other` |
| Reason | string(1000)? | optional; **sensitive** (see `AUTHORIZATION.md` §5) |

"Today" is evaluated in the configured company time zone (`FlowOps:CompanyTimeZone`, default `Asia/Amman`), not server UTC.

### 2.4 Money (value object)

`record Money(decimal Amount, string Currency)`. Stored as `numeric(18,3)` + `char(3)`. JOD has three minor-unit decimals, hence scale 3. Construction validates the currency against `SupportedCurrencies` and rejects more than 3 decimals. No arithmetic is needed in Phase 1; don't add any.

### 2.5 WorkTask (entity inside Request)

One entity for both approval and fulfillment work. See ADR-0005 for why `ApprovalTask`, `FulfillmentTask` and `ApprovalDecision` are **not** separate entities.

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| RequestId | Guid | |
| Kind | `WorkTaskKind` | `Approval` \| `Fulfillment` |
| Stage | `RequestStage` | the step this task belongs to |
| AssigneeUserId | Guid? | exactly one of AssigneeUserId / AssigneeRole is set (DB check constraint) |
| AssigneeRole | string(32)? | a role name from `Roles` |
| Status | `WorkTaskStatus` | `Open` \| `Done` |
| Outcome | `WorkTaskOutcome?` | `Approved` \| `Rejected` \| `Fulfilled`; null iff Open |
| DecidedById | Guid? | null iff Open |
| DecidedAt | DateTimeOffset? | null iff Open |
| Comment | string(2000)? | **required when Outcome = Rejected** (the rejection reason) |
| CreatedAt | DateTimeOffset | |
| Version | uint (`xmin`) | optimistic concurrency token |

Valid (Kind, Outcome) pairs: Approval → {Approved, Rejected}; Fulfillment → {Fulfilled}. Procurement cannot reject in Phase 1 (approvals already happened; a failed purchase is an open question, see `PRD.md` §8).

### 2.6 RequestActivity (append-only)

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| RequestId | Guid | |
| Type | `ActivityType` | `Submitted`, `TaskAssigned`, `Approved`, `Rejected`, `Fulfilled`, `Completed`, `CommentAdded` |
| ActorId | Guid? | null for system-generated entries (e.g. `TaskAssigned`, `Completed`) |
| TaskId | Guid? | |
| Stage | `RequestStage?` | the step the activity relates to (null for `CommentAdded`) |
| Body | string(2000)? | comment text / rejection reason |
| OccurredAt | DateTimeOffset | |

Activities are never updated or deleted. Free-form comments are activities of type `CommentAdded` — there is no separate comment table.

### 2.7 Notification

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| RecipientId | Guid | |
| Type | `NotificationType` | `TaskAssigned`, `RequestApproved`, `RequestRejected`, `RequestCompleted`, `StepApproved`, `CommentAdded` |
| Title | string(150) | rendered server-side; no HTML |
| Body | string(500) | rendered server-side; no HTML; **never contains the leave reason** |
| RequestId | Guid? | |
| TaskId | Guid? | |
| CreatedAt | DateTimeOffset | |
| ReadAt | DateTimeOffset? | |

Role-queue tasks notify **every eligible member** of the role (excluding the requester) — one row per recipient.

---

## 3. Statuses and state machines

Three separate state dimensions, as required:

1. **Request.Status** — what a user cares about ("where is my request overall?").
2. **Request.Stage** — which step of the process is current.
3. **WorkTask.Status/Outcome** — the state of one unit of work.

### 3.1 RequestStatus

| Value | Meaning | UI label |
|---|---|---|
| `InReview` | At least one approval step is still open | Pending review |
| `Approved` | All approvals granted; awaiting fulfillment (purchase only) | Approved |
| `Completed` | Process finished successfully | Completed |
| `Rejected` | An approver rejected; process terminated | Rejected |

`Completed` and `Rejected` are **terminal**.

### 3.2 RequestStage

`ManagerApproval`, `FinanceApproval`, `HrConfirmation`, `ProcurementFulfillment`, `Closed`.

### 3.3 Fixed processes

Defined once, in `FlowOps.Domain.Requests.FixedProcesses`:

```
EquipmentPurchase:
  1. ManagerApproval        Kind=Approval     Assignee=RequesterManager
  2. FinanceApproval        Kind=Approval     Assignee=Role(FinanceOfficer)
  3. ProcurementFulfillment Kind=Fulfillment  Assignee=Role(ProcurementOfficer)

Leave:
  1. ManagerApproval        Kind=Approval     Assignee=RequesterManager
  2. HrConfirmation         Kind=Approval     Assignee=Role(HrOfficer)
```

A `ProcessStep` is `record ProcessStep(RequestStage Stage, WorkTaskKind Kind, AssigneeRule Assignee)`. This is plain C# data, not a configurable engine. It is the seam Phase 2 replaces with persisted workflow definitions (ADR-0004).

### 3.4 Request transitions

| From (Status / Stage) | Trigger | To |
|---|---|---|
| — | Submit | `InReview` / `ManagerApproval` (+ manager task) |
| `InReview` / approval step *n* | Approve, more approval steps follow | `InReview` / step *n+1* (+ next task) |
| `InReview` / last approval step, next is fulfillment | Approve | `Approved` / `ProcurementFulfillment` (+ fulfillment task) |
| `InReview` / last step overall (leave HR) | Approve | `Completed` / `Closed` |
| `InReview` / any approval step | Reject | `Rejected` / `Closed` |
| `Approved` / `ProcurementFulfillment` | Fulfill | `Completed` / `Closed` |
| `Completed` or `Rejected` | anything | **error** `request.closed` (409) |

Status is **derived inside the aggregate** from the step transition; no caller ever sets `Status` or `Stage` directly (private setters).

### 3.5 WorkTask transitions

```
Open ──Approve──▶ Done(Approved)
Open ──Reject───▶ Done(Rejected)     (Kind=Approval only, comment required)
Open ──Fulfill──▶ Done(Fulfilled)    (Kind=Fulfillment only)
Done ──anything─▶ error task.already_decided (409)
```

---

## 4. Invariants

Enforced in the domain (D), application layer (A), and/or database (DB). "Where" lists every layer that guards it; the domain is the primary guard for business rules.

| # | Invariant | Where | Error code |
|---|---|---|---|
| I1 | Only an eligible actor can decide a task (assignee user, or holder of assignee role) | A (`TaskEligibility`) | `task.not_eligible` (403) |
| I2 | The requester can never decide a task on their own request | D + A | `task.self_decision` (403) |
| I3 | A task can be decided once | D + DB (xmin) | `task.already_decided` (409) |
| I4 | At most one Open task per request | D + DB (partial unique index `ux_work_tasks_open_per_request`) | — |
| I5 | Only the task matching the request's current Stage can be decided | D | `task.not_current` (409) |
| I6 | Closed requests cannot change | D | `request.closed` (409) |
| I7 | Steps run strictly in `FixedProcesses` order; no skipping | D (only `AdvanceTo(next)` exists) | — |
| I8 | Reject requires a non-blank comment | D + API validation | `validation` (400) |
| I9 | Every state change appends an Activity with actor and time | D (same method appends) | — |
| I10 | Request, task and activity changes commit atomically | A (one `SaveChangesAsync` per use case) | — |
| I11 | Concurrent decisions: exactly one succeeds | DB (`xmin`) + A maps conflict | `concurrency.conflict` (409) |
| I12 | Submission requires an assigned, active manager who is not the requester | A | `request.manager_not_assigned` (422) |
| I13 | A user's manager chain has no cycles and no self-reference | A (`ManagerAssignmentRules`) + DB check (no self) | `user.manager_cycle` (422) |
| I14 | Requester and department come from server context, never the client | A | — |
| I15 | Process state survives restarts | Persistence only — there is no in-memory process state | — |

---

## 5. Aggregate API (shape, not final code)

```csharp
public sealed class Request
{
    public static Request SubmitPurchase(RequestNumber number, UserRef requester, Guid departmentId,
        PurchaseDetails details, Guid managerId, DateTimeOffset now);
    public static Request SubmitLeave(/* … */);

    // The only way state advances. Validates I2, I3, I5, I6, I8; appends activities (I9);
    // creates the next task or closes the request. Returns what happened so the
    // application layer can create notifications without re-deriving the outcome.
    public DecisionResult DecideTask(Guid taskId, Guid actorId, TaskDecision decision,
        Func<ProcessStep, Assignee> resolveAssignee, DateTimeOffset now);

    public void AddComment(Guid actorId, string body, DateTimeOffset now);
}
```

The domain does **not** decide *who* is eligible for a role queue (that needs the user store); it receives the resolved assignee for the next step and checks only rules it can know (self-decision, current step, closed, already decided).

---

## 6. Database notes

- Naming: `snake_case` tables/columns (`EFCore.NamingConventions`).
- Enums stored as `text` (readable, migration-safe), with check constraints generated per enum.
- Indexes: `requests(requester_id, submitted_at desc)`, `requests(status)`, `work_tasks(assignee_user_id) where status='Open'`, `work_tasks(assignee_role) where status='Open'`, `notifications(recipient_id, created_at desc)`, `notifications(recipient_id) where read_at is null`, `request_activities(request_id, occurred_at)`.
- Partial unique index `ux_work_tasks_open_per_request on work_tasks(request_id) where status='Open'` (I4).
- Check: `num_nonnulls(assignee_user_id, assignee_role) = 1`; `manager_id <> id` on users.
- `request_number_seq` sequence for `Request.Number`.

**As built (BE-05 users/departments, BE-07 the rest; configurations in `Infrastructure/Persistence/Configurations`):**
- Enum and currency checks are generated from code (`CheckConstraints.InEnum<T>`, `CheckConstraints.In(column, SupportedCurrencies.All)`), so a new value lands in the next migration. Enum columns are `varchar(32)`.
- Extra checks beyond the list above: `ck_work_tasks_decision_iff_done` (outcome, decider and time are set exactly when Done), `ck_work_tasks_assignee_role` (a known role), `ck_purchase_request_details_amount` (0 < amount ≤ 1,000,000.000), `ck_leave_request_details_dates` (end ≥ start); department names unique case-insensitively (`ux_departments_name_ci` on `lower(name)`).
- Details tables are keyed by `request_id` (PK = FK to `requests`, cascade). Tasks and activities cascade with their request; everything referencing users, departments or tasks is `RESTRICT` (users and departments are deactivated, never deleted).
- `xmin` is mapped with `IsRowVersion()` on `Request.Version` and `WorkTask.Version`; it is a system column, so it doesn't appear in the DDL.
- Every FK has an index except `work_tasks.assignee_user_id`, which only has the open-task partial index, because users are never deleted.
- Domain types have a private parameterless constructor for materialization only; `RequestNumber.Parse` rebuilds the stored value.
- **Ids (BE-11).** Domain ids come from `Common/SequentialGuid.NewV7(now)`: UUIDv7 with a per-process counter in the same millisecond, so ordering by id is creation order. Lists sort by time then `id`, e.g. a request's `Submitted` before its first `TaskAssigned`, which share an instant. `Guid.CreateVersion7` randomises within the millisecond, so that order was not guaranteed before.
