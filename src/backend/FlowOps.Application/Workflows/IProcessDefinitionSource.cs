using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Workflows;

/// <summary>
/// Where the <c>Request</c> aggregate's steps come from (ADR-0014 §1; it replaced <c>FixedProcesses</c>): the
/// persisted workflow versions. Read-only; the versions it returns are not tracked, so they can't be saved by mistake.
/// </summary>
public interface IProcessDefinitionSource
{
    /// <summary>The type's Published version, which a new request is pinned to.</summary>
    Task<WorkflowVersion> PublishedAsync(RequestType type, CancellationToken ct);

    /// <summary>The version a request is pinned to, whatever its status now.</summary>
    Task<WorkflowVersion> VersionAsync(Guid versionId, CancellationToken ct);
}
