using FlowOps.Application.Common;
using FlowOps.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Organization;

/// <summary>
/// Projects role changes into <see cref="RoleChangeDto"/> for the current administrator. Shared by
/// <see cref="UserAdministrationService"/> (after a proposal) and <see cref="RoleChangeService"/>; never saves.
/// </summary>
public sealed class RoleChangeReader(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public async Task<RoleChangeDto> GetAsync(Guid id, CancellationToken ct) =>
        (await ListAsync(db.RoleChanges.Where(c => c.Id == id), page: null, ct)).Items.SingleOrDefault()
        ?? throw new NotFoundException();

    public async Task<PagedResult<RoleChangeDto>> ListAsync(RoleChangeListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var now = time.GetUtcNow();
        var changes = db.RoleChanges.AsQueryable();
        changes = query.Status switch
        {
            null => changes,
            RoleChangeStatus.Pending => changes.Where(c => c.Status == RoleChangeStatus.Pending && c.ExpiresAt > now),
            RoleChangeStatus.Expired => changes.Where(c =>
                c.Status == RoleChangeStatus.Expired || (c.Status == RoleChangeStatus.Pending && c.ExpiresAt <= now)),
            var status => changes.Where(c => c.Status == status),
        };
        return await ListAsync(changes, query, ct);
    }

    private async Task<PagedResult<RoleChangeDto>> ListAsync(IQueryable<RoleChange> changes, PageRequest? page, CancellationToken ct)
    {
        var rows = changes.AsNoTracking()
            .OrderByDescending(c => c.ProposedAt).ThenByDescending(c => c.Id)
            .Select(c => new Row(
                c.Id,
                db.Users.Where(u => u.Id == c.UserId).Select(u => new UserRef(u.Id, u.DisplayName)).Single(),
                c.ProposedRoles,
                c.PreviousRoles,
                c.Status,
                c.ProposedAt,
                c.ExpiresAt,
                db.Users.Where(u => u.Id == c.DecidedById).Select(u => new UserRef(u.Id, u.DisplayName)).SingleOrDefault(),
                c.DecidedAt,
                c.RejectionReason,
                c.Version));
        var result = page is null
            ? new PagedResult<Row>(await rows.ToListAsync(ct), 1, 1, 1)
            : await rows.ToPagedResultAsync(page, ct);
        var now = time.GetUtcNow();
        return result.Map(row => row.ToDto(now, currentUser.Id));
    }

    private sealed record Row(
        Guid Id, UserRef User, IReadOnlyList<string> ProposedRoles, IReadOnlyList<string> PreviousRoles, RoleChangeStatus Status,
        DateTimeOffset ProposedAt, DateTimeOffset ExpiresAt, UserRef? DecidedBy, DateTimeOffset? DecidedAt, string? RejectionReason,
        uint Version)
    {
        public RoleChangeDto ToDto(DateTimeOffset now, Guid actorId)
        {
            var status = RoleChange.StatusAt(Status, ExpiresAt, now);
            return new RoleChangeDto(
                Id, User, ToRoles(ProposedRoles), ToRoles(PreviousRoles), status, ProposedAt, ExpiresAt, DecidedBy,
                status == RoleChangeStatus.Expired ? DecidedAt ?? ExpiresAt : DecidedAt,
                RejectionReason, VersionToken.Format(Version), Actions(status, actorId));
        }

        private List<RoleChangeAction> Actions(RoleChangeStatus status, Guid actorId)
        {
            if (RoleChange.CanCancel(status, User.Id, actorId))
            {
                return [RoleChangeAction.Cancel];
            }

            return RoleChange.CanDecide(status, User.Id, actorId) ? [RoleChangeAction.Approve, RoleChangeAction.Reject] : [];
        }

        private static List<Role> ToRoles(IEnumerable<string> roles) => [.. roles.Select(Enum.Parse<Role>).Order()];
    }
}
