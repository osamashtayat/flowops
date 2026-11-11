using System.Diagnostics;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary>
/// A process step as the API shows it (ADR-0014 Q2): a stable <see cref="Key"/> unique within a workflow version and
/// the <see cref="Name"/> users see. Replaces the <c>stage</c> enum; both are served until P2-BE-04 removes <c>stage</c>.
/// </summary>
public sealed record StepRef(string Key, string Name)
{
    /// <summary>
    /// The seeded v1 key and name of a Phase 1 stage (DOMAIN_MODEL.md §7.4). The single home of this mapping until
    /// P2-BE-04 stores the key and a name snapshot on tasks and activities. <c>Closed</c> is a status, not a step.
    /// </summary>
    public static StepRef? For(RequestStage stage) => stage switch
    {
        RequestStage.ManagerApproval => new("manager-approval", "Manager approval"),
        RequestStage.FinanceApproval => new("finance-approval", "Finance approval"),
        RequestStage.HrConfirmation => new("hr-confirmation", "HR confirmation"),
        RequestStage.ProcurementFulfillment => new("procurement-fulfillment", "Procurement fulfilment"),
        RequestStage.Closed => null,
        _ => throw new UnreachableException($"Unknown stage {stage}."),
    };

    /// <summary>For a task's step, which is never <c>Closed</c>.</summary>
    public static StepRef ForTask(RequestStage stage) =>
        For(stage) ?? throw new UnreachableException("A task always belongs to a process step.");
}
