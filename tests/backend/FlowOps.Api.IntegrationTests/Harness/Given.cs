using FlowOps.Domain.Organization;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>A user created by a test, with the password needed to sign in as them.</summary>
public sealed record TestUser(Guid Id, string Email, string Password, string DisplayName);

/// <summary>
/// Org setup for tests (TESTING.md §2). Writing users and departments directly is the allowed exception
/// ("bulk org setup") until the admin API (BE-08) exists; everything else is arranged through HTTP.
/// </summary>
public static class Given
{
    public const string DefaultPassword = "Correct-Horse-9-battery";

    public static UserBuilder User(string name) => new(name);

    public static async Task<Department> DepartmentAsync(FlowOpsApiFactory factory, string name, string code)
    {
        ArgumentNullException.ThrowIfNull(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var department = Department.Create(name, code, factory.Clock.GetUtcNow());
        db.Departments.Add(department);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return department;
    }
}

public sealed class UserBuilder(string name)
{
    private readonly List<string> _roles = [Roles.Employee];
    private Guid? _managerId;
    private Guid? _departmentId;
    private bool _mustChangePassword;
    private bool _isActive = true;
    private DateTimeOffset? _createdAt;

    /// <summary>Replaces the default role set (<c>Employee</c>).</summary>
    public UserBuilder WithRoles(params string[] roles)
    {
        _roles.Clear();
        _roles.AddRange(roles);
        return this;
    }

    public UserBuilder ManagedBy(TestUser manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        _managerId = manager.Id;
        return this;
    }

    public UserBuilder InDepartment(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _departmentId = department.Id;
        return this;
    }

    public UserBuilder MustChangePassword()
    {
        _mustChangePassword = true;
        return this;
    }

    /// <summary>
    /// When the account was created. Default: 30 days before the test clock, i.e. an established account. Rules that depend
    /// on account age (ADR-0013: role-change approvers) need this to arrange a new account.
    /// </summary>
    public UserBuilder CreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public UserBuilder Inactive()
    {
        _isActive = false;
        return this;
    }

    public async Task<TestUser> CreateAsync(FlowOpsApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{name.ToLowerInvariant()}@flowops.test";
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = char.ToUpperInvariant(name[0]) + name[1..],
            DepartmentId = _departmentId,
            ManagerId = _managerId,
            IsActive = _isActive,
            MustChangePassword = _mustChangePassword,
            CreatedAt = _createdAt ?? factory.Clock.GetUtcNow().AddDays(-30),
        };
        Ensure(await users.CreateAsync(user, Given.DefaultPassword));
        if (_roles.Count > 0)
        {
            Ensure(await users.AddToRolesAsync(user, _roles));
        }

        return new TestUser(user.Id, email, Given.DefaultPassword, user.DisplayName);
    }

    private static void Ensure(IdentityResult result) =>
        result.Succeeded.ShouldBeTrue(string.Join(", ", result.Errors.Select(e => e.Code)));
}
