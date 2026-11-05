namespace FlowOps.Domain.Requests;

/// <summary>The current process step, or Closed (DOMAIN_MODEL.md §3.2).</summary>
public enum RequestStage
{
    ManagerApproval,
    FinanceApproval,
    HrConfirmation,
    ProcurementFulfillment,
    Closed,
}
