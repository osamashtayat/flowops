using System.Diagnostics;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Requests;

/// <summary>
/// A process step as the API shows it (ADR-0014 Q2): a stable <see cref="Key"/> unique within a workflow version and
/// the <see cref="Name"/> users see. Replaces the <c>stage</c> enum; both are served until P2-BE-04 removes <c>stage</c>.
/// </summary>
public sealed record StepRef(string Key, string Name)
{
    /// <summary>
    /// The seeded v1 key (<see cref="StageKeys"/>) and name of a Phase 1 stage (DOMAIN_MODEL.md §7.4), until P2-BE-04
    /// stores the key and a name snapshot on tasks and activities. <c>Closed</c> is a status, not a step. The names equal
    /// the seeded v1 step names (asserted by the seed tests).
    /// </summary>
    public static StepRef? For(RequestStage stage) => StageKeys.KeyOf(stage) is { } key ? new(key, NameOf(stage)) : null;

    /// <summary>For a task's step, which is never <c>Closed</c>.</summary>
    public static StepRef ForTask(RequestStage stage) =>
        For(stage) ?? throw new UnreachableException("A task always belongs to a process step.");

    /// <summary>A step of a workflow version, as stored.</summary>
    public static StepRef For(WorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(step.Key, step.Name);
    }

    private static string NameOf(RequestStage stage) => stage switch
    {
        RequestStage.ManagerApproval => "Manager approval",
        RequestStage.FinanceApproval => "Finance approval",
        RequestStage.HrConfirmation => "HR confirmation",
        RequestStage.ProcurementFulfillment => "Procurement fulfilment",
        _ => throw new UnreachableException($"{stage} is not a step."),
    };
}
