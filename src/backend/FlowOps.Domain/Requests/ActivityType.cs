namespace FlowOps.Domain.Requests;

/// <summary>Kinds of append-only audit entries on a request.</summary>
public enum ActivityType
{
    Submitted,
    TaskAssigned,
    Approved,
    Rejected,
    Fulfilled,
    Completed,
    CommentAdded,
}
