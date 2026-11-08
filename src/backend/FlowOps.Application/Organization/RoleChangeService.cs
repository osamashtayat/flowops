using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowOps.Application.Organization;

/// <summary>
/// Listing and deciding administrators' own role changes (ADR-0012 §3–7). Proposals are made by
/// <see cref="UserAdministrationService.UpdateRolesAsync"/>, where the role edit happens.
/// </summary>
public sealed partial class RoleChangeService(
    IAppDbContext db, IIdentityAdmin identity, RoleChangeReader reader, ICurrentUser currentUser, TimeProvider time,
    PostCommitPublisher realtime, ILogger<RoleChangeService> logger)
{
    public Task<PagedResult<RoleChangeDto>> ListAsync(RoleChangeListQuery query, CancellationToken ct) => reader.ListAsync(query, ct);

    public Task<RoleChangeDto> GetAsync(Guid id, CancellationToken ct) => reader.GetAsync(id, ct);

    /// <summary>Applies the proposed roles after re-checking them against current data, in one save.</summary>
    public async Task<RoleChangeDto> ApproveAsync(Guid id, RoleChangeDecisionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var change = await LoadForDecisionAsync(id, body.Version, ct);
        var now = time.GetUtcNow();
        change.Approve(currentUser.Id, now);

        // Claimed first, so a direct edit committing during this approval fails one of the two (409) instead of being overwritten.
        var currentRoles = await identity.ClaimRolesForChangeAsync(change.UserId, ct);
        if (!RoleAssignmentRules.SameRoles(currentRoles, change.PreviousRoles))
        {
            throw new ConflictException(ErrorCodes.RoleChangeStale,
                "This administrator's roles changed after the request was made. Reject it and ask for a new one.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == change.UserId && u.IsActive, ct))
        {
            throw new ConflictException(ErrorCodes.RoleChangeStale,
                "This administrator's account was deactivated after the request was made. Reject it.");
        }

        await RoleAssignmentRules.EnsureAllowedAsync(db, change.UserId, currentRoles, change.ProposedRoles, ct);
        await identity.StageRolesAsync(change.UserId, change.ProposedRoles, ct);
        var notification = NotificationFactory.RoleChangeDecided(change, await DisplayNameAsync(currentUser.Id, ct), now);
        db.Notifications.Add(notification);
        await SaveAsync(ct);
        LogApproved(logger, currentUser.Id, change.Id, change.UserId, change.ProposedRoles);
        await realtime.NotificationsAsync([notification]);
        return await reader.GetAsync(id, ct);
    }

    public async Task<RoleChangeDto> RejectAsync(Guid id, RejectRoleChangeRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var change = await LoadForDecisionAsync(id, body.Version, ct);
        var now = time.GetUtcNow();
        change.Reject(currentUser.Id, body.Reason, now);
        var notification = NotificationFactory.RoleChangeDecided(change, await DisplayNameAsync(currentUser.Id, ct), now);
        db.Notifications.Add(notification);
        await SaveAsync(ct);
        LogRejected(logger, currentUser.Id, change.Id, change.UserId);
        await realtime.NotificationsAsync([notification]);
        return await reader.GetAsync(id, ct);
    }

    public async Task<RoleChangeDto> CancelAsync(Guid id, RoleChangeDecisionRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var change = await LoadAsync(id, body.Version, ct);
        change.Cancel(currentUser.Id, time.GetUtcNow());
        await SaveAsync(ct);
        LogCancelled(logger, currentUser.Id, change.Id);
        return await reader.GetAsync(id, ct);
    }

    /// <summary>The decider must be an active administrator in the database, not only by the (up to a minute old) session claims.</summary>
    private async Task<RoleChange> LoadForDecisionAsync(Guid id, string version, CancellationToken ct)
    {
        var change = await LoadAsync(id, version, ct);
        if (!await RoleAssignmentRules.ActiveAdministrators(db).AnyAsync(u => u.Id == currentUser.Id, ct))
        {
            throw new ForbiddenException();
        }

        return change;
    }

    private async Task<RoleChange> LoadAsync(Guid id, string version, CancellationToken ct)
    {
        var change = await db.RoleChanges.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException();
        if (!VersionToken.TryParse(version, out var expected) || expected != change.Version)
        {
            throw Conflict();
        }

        return change;
    }

    private async Task<string> DisplayNameAsync(Guid userId, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(ct);

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
    }

    private static ConflictException Conflict() =>
        new(ErrorCodes.ConcurrencyConflict, "The role change was changed by someone else. Reload and try again.");

    // Audit trail: ids and role names only, never the rejection reason.
    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} approved role change {RoleChangeId}: user {UserId} now has roles {Roles}")]
    private static partial void LogApproved(ILogger logger, Guid actorId, Guid roleChangeId, Guid userId, IEnumerable<string> roles);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} rejected role change {RoleChangeId} for user {UserId}")]
    private static partial void LogRejected(ILogger logger, Guid actorId, Guid roleChangeId, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin {ActorId} cancelled their role change {RoleChangeId}")]
    private static partial void LogCancelled(ILogger logger, Guid actorId, Guid roleChangeId);
}
