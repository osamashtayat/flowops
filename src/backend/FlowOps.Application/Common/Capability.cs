using System.Text.Json.Serialization;

namespace FlowOps.Application.Common;

/// <summary>What the UI may offer the current user (AUTHORIZATION.md §2). Computed by the server, never by the UI.</summary>
public enum Capability
{
    [JsonStringEnumMemberName("requests.submit")]
    RequestsSubmit,

    [JsonStringEnumMemberName("tasks.view")]
    TasksView,

    [JsonStringEnumMemberName("admin.manage")]
    AdminManage,

    /// <summary>Edit and publish workflows (ADR-0014 Q1): derived from Administrator, like <see cref="AdminManage"/>.</summary>
    [JsonStringEnumMemberName("workflows.manage")]
    WorkflowsManage,
}
