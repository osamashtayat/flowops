using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Workflows;

namespace FlowOps.Domain.Requests;

/// <summary>
/// The aggregate root (DOMAIN_MODEL.md §2.3). Status and the current step change only through <see cref="DecideTask"/>,
/// which advances generically through the steps of the pinned <see cref="WorkflowVersion"/> (§7.4) and appends
/// activities in the same call.
/// </summary>
public sealed class Request
{
    private readonly List<WorkTask> _tasks = [];
    private readonly List<RequestActivity> _activities = [];

    /// <summary>For persistence materialization only; state is set by the store, never by callers.</summary>
    private Request()
    {
        Number = null!;
    }

    private Request(
        RequestNumber number, RequestType type, Guid requesterId, Guid departmentId, WorkflowVersion workflow, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (workflow.Status != WorkflowVersionStatus.Published)
        {
            throw new ArgumentException("A request is pinned to its type's Published workflow version.", nameof(workflow));
        }

        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException("A request needs a requester.", nameof(requesterId));
        }

        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("A request needs a department.", nameof(departmentId));
        }

        Id = SequentialGuid.NewV7(now);
        Number = number;
        Type = type;
        RequesterId = requesterId;
        DepartmentId = departmentId;
        WorkflowVersionId = workflow.Id;
        Status = RequestStatus.InReview;
        SubmittedAt = now;
    }

    public Guid Id { get; private set; }

    public RequestNumber Number { get; private set; }

    public RequestType Type { get; private set; }

    public Guid RequesterId { get; private set; }

    /// <summary>Snapshot of the requester's department at submission.</summary>
    public Guid DepartmentId { get; private set; }

    /// <summary>The workflow version every step of this request comes from, set at submission (DOMAIN_MODEL.md §7.4).</summary>
    public Guid WorkflowVersionId { get; private set; }

    public RequestStatus Status { get; private set; }

    /// <summary>Stored half of <see cref="Step"/>; <c>null</c> once the request is closed.</summary>
    public string? StepKey { get; private set; }

    /// <summary>Stored half of <see cref="Step"/>: the current step's name when it was reached.</summary>
    public string? StepName { get; private set; }

    /// <summary>The current step, or <c>null</c> once the request is closed: closed is a status, not a step.</summary>
    public StepRef? Step => StepRef.From(StepKey, StepName);

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>, ADR-0006); set by persistence only.</summary>
    public uint Version { get; private set; }

    public PurchaseDetails? Purchase { get; private set; }

    public LeaveDetails? Leave { get; private set; }

    public IReadOnlyList<WorkTask> Tasks => _tasks.AsReadOnly();

    public IReadOnlyList<RequestActivity> Activities => _activities.AsReadOnly();

    public bool IsClosed => Status is RequestStatus.Completed or RequestStatus.Rejected;

    /// <summary>The single open task (I4), or <c>null</c> once the request is closed.</summary>
    public WorkTask? OpenTask => _tasks.SingleOrDefault(t => t.IsOpen);

    /// <param name="workflow">The type's Published version; the request is pinned to it.</param>
    /// <param name="resolveAssignee">Resolves the first step's assignee, as <see cref="DecideTask"/> does for later steps.</param>
    public static Request SubmitPurchase(
        RequestNumber number, Guid requesterId, Guid departmentId, PurchaseDetails details,
        WorkflowVersion workflow, Func<WorkflowStep, Assignee> resolveAssignee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var request = new Request(number, RequestType.EquipmentPurchase, requesterId, departmentId, workflow, now) { Purchase = details };
        request.Start(workflow.FirstStep, resolveAssignee, now);
        return request;
    }

    /// <param name="workflow">The type's Published version; the request is pinned to it.</param>
    /// <param name="resolveAssignee">Resolves the first step's assignee, as <see cref="DecideTask"/> does for later steps.</param>
    public static Request SubmitLeave(
        RequestNumber number, Guid requesterId, Guid departmentId, LeaveDetails details,
        WorkflowVersion workflow, Func<WorkflowStep, Assignee> resolveAssignee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var request = new Request(number, RequestType.Leave, requesterId, departmentId, workflow, now) { Leave = details };
        request.Start(workflow.FirstStep, resolveAssignee, now);
        return request;
    }

    /// <summary>
    /// The only way state advances. Guards I6 (closed), I3 (decided once), I5 (current step), I2 (self-decision)
    /// and the valid action for the task kind, all before anything changes; then appends activities (I9) and
    /// opens the next step's task or closes the request (I7). Eligibility (I1) is the Application layer's job.
    /// </summary>
    /// <param name="workflow">The version this request is pinned to (<see cref="WorkflowVersionId"/>).</param>
    /// <param name="resolveAssignee">Resolves the next step's assignee; only called when a next step exists.</param>
    public DecisionResult DecideTask(
        Guid taskId, Guid actorId, TaskDecision decision, WorkflowVersion workflow,
        Func<WorkflowStep, Assignee> resolveAssignee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(resolveAssignee);
        if (workflow.Id != WorkflowVersionId)
        {
            throw new ArgumentException("A request advances only through the workflow version it is pinned to.", nameof(workflow));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("A decision needs an actor.", nameof(actorId));
        }

        EnsureNotClosed();
        var task = _tasks.SingleOrDefault(t => t.Id == taskId) ?? throw DomainErrors.TaskNotFound();
        task.EnsureOpen();
        if (task.StepKey != StepKey)
        {
            throw DomainErrors.TaskNotCurrent();
        }

        if (actorId == RequesterId)
        {
            throw DomainErrors.SelfDecision();
        }

        var outcome = task.OutcomeFor(decision.Action);
        var nextStep = outcome == WorkTaskOutcome.Rejected ? null : workflow.StepAfter(task.StepKey);
        var nextAssignee = nextStep is null ? null : ResolveAssignee(nextStep, resolveAssignee);

        // Every check and lookup has passed: from here on the aggregate changes.
        task.Complete(outcome, decision.Comment, actorId, now);
        Append(ActivityFor(outcome), actorId, task.Id, task.Step, decision.Comment, now);

        WorkTask? nextTask = null;
        if (outcome == WorkTaskOutcome.Rejected)
        {
            Close(RequestStatus.Rejected, now);
        }
        else if (nextStep is null)
        {
            Close(RequestStatus.Completed, now);
            Append(ActivityType.Completed, actorId: null, taskId: null, step: null, body: null, now);
        }
        else
        {
            nextTask = AdvanceTo(nextStep, nextAssignee!, now);
        }

        return new DecisionResult(task, nextTask, Status, Step);
    }

    /// <summary>Free-form comment. Allowed on closed requests (follow-up notes, API_CONTRACT.md §4).</summary>
    public RequestActivity AddComment(Guid actorId, string body, DateTimeOffset now)
    {
        var text = Text.Required(body, RequestLimits.CommentMaxLength, "body");
        return Append(ActivityType.CommentAdded, actorId, taskId: null, step: null, text, now);
    }

    /// <summary>
    /// Validated before anything changes, so a faulty resolver cannot leave a half-applied decision.
    /// A wrong assignee is a programming error (ArgumentException), not a business-rule violation.
    /// </summary>
    private static Assignee ResolveAssignee(WorkflowStep step, Func<WorkflowStep, Assignee> resolveAssignee)
    {
        ArgumentNullException.ThrowIfNull(resolveAssignee);
        var assignee = resolveAssignee(step)
            ?? throw new ArgumentNullException(nameof(resolveAssignee), $"No assignee was resolved for the '{step.Key}' step.");

        return step.Assignee.Accepts(assignee)
            ? assignee
            : throw new ArgumentException($"The resolved assignee does not satisfy the '{step.Key}' step's rule.", nameof(resolveAssignee));
    }

    /// <summary>
    /// Opens the process's first step through the same resolver path as every later step, so a type whose first step is
    /// a role queue needs nothing here. The Application layer checks I12 first (manager exists and is active); this guards
    /// the part the domain can know: a requester is never their own approver (I2).
    /// </summary>
    private void Start(WorkflowStep first, Func<WorkflowStep, Assignee> resolveAssignee, DateTimeOffset now)
    {
        var assignee = ResolveAssignee(first, resolveAssignee);
        if (assignee.UserId == RequesterId)
        {
            throw DomainErrors.ManagerNotAssigned();
        }

        Append(ActivityType.Submitted, RequesterId, taskId: null, first.Ref, body: null, now);
        AdvanceTo(first, assignee, now);
    }

    private WorkTask AdvanceTo(WorkflowStep step, Assignee assignee, DateTimeOffset now)
    {
        if (_tasks.Any(t => t.IsOpen))
        {
            throw new InvalidOperationException("A request can have at most one open task."); // I4
        }

        var task = WorkTask.Open(Id, step, assignee, now);
        _tasks.Add(task);
        StepKey = step.Key;
        StepName = step.Name;
        // Approved = every approval granted, only fulfillment left.
        Status = step.Kind == WorkTaskKind.Fulfillment ? RequestStatus.Approved : RequestStatus.InReview;
        Append(ActivityType.TaskAssigned, actorId: null, task.Id, step.Ref, body: null, now);
        return task;
    }

    private void Close(RequestStatus terminalStatus, DateTimeOffset now)
    {
        Status = terminalStatus;
        StepKey = null;
        StepName = null;
        ClosedAt = now;
    }

    private void EnsureNotClosed()
    {
        if (IsClosed)
        {
            throw DomainErrors.RequestClosed();
        }
    }

    private RequestActivity Append(
        ActivityType type, Guid? actorId, Guid? taskId, StepRef? step, string? body, DateTimeOffset now)
    {
        var activity = RequestActivity.Create(Id, type, actorId, taskId, step, body, now);
        _activities.Add(activity);
        return activity;
    }

    private static ActivityType ActivityFor(WorkTaskOutcome outcome) => outcome switch
    {
        WorkTaskOutcome.Approved => ActivityType.Approved,
        WorkTaskOutcome.Rejected => ActivityType.Rejected,
        WorkTaskOutcome.Fulfilled => ActivityType.Fulfilled,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
