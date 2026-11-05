using FlowOps.Domain.Common;
using System.Collections.ObjectModel;
using System.Diagnostics;
using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Requests;

/// <summary>
/// One unit of work on a request: an approval decision or a fulfillment action (ADR-0005).
/// Created and completed only by the <see cref="Request"/> aggregate.
/// </summary>
public sealed class WorkTask
{
    /// <summary>For persistence materialization only; state is set by the store, never by callers.</summary>
    private WorkTask()
    {
    }

    private WorkTask(Guid requestId, ProcessStep step, Assignee assignee, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        RequestId = requestId;
        Kind = step.Kind;
        Stage = step.Stage;
        AssigneeUserId = assignee.UserId;
        AssigneeRole = assignee.Role;
        Status = WorkTaskStatus.Open;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid RequestId { get; private set; }

    public WorkTaskKind Kind { get; private set; }

    public RequestStage Stage { get; private set; }

    public Guid? AssigneeUserId { get; private set; }

    public string? AssigneeRole { get; private set; }

    public WorkTaskStatus Status { get; private set; }

    public WorkTaskOutcome? Outcome { get; private set; }

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>, ADR-0006); set by persistence only.</summary>
    public uint Version { get; private set; }

    public bool IsOpen => Status == WorkTaskStatus.Open;

    internal static WorkTask Open(Guid requestId, ProcessStep step, Assignee assignee, DateTimeOffset now)
    {
        if (!step.Assignee.Accepts(assignee))
        {
            throw new ArgumentException($"The assignee does not satisfy the {step.Stage} step's rule.", nameof(assignee));
        }

        return new WorkTask(requestId, step, assignee, now);
    }

    /// <summary>I3: a task can be decided once.</summary>
    internal void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw DomainErrors.TaskAlreadyDecided();
        }
    }

    private static readonly ReadOnlyCollection<TaskDecisionAction> ApprovalActions =
        Array.AsReadOnly([TaskDecisionAction.Approve, TaskDecisionAction.Reject]);

    private static readonly ReadOnlyCollection<TaskDecisionAction> FulfillmentActions = Array.AsReadOnly([TaskDecisionAction.Fulfill]);

    /// <summary>The actions valid for this task's kind (what the UI offers): Approval → Approve/Reject; Fulfillment → Fulfill.</summary>
    public IReadOnlyList<TaskDecisionAction> AllowedActions => Kind switch
    {
        WorkTaskKind.Approval => ApprovalActions,
        WorkTaskKind.Fulfillment => FulfillmentActions,
        _ => throw new UnreachableException($"Unknown task kind {Kind}."),
    };

    /// <summary>Valid (Kind, action) pairs and their outcomes; must agree with <see cref="AllowedActions"/> (a test checks).</summary>
    internal WorkTaskOutcome OutcomeFor(TaskDecisionAction action) => (Kind, action) switch
    {
        (WorkTaskKind.Approval, TaskDecisionAction.Approve) => WorkTaskOutcome.Approved,
        (WorkTaskKind.Approval, TaskDecisionAction.Reject) => WorkTaskOutcome.Rejected,
        (WorkTaskKind.Fulfillment, TaskDecisionAction.Fulfill) => WorkTaskOutcome.Fulfilled,
        _ => throw DomainErrors.TaskInvalidAction(Kind, action),
    };

    internal void Complete(WorkTaskOutcome outcome, string? comment, Guid actorId, DateTimeOffset now)
    {
        EnsureOpen();
        Status = WorkTaskStatus.Done;
        Outcome = outcome;
        Comment = comment;
        DecidedById = actorId;
        DecidedAt = now;
    }
}
