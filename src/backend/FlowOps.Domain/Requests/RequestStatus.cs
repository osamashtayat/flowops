namespace FlowOps.Domain.Requests;

/// <summary>Overall request state (DOMAIN_MODEL.md §3.1). Completed and Rejected are terminal.</summary>
public enum RequestStatus
{
    InReview,
    Approved,
    Completed,
    Rejected,
}
