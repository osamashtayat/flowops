using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Organization;

/// <summary>
/// An administrator's request to change their own roles, waiting for a different administrator (ADR-0012 §3–5).
/// The proposer is always the user whose roles change. Whether the change is still allowed against current data
/// (last administrator, Administrator only on new accounts, roles changed meanwhile) needs the database and is checked
/// by the Application layer at approval; this class owns the lifecycle.
/// </summary>
public sealed class RoleChange
{
    public const int RejectionReasonMaxLength = 500;

    /// <summary>A pending change that nobody decided within this time is expired (ADR-0012 §5).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(72);

    /// <summary>
    /// How long before the proposal the approver's account must have existed (ADR-0013, #48), so an administrator can't
    /// create a second admin account and approve their own change with it.
    /// </summary>
    public static readonly TimeSpan MinimumApproverAccountAge = TimeSpan.FromHours(24);

    private RoleChange(Guid userId, string[] previousRoles, string[] proposedRoles, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        UserId = userId;
        PreviousRoles = previousRoles;
        ProposedRoles = proposedRoles;
        ProposedAt = now;
        ExpiresAt = now + Lifetime;
        Status = RoleChangeStatus.Pending;
    }

    /// <summary>For persistence materialization only; state is set by the store, never by callers.</summary>
    private RoleChange()
    {
        PreviousRoles = [];
        ProposedRoles = [];
    }

    public Guid Id { get; private set; }

    /// <summary>The administrator whose roles change; also the proposer.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The user's roles when the change was proposed. Approval is refused if they differ by then.</summary>
    public IReadOnlyList<string> PreviousRoles { get; private set; }

    /// <summary>The complete new role set.</summary>
    public IReadOnlyList<string> ProposedRoles { get; private set; }

    public DateTimeOffset ProposedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>The stored status. A <c>Pending</c> change past <see cref="ExpiresAt"/> is expired: use <see cref="StatusAt"/>.</summary>
    public RoleChangeStatus Status { get; private set; }

    public Guid? DecidedById { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Shown only to administrators in the role-change details; never logged or put in notifications.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Optimistic concurrency token (PostgreSQL <c>xmin</c>, ADR-0006); set by persistence only.</summary>
    public uint Version { get; private set; }

    public static RoleChange Propose(Guid userId, IEnumerable<string> previousRoles, IEnumerable<string> proposedRoles, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A role change needs a user.", nameof(userId));
        }

        var previous = Normalize(previousRoles);
        var proposed = Normalize(proposedRoles);
        if (previous.SequenceEqual(proposed, StringComparer.Ordinal))
        {
            throw DomainErrors.Validation("The proposed roles are the same as the current roles.", "roles");
        }

        return new RoleChange(userId, previous, proposed, now);
    }

    /// <summary>The status as of <paramref name="now"/>, with expiry applied (ADR-0012 §5: no background job).</summary>
    public RoleChangeStatus StatusAt(DateTimeOffset now) => StatusAt(Status, ExpiresAt, now);

    /// <summary>The same rule for read models projected from the database.</summary>
    public static RoleChangeStatus StatusAt(RoleChangeStatus stored, DateTimeOffset expiresAt, DateTimeOffset now) =>
        stored == RoleChangeStatus.Pending && now >= expiresAt ? RoleChangeStatus.Expired : stored;

    public bool IsPendingAt(DateTimeOffset now) => StatusAt(now) == RoleChangeStatus.Pending;

    /// <summary>
    /// Approve or reject: any administrator except the user it is about (the Application layer checks the role).
    /// Static so read models projected from the database use the same rule.
    /// </summary>
    public static bool CanDecide(RoleChangeStatus statusNow, Guid userId, Guid actorId) =>
        statusNow == RoleChangeStatus.Pending && actorId != userId;

    /// <summary>
    /// Approve additionally needs an approver account created at least <see cref="MinimumApproverAccountAge"/> before
    /// <paramref name="proposedAt"/> (ADR-0013). Reject does not: refusing a change can't be used to gain anything.
    /// </summary>
    public static bool IsOldEnoughToApprove(DateTimeOffset approverCreatedAt, DateTimeOffset proposedAt) =>
        approverCreatedAt <= proposedAt - MinimumApproverAccountAge;

    /// <summary>Cancel: only the administrator who asked.</summary>
    public static bool CanCancel(RoleChangeStatus statusNow, Guid userId, Guid actorId) =>
        statusNow == RoleChangeStatus.Pending && actorId == userId;

    public void Approve(Guid approverId, DateTimeOffset now)
    {
        EnsureDecidable(approverId, now);
        Close(RoleChangeStatus.Approved, approverId, now);
    }

    public void Reject(Guid approverId, string reason, DateTimeOffset now)
    {
        EnsureDecidable(approverId, now);
        RejectionReason = Text.Required(reason, RejectionReasonMaxLength, "reason");
        Close(RoleChangeStatus.Rejected, approverId, now);
    }

    public void Cancel(Guid actorId, DateTimeOffset now)
    {
        EnsurePending(now);
        if (actorId != UserId)
        {
            throw DomainErrors.RoleChangeNotProposer();
        }

        Close(RoleChangeStatus.Cancelled, actorId, now);
    }

    /// <summary>
    /// Stores the expiry of a pending change that ran out. Needed before proposing a new change for the same user,
    /// because the database allows one stored <c>Pending</c> row per user. Returns whether it was expired.
    /// </summary>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (Status != RoleChangeStatus.Pending || now < ExpiresAt)
        {
            return false;
        }

        Status = RoleChangeStatus.Expired;
        DecidedAt = ExpiresAt;
        return true;
    }

    private void EnsureDecidable(Guid actorId, DateTimeOffset now)
    {
        EnsurePending(now);
        if (actorId == UserId)
        {
            throw DomainErrors.RoleChangeSelfApproval();
        }
    }

    private void EnsurePending(DateTimeOffset now)
    {
        if (!IsPendingAt(now))
        {
            throw DomainErrors.RoleChangeNotPending();
        }
    }

    private void Close(RoleChangeStatus status, Guid actorId, DateTimeOffset now)
    {
        Status = status;
        DecidedById = actorId;
        DecidedAt = now;
    }

    private static string[] Normalize(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var set = roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var unknown = set.FirstOrDefault(r => !Roles.All.Contains(r));
        return unknown is null ? set : throw DomainErrors.Validation($"'{unknown}' is not a role.", "roles");
    }
}

public enum RoleChangeStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Expired,
}
