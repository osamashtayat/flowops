namespace FlowOps.Domain.Requests;

/// <summary>What an actor asks to do with a task.</summary>
public enum TaskDecisionAction
{
    Approve,
    Reject,
    Fulfill,
}
