using FlowOps.Application.Common;
using FlowOps.Application.Organization;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Identity;

namespace FlowOps.Infrastructure.Identity;

/// <summary><see cref="IIdentitySignIn"/> over ASP.NET Core Identity and the application cookie (ADR-0003).</summary>
internal sealed class IdentitySignIn(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    : IIdentitySignIn
{
    private const string InvalidCredentials = "The email or password is incorrect.";

    public async Task<Guid> PasswordSignInAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Spend the same hashing work as a real check so response time doesn't reveal which emails exist.
            _ = userManager.PasswordHasher.HashPassword(new ApplicationUser(), password);
            throw new AuthenticationFailedException(ErrorCodes.AuthInvalidCredentials, InvalidCredentials);
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            throw new AuthenticationFailedException(ErrorCodes.AuthLockedOut, "Too many failed attempts. Try again in 15 minutes.");
        }

        // NotAllowed (inactive account) deliberately looks like a wrong password.
        if (!result.Succeeded)
        {
            throw new AuthenticationFailedException(ErrorCodes.AuthInvalidCredentials, InvalidCredentials);
        }

        // The cookie only authenticates the next request; make this one see the new user too, so the
        // antiforgery token issued in the login response is bound to them.
        signInManager.Context.User = await signInManager.CreateUserPrincipalAsync(user);
        return user.Id;
    }

    public Task SignOutAsync() => signInManager.SignOutAsync();

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthenticationFailedException(ErrorCodes.AuthUnauthenticated, "Your session has ended. Please sign in again.");

        // Cleared before ChangePasswordAsync so both are saved in its single update; a failed change saves nothing.
        user.MustChangePassword = false;
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            throw ToValidationFailure(result);
        }

        // The change rotated the security stamp: re-issue this session's cookie (other sessions end at validation).
        await signInManager.RefreshSignInAsync(user);
        signInManager.Context.User = await signInManager.CreateUserPrincipalAsync(user);
    }

    private static ValidationFailedException ToValidationFailure(IdentityResult result)
    {
        var field = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
            ? "currentPassword"
            : "newPassword";
        return ValidationFailedException.For(field, [.. result.Errors.Select(e => e.Description)]);
    }
}
