namespace FlowOps.Application.Common;

/// <summary>A user as other DTOs reference them (API_CONTRACT.md §1).</summary>
public sealed record UserRef(Guid Id, string DisplayName);
