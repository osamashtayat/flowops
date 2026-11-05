using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Organization;

/// <summary>Auth use cases (API_CONTRACT.md §3). The actor always comes from <see cref="ICurrentUser"/>.</summary>
public sealed class AuthService(IIdentitySignIn signIn, IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<CurrentUserDto> LoginAsync(LoginRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var userId = await signIn.PasswordSignInAsync(body.Email, body.Password);
        return await LoadAsync(userId, ct);
    }

    public Task LogoutAsync() => signIn.SignOutAsync();

    public Task<CurrentUserDto> GetCurrentAsync(CancellationToken ct) => LoadAsync(currentUser.Id, ct);

    public Task ChangePasswordAsync(ChangePasswordRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return signIn.ChangePasswordAsync(currentUser.Id, body.CurrentPassword, body.NewPassword);
    }

    private async Task<CurrentUserDto> LoadAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.MustChangePassword,
                Department = db.Departments.Where(d => d.Id == u.DepartmentId).Select(d => new DepartmentRef(d.Id, d.Name)).SingleOrDefault(),
                Manager = db.Users.Where(m => m.Id == u.ManagerId).Select(m => new UserRef(m.Id, m.DisplayName)).SingleOrDefault(),
            })
            .SingleOrDefaultAsync(ct)
            // The session outlived its user: treat it as signed out.
            ?? throw new AuthenticationFailedException(ErrorCodes.AuthUnauthenticated, "Your session has ended. Please sign in again.");

        var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.RoleName).ToListAsync(ct);
        var isManager = await db.Users.AnyAsync(ManagerRelationship.ActiveDirectReportOf(userId), ct);

        return new CurrentUserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Department,
            user.Manager,
            [.. roles.Select(Enum.Parse<Role>).Order()],
            CapabilityRules.For(roles, isManager),
            user.MustChangePassword);
    }
}
