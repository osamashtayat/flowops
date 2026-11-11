namespace FlowOps.Domain.Workflows;

/// <summary>A version's lifecycle: Draft → Published → Retired, nothing else (DOMAIN_MODEL.md §7.1).</summary>
public enum WorkflowVersionStatus
{
    Draft,
    Published,
    Retired,
}
