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
        if (result.IsLockedOut || result.IsNotAllowed)
        {
            // Identity refuses these before hashing; spend the work anyway so timing doesn't single them out.
            _ = userManager.PasswordHasher.HashPassword(user, password);
        }

        // Locked out and NotAllowed (inactive account) deliberately look like a wrong password: a distinct answer
        // would tell an attacker which emails belong to real accounts (AUTHORIZATION.md §1.6).
        if (!result.Succeeded)
        {
            throw new AuthenticationFailedException(ErrorCodes.AuthInvalidCredentials, InvalidCredentials);
        }

        // The cookie only authenticates the next request; make this one see the new user too, so the
        // antiforgery token issued in the login response is bound to them.
        signInManager.Context.User = await signInManager.CreateUserPrincipalAsync(user);
        return user.Id;
    }

    public async Task SignOutAsync()
    {
        // Rotating the stamp revokes the session server-side (AUTHORIZATION.md §1.1): a copied cookie dies at the
        // next re-validation. The price: the user's other sessions end too.
        var user = await userManager.GetUserAsync(signInManager.Context.User);
        if (user is not null)
        {
            await userManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new AuthenticationFailedException(ErrorCodes.AuthUnauthenticated, "Your session has ended. Please sign in again.");

        // Wrong current passwords count towards lockout here too, so a stolen session can't guess the password.
        var check = await signInManager.CheckPasswordSignInAsync(user, currentPassword, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            throw ValidationFailedException.For("currentPassword", check.IsLockedOut
                ? "Too many failed attempts. Try again in 15 minutes."
                : "The current password is incorrect.");
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            throw ValidationFailedException.For("newPassword", "The new password must differ from the current one.");
        }

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
