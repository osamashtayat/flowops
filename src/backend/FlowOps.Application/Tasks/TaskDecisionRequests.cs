using System.ComponentModel.DataAnnotations;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Tasks;

/// <summary><c>POST /api/tasks/{taskId}/approve</c>. <see cref="Version"/> is the opaque value the client last saw.</summary>
public sealed record ApproveTaskRequest
{
    [Required]
    public string Version { get; init; } = "";

    [StringLength(RequestLimits.CommentMaxLength)]
    public string? Comment { get; init; }
}

/// <summary><c>POST /api/tasks/{taskId}/reject</c>. The reason is required and may not be whitespace (I8).</summary>
public sealed record RejectTaskRequest
{
    [Required]
    public string Version { get; init; } = "";

    [Required]
    [StringLength(RequestLimits.CommentMaxLength, MinimumLength = 1)]
    public string Reason { get; init; } = "";
}

/// <summary><c>POST /api/tasks/{taskId}/fulfill</c>.</summary>
public sealed record FulfillTaskRequest
{
    [Required]
    public string Version { get; init; } = "";

    [StringLength(RequestLimits.CommentMaxLength)]
    public string? Note { get; init; }
}
