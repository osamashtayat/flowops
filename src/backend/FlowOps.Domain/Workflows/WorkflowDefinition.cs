using FlowOps.Domain.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>The workflow of one request type (DOMAIN_MODEL.md §7.1; one per type in Phase 2a). Its steps live in versions.</summary>
public sealed class WorkflowDefinition
{
    public const int NameMaxLength = 100;

    /// <summary>For persistence materialization only.</summary>
    private WorkflowDefinition()
    {
        Name = null!;
    }

    private WorkflowDefinition(RequestType requestType, string name, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        RequestType = requestType;
        Name = name;
    }

    public Guid Id { get; private set; }

    public RequestType RequestType { get; private set; }

    public string Name { get; private set; }

    public static WorkflowDefinition Create(RequestType requestType, string name, DateTimeOffset now) =>
        new(requestType, Text.Required(name, NameMaxLength, "name"), now);
}
