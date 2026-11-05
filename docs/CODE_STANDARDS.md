# Code Standards — FlowOps

**Purpose:** the engineering rules every contributor (human or agent) follows, with concrete good/bad examples. Short enforceable versions live in `.claude/rules/`; this document explains the *why*.

---

## 1. Separation of concerns — where code goes

Ask "what is this code's reason to change?" and put it where that reason lives.

| If the code… | It belongs in | Not in |
|---|---|---|
| decides whether a business action is allowed or what state comes next | Domain aggregate method / domain rule | controller, service `if` chains, React |
| orchestrates a use case (load → check → act → save → notify) | Application service | controller, domain |
| decides who may see/act on a resource | `Application/Access` (`AccessRules`, `TaskEligibility`) | controller, each service separately, frontend |
| shapes HTTP (routes, status codes, auth attributes) | Api controller / middleware | service |
| talks to PostgreSQL, Identity, SignalR | Infrastructure | domain, controller |
| shapes a read model for one screen | Query service with `Select` projection | controller, React component |
| renders UI and handles user interaction | React component | — |
| fetches/mutates server data | feature `api.ts` hook → `lib/api/client` | component body, `useEffect` |
| formats a value for display | `lib/format/*` | inline in each component |

### 1.1 Bad → good: controller

```csharp
// ❌ BAD: business rules, data access and authorization in the controller
[HttpPost("{id}/approve")]
public async Task<IActionResult> Approve(Guid id, ApproveDto dto)
{
    var task = await _db.WorkTasks.Include(t => t.Request).FirstOrDefaultAsync(t => t.Id == id);
    if (task == null) return NotFound();
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    if (task.AssigneeUserId != userId && !User.IsInRole(task.AssigneeRole!)) return Forbid();
    if (task.Status == "Done") return Conflict();
    task.Status = "Done";
    if (task.Request.Type == "Purchase" && task.Stage == "ManagerApproval")
        _db.WorkTasks.Add(new WorkTask { /* finance … */ });
    // …40 more lines, duplicated in Reject()
    await _db.SaveChangesAsync();
    return Ok();
}

// ✅ GOOD
[HttpPost("{taskId:guid}/approve")]
public async Task<IActionResult> Approve(Guid taskId, ApproveTaskRequest body, CancellationToken ct)
{
    await taskDecisions.ApproveAsync(taskId, body.Version, body.Comment, ct);
    return NoContent();
}
```

### 1.2 Bad → good: business rule placement

```csharp
// ❌ BAD: the "can't decide own request" rule lives in a service — and gets copied into Reject and Fulfill
if (request.RequesterId == currentUser.Id) throw new ForbiddenException(...);

// ✅ GOOD: the aggregate owns the rule; every decision path goes through one method
public DecisionResult DecideTask(Guid taskId, Guid actorId, TaskDecision decision, …)
{
    EnsureOpen();                                   // I6
    var task = CurrentTaskOrThrow(taskId);          // I5
    if (actorId == RequesterId) throw DomainErrors.SelfDecision();   // I2
    task.Complete(decision, actorId, now);          // I3, I8
    …
}
```
`TaskEligibility` *also* checks self-decision so the API returns a clear 403 before loading more — that is defence in depth around **one rule definition** (`DomainErrors.SelfDecision` code), not two independent implementations.

## 2. DRY — duplicate *knowledge*, not syntax

DRY means every piece of business knowledge has **one** authoritative representation. It does not mean every two similar lines must be merged.

### 2.1 Extract when
- The same **rule** applies in several places (visibility, eligibility, transition rules, assignee resolution, status labels, error messages).
- The same **behaviour** with the same meaning is needed (paging, ProblemDetails mapping, current user, decision dialog).
- A change to one copy would *have to* be made to the others to stay correct.

### 2.2 Don't extract when
- Two things look alike but mean different things (purchase and leave *forms* share inputs, but are separate forms — their fields change for different reasons).
- You'd need a flag parameter to make the shared version work for both callers (`DoThing(isLeave: true)` is a smell).
- There's one caller and an imagined second one ("we might need…").
- The abstraction is harder to read than the duplication (three lines of mapping repeated twice is fine).

### 2.3 Where FlowOps's shared knowledge lives (check here before writing new code)

| Knowledge | Single home |
|---|---|
| Process steps and order | `Domain/Requests/FixedProcesses.cs` |
| State transitions & invariants | `Domain/Requests/Request.cs`, `WorkTask.cs` |
| Error codes | `Domain/Errors/ErrorCodes.cs` (+ `frontend/lib/api/problem.ts` messages) |
| Who can see a request | `Application/Access/AccessRules.cs` |
| Who can decide a task / which actions are available | `Application/Access/TaskEligibility.cs` |
| Who a step is assigned to | `Application/Requests/TaskAssigneeResolver.cs` |
| Who gets notified and what it says | `Application/Notifications/NotificationFactory.cs` |
| Current user | `ICurrentUser` |
| Paging | `Application/Common/Paging.cs` |
| Exception → HTTP mapping | `Api/Errors/ProblemDetailsExceptionHandler.cs` |
| Colours, type, radius | `frontend/src/styles/theme.css` |
| Status labels/tones | `frontend/src/lib/format/status.ts` |
| Query keys | `frontend/src/lib/api/queryKeys.ts` |
| API calls | `frontend/src/features/*/api.ts` via `lib/api/client.ts` |
| Decision UI + mutation | `DecisionDialog` + `useDecideTask` |

### 2.4 Example: one decision path for three actions

```csharp
// ✅ Three thin public methods, one private pipeline
public Task ApproveAsync(Guid taskId, string version, string? comment, CancellationToken ct)
    => DecideAsync(taskId, version, TaskDecision.Approve(comment), ct);
public Task RejectAsync(Guid taskId, string version, string reason, CancellationToken ct)
    => DecideAsync(taskId, version, TaskDecision.Reject(reason), ct);
public Task FulfillAsync(Guid taskId, string version, string? note, CancellationToken ct)
    => DecideAsync(taskId, version, TaskDecision.Fulfill(note), ct);
```
Separate `PurchaseApprovalService` and `LeaveApprovalService` would be wrong: approval mechanics don't differ by request type.

## 3. SOLID, pragmatically

- **S** — One reason to change per class. A service that both submits requests and renders dashboards has two. A 600-line service is a signal, not automatically a violation; a service mixing unrelated features is.
- **O** — New request types plug in via `FixedProcesses` + details + submit endpoint (ARCHITECTURE.md §5). We do **not** build plugin discovery, reflection registries or strategy hierarchies for two types.
- **L** — Avoid inheritance for behaviour. Prefer composition; `sealed` classes by default.
- **I** — Ports are small and named for their use: `IRealtimePublisher`, `ICurrentUser`, `IIdentityAdmin`. No `IService<T>` mega-interfaces.
- **D** — Application depends on abstractions it owns (ports). Interfaces exist **only** at real boundaries (I/O, identity, time, realtime) or where a test needs a seam. Services themselves are concrete classes registered directly — no `IRequestSubmissionService` with one implementation.

## 4. Naming

| Item | Convention | Example |
|---|---|---|
| C# types/methods | PascalCase; async methods end in `Async` | `TaskDecisionService.ApproveAsync` |
| Private fields | `_camelCase` (primary-constructor parameters preferred) | |
| Interfaces | `I` + role | `IRealtimePublisher` |
| DTOs | `…Request` for inputs, `…Dto` for outputs | `SubmitLeaveRequest`, `RequestDetailsDto` |
| Booleans | `is/has/can` | `canComment`, `IsActive` |
| Error codes | `area.snake_case` | `task.already_decided` |
| TS files | components `PascalCase.tsx`, hooks `useThing.ts`, utils `camelCase.ts` | |
| React components | PascalCase, named exports | `export function StatusBadge` |
| Domain vocabulary | exactly as in `DOMAIN_MODEL.md` §1 | `WorkTask`, not `ApprovalItem` |

No abbreviations except well-known ones (`Id`, `Dto`, `Api`, `Url`). No magic strings: roles, policies, error codes, claim types, query keys are constants.

## 5. Size and shape

- Methods: aim ≤ 30 lines; more than 3 levels of nesting → extract or use guard clauses.
- Controllers: actions ≤ 10 lines; ≤ ~10 actions per controller.
- React components: aim ≤ 150 lines; pages compose sections; a component that fetches *and* renders a complex list *and* owns a dialog should be split.
- Files: one public type per file in C# except small related records (a DTO file may hold a request DTO and its nested records).
- Prefer early returns, `switch` expressions over `if/else` ladders on enums.

## 6. Error handling

- Throw typed exceptions (`DomainException(code)`, `NotFoundException`, `ForbiddenException`, `ConflictException`); **never** return error tuples or `null` for "not allowed".
- No `try/catch` in controllers. Catch only where you can add meaning (e.g. map `DbUpdateConcurrencyException` → `ConflictException(concurrency.conflict)` inside the service that saved) or clean up.
- Never swallow: no empty `catch`, no `catch { return null; }`.
- Messages in exceptions are for logs/`detail`; the UI uses `code` → message mapping.
- Frontend: `lib/api/client` converts non-2xx into a typed `ApiError` carrying the ProblemDetails; components render `toUserMessage(error)`. No `catch (e: any)`.

## 7. Validation layers (no inconsistent duplicates)

| Layer | Validates | Mechanism |
|---|---|---|
| Frontend | Shape & limits for UX | zod schema mirroring `API_CONTRACT.md` limits |
| Api | Shape & limits (authoritative for input) | DataAnnotations on `…Request` DTOs |
| Application | Preconditions needing data (manager exists, no cycle) | rule helpers |
| Domain | Invariants | aggregate methods / value objects |
| Database | Integrity | FKs, unique, check constraints |

Limits (max lengths, date windows) are **constants in one place per stack**: `Domain/Requests/RequestLimits.cs` and `frontend/src/features/requests/limits.ts`. The contract doc is the bridge; the integration tests assert the server limits.

## 8. Async & cancellation
All I/O is async; pass `CancellationToken` from the controller down to EF calls. No `.Result`/`.Wait()`. Don't pass tokens into the domain (it does no I/O).

## 9. Comments & docs
Code says *what*; comments say *why* (a rule's business reason, a non-obvious constraint). Public Application services get an XML summary of the use case. Update the relevant doc in the same change when behaviour changes.

## 10. Refactoring expectations
- Leave touched code slightly better; **don't** refactor unrelated areas in a feature task — open a follow-up task instead.
- When you find duplicated knowledge, consolidate it if it's inside your task's scope; otherwise report it with file references.
- Introducing a new shared abstraction requires: a clear single responsibility, ≥ 2 real call sites now, and a one-line note in the PR ("Added X to share Y between A and B").

## 11. Patterns we use / don't use

| Use | Don't use (Phase 1) |
|---|---|
| Aggregate with behaviour methods | Anemic entities + "manager" classes holding the rules |
| Feature-folder application services | MediatR / CQRS pipelines |
| Explicit mapping & LINQ projections | AutoMapper |
| `IAppDbContext` + query services | Generic `IRepository<T>` |
| Value objects for Money | Primitive obsession for money |
| Typed exceptions + global handler | Result<T> monads everywhere |
| `TimeProvider` | `DateTime.Now` |
| TanStack Query for server state | Redux / Zustand for server data |
| react-hook-form + zod | Hand-rolled form state per page |

## 12. Code review checklist
1. Is each change in the right layer (§1)?
2. Is any rule from §2.3 re-implemented instead of reused?
3. Any new abstraction justified by ≥ 2 real uses?
4. Authorization enforced server-side via the shared access helpers?
5. One `SaveChangesAsync` per use case; realtime after commit?
6. DTOs only at the boundary; no entity leaks?
7. Tests for the behaviour and its failure paths?
8. Contract, docs and generated types in sync?
9. UI uses shared components and tokens only?
10. Anything from a future phase sneaking in?
