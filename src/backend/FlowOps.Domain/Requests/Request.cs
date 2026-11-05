using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Requests;

/// <summary>
/// The aggregate root (DOMAIN_MODEL.md §2.3). Status and Stage change only through <see cref="DecideTask"/>,
/// which advances generically through <see cref="FixedProcesses"/> and appends activities in the same call.
/// </summary>
public sealed class Request
{
    private readonly List<WorkTask> _tasks = [];
    private readonly List<RequestActivity> _activities = [];

    private Request(RequestNumber number, RequestType type, Guid requesterId, Guid departmentId, DateTimeOffset now)
    {
        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException("A request needs a requester.", nameof(requesterId));
        }

        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("A request needs a department.", nameof(departmentId));
        }

        Id = Guid.CreateVersion7(now);
        Number = number;
        Type = type;
        RequesterId = requesterId;
        DepartmentId = departmentId;
        Status = RequestStatus.InReview;
        SubmittedAt = now;
    }

    public Guid Id { get; private set; }

    public RequestNumber Number { get; private set; }

    public RequestType Type { get; private set; }

    public Guid RequesterId { get; private set; }

    /// <summary>Snapshot of the requester's department at submission.</summary>
    public Guid DepartmentId { get; private set; }

    public RequestStatus Status { get; private set; }

    public RequestStage Stage { get; private set; }

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

    public static Request SubmitPurchase(
        RequestNumber number, Guid requesterId, Guid departmentId, PurchaseDetails details, Guid managerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var request = new Request(number, RequestType.EquipmentPurchase, requesterId, departmentId, now) { Purchase = details };
        request.Start(managerId, now);
        return request;
    }

    public static Request SubmitLeave(
        RequestNumber number, Guid requesterId, Guid departmentId, LeaveDetails details, Guid managerId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        var request = new Request(number, RequestType.Leave, requesterId, departmentId, now) { Leave = details };
        request.Start(managerId, now);
        return request;
    }

    /// <summary>
    /// The only way state advances. Guards I6 (closed), I3 (decided once), I5 (current step), I2 (self-decision)
    /// and the valid action for the task kind, all before anything changes; then appends activities (I9) and
    /// opens the next step's task or closes the request (I7). Eligibility (I1) is the Application layer's job.
    /// </summary>
    /// <param name="resolveAssignee">Resolves the next step's assignee; only called when a next step exists.</param>
    public DecisionResult DecideTask(
        Guid taskId, Guid actorId, TaskDecision decision, Func<ProcessStep, Assignee> resolveAssignee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(resolveAssignee);
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("A decision needs an actor.", nameof(actorId));
        }

        EnsureNotClosed();
        var task = _tasks.SingleOrDefault(t => t.Id == taskId) ?? throw DomainErrors.TaskNotFound();
        task.EnsureOpen();
        if (task.Stage != Stage)
        {
            throw DomainErrors.TaskNotCurrent();
        }

        if (actorId == RequesterId)
        {
            throw DomainErrors.SelfDecision();
        }

        var outcome = task.OutcomeFor(decision.Action);
        var nextStep = outcome == WorkTaskOutcome.Rejected ? null : FixedProcesses.Next(Type, task.Stage);
        var nextAssignee = nextStep is null ? null : ResolveNextAssignee(nextStep, resolveAssignee);

        // Every check and lookup has passed: from here on the aggregate changes.
        task.Complete(outcome, decision.Comment, actorId, now);
        Append(ActivityFor(outcome), actorId, task.Id, task.Stage, decision.Comment, now);

        WorkTask? nextTask = null;
        if (outcome == WorkTaskOutcome.Rejected)
        {
            Close(RequestStatus.Rejected, now);
        }
        else if (nextStep is null)
        {
            Close(RequestStatus.Completed, now);
            Append(ActivityType.Completed, actorId: null, taskId: null, RequestStage.Closed, body: null, now);
        }
        else
        {
            nextTask = AdvanceTo(nextStep, nextAssignee!, now);
        }

        return new DecisionResult(task, nextTask, Status, Stage);
    }

    /// <summary>Free-form comment. Allowed on closed requests (follow-up notes, API_CONTRACT.md §4).</summary>
    public RequestActivity AddComment(Guid actorId, string body, DateTimeOffset now)
    {
        var text = Text.Required(body, RequestLimits.CommentMaxLength, "body");
        return Append(ActivityType.CommentAdded, actorId, taskId: null, stage: null, text, now);
    }

    /// <summary>
    /// Validated before anything changes, so a faulty resolver cannot leave a half-applied decision.
    /// A wrong assignee is a programming error (ArgumentException), not a business-rule violation.
    /// </summary>
    private static Assignee ResolveNextAssignee(ProcessStep nextStep, Func<ProcessStep, Assignee> resolveAssignee)
    {
        var assignee = resolveAssignee(nextStep)
            ?? throw new ArgumentNullException(nameof(resolveAssignee), $"No assignee was resolved for the {nextStep.Stage} step.");

        return nextStep.Assignee.Accepts(assignee)
            ? assignee
            : throw new ArgumentException($"The resolved assignee does not satisfy the {nextStep.Stage} step's rule.", nameof(resolveAssignee));
    }

    private void Start(Guid managerId, DateTimeOffset now)
    {
        // The Application layer checks I12 first (manager exists and is active); this guards the part the domain can know.
        if (managerId == Guid.Empty || managerId == RequesterId)
        {
            throw DomainErrors.ManagerNotAssigned();
        }

        var first = FixedProcesses.First(Type);
        Append(ActivityType.Submitted, RequesterId, taskId: null, first.Stage, body: null, now);
        AdvanceTo(first, Assignee.ForUser(managerId), now);
    }

    private WorkTask AdvanceTo(ProcessStep step, Assignee assignee, DateTimeOffset now)
    {
        if (_tasks.Any(t => t.IsOpen))
        {
            throw new InvalidOperationException("A request can have at most one open task."); // I4
        }

        var task = WorkTask.Open(Id, step, assignee, now);
        _tasks.Add(task);
        Stage = step.Stage;
        // Approved = every approval granted, only fulfillment left.
        Status = step.Kind == WorkTaskKind.Fulfillment ? RequestStatus.Approved : RequestStatus.InReview;
        Append(ActivityType.TaskAssigned, actorId: null, task.Id, step.Stage, body: null, now);
        return task;
    }

    private void Close(RequestStatus terminalStatus, DateTimeOffset now)
    {
        Status = terminalStatus;
        Stage = RequestStage.Closed;
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
        ActivityType type, Guid? actorId, Guid? taskId, RequestStage? stage, string? body, DateTimeOffset now)
    {
        var activity = RequestActivity.Create(Id, type, actorId, taskId, stage, body, now);
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
