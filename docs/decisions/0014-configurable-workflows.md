# ADR-0014: Configurable workflows as versioned step lists with typed conditions (Phase 2)

- **Status:** Accepted. The owner answered §5 on 2026-11-11 (#98): Q1 changed to single-admin publish, Q2–Q7 as recommended.
- **Date:** 2026-11-11
- **Supersedes:** ADR-0004 (fixed processes in code)

## Context
Phase 2 (ROADMAP.md) lets administrators change how requests are routed without a deploy. A typical rule from the owner:
"Manager approves. If the amount is over 1,000 JOD, Finance approves next. Otherwise it goes straight to Procurement."

Phase 1 already routes requests generically. `Request.DecideTask` asks `FixedProcesses.Next(type, stage)` for the next step, and inbox, access, eligibility and notification code work on `WorkTask`s, not on process shapes (ADR-0004, ADR-0005). What's fixed is the *source* of the steps (C# data) and a few places that still name specific steps or types (#83, #84, `RequestStage`, the HR rule for leave reasons).

Off-the-shelf engines were evaluated on 2026-11-11:

| Option | Why not |
|---|---|
| Elsa 3 (MIT) | Keeps its own workflow-instance state beside `Request`/`WorkTask`, which would need two-way sync. Its designer (Elsa Studio) is Blazor, beside our React + token design system. Strong at general automation (HTTP calls, timers), which Phase 2 doesn't need. |
| Workflow Core (MIT) | Workflows defined in C#/JSON by developers, no designer: admins can't configure it. |
| Temporal (.NET SDK) | Workflows are code; needs a separately operated server cluster. |
| Dapr Workflow | Workflows are code; requires adopting the Dapr runtime. |
| Camunda 8 | Production use needs a commercial licence since 8.6; BPMN/Java; ROADMAP rejects embedding a BPMN engine. |

The hard parts of an approval system (human tasks, eligibility, separation of duties, leave-reason privacy, optimistic concurrency, notifications) are FlowOps-specific and already built. An engine would replace none of them.

## Decision

### 1. No workflow engine; extend the existing seam
Routing stays inside the `Request` aggregate. `FixedProcesses` becomes `IProcessDefinitionSource` (Domain-facing, implemented in Application over `IAppDbContext`), as ROADMAP Phase 2 §3 already planned. No interpreter, scripting or expression language, BPMN, or graph.

### 2. A workflow is an ordered list of steps; a step may have a condition
```
WorkflowDefinition        Id, RequestType, Name
 └─ WorkflowVersion       Id, Number, Status (Draft | Published | Retired), PublishedAt, PublishedBy
     └─ WorkflowStep      Order, Key, Name, Kind (Approval | Fulfillment), AssigneeRule, Condition?
```
- **Key** is a stable slug unique within the version (`manager`, `finance`). It replaces the `RequestStage` enum (§4). **Name** is what users see ("Finance approval").
- **Condition** is optional and has exactly one typed comparison: `(Fact, Operator, Value)`. Example: `(amount, GreaterThan, 1000.000)`.
- The **first step has no condition**, so every request gets at least one task.
- A request that is rejected at any step ends, as in Phase 1. There are no "go back" or "jump to" steps.

### 3. Runtime semantics
- When a step's task is decided (approved or fulfilled), the next step is the **first later step whose condition is true or absent**. With none left, the request completes.
- A step whose condition is false is **skipped**. That appends a `StepSkipped` activity naming the step and the condition, so the timeline explains why Finance never saw the request. No task is created.
- **Facts** are read from the request's submitted details, which can't change after submission. The same request always takes the same route.
- **Version pinning.** `Request` stores `WorkflowVersionId` at submission. Publishing a new version affects new requests only. Published versions are immutable; editing creates a new Draft.
- Exactly one Published version per request type at a time. Publishing retires the previous one.
- Invariants I1–I6, I8–I15, T3 (separation of duties) and the 409 paths are unchanged. **I7** ("steps run strictly in order; no skipping") becomes: *steps run in version order; a step is skipped only when its condition is false, and every skip is recorded.*

### 4. Facts, operators and assignee rules are a closed, typed catalogue in code
- **Facts per request type** are declared once in Domain:
  - `EquipmentPurchase`: `amount` (Money)
  - `Leave`: `leaveType` (enum), `days` (int, inclusive count per BE-10b)

  A condition can reference only a fact of its request type.
- **Operators by fact type:**
  - Money and number: `>`, `≥`, `<`, `≤`, `=`
  - Enum: `is`, `is one of`
  - Date: `before`, `after`
- **Assignee rules** extend the Phase 1 two:
  - `RequesterManager`
  - `Role(r)`
  - `DepartmentRole(dept, r)`
  - `SpecificUser(id)`
  - `ManagerOfManager`

  `Administrator` is never assignable (AUTHORIZATION §2). `TaskAssigneeResolver` stays the one home that turns a rule into an `Assignee`.
- Adding a fact, an operator or a rule is a code change with tests. Admins combine them; they don't invent them.

### 5. Owner decisions (#98, 2026-11-11)
These were open questions with recommended defaults. The owner accepted every default except Q1.

| # | Question | Decision |
|---|---|---|
| Q1 | **Who can edit and publish workflows?** Administrators have no process authority today, but a workflow decides who approves money and leave. | **Decided (differs from the proposal):** a single Administrator edits drafts **and publishes** them, with **no second-admin approval**. A `workflows.manage` capability is derived from the Administrator role. Every publish is audit-logged (actor id, definition, version) and recorded on the version (`PublishedBy`, `PublishedAt`). Unlike ADR-0012 role changes, this is not a two-person control; see Consequences. |
| Q2 | **Steps become admin-named**, so `RequestStage` (an API enum used by DB, contract and frontend) can't stay a fixed enum. | Replace `stage: RequestStage` with `step: { key, name }` on requests, tasks, inbox items, activities and stalled tasks. Keep `Closed` as a request **status** concern, not a step. Migrate existing rows by mapping each enum value to a key of the same name in the seeded v1 workflows. One coordinated contract change (AGENT_HANDOFF §3). |
| Q3 | **Leave-reason visibility** is hardcoded as "HrOfficer through the HR task" (AUTHORIZATION §4–5). | Add a per-step flag `canReadRestrictedDetails`. The leave reason is readable by R1, R2, and the assignee(s) of a step with the flag. The seeded HR step has it set; the editor shows a warning when an admin sets it. |
| Q4 | **Can admins create new request types (with their own fields), or only re-route purchase and leave?** | Phase 2a: re-route existing types only. Phase 2b: the form builder (`FormSubmission` jsonb beside typed details, ROADMAP Phase 2 §2), which also adds facts from form fields. Ship 2a first. |
| Q5 | **How is the route shown?** | Request details show the remaining *planned* route, computed from the pinned version and the facts. The timeline shows skipped steps. The requester sees the route at submission ("Manager → Procurement"). |
| Q6 | **Unassignable steps** (e.g. `DepartmentRole(Finance, ProcurementOfficer)` with no holder, or a deactivated `SpecificUser`). | Publish validation **warns, not blocks**, because people change after publishing anyway. At runtime the task opens and shows up in the existing stalled-tasks list with a reason (`NoEligibleRoleMember` / `AssigneeInactive`, extended to the new rules). |
| Q7 | **Reassignment and delegation** (ROADMAP Phase 2 §6). | Its own later ADR and task. Not needed for the routing rule above. |

### 6. Order of work
1. Pay down the Phase 1 coupling first: #83 (first step bypasses the resolver) and #84 (request-type branching outside its home).
2. Persist definitions and seed purchase/leave as v1, with no behaviour change (contract change from Q2 lands here).
3. Conditions, skipping, `StepSkipped` and route preview in the aggregate.
4. New assignee rules.
5. Admin API (drafts, validation, single-admin publish per Q1) and the React step-list editor.
6. Phase 2b form builder (Q4), only after 2a ships.

The tasks are in `docs/PHASE2_TASKS.md`.

## Consequences
+ The owner's rule ("over 1,000 → Finance, else → Procurement") is a one-row condition, with no deploy.
+ The aggregate stays the single place that advances a request. Access, eligibility, inbox and notifications don't learn about workflows.
+ In-flight requests are never re-routed by an edit (version pinning). Every skip is visible in the timeline.
+ No new runtime, server or UI framework to operate or theme.
- Real branching (different multi-step paths), parallel approvals and loops are not expressible. Parallel is Phase 3 (ROADMAP). Multi-step branches can be added later as "condition groups" if a real need appears.
- `stage` → `step` is a breaking contract change touching backend, `openapi.json`, generated types and every screen that shows a stage.
- Each new fact, operator or assignee rule needs a code change. This is intentional: it keeps conditions typed and safe.
- **One administrator can re-route approvals alone (Q1).** For example, they could send purchases to a `SpecificUser` or skip Finance below an amount. The owner accepted this risk. It is limited by the following:
  - Administrator is never an assignee.
  - Self-decision (I2) and separation of duties (T3) still apply to whoever is assigned.
  - In-flight requests keep their pinned version.
  - Every publish is audit-logged and visible in the version history.

  If a two-person control is wanted later, the ADR-0012 pending/approve pattern can be added to publishing without changing the model.

## Alternatives rejected
- **Embedding an engine** (Elsa, Workflow Core, Temporal, Dapr Workflow, Camunda): see Context.
- **A free-form expression language** (`amount > 1000 && dept == "IT"`): parsing, injection and error surface for little gain over typed comparisons.
- **A graph model with arbitrary edges from day one**: harder to validate and edit, and needed only for branches and parallelism that Phase 2 doesn't require.
- **Editing published versions in place**: would silently re-route in-flight requests.
