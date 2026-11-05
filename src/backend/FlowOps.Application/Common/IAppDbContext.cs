using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Common;

/// <summary>
/// The Application layer's unit of work (ADR-0007). Only use-case services use it, and they call
/// <see cref="SaveChangesAsync"/> exactly once per use case. Entity sets are added as their
/// mappings land; there is deliberately no generic repository.
/// </summary>
public interface IAppDbContext
{
    DbSet<Department> Departments { get; }

    /// <summary>Request aggregates; tasks and activities are reached through them (no separate sets).</summary>
    DbSet<Request> Requests { get; }

    DbSet<Notification> Notifications { get; }

    /// <summary>Read-only projection of the Identity user (ADR-0002): Application never sees Identity types.</summary>
    IQueryable<UserReadModel> Users { get; }

    /// <summary>Read-only (user, role name) pairs.</summary>
    IQueryable<UserRoleReadModel> UserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
