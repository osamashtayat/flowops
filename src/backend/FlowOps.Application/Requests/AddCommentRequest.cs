using System.ComponentModel.DataAnnotations;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Requests;

/// <summary><c>POST /api/requests/{id}/comments</c>.</summary>
public sealed record AddCommentRequest
{
    [Required]
    [StringLength(RequestLimits.CommentMaxLength, MinimumLength = 1)]
    public string Body { get; init; } = "";
}
