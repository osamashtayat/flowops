namespace FlowOps.Domain.Requests;

/// <summary>
/// What a decision did, so the Application layer can create notifications without re-deriving the outcome.
/// <see cref="NextTask"/> is <c>null</c> when the request closed.
/// </summary>
public sealed record DecisionResult(WorkTask DecidedTask, WorkTask? NextTask, RequestStatus Status, RequestStage Stage);
