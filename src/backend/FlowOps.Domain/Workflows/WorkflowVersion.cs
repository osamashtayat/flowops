using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>
/// One numbered version of a workflow: an ordered list of steps (DOMAIN_MODEL.md §7.1). Only a Draft can change;
/// Published and Retired versions are immutable, so a request pinned to a version keeps its route (§7.4).
/// </summary>
public sealed class WorkflowVersion
{
    private readonly List<WorkflowStep> _steps = [];

    /// <summary>For persistence materialization only.</summary>
    private WorkflowVersion()
    {
    }

    private WorkflowVersion(Guid workflowId, int number, Guid createdById, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        WorkflowId = workflowId;
        Number = number;
        Status = WorkflowVersionStatus.Draft;
        CreatedAt = now;
        CreatedById = createdById;
    }

    public Guid Id { get; private set; }

    public Guid WorkflowId { get; private set; }

    /// <summary>1, 2, 3 … per workflow, never reused.</summary>
    public int Number { get; private set; }

    public WorkflowVersionStatus Status { get; private set; }

    /// <summary>In <see cref="WorkflowStep.Order"/>.</summary>
    public IReadOnlyList<WorkflowStep> Steps => _steps.OrderBy(s => s.Order).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary><c>null</c> only for the versions the database seeds (v1), which no user created.</summary>
    public Guid? CreatedById { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary><c>null</c> until published, and for the seeded v1.</summary>
    public Guid? PublishedById { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>, ADR-0006); set by persistence only.</summary>
    public uint Version { get; private set; }

    /// <summary>A step every request of this version starts with (it never has a condition).</summary>
    public WorkflowStep FirstStep => Steps[0];

    public static WorkflowVersion CreateDraft(
        Guid workflowId, int number, IEnumerable<WorkflowStep> steps, Guid createdById, DateTimeOffset now)
    {
        if (workflowId == Guid.Empty || createdById == Guid.Empty)
        {
            throw new ArgumentException("A version needs a workflow and an author.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        var version = new WorkflowVersion(workflowId, number, createdById, now);
        version.SetSteps(steps);
        return version;
    }

    /// <summary>Only a Draft can be edited (<c>workflow.version_not_draft</c> otherwise).</summary>
    public void ReplaceSteps(IEnumerable<WorkflowStep> steps)
    {
        EnsureDraft();
        SetSteps(steps);
    }

    /// <summary>Draft → Published. Retiring the previous Published version is the caller's job, in the same transaction.</summary>
    public void Publish(Guid actorId, DateTimeOffset now)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Publishing needs an actor.", nameof(actorId));
        }

        EnsureDraft();
        EnsurePublishable();
        Status = WorkflowVersionStatus.Published;
        PublishedAt = now;
        PublishedById = actorId;
    }

    /// <summary>Published → Retired, when a newer version is published.</summary>
    public void Retire(DateTimeOffset now)
    {
        if (Status != WorkflowVersionStatus.Published)
        {
            throw new InvalidOperationException($"Only the Published version can be retired; this one is {Status}.");
        }

        Status = WorkflowVersionStatus.Retired;
        RetiredAt = now;
    }

    /// <summary>The step after <paramref name="key"/> in version order, or <c>null</c> when it is the last one.</summary>
    public WorkflowStep? StepAfter(string key)
    {
        var steps = Steps;
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Key == key)
            {
                return i + 1 < steps.Count ? steps[i + 1] : null;
            }
        }

        throw new ArgumentException($"Version {Id} has no step '{key}'.", nameof(key));
    }

    private void EnsureDraft()
    {
        if (Status != WorkflowVersionStatus.Draft)
        {
            throw DomainErrors.WorkflowVersionNotDraft();
        }
    }

    private void SetSteps(IEnumerable<WorkflowStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var list = steps.ToList();
        if (list.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() != list.Count)
        {
            throw DomainErrors.Validation("Each step needs its own key.", "steps");
        }

        _steps.Clear();
        _steps.AddRange(list.Select((s, i) => s.At(i)));
    }

    /// <summary>The structural rules every Published version keeps (§7.1). P2-BE-07 reports them as draft issues first.</summary>
    private void EnsurePublishable()
    {
        var steps = Steps;
        var fulfillments = steps.Count(s => s.Kind == WorkTaskKind.Fulfillment);
        var fulfillmentIsLast = fulfillments == 0 || steps[^1].Kind == WorkTaskKind.Fulfillment;
        if (steps.Count == 0 || fulfillments > 1 || !fulfillmentIsLast)
        {
            throw DomainErrors.WorkflowInvalid();
        }
    }
}
