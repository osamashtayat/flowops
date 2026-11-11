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

    /// <summary>A step whose condition was false; no task was created (ADR-0014 §3). Appended from P2-BE-05.</summary>
    StepSkipped,
}
