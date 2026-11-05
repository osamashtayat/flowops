using System.Collections.ObjectModel;
using FlowOps.Domain.Organization;

namespace FlowOps.Domain.Requests;

/// <summary>
/// The only place process step order exists (DOMAIN_MODEL.md §3.3, ADR-0004).
/// Plain data the <see cref="Request"/> aggregate advances through; Phase 2 replaces it with persisted definitions.
/// </summary>
public static class FixedProcesses
{
    private static readonly ReadOnlyCollection<ProcessStep> EquipmentPurchase = Array.AsReadOnly(
    [
        new ProcessStep(RequestStage.ManagerApproval, WorkTaskKind.Approval, AssigneeRule.RequesterManager),
        new ProcessStep(RequestStage.FinanceApproval, WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.FinanceOfficer)),
        new ProcessStep(RequestStage.ProcurementFulfillment, WorkTaskKind.Fulfillment, AssigneeRule.ForRole(Roles.ProcurementOfficer)),
    ]);

    private static readonly ReadOnlyCollection<ProcessStep> Leave = Array.AsReadOnly(
    [
        new ProcessStep(RequestStage.ManagerApproval, WorkTaskKind.Approval, AssigneeRule.RequesterManager),
        new ProcessStep(RequestStage.HrConfirmation, WorkTaskKind.Approval, AssigneeRule.ForRole(Roles.HrOfficer)),
    ]);

    public static IReadOnlyList<ProcessStep> For(RequestType type) => type switch
    {
        RequestType.EquipmentPurchase => EquipmentPurchase,
        RequestType.Leave => Leave,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown request type."),
    };

    public static ProcessStep First(RequestType type) => For(type)[0];

    /// <summary>The step after <paramref name="stage"/>, or <c>null</c> when it is the last one.</summary>
    public static ProcessStep? Next(RequestType type, RequestStage stage)
    {
        var steps = For(type);
        var index = IndexOf(steps, type, stage);
        return index + 1 < steps.Count ? steps[index + 1] : null;
    }

    private static int IndexOf(IReadOnlyList<ProcessStep> steps, RequestType type, RequestStage stage)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Stage == stage)
            {
                return i;
            }
        }

        throw new ArgumentException($"{type} has no step {stage}.", nameof(stage));
    }
}
