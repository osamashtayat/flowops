using FlowOps.Application.Common;
using FlowOps.Domain.Notifications;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FlowOps.Domain.Errors;
using Npgsql;

namespace FlowOps.Infrastructure.Persistence;

/// <summary>The single EF Core context: Identity stores plus FlowOps aggregates (configurations added per task).</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    /// <summary>Feeds <c>Request.Number</c> (DOMAIN_MODEL.md §2.3); read by the number generator (BE-10).</summary>
    public const string RequestNumberSequence = "request_number_seq";

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<RoleChange> RoleChanges => Set<RoleChange>();

    IQueryable<UserReadModel> IAppDbContext.Users => Users.Select(u => new UserReadModel
    {
        Id = u.Id,
        Email = u.Email!,
        DisplayName = u.DisplayName,
        DepartmentId = u.DepartmentId,
        ManagerId = u.ManagerId,
        IsActive = u.IsActive,
        MustChangePassword = u.MustChangePassword,
        CreatedAt = u.CreatedAt,
    });

    IQueryable<UserRoleReadModel> IAppDbContext.UserRoles =>
        UserRoles.Join(Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new UserRoleReadModel { UserId = ur.UserId, RoleName = r.Name! });

    /// <summary>
    /// Unique-index violations with a contract meaning (mostly "duplicate"), by index name. Translating them here keeps the
    /// database the single judge of uniqueness (no check-then-insert race) and keeps Npgsql out of Application.
    /// </summary>
    private static readonly Dictionary<string, (string Code, string Message)> DuplicateByIndex = new(StringComparer.Ordinal)
    {
        [DepartmentConfiguration.CodeIndex] = (ErrorCodes.DepartmentDuplicate, "A department with this code already exists."),
        [DepartmentConfiguration.NameIndex] = (ErrorCodes.DepartmentDuplicate, "A department with this name already exists."),
        [IdentityTableNames.UserNameIndex] = (ErrorCodes.UserDuplicateEmail, "A user with this email already exists."),
        // Two concurrent role edits adding the same role: the loser sees the winner's row.
        // Defence in depth for two simultaneous decisions (WORKFLOWS.md §5): xmin normally stops the second one first.
        ["ux_work_tasks_open_per_request"] = (ErrorCodes.ConcurrencyConflict, "Someone else changed this task. Reload to see the latest state."),
        // Two proposals racing for the same administrator: the second one sees the first.
        [RoleChangeConfiguration.PendingPerUserIndex] =
            (ErrorCodes.RoleChangeAlreadyPending, "You already have a role change waiting for approval."),
        ["pk_user_roles"] = (ErrorCodes.ConcurrencyConflict, "The user was changed by someone else. Reload and try again."),
    };

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName is { } index && DuplicateByIndex.TryGetValue(index, out var duplicate))
        {
            throw new ConflictException(duplicate.Code, duplicate.Message);
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        IdentityTableNames.Apply(builder);
        builder.HasSequence<long>(RequestNumberSequence);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
