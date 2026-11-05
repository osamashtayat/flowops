namespace FlowOps.Domain.Requests;

/// <summary>The decision recorded on a Done WorkTask.</summary>
public enum WorkTaskOutcome
{
    Approved,
    Rejected,
    Fulfilled,
}
