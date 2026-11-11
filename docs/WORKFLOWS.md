# Workflows — FlowOps Phase 1 and Phase 2a

**Purpose:** the behaviour of the two fixed processes end to end: happy paths, rejection, invalid transitions, transactions, concurrency, notifications. §6 adds Phase 2a: configurable workflows, skipping and publishing (ADR-0014).
Entities/statuses are defined in `DOMAIN_MODEL.md`; permissions in `AUTHORIZATION.md`.

---

## 1. Shared mechanics (both processes)

### 1.1 Assignee resolution — `TaskAssigneeResolver` (Application)

| Rule | Resolves to | Failure |
|---|---|---|
| `RequesterManager` | `AssigneeUserId = requester.ManagerId` | No manager, inactive manager, or manager == requester → **submission fails** `422 request.manager_not_assigned` |
| `Role(R)` | `AssigneeRole = R` | Never fails at assignment time. If no *eligible* member exists (all inactive, or the only one is the requester) the task stays Open and appears in the admin **Stalled tasks** list |

Eligibility for a role-queue task is evaluated **at decision time**, not assignment time: active user, holds role R, is not the requester. This means adding a new finance officer immediately unblocks stalled finance tasks with no data migration.

The manager is captured **at submission** (`AssigneeUserId` is fixed on the task). If an admin later changes the requester's manager, existing open manager tasks keep their original assignee. Reassignment is out of scope for Phase 1 and listed in `ROADMAP.md`.

### 1.2 One use case = one transaction

Every state-changing use case follows the same shape (`TaskDecisionService.DecideAsync` shown):

```
1. Resolve actor from ICurrentUser            (never from the request body)
2. Load Request aggregate with its Open task  (single query, tracked)
3. Visibility check (AccessRules)             → 404 if not visible
4. Eligibility check (TaskEligibility)        → 403 if not eligible
5. Compare client Version with task Version   → 409 if stale
6. Resolve next step's assignee (if any)      → may 422
7. request.DecideTask(...)                    → domain enforces I2–I9
8. NotificationFactory.For(result)            → adds Notification rows
9. await db.SaveChangesAsync()                → ONE transaction; xmin checked here
10. await realtime.PublishAsync(result)       → AFTER commit, best effort
```

There is no explicit `BeginTransaction`: a single `SaveChangesAsync` is atomic in EF Core. Explicit transactions are only allowed where a use case genuinely needs two round-trips (none in Phase 1).

### 1.3 Notifications and real-time

- Notification rows are written **in the same transaction** as the state change. If the transaction rolls back, no notification exists.
- SignalR messages are sent **after** commit. They carry ids only; clients refetch through the REST API. A lost SignalR message therefore costs freshness, never correctness — clients also refetch on reconnect and on window focus.
- No outbox in Phase 1 (see ADR-0009 for why and when that changes).

### 1.4 Who gets notified

| Event | Recipients | Type |
|---|---|---|
| Task created for user | that user | `TaskAssigned` |
| Task created for role queue | every active role member except the requester | `TaskAssigned` |
| An approval step approved, more steps follow | requester | `StepApproved` |
| Request reaches `Approved` (purchase, before procurement) | requester | `RequestApproved` |
| Request `Completed` | requester | `RequestCompleted` |
| Request `Rejected` | requester | `RequestRejected` (body includes rejection reason) |
| A step is skipped (Phase 2a, §6.1) | nobody; the `StepSkipped` activity is in the timeline | — |
| Comment added | requester + decided/eligible participants except the author | `CommentAdded` |

---

## 2. Equipment purchase

### 2.1 Happy path

| # | Actor | Action | Request Status / Stage after | Tasks after | Activities appended |
|---|---|---|---|---|---|
| 1 | Employee E | `POST /api/requests/purchase` | InReview / ManagerApproval | T1 Open, Approval, user=M | Submitted, TaskAssigned |
| 2 | Manager M | `POST /api/tasks/T1/approve` | InReview / FinanceApproval | T1 Done(Approved); T2 Open, Approval, role=FinanceOfficer | Approved, TaskAssigned |
| 3 | Finance F | `POST /api/tasks/T2/approve` | **Approved** / ProcurementFulfillment | T2 Done(Approved); T3 Open, Fulfillment, role=ProcurementOfficer | Approved, TaskAssigned |
| 4 | Procurement P | `POST /api/tasks/T3/fulfill` | **Completed** / Closed | T3 Done(Fulfilled) | Fulfilled, Completed |

### 2.2 Rejection

At step 2 or 3, `POST /api/tasks/{id}/reject { reason, version }`:
- Task → Done(Rejected) with `Comment = reason`.
- Request → `Rejected` / `Closed`, `ClosedAt = now`.
- No further task is created. Activities: `Rejected` (Body = reason).
- Requester notified with the reason.

### 2.3 Not in Phase 1
Vendor integration, payment, accounting, inventory, budget checks, cost thresholds that skip finance. (A threshold rule is a natural Phase 2 conditional branch.)

---

## 3. Leave request

### 3.1 Happy path

| # | Actor | Action | Status / Stage after | Tasks after |
|---|---|---|---|---|
| 1 | Employee E | `POST /api/requests/leave` | InReview / ManagerApproval | T1 Open, user=M |
| 2 | Manager M | approve T1 | InReview / HrConfirmation | T2 Open, role=HrOfficer |
| 3 | HR H | approve T2 ("confirm") | **Completed** / Closed | — |

"Confirm" is the UI label for approving an `HrConfirmation` task; the API verb is `approve` for all approval tasks (one endpoint, one authorization path — DRY).

### 3.2 Rejection
Manager or HR rejects with a reason → `Rejected` / `Closed`. Same mechanics as §2.2.

### 3.3 Privacy
The leave `Reason` is returned only to the requester, the manager-approver and HR officers (see `AUTHORIZATION.md` §5). Notifications never include it. Finance and procurement cannot see leave requests at all.

### 3.4 Not in Phase 1
Leave balances, payroll, calendars, overlapping-leave detection, half days.

---

## 4. Invalid transitions and edge cases

| Scenario | Result |
|---|---|
| Approve a task that is already Done | 409 `task.already_decided` |
| Approve a task on a Rejected/Completed request | 409 `request.closed` |
| Decide a non-current task (cannot happen with I4, but guarded) | 409 `task.not_current` |
| `reject` on a Fulfillment task / `fulfill` on an Approval task | 409 `task.invalid_action` |
| Requester tries to decide own task (e.g. finance officer's own purchase) | 403 `task.self_decision` |
| Someone who decided an earlier step tries a later one (e.g. a manager who is also a finance officer) | 403 `task.already_participated` (#50) |
| User not assignee / not in role tries to decide | 403 `task.not_eligible` if the user can see the request, otherwise 404 |
| Stale `version` from an old browser tab | 409 `concurrency.conflict` — client refetches |
| Submit with no manager assigned | 422 `request.manager_not_assigned` |
| Submit with no department assigned | 422 `request.department_not_assigned` |
| Manager deactivated while task open | Task stays assigned to them; shows in Stalled tasks (admin). Reassignment is Phase 2 |
| Role queue with no eligible member | Task stays Open; shows in Stalled tasks |
| Administrator tries to approve without being eligible | 403 — admin role grants **no** process authority |

---

## 5. Concurrency

**Mechanism:** optimistic concurrency on PostgreSQL's system column `xmin`, mapped as the EF Core concurrency token on `Request` and `WorkTask` (ADR-0006).

**Two finance officers approve the same task at the same moment:**
1. Both load T2 (Open, xmin=100).
2. Both pass checks; both call `DecideTask`.
3. Officer A's `SaveChanges` updates `work_tasks … WHERE id=T2 AND xmin=100` → 1 row; commits; creates T3.
4. Officer B's update matches 0 rows → `DbUpdateConcurrencyException` → mapped to **409 `concurrency.conflict`**.
5. Defence in depth: even without xmin, B's insert of a second open task would violate `ux_work_tasks_open_per_request`.

**Client contract:** decision endpoints require the `version` the client last saw. A mismatch returns 409 *before* any work, which also catches the "stale tab" case where no concurrent write is in flight.

**Not used:** `SELECT … FOR UPDATE` row locks, distributed locks, retries on conflict. A decision conflict is a user-visible fact ("someone else already decided this"), not something to retry silently.

Required tests: see `TESTING.md` §3.4.

---

## 6. Phase 2a: configurable workflows (ADR-0014)

Status: **designed** (P2-DOC-01), implemented in P2-BE-03 to P2-BE-07. Entities and the facts catalogue are in DOMAIN_MODEL §7; permissions are in AUTHORIZATION §8; endpoints are in API_CONTRACT §10. Everything in §1–5 still applies. The steps now come from the request's **pinned workflow version** instead of `FixedProcesses`.

### 6.1 Skipping

When a task is approved or fulfilled, the next step is the **first later step of the pinned version whose condition is true or has no condition**. Every later step passed over on the way has a false condition. Each of those gets a `StepSkipped` activity (actor null, the step, the condition in words) in the same transaction as the decision, and no task. With no step left, the request completes and gets the usual `Completed` activity after any `StepSkipped` entries.

The owner's rule, as v2 of the purchase workflow:

| # | Key | Kind | Assignee | Condition |
|---|---|---|---|---|
| 1 | `manager-approval` | Approval | RequesterManager | — |
| 2 | `finance-approval` | Approval | Role(FinanceOfficer) | `amount GreaterThan 1000.000` |
| 3 | `procurement-fulfillment` | Fulfillment | Role(ProcurementOfficer) | — |

| Purchase | After the manager approves | Activities appended | Status / step after |
|---|---|---|---|
| 1,500.000 JOD | Finance task opens | `Approved`, `TaskAssigned` | InReview / `finance-approval` |
| 400.000 JOD | Finance skipped; procurement task opens | `Approved`, `StepSkipped` (finance-approval, "Only if amount is greater than 1,000.000 JOD"), `TaskAssigned` | Approved / `procurement-fulfillment` |
| exactly 1,000.000 JOD | as 400 (not greater) | as 400 | as 400 |

Notifications are unchanged (§1.4). The requester of the 400 JOD purchase gets `RequestApproved`, because the request went straight to the fulfilment step. Nobody is notified about the skip itself: it is in the timeline.

What doesn't change:
- A rejection ends the request; later steps are not evaluated.
- The first step never has a condition.
- Separation of duties (T3), self-decision (I2), the current-step check (I5) and every 409 apply to the steps that do run.
- Facts come from the submitted details, which can't change, so the route can't change while the request is in flight.

### 6.2 Version pinning

- A request is pinned at submission to its type's Published version (`WorkflowVersionId`).
- Publishing a new version never changes an in-flight request. A request submitted under v1 finishes on v1's steps, even after v2 removes or reorders them.
- Retired versions stay readable in the version history and keep serving the requests pinned to them.

### 6.3 Planned route

Request details show `route` (API_CONTRACT §4):
- the steps the request went through (`Done`, or `Skipped`);
- the current step (`Current`);
- the remaining steps that will run for its facts (`Planned`).

Before submitting, the requester can ask for the same route for the values in the form (`POST /api/requests/route-preview`). The form shows it as "Manager approval → Procurement fulfilment" (ADR-0014 Q5, UI_SPEC §7).

### 6.4 Editing and publishing (administrators)

```
Published vN ──"Edit"──▶ Draft vN+1 (copy of vN) ──edit / validate / preview──▶ Publish
                                                            │
                                     vN becomes Retired ◀───┘  vN+1 becomes Published
```

1. **Start a draft.** `POST …/{workflowId}/draft` copies the Published version into a new Draft. Only one Draft per workflow (409 `workflow.draft_exists`).
2. **Edit.** `PUT …/versions/{id}` replaces the draft's whole step list and carries the draft's `version` (409 `concurrency.conflict` if another admin saved in between). Shape errors give 400. Rule errors are saved and reported by validate.
3. **Validate.** `POST …/validate` returns errors (block publishing) and warnings (don't):

   | Errors (`workflow.*`) | Warning |
   |---|---|
   | `no_steps`, `first_step_conditional`, `duplicate_key`, `unknown_fact` (not a fact of this request type), `operator_not_allowed` (wrong for the fact's type), `invalid_value`, `administrator_assignee`, `invalid_assignee` (unknown department, user or role; missing fields for the rule), `fulfillment_not_last` | `assignee_unavailable`: no active user satisfies the rule right now (ADR-0014 Q6) |

4. **Preview.** `POST …/preview` with sample facts (e.g. amount 400 and 1,500) returns the route each would take, with skipped steps marked.
5. **Publish.** `POST …/publish` with the draft's `version`:
   1. re-validate (errors → 422 `workflow.invalid`, nothing changes);
   2. retire the current Published version and publish the draft;
   3. record `PublishedBy`/`PublishedAt`; one `SaveChangesAsync`;
   4. write the audit log entry.

   One administrator does all of this alone (ADR-0014 Q1); there is no second approval. Two admins publishing at once: exactly one succeeds, the other gets 409 (`xmin` on the draft, and the one-Published index).
6. **Discard.** `DELETE …/versions/{id}?version=…` deletes a Draft. Published and Retired versions can't be deleted or edited (409 `workflow.version_not_draft`).

No request notification is sent for publishing; administrators see the result in the version history.

### 6.5 Edge cases

| Scenario | Result |
|---|---|
| Every step after the current one has a false condition | The request completes after the decision, with one `StepSkipped` per passed step |
| A published step's assignee has no eligible holder (warning ignored at publish) | The task opens and shows in Stalled tasks (§4), with the reason extended to the new rules |
| Draft edited by two admins | The second save gets 409 `concurrency.conflict`; the editor reloads |
| Publishing while requests are in flight | They keep their pinned version (§6.2) |
| Admin removes the restricted-details flag from the HR step and publishes | New leave requests: HR officers no longer see the reason. Requests pinned to the old version keep its flag (AUTHORIZATION §5) |
