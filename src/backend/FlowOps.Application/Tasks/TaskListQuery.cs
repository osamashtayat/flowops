using FlowOps.Application.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Application.Tasks;

/// <summary><c>GET /api/tasks</c>: Open = tasks the caller may decide (oldest first); Done = tasks they decided (newest first).</summary>
public sealed record TaskListQuery : PageRequest
{
    public WorkTaskStatus Status { get; init; } = WorkTaskStatus.Open;
}
