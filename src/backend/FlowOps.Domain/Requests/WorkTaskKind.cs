namespace FlowOps.Domain.Requests;

/// <summary>Whether a task is an approval decision or a fulfillment action.</summary>
public enum WorkTaskKind
{
    Approval,
    Fulfillment,
}
