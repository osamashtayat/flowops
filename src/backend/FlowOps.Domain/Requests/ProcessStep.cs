namespace FlowOps.Domain.Requests;

/// <summary>One position in a fixed process (ADR-0004).</summary>
public sealed record ProcessStep(RequestStage Stage, WorkTaskKind Kind, AssigneeRule Assignee);
