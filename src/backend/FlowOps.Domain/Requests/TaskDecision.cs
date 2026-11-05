using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Requests;

/// <summary>What an actor decided, with its comment. Reject requires a non-blank reason (I8).</summary>
public sealed record TaskDecision
{
    private TaskDecision(TaskDecisionAction action, string? comment)
    {
        Action = action;
        Comment = comment;
    }

    public TaskDecisionAction Action { get; }

    public string? Comment { get; }

    public static TaskDecision Approve(string? comment) =>
        new(TaskDecisionAction.Approve, Text.Optional(comment, RequestLimits.CommentMaxLength, "comment"));

    public static TaskDecision Reject(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw DomainErrors.Validation("A rejection reason is required.")
            : new(TaskDecisionAction.Reject, Text.Required(reason, RequestLimits.CommentMaxLength, "reason"));

    public static TaskDecision Fulfill(string? note) =>
        new(TaskDecisionAction.Fulfill, Text.Optional(note, RequestLimits.CommentMaxLength, "note"));
}
