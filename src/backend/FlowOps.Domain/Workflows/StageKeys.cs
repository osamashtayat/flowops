using System.Diagnostics;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>
/// Phase 1 <see cref="RequestStage"/> values ↔ the seeded v1 step keys, one to one (DOMAIN_MODEL.md §7.4). Requests,
/// tasks and activities still store the stage until P2-BE-04 stores the step key instead and deletes this mapping.
/// </summary>
public static class StageKeys
{
    public const string ManagerApproval = "manager-approval";
    public const string FinanceApproval = "finance-approval";
    public const string HrConfirmation = "hr-confirmation";
    public const string ProcurementFulfillment = "procurement-fulfillment";

    /// <summary>The v1 key of a stage, or <c>null</c> for <c>Closed</c>, which is a status, not a step.</summary>
    public static string? KeyOf(RequestStage stage) => stage switch
    {
        RequestStage.ManagerApproval => ManagerApproval,
        RequestStage.FinanceApproval => FinanceApproval,
        RequestStage.HrConfirmation => HrConfirmation,
        RequestStage.ProcurementFulfillment => ProcurementFulfillment,
        RequestStage.Closed => null,
        _ => throw new UnreachableException($"Unknown stage {stage}."),
    };

    /// <summary>The stage stored for a step. Only the v1 keys have one until P2-BE-04.</summary>
    public static RequestStage StageOf(string key) => key switch
    {
        ManagerApproval => RequestStage.ManagerApproval,
        FinanceApproval => RequestStage.FinanceApproval,
        HrConfirmation => RequestStage.HrConfirmation,
        ProcurementFulfillment => RequestStage.ProcurementFulfillment,
        _ => throw new InvalidOperationException($"The step '{key}' has no Phase 1 stage; storing new step keys is P2-BE-04."),
    };
}
