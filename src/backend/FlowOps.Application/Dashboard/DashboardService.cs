using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Application.Organization;
using FlowOps.Application.Requests;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Dashboard;

/// <summary>
/// <c>GET /api/dashboard</c> (API_CONTRACT.md §7, UI_SPEC.md §4.2) in four round trips: the caller's access context, one
/// statement with every count, the five newest own requests, and the ten newest visible activities. Sections the caller
/// lacks the capability for (<see cref="CapabilityRules"/>) are null.
/// </summary>
public sealed class DashboardService(IAppDbContext db, AccessRules access, TimeProvider time)
{
    public const int RecentRequests = 5;
    public const int RecentActivities = 10;
    public static readonly TimeSpan DecidedWindow = TimeSpan.FromDays(7);

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var me = await access.ForCurrentUserAsync(ct);
        if (!me.IsActive)
        {
            // Deactivated with a session that hasn't re-validated yet: like AccessRules, an inactive user sees nothing.
            return new DashboardDto(new MyRequestsSummaryDto(0, 0, 0, 0, []), null, [], null);
        }

        var counts = await CountsAsync(me, ct);
        var capabilities = CapabilityRules.For(me.Roles.ToList(), counts.IsManager);

        var mine = db.Requests.AsNoTracking().Where(r => r.RequesterId == me.UserId)
            .OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).Take(RecentRequests);
        var recent = await RequestSummaries.Project(db, mine).ToListAsync(ct);

        return new DashboardDto(
            new MyRequestsSummaryDto(counts.InReview, counts.Approved, counts.Completed, counts.Rejected, [.. recent.Select(r => r.ToDto())]),
            capabilities.Contains(Capability.TasksView)
                ? new MyTasksSummaryDto(counts.OpenTasks, counts.OldestOpenTask, counts.DecidedLast7Days)
                : null,
            await RecentActivityAsync(me, ct),
            capabilities.Contains(Capability.AdminManage)
                ? new AdminSummaryDto(counts.ActiveUsers, counts.Departments, counts.StalledTasks)
                : null);
    }

    /// <summary>
    /// Every number in one statement (scalar subqueries over the caller's row). The org-wide admin numbers are only
    /// evaluated for administrators (a SQL <c>CASE</c> on a parameter), so other users never run the stalled scan.
    /// </summary>
    private async Task<Counts> CountsAsync(AccessContext me, CancellationToken ct)
    {
        var userId = me.UserId;
        var isAdmin = CapabilityRules.CanAdminister(me.Roles.ToList());
        var since = time.GetUtcNow() - DecidedWindow;
        var own = db.Requests.Where(r => r.RequesterId == userId);
        var decidable = TaskEligibility.DecidableBy(db.Requests, me).Select(x => x.Task);
        var decided = db.Requests.SelectMany(r => r.Tasks).Where(t => t.DecidedById == userId && t.DecidedAt >= since);
        var stalled = TaskEligibility.Stalled(db);
        return await db.Users.Where(u => u.Id == userId)
            .Select(u => new Counts
            {
                IsManager = db.Users.Any(ManagerRelationship.ActiveDirectReportOf(userId)),
                InReview = own.Count(r => r.Status == RequestStatus.InReview),
                Approved = own.Count(r => r.Status == RequestStatus.Approved),
                Completed = own.Count(r => r.Status == RequestStatus.Completed),
                Rejected = own.Count(r => r.Status == RequestStatus.Rejected),
                OpenTasks = decidable.Count(),
                OldestOpenTask = decidable.Min(t => (DateTimeOffset?)t.CreatedAt),
                DecidedLast7Days = decided.Count(),
                ActiveUsers = isAdmin ? db.Users.Count(x => x.IsActive) : 0,
                Departments = isAdmin ? db.Departments.Count(d => d.IsActive) : 0,
                StalledTasks = isAdmin ? stalled.Count() : 0,
            })
            .SingleAsync(ct);
    }

    /// <summary>The newest activities on requests the caller can see, whatever their type (comments included).</summary>
    private async Task<IReadOnlyList<DashboardActivityDto>> RecentActivityAsync(AccessContext me, CancellationToken ct) =>
        await access.VisibleTo(db.Requests.AsNoTracking(), me)
            .SelectMany(r => r.Activities, (r, a) => new { Request = r, Activity = a })
            .OrderByDescending(x => x.Activity.OccurredAt).ThenByDescending(x => x.Activity.Id)
            .Take(RecentActivities)
            .Select(x => new DashboardActivityDto(
                x.Activity.Id,
                x.Activity.Type,
                db.Users.Where(u => u.Id == x.Activity.ActorId).Select(u => new UserRef(u.Id, u.DisplayName)).FirstOrDefault(),
                StepRef.From(x.Activity.StepKey, x.Activity.StepName),
                x.Activity.Body,
                x.Activity.OccurredAt,
                x.Request.Id,
                x.Request.Number.Value))
            .ToListAsync(ct);

    private sealed class Counts
    {
        public bool IsManager { get; init; }

        public int InReview { get; init; }

        public int Approved { get; init; }

        public int Completed { get; init; }

        public int Rejected { get; init; }

        public int OpenTasks { get; init; }

        public DateTimeOffset? OldestOpenTask { get; init; }

        public int DecidedLast7Days { get; init; }

        public int ActiveUsers { get; init; }

        public int Departments { get; init; }

        public int StalledTasks { get; init; }
    }
}
