namespace FlowOps.Application.Organization;

/// <summary>
/// Port to the session/credential machinery (ASP.NET Core Identity + cookie, ADR-0003), implemented in
/// Infrastructure. Failures surface as <c>AppException</c>s carrying contract error codes.
/// </summary>
public interface IIdentitySignIn
{
    /// <summary>Checks the password (with lockout) and issues the session cookie. Returns the user id.</summary>
    /// <exception cref="Common.AuthenticationFailedException"><c>auth.invalid_credentials</c> or <c>auth.locked_out</c>.</exception>
    Task<Guid> PasswordSignInAsync(string email, string password);

    Task SignOutAsync();

    /// <summary>Changes the password, clears <c>MustChangePassword</c> and re-issues the cookie.</summary>
    /// <exception cref="Common.ValidationFailedException">Wrong current password or new password breaks the rules.</exception>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
