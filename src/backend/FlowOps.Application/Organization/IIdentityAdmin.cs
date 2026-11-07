namespace FlowOps.Application.Organization;

/// <summary>
/// Port for writing Identity users (ADR-0002: Application never sees Identity types), implemented in Infrastructure.
/// Every <c>Stage…</c> method only changes the shared unit of work; the calling service saves once.
/// Callers check existence and business rules first.
/// </summary>
public interface IIdentityAdmin
{
    /// <summary>The password rules (AUTHORIZATION.md §1.6) the password breaks; empty when it is acceptable.</summary>
    Task<IReadOnlyList<string>> CheckPasswordAsync(string password, CancellationToken ct);

    /// <summary>Stages a new account with <c>MustChangePassword = true</c>. Returns its id.</summary>
    /// <exception cref="Common.ValidationFailedException">Identity rejects the email as a user name (<c>errors.email</c>).</exception>
    /// <exception cref="Common.ConflictException"><c>user.duplicate_email</c>.</exception>
    Task<Guid> StageNewUserAsync(NewUserAccount account, CancellationToken ct);

    Task StageProfileAsync(Guid userId, UserProfile profile, CancellationToken ct);

    /// <summary>
    /// The user's current role names, read after claiming the account for this unit of work: an Identity change to the
    /// user that commits after this call makes the save fail with a concurrency conflict, so rules checked against these
    /// roles can't be overtaken by a parallel edit (e.g. a demotion racing an approval).
    /// </summary>
    Task<IReadOnlyList<string>> ClaimRolesForChangeAsync(Guid userId, CancellationToken ct);

    /// <summary>Replaces the user's roles with <paramref name="roles"/> (Identity role names).</summary>
    Task StageRolesAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct);

    /// <summary>Sets the password, requires a change at next sign-in, clears lockout and ends the user's sessions.</summary>
    Task StagePasswordResetAsync(Guid userId, string newPassword, CancellationToken ct);
}

public sealed record NewUserAccount(
    string Email,
    string DisplayName,
    string Password,
    Guid? DepartmentId,
    Guid? ManagerId,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset CreatedAt);

public sealed record UserProfile(string DisplayName, Guid? DepartmentId, Guid? ManagerId, bool IsActive);
