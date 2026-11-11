# Domain Model — FlowOps Phase 1 and Phase 2a

**Purpose:** the single source of truth for entities, fields, statuses, invariants and state transitions.
If code and this document disagree, the code is wrong or this document must be updated in the same change (see `AGENT_HANDOFF.md`).

Related: `WORKFLOWS.md` (process behaviour), `AUTHORIZATION.md` (who may see/do what), ADR-0005 (state model), ADR-0006 (concurrency), ADR-0014 (configurable workflows, Phase 2a: §7).

---

## 1. Vocabulary (use these names exactly)

| Term | Meaning |
|---|---|
| **Request** | A business request submitted by an employee (equipment purchase or leave). The aggregate root. |
| **Requester** | The user who submitted the request. Always the authenticated user at submission time. |
| **Process** | The ordered list of steps a request type goes through. Phase 1 defined two in code (`FixedProcesses`); since P2-BE-03 they are the Published versions of persisted workflows (§7). |
| **Step / Stage** | One position in a process (e.g. `FinanceApproval`). `Request.Stage` is the *current* step. |
| **WorkTask** | A unit of work someone must act on: an approval decision or a fulfillment action. One per step. |
| **Assignee** | Who may act on a task: a *specific user* (manager step) or a *role queue* (officer steps). |
| **Decision** | The outcome recorded on a WorkTask (`Approved`, `Rejected`, `Fulfilled`) plus actor, time, comment. |
| **Activity** | An append-only audit record on a request (submitted, task created, decided, commented, …). |
| **Notification** | A persisted message to one user, optionally linked to a request/task. |
| **Workflow** *(Phase 2a)* | The configurable process of one request type (`WorkflowDefinition`). It has numbered **versions**; exactly one is Published. See §7. |
| **Step key** *(Phase 2a)* | A step's stable slug inside a workflow version (`finance-approval`). It replaced the `RequestStage` enum (P2-BE-04). The step's **name** is what users see; requests, tasks and activities store a snapshot of it. |
| **Condition** *(Phase 2a)* | One typed comparison on a **fact** of the request (`amount > 1000.000`). A step whose condition is false is **skipped**. |
| **Fact** *(Phase 2a)* | A value read from a request's submitted details that a condition may test (`amount`, `leaveType`, `days`). |

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
| StepKey / StepName | string(40)? / string(80)? | the current step and its name snapshot (§7.4); both null iff the request is closed (DB check) |
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
| StepKey / StepName | string(40) / string(80) | the step this task belongs to, with its name when the task opened |
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
| Type | `ActivityType` | `Submitted`, `TaskAssigned`, `Approved`, `Rejected`, `Fulfilled`, `Completed`, `CommentAdded`, `StepSkipped` (Phase 2a, §7.5) |
| ActorId | Guid? | null for system-generated entries (e.g. `TaskAssigned`, `Completed`) |
| TaskId | Guid? | |
| StepKey / StepName | string(40)? / string(80)? | the step the activity relates to; null for `CommentAdded` and `Completed` |
| Body | string(2000)? | comment text / rejection reason |
| OccurredAt | DateTimeOffset | |

Activities are never updated or deleted. Free-form comments are activities of type `CommentAdded` — there is no separate comment table.

### 2.7 Notification

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| RecipientId | Guid | |
| Type | `NotificationType` | `TaskAssigned`, `RequestApproved`, `RequestRejected`, `RequestCompleted`, `StepApproved`, `CommentAdded`, `RoleChangePending`, `RoleChangeApproved`, `RoleChangeRejected` (ADR-0012) |
| Title | string(150) | rendered server-side; no HTML |
| Body | string(500) | rendered server-side; no HTML; **never contains the leave reason** |
| RequestId | Guid? | |
| TaskId | Guid? | |
| CreatedAt | DateTimeOffset | |
| ReadAt | DateTimeOffset? | |

Role-queue tasks notify **every eligible member** of the role (excluding the requester) — one row per recipient.

### 2.8 RoleChange (ADR-0012)

An administrator's request to change **their own** roles, applied only when a different active administrator approves it. Not a `Request`: it is not a business process.

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| UserId | Guid | the administrator whose roles change; also the proposer |
| PreviousRoles | string[] | the user's roles at proposal; approval is refused (`role_change.stale`) if they differ by then |
| ProposedRoles | string[] | the complete new set; distinct, sorted, known roles, different from `PreviousRoles` |
| ProposedAt / ExpiresAt | DateTimeOffset | `ExpiresAt = ProposedAt + 72 h` |
| Status | `RoleChangeStatus` | `Pending`, `Approved`, `Rejected`, `Cancelled`, `Expired`; a stored `Pending` past `ExpiresAt` reads as `Expired` (`StatusAt(now)`) |
| DecidedById / DecidedAt | Guid? / DateTimeOffset? | set exactly when no longer `Pending` (expiry: `DecidedAt = ExpiresAt`, no decider) |
| RejectionReason | string(500)? | set exactly when `Rejected`; never logged or put in notifications |
| Version | uint | `xmin` |

Transitions: `Pending → Approved | Rejected` by anyone but `UserId` (`role_change.self_approval`); `Pending → Cancelled` by `UserId` only (`role_change.not_proposer`); `Pending → Expired` when a new proposal finds an expired row (`ExpireIfDue`). Anything else: `role_change.not_pending`. Checks that need data (decider is an active admin, last administrator, Administrator only on new accounts, roles unchanged) live in `Application/Organization/RoleAssignmentRules` and `RoleChangeService`.

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

### 3.2 Current step

Phase 1 stored a `RequestStage` enum (`ManagerApproval`, `FinanceApproval`, `HrConfirmation`, `ProcurementFulfillment`, `Closed`). Since P2-BE-04 a request stores its current step's key and name (§7.4) and has none once closed; `Closed` is a status, not a step. Where §3.4 and §4 name a stage, read the v1 step with that key.

### 3.3 Fixed processes

Phase 1 defined these in `FlowOps.Domain.Requests.FixedProcesses`. Since P2-BE-03 they are the seeded **v1** workflow versions (§7.4), with the same steps, kinds and assignees; `FixedProcesses` is gone:

```
EquipmentPurchase:
  1. ManagerApproval        Kind=Approval     Assignee=RequesterManager
  2. FinanceApproval        Kind=Approval     Assignee=Role(FinanceOfficer)
  3. ProcurementFulfillment Kind=Fulfillment  Assignee=Role(ProcurementOfficer)

Leave:
  1. ManagerApproval        Kind=Approval     Assignee=RequesterManager
  2. HrConfirmation         Kind=Approval     Assignee=Role(HrOfficer)
```

The aggregate advances through the `WorkflowStep`s of the version the request is pinned to, which the Application layer loads through `IProcessDefinitionSource` (ADR-0014 §1). Still not an engine: an ordered list of steps.

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
| I7 | Steps run in order. **Phase 1:** `FixedProcesses` order, no skipping. **Phase 2a (ADR-0014 §3, since P2-BE-05):** steps run in the pinned workflow version's order; a step is skipped only when its condition is false, and every skip is recorded as a `StepSkipped` activity | D (only `AdvanceTo(next)` exists; the next step is chosen in the aggregate) | — |
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
        PurchaseDetails details, Func<ProcessStep, Assignee> resolveAssignee, DateTimeOffset now);
    public static Request SubmitLeave(/* … */);

    // The only way state advances. Validates I2, I3, I5, I6, I8; appends activities (I9);
    // creates the next task or closes the request. Returns what happened so the
    // application layer can create notifications without re-deriving the outcome.
    public DecisionResult DecideTask(Guid taskId, Guid actorId, TaskDecision decision,
        Func<ProcessStep, Assignee> resolveAssignee, DateTimeOffset now);

    public void AddComment(Guid actorId, string body, DateTimeOffset now);
}
```

The domain does **not** decide *who* is eligible for a role queue (that needs the user store); it receives a resolver (`TaskAssigneeResolver.ResolverFor(requester)`, the same one for submission and decisions, #83), checks that each resolved assignee satisfies its step's `AssigneeRule`, and checks only rules it can know (self-decision, current step, closed, already decided). The first step goes through the same path as every later one, so a process whose first step is a role queue needs only its process entry.

---

## 6. Database notes

- Naming: `snake_case` tables/columns (`EFCore.NamingConventions`).
- Enums stored as `text` (readable, migration-safe), with check constraints generated per enum.
- Indexes: `requests(requester_id, submitted_at desc)`, `requests(status)`, `work_tasks(assignee_user_id) where status='Open'`, `work_tasks(assignee_role) where status='Open'`, `notifications(recipient_id, created_at desc)`, `notifications(recipient_id) where read_at is null`, `request_activities(request_id, occurred_at)`.
- Partial unique index `ux_work_tasks_open_per_request on work_tasks(request_id) where status='Open'` (I4).
- Partial unique index `ux_role_changes_pending_per_user on role_changes(user_id) where status='Pending'` (one pending role change per admin, ADR-0012); checks `ck_role_changes_decision_iff_closed` and `ck_role_changes_reason_iff_rejected`.
- Check: `num_nonnulls(assignee_user_id, assignee_role) = 1`; `manager_id <> id` on users.
- `request_number_seq` sequence for `Request.Number`.

**As built (BE-05 users/departments, BE-07 the rest; configurations in `Infrastructure/Persistence/Configurations`):**
- Enum and currency checks are generated from code (`CheckConstraints.InEnum<T>`, `CheckConstraints.In(column, SupportedCurrencies.All)`), so a new value lands in the next migration. Enum columns are `varchar(32)`.
- Extra checks beyond the list above: `ck_work_tasks_decision_iff_done` (outcome, decider and time are set exactly when Done), `ck_work_tasks_assignee_role` (a known role), `ck_purchase_request_details_amount` (0 < amount ≤ 1,000,000.000), `ck_leave_request_details_dates` (end ≥ start); department names unique case-insensitively (`ux_departments_name_ci` on `lower(name)`).
- Details tables are keyed by `request_id` (PK = FK to `requests`, cascade). Tasks and activities cascade with their request; everything referencing users, departments or tasks is `RESTRICT` (users and departments are deactivated, never deleted).
- `xmin` is mapped with `IsRowVersion()` on `Request.Version`, `WorkTask.Version` and `RoleChange.Version`; it is a system column, so it doesn't appear in the DDL.
- Every FK has an index except `work_tasks.assignee_user_id`, which only has the open-task partial index, because users are never deleted.
- Domain types have a private parameterless constructor for materialization only; `RequestNumber.Parse` rebuilds the stored value.
- **Ids (BE-11).** Domain ids come from `Common/SequentialGuid.NewV7(now)`: UUIDv7 with a per-process counter in the same millisecond, so ordering by id is creation order. Lists sort by time then `id`, e.g. a request's `Submitted` before its first `TaskAssigned`, which share an instant. `Guid.CreateVersion7` randomises within the millisecond, so that order was not guaranteed before.

---

## 7. Phase 2a: configurable workflows (ADR-0014)

Status: **designed** (P2-DOC-01). The API already serves `step` and `route` and has the workflow types (API_CONTRACT §10). The entities below are built in P2-BE-03 to P2-BE-07. **Since P2-BE-03** the entities, the seeded v1 and `Request.WorkflowVersionId` exist and are the only source of steps (`IProcessDefinitionSource`); Since P2-BE-05 steps store their `Condition` (`condition_fact`, `condition_operator`, `condition_values text[]`), the aggregate skips steps and records `StepSkipped`, and the route and route preview read conditions. `CanReadRestrictedDetails` (P2-BE-06) is not stored yet.

### 7.1 Entities

Routing stays inside the `Request` aggregate. There is no workflow engine, no expression language and no graph. A workflow is an **ordered list of steps**, and a step may have one condition.

**WorkflowDefinition** (one per request type)

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| RequestType | `RequestType` | unique: one workflow per type in Phase 2a (ADR-0014 Q4) |
| Name | string(100) | e.g. "Equipment purchase" |

**WorkflowVersion**

| Field | Type | Rules |
|---|---|---|
| Id | Guid | |
| WorkflowId | Guid | |
| Number | int | 1, 2, 3 … per workflow, never reused |
| Status | `WorkflowVersionStatus` | `Draft` \| `Published` \| `Retired` |
| Steps | `List<WorkflowStep>` | owned, ordered by `Order`; at least one |
| CreatedAt / CreatedById | DateTimeOffset / Guid | |
| PublishedAt / PublishedById | DateTimeOffset? / Guid? | set exactly when it was published (`Published` or `Retired`) |
| RetiredAt | DateTimeOffset? | set exactly when `Retired` |
| Version | uint (`xmin`) | optimistic concurrency token for draft edits and publishing |

**WorkflowStep** (owned by its version)

| Field | Type | Rules |
|---|---|---|
| Order | int | 0-based position; unique within the version |
| Key | string(40) | lower-case slug `^[a-z0-9]+(-[a-z0-9]+)*$`, unique within the version |
| Name | string(80) | what users see ("Finance approval") |
| Kind | `WorkTaskKind` | `Approval` \| `Fulfillment`; a Fulfillment step must be the last step, and there is at most one |
| Assignee | `AssigneeRule` | §7.3 |
| Condition | `Condition?` | §7.2; **always null on the first step**, so every request gets at least one task |
| CanReadRestrictedDetails | bool | the step's assignees may read restricted details (the leave reason). AUTHORIZATION §5 |

**Lifecycle of a version.** `Draft → Published → Retired`. Nothing else is allowed.
- A Draft is created as a copy of the Published version. A workflow has at most one Draft.
- Only a Draft can be edited or discarded.
- Publishing a Draft retires the previous Published version in the same transaction.
- **Published and Retired versions are immutable** (domain-enforced). Editing a workflow always means a new Draft.
- Exactly one Published version per request type (partial unique index `ux_workflow_versions_published` on `workflow_versions(workflow_id) where status = 'Published'`; also `ux_workflow_versions_draft` for at most one Draft).

### 7.2 Facts and conditions

A **condition** is `(Fact, Operator, Values)`: exactly one typed comparison. There is no `and`/`or`, nesting or free text.

**Facts catalogue.** Declared once in Domain, per request type. Facts are read from the submitted details, which never change, so a request always takes the same route.

| Request type | Fact key | Type | Source |
|---|---|---|---|
| `EquipmentPurchase` | `amount` | Money (JOD) | `PurchaseDetails.EstimatedCost` |
| `Leave` | `leaveType` | enum `LeaveType` | `LeaveDetails.LeaveType` |
| `Leave` | `days` | int | `LeaveDetails.Days` (inclusive count) |

**Operators by fact type**

| Fact type | Operators |
|---|---|
| Money, number | `GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Equal` |
| Enum | `Is`, `IsOneOf` |
| Date | `Before`, `After` (no date fact exists in Phase 2a) |

Rules, checked when a draft is validated or published (`workflow.*` issues, API_CONTRACT §10):
- A condition may reference only a fact of its workflow's request type.
- The operator must suit the fact's type.
- `Values` has exactly one entry, except `IsOneOf`, which has one or more. A Money value has at most 3 decimals and is compared in JOD.
- `Condition.IsMetBy(facts)` is pure domain code. Money compares exactly (`1000.000` is not greater than `1000.000`).

Adding a fact or an operator is a code change with tests. Admins combine them; they don't invent them.

### 7.3 Assignee rules

`TaskAssigneeResolver` stays the one place that turns a rule into an `Assignee` (CODE_STANDARDS §2.3). Since P2-BE-01, the first step goes through it too.

| Rule | Resolves to | Stalled when (WORKFLOWS §4) |
|---|---|---|
| `RequesterManager` | the requester's manager (a user) | the manager is inactive (`AssigneeInactive`) |
| `Role(r)` | the role queue `r` | no active holder other than the requester (`NoEligibleRoleMember`) |
| `DepartmentRole(dept, r)` *(P2-BE-06)* | holders of `r` in department `dept` | no active holder in that department |
| `SpecificUser(id)` *(P2-BE-06)* | that user | the user is inactive |
| `ManagerOfManager` *(P2-BE-06)* | the manager's manager | missing or inactive |

`Administrator` is never assignable (`workflow.administrator_assignee`), whatever the rule.

### 7.4 Version pinning and the seeded v1

- `Request.WorkflowVersionId` (Guid) is set at submission to the type's Published version. **Every later step of that request comes from that version.** Publishing a new version affects new requests only; in-flight requests keep their route.
- P2-BE-03 seeds purchase and leave as Published **v1**, identical to Phase 1's `FixedProcesses`, and backfills existing requests with it (migration `AddWorkflowDefinitions`). The seed has no author: its `CreatedById` and `PublishedById` are null. Phase 1 stage values map to v1 keys one to one:

| `RequestStage` (Phase 1) | v1 step key | Name |
|---|---|---|
| `ManagerApproval` | `manager-approval` | Manager approval |
| `FinanceApproval` | `finance-approval` | Finance approval |
| `HrConfirmation` | `hr-confirmation` | HR confirmation (`CanReadRestrictedDetails = true`) |
| `ProcurementFulfillment` | `procurement-fulfillment` | Procurement fulfilment |
| `Closed` | — | not a step: a closed request has `Status` `Completed` or `Rejected` and no current step |

  Migration `StoreStepKeys` (P2-BE-04) applied this mapping once to every stored request, task and activity; the mapping now exists only in that migration.
- Since P2-BE-04, `Request`, `WorkTask` and `RequestActivity` store a **step key plus a name snapshot** (`StepRef`, Domain) in place of `RequestStage`. Renaming a step in a later version doesn't rewrite history.

### 7.5 Runtime semantics and `StepSkipped`

When a task is approved or fulfilled, the aggregate walks the pinned version's later steps in order:
1. A step whose condition is **true or absent** is the next step. Its task opens as in Phase 1.
2. A step whose condition is **false** is skipped. The aggregate appends a `StepSkipped` activity: `ActorId` null, the skipped step, and `Body` = the condition in words ("Only if amount is greater than 1,000.000 JOD"). No task is created.
3. If no step is left, the request completes, as in Phase 1.

A rejection ends the request at any step; no later step is evaluated or recorded. The status rule is unchanged: `Approved` while the open step is the Fulfillment step, otherwise `InReview`. Invariants I1–I6 and I8–I15, T3 (separation of duties) and every 409 path are unchanged. Only I7 is reworded (§4).

**Planned route.** `RequestRoute` builds the list shown on request details (API_CONTRACT §4 `route`):
- each step the request went through, in order: `Done`, or `Skipped` (from its activity);
- the current step: `Current`;
- the later steps of the pinned version whose conditions hold for the request's facts: `Planned`. Later steps whose conditions don't hold are left out until the request passes them, when they appear as `Skipped`.

A closed request shows only the steps it went through.

### 7.6 Publish audit

Publishing records `PublishedById` and `PublishedAt` on the version. It also writes one structured log entry (`WorkflowPublished`: actor id, workflow id, request type, version number and id, retired version id). Like other admin audit entries, it never includes step contents. AUTHORIZATION §8 has the full rules.
