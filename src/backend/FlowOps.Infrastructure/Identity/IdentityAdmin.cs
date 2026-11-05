using FlowOps.Application.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Infrastructure.Identity;

/// <summary>
/// <see cref="IIdentityAdmin"/> over the shared <see cref="AppDbContext"/>. It writes the Identity tables through EF
/// instead of <see cref="UserManager{TUser}"/>'s saving methods so the use case keeps one <c>SaveChangesAsync</c>;
/// it still uses Identity's normalizer, password validators and hasher, so stored values match what Identity writes.
/// </summary>
internal sealed class IdentityAdmin(AppDbContext db, UserManager<ApplicationUser> users) : IIdentityAdmin
{
    public async Task<IReadOnlyList<string>> CheckPasswordAsync(string password)
    {
        var problems = new List<string>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, new ApplicationUser(), password);
            problems.AddRange(result.Errors.Select(e => e.Description));
        }

        return problems;
    }

    public async Task<Guid> StageNewUserAsync(NewUserAccount account, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(account);
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(account.CreatedAt),
            UserName = account.Email,
            NormalizedUserName = users.NormalizeName(account.Email),
            Email = account.Email,
            NormalizedEmail = users.NormalizeEmail(account.Email),
            // No email delivery in Phase 1: an administrator vouches for the address.
            EmailConfirmed = true,
            LockoutEnabled = true,
            SecurityStamp = NewStamp(),
            ConcurrencyStamp = NewStamp(),
            DisplayName = account.DisplayName,
            DepartmentId = account.DepartmentId,
            ManagerId = account.ManagerId,
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = account.CreatedAt,
        };
        user.PasswordHash = users.PasswordHasher.HashPassword(user, account.Password);
        db.Users.Add(user);
        await AddRolesAsync(user.Id, account.Roles, ct);
        return user.Id;
    }

    public async Task StageProfileAsync(Guid userId, UserProfile profile, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var user = await LoadAsync(userId, ct);
        user.DisplayName = profile.DisplayName;
        user.DepartmentId = profile.DepartmentId;
        user.ManagerId = profile.ManagerId;
        // Deactivation needs no stamp change: re-validation rejects inactive users (FlowOpsSignInManager).
        user.IsActive = profile.IsActive;
    }

    public async Task StageRolesAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var wanted = await RoleIdsAsync(roles, ct);
        var current = await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(ct);
        db.UserRoles.RemoveRange(current.Where(ur => !wanted.Contains(ur.RoleId)));
        var existing = current.Select(ur => ur.RoleId).ToHashSet();
        db.UserRoles.AddRange(wanted.Where(id => !existing.Contains(id)).Select(id => new IdentityUserRole<Guid> { UserId = userId, RoleId = id }));
        // Sessions pick up the new roles at the next re-validation (AUTHORIZATION.md §6, claim staleness).
    }

    public async Task StagePasswordResetAsync(Guid userId, string newPassword, CancellationToken ct)
    {
        var user = await LoadAsync(userId, ct);
        user.PasswordHash = users.PasswordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        // A new stamp ends every existing session at its next re-validation, as a password change does.
        user.SecurityStamp = NewStamp();
    }

    private async Task<ApplicationUser> LoadAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        // Identity's optimistic-concurrency token: a concurrent Identity update of this user now fails instead of being lost.
        user.ConcurrencyStamp = NewStamp();
        return user;
    }

    private async Task AddRolesAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var ids = await RoleIdsAsync(roles, ct);
        db.UserRoles.AddRange(ids.Select(id => new IdentityUserRole<Guid> { UserId = userId, RoleId = id }));
    }

    private async Task<HashSet<Guid>> RoleIdsAsync(IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var ids = await db.Roles.Where(r => roles.Contains(r.Name!)).Select(r => r.Id).ToListAsync(ct);
        return ids.Count == roles.Count
            ? [.. ids]
            : throw new InvalidOperationException("A role in the request is not seeded; Role and Roles.All have drifted.");
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
