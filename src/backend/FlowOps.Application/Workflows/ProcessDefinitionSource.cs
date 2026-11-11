using FlowOps.Application.Common;
using FlowOps.Domain.Requests;
using FlowOps.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Workflows;

/// <summary><see cref="IProcessDefinitionSource"/> over the database. Never saves.</summary>
public sealed class ProcessDefinitionSource(IAppDbContext db) : IProcessDefinitionSource
{
    /// <remarks>
    /// Every type has exactly one Published version: the migration seeds v1 and the partial unique index
    /// <c>ux_workflow_versions_published</c> allows no second one, so a miss is a broken database, not a user error.
    /// </remarks>
    public async Task<WorkflowVersion> PublishedAsync(RequestType type, CancellationToken ct) =>
        await db.WorkflowVersions.AsNoTracking()
            .Where(v => v.Status == WorkflowVersionStatus.Published
                && db.WorkflowDefinitions.Any(d => d.Id == v.WorkflowId && d.RequestType == type))
            .SingleOrDefaultAsync(ct)
        ?? throw new InvalidOperationException($"{type} has no Published workflow version.");

    public async Task<WorkflowVersion> VersionAsync(Guid versionId, CancellationToken ct) =>
        await db.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, ct)
        ?? throw new InvalidOperationException($"Workflow version {versionId} does not exist.");
}
