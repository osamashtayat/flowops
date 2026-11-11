using System.Text.RegularExpressions;
using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Requests;

namespace FlowOps.Domain.Workflows;

/// <summary>One position in a workflow version (DOMAIN_MODEL.md §7.1), owned by that version.</summary>
public sealed partial class WorkflowStep
{
    public const int KeyMaxLength = 40;
    public const int NameMaxLength = 80;

    /// <summary>For persistence materialization only.</summary>
    private WorkflowStep()
    {
        Key = null!;
        Name = null!;
    }

    private WorkflowStep(int order, string key, string name, WorkTaskKind kind, AssigneeRule assignee)
    {
        Order = order;
        Key = key;
        Name = name;
        Kind = kind;
        AssigneeKind = assignee.Kind;
        AssigneeRole = assignee.Role;
    }

    /// <summary>0-based position in the version; set by the version.</summary>
    public int Order { get; private set; }

    /// <summary>Stable slug, unique within the version (<c>manager-approval</c>).</summary>
    public string Key { get; private set; }

    /// <summary>What users see ("Finance approval").</summary>
    public string Name { get; private set; }

    public WorkTaskKind Kind { get; private set; }

    /// <summary>Stored half of <see cref="Assignee"/>.</summary>
    public AssigneeRuleKind AssigneeKind { get; private set; }

    /// <summary>Stored half of <see cref="Assignee"/>: the role queue, for a role rule.</summary>
    public string? AssigneeRole { get; private set; }

    public AssigneeRule Assignee => AssigneeRule.From(AssigneeKind, AssigneeRole);

    /// <summary>The key and name snapshot requests, tasks and activities store for this step.</summary>
    public StepRef Ref => new(Key, Name);

    public static WorkflowStep Create(string key, string name, WorkTaskKind kind, AssigneeRule assignee)
    {
        ArgumentNullException.ThrowIfNull(assignee);
        if (key is null || key.Length > KeyMaxLength || !KeyPattern().IsMatch(key))
        {
            throw DomainErrors.Validation(
                $"key must be lower-case letters and digits separated by single hyphens, at most {KeyMaxLength} characters.", "key");
        }

        return Enum.IsDefined(kind)
            ? new WorkflowStep(0, key, Text.Required(name, NameMaxLength, "name"), kind, assignee)
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }

    /// <summary>A copy at <paramref name="order"/>: a step belongs to exactly one version.</summary>
    internal WorkflowStep At(int order) => new(order, Key, Name, Kind, Assignee);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
