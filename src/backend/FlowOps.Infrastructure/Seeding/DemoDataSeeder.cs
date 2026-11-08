using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using FlowOps.Application.Requests;
using FlowOps.Domain.Common;
using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;
using FlowOps.Infrastructure.Identity;
using FlowOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowOps.Infrastructure.Seeding;

/// <summary>
/// <c>--seed-demo</c> (BE-16): creates <see cref="DemoCompany"/> in a Development database. Requests are built through the
/// <see cref="Request"/> aggregate (<see cref="FixedProcesses"/> order, every invariant) and the pending role change through
/// <see cref="RoleChange"/>; notifications come from <see cref="NotificationFactory"/>. Everything is saved in one
/// <c>SaveChangesAsync</c>, so a failed seed leaves nothing behind, and a seeded database is detected and left alone.
/// </summary>
public sealed partial class DemoDataSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IRequestNumberSequence numbers,
    CompanyCalendar calendar,
    TimeProvider time,
    IHostEnvironment environment,
    ILogger<DemoDataSeeder> logger)
{
    public const string CommandLineFlag = "--seed-demo";

    /// <summary>Runs the seeder in its own scope; called by the Api's composition root when <see cref="CommandLineFlag"/> is given.</summary>
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(ct);
    }

    /// <summary>Returns <c>false</c> when the demo company already exists. Throws outside Development.</summary>
    public async Task<bool> SeedAsync(CancellationToken ct)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Demo data can only be seeded in the Development environment, not '{environment.EnvironmentName}'.");
        }

        if (await db.Users.AnyAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(DemoCompany.MarkerEmail), ct))
        {
            LogAlreadySeeded(logger);
            return false;
        }

        var now = time.GetUtcNow();
        var people = await AddPeopleAsync(now, ct);
        foreach (var spec in DemoCompany.Requests.OrderByDescending(r => r.DaysAgo))
        {
            await AddRequestAsync(spec, people, now, ct);
        }

        AddPendingRoleChange(people, now);
        await db.SaveChangesAsync(ct);
        LogSeeded(logger, DemoCompany.Users.Length, DemoCompany.Requests.Length);
        return true;
    }

    /// <summary>
    /// Users are added as entities rather than through <c>UserManager.CreateAsync</c>, which saves on every call: one save keeps
    /// the seed atomic without an explicit transaction. Hashing and normalization still come from Identity.
    /// </summary>
    private async Task<Dictionary<string, ApplicationUser>> AddPeopleAsync(DateTimeOffset now, CancellationToken ct)
    {
        var departments = DemoCompany.Departments.ToDictionary(d => d.Code, d => Department.Create(d.Name, d.Code, now.AddDays(-60)));
        db.Departments.AddRange(departments.Values);
        var roleIds = await db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Name!, r => r.Id, ct);

        var people = new Dictionary<string, ApplicationUser>(StringComparer.Ordinal);
        foreach (var spec in DemoCompany.Users)
        {
            var user = NewUser(spec, departments, people, now);
            people[spec.Key] = user;
            db.Users.Add(user);
            db.UserRoles.AddRange(spec.Roles.Select(role => new IdentityUserRole<Guid> { UserId = user.Id, RoleId = roleIds[role] }));
        }

        return people;
    }

    private ApplicationUser NewUser(
        DemoUser spec, Dictionary<string, Department> departments, Dictionary<string, ApplicationUser> people, DateTimeOffset now)
    {
        var createdAt = now.AddDays(-45);
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(createdAt),
            UserName = spec.Email,
            NormalizedUserName = userManager.NormalizeName(spec.Email),
            Email = spec.Email,
            NormalizedEmail = userManager.NormalizeEmail(spec.Email),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true,
            DisplayName = spec.DisplayName,
            DepartmentId = spec.DepartmentCode is null ? null : departments[spec.DepartmentCode].Id,
            ManagerId = spec.ManagerKey is null ? null : people[spec.ManagerKey].Id,
            IsActive = spec.IsActive,
            CreatedAt = createdAt,
        };
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, DemoCompany.Password);
        return user;
    }

    private async Task AddRequestAsync(DemoRequest spec, Dictionary<string, ApplicationUser> people, DateTimeOffset now, CancellationToken ct)
    {
        var requester = people[spec.Requester];
        var submittedAt = now.AddDays(-spec.DaysAgo);
        var managerKey = spec.DaysAgo > DemoCompany.ManagerChangeDaysAgo
            ? DemoCompany.Users.Single(u => u.Key == spec.Requester).PreviousManagerKey ?? ManagerKeyOf(spec.Requester)
            : ManagerKeyOf(spec.Requester);
        var managerId = people[managerKey].Id;
        var number = RequestNumber.Create(calendar.YearOf(submittedAt), await numbers.NextAsync(ct));
        var request = spec.Purchase is { } purchase
            ? Request.SubmitPurchase(number, requester.Id, requester.DepartmentId!.Value, ToDetails(purchase), managerId, submittedAt)
            : Request.SubmitLeave(number, requester.Id, requester.DepartmentId!.Value, ToDetails(spec.Leave!, now, submittedAt), managerId, submittedAt);
        db.Requests.Add(request);
        db.Notifications.AddRange(NotificationFactory.TaskAssigned(request, request.OpenTask!, requester.DisplayName, [managerId], submittedAt));

        foreach (var decision in spec.Decisions)
        {
            Decide(request, decision, people, managerId, now.AddDays(-decision.DaysAgo));
        }

        if (spec.Comment is var (by, body))
        {
            request.AddComment(people[by].Id, body, submittedAt.AddHours(2));
        }
    }

    /// <summary>
    /// The seed notifies requesters of outcomes and managers of their tasks. Role-queue tasks are not announced: who is eligible
    /// is <c>TaskEligibility</c>'s rule, which needs the saved database; the tasks still show in the officers' inboxes.
    /// </summary>
    private void Decide(Request request, DemoDecision spec, Dictionary<string, ApplicationUser> people, Guid managerId, DateTimeOffset at)
    {
        var decider = people[spec.By];
        var decision = spec.Action switch
        {
            TaskDecisionAction.Approve => TaskDecision.Approve(spec.Comment),
            TaskDecisionAction.Reject => TaskDecision.Reject(spec.Comment!),
            TaskDecisionAction.Fulfill => TaskDecision.Fulfill(spec.Comment),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Action, null),
        };
        var result = request.DecideTask(request.OpenTask!.Id, decider.Id, decision, step => TaskAssigneeResolver.Resolve(step, managerId), at);
        db.Notifications.Add(NotificationFactory.DecisionOutcome(request, result, decider.DisplayName, at));
    }

    private void AddPendingRoleChange(Dictionary<string, ApplicationUser> people, DateTimeOffset now)
    {
        var spec = DemoCompany.PendingRoleChange;
        var proposer = people[spec.UserKey];
        var proposedAt = now.AddDays(-spec.DaysAgo);
        var previous = DemoCompany.Users.Single(u => u.Key == spec.UserKey).Roles;
        var change = RoleChange.Propose(proposer.Id, previous, spec.ProposedRoles, proposedAt);
        db.RoleChanges.Add(change);
        var otherAdmins = DemoCompany.Users.Where(u => u.Key != spec.UserKey && u.Roles.Contains(Roles.Administrator)).Select(u => people[u.Key].Id);
        db.Notifications.AddRange(NotificationFactory.RoleChangePending(change, proposer.DisplayName, otherAdmins, proposedAt));
    }

    private static string ManagerKeyOf(string userKey) =>
        DemoCompany.Users.Single(u => u.Key == userKey).ManagerKey
        ?? throw new InvalidOperationException($"Demo user {userKey} has no manager and cannot submit.");

    private static PurchaseDetails ToDetails(DemoPurchase spec) =>
        PurchaseDetails.Create(spec.ItemName, spec.Description, Money.Create(spec.AmountJod, SupportedCurrencies.Jod), spec.Justification);

    /// <summary>Dates count from the seed day in the company time zone, so leave never starts in the past.</summary>
    private LeaveDetails ToDetails(DemoLeave spec, DateTimeOffset now, DateTimeOffset submittedAt)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, calendar.TimeZone).DateTime);
        var start = today.AddDays(spec.StartsInDays);
        return LeaveDetails.Create(start, start.AddDays(spec.Days - 1), spec.Type, spec.Reason, submittedAt, calendar.TimeZone);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data already present; nothing seeded")]
    private static partial void LogAlreadySeeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data seeded: {UserCount} users, {RequestCount} requests")]
    private static partial void LogSeeded(ILogger logger, int userCount, int requestCount);
}
