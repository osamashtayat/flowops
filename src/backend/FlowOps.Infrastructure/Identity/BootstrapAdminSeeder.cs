using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowOps.Infrastructure.Identity;

/// <summary><c>FlowOps:BootstrapAdmin</c> — set through environment variables or user-secrets only.</summary>
public sealed class BootstrapAdminOptions
{
    public const string Section = "FlowOps:BootstrapAdmin";

    /// <summary>The value <c>.env.example</c> ships. It is public, so it must never become a real password (#79).</summary>
    public const string PasswordPlaceholder = "CHANGE-ME-before-first-start";

    /// <summary>
    /// Passwords the app refuses to bootstrap with: the current placeholder, and the one <c>.env.example</c> shipped before
    /// #79 (it passed the password policy, and copies of it may still be in local <c>.env</c> files).
    /// </summary>
    public static readonly IReadOnlySet<string> KnownPlaceholders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PasswordPlaceholder, "change-me-Local-Only-1" };

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string DisplayName { get; set; } = "Administrator";
}

/// <summary>
/// On start-up, creates the first Administrator from configuration if none exists (AUTHORIZATION.md §1.6).
/// Roles themselves are seeded by migration. Without configuration it logs a warning and touches nothing.
/// </summary>
public sealed partial class BootstrapAdminSeeder(
    IServiceScopeFactory scopes,
    IOptions<BootstrapAdminOptions> options,
    TimeProvider time,
    ILogger<BootstrapAdminSeeder> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => SeedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task SeedAsync(CancellationToken ct)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Email) || string.IsNullOrWhiteSpace(config.Password))
        {
            LogNotConfigured(logger, BootstrapAdminOptions.Section);
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if ((await users.GetUsersInRoleAsync(Roles.Administrator)).Count > 0)
        {
            return;
        }

        // Only when it would actually create the admin: an install bootstrapped long ago is not stopped by stale config.
        if (BootstrapAdminOptions.KnownPlaceholders.Contains(config.Password.Trim()))
        {
            throw new InvalidOperationException(
                $"Bootstrap administrator not created: {BootstrapAdminOptions.Section}:Password is still the published placeholder. " +
                "Set a strong password of your own (README, \"Full stack in Docker\").");
        }

        // Create + role assignment are two Identity saves; one transaction keeps a half-made admin from surviving.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(time.GetUtcNow()),
            UserName = config.Email,
            Email = config.Email,
            EmailConfirmed = true,
            DisplayName = config.DisplayName,
            MustChangePassword = true,
            CreatedAt = time.GetUtcNow(),
        };
        Ensure(await users.CreateAsync(admin, config.Password));
        Ensure(await users.AddToRoleAsync(admin, Roles.Administrator));
        await transaction.CommitAsync(ct);
        LogCreated(logger, admin.Id);
    }

    // Misconfiguration (weak password, email taken by a non-admin) must stop start-up, not limp on.
    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Bootstrap administrator could not be created: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Section} is not configured; no bootstrap administrator will be created")]
    private static partial void LogNotConfigured(ILogger logger, string section);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrap administrator {UserId} created")]
    private static partial void LogCreated(ILogger logger, Guid userId);
}
