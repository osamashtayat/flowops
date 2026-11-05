using System.Text.Json.Serialization;

namespace FlowOps.Application.Access;

/// <summary>The caller's possible decisions on the current task, and the version to echo back.</summary>
public sealed record AvailableActionsDto(Guid TaskId, IReadOnlyList<TaskAction> Actions, string Version);

public enum TaskAction
{
    [JsonStringEnumMemberName("approve")]
    Approve,

    [JsonStringEnumMemberName("reject")]
    Reject,

    [JsonStringEnumMemberName("fulfill")]
    Fulfill,
}
