using FlowOps.Application.Access;
using FlowOps.Application.Common;
using FlowOps.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Application.Requests;

/// <summary>
/// Request reads (API_CONTRACT.md §4): list, details and activity. Projections only; visibility always through
/// <see cref="AccessRules"/>, so an invisible request is 404 and never appears in a list.
/// </summary>
public sealed class RequestQueryService(IAppDbContext db, AccessRules access, RequestDetailsReader details)
{
    /// <summary>Activity is bounded by process length plus comments; the cap keeps a pathological request cheap.</summary>
    public const int ActivityCap = 500;

    public async Task<PagedResult<RequestSummaryDto>> ListAsync(RequestListQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var me = await access.ForCurrentUserAsync(ct);
        var requests = access.VisibleTo(db.Requests.AsNoTracking(), me);
        if (query.Scope == RequestScope.Mine)
        {
            requests = requests.Where(r => r.RequesterId == me.UserId);
        }

        requests = Filter(requests, query);
        var ordered = requests.OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id);
        var page = await RequestSummaries.Project(db, ordered).ToPagedResultAsync(query, ct);
        return page.Map(row => row.ToDto());
    }

    public async Task<RequestDetailsDto> GetAsync(Guid id, CancellationToken ct)
    {
        var me = await access.ForCurrentUserAsync(ct);
        return await details.ReadAsync(id, me, ct) ?? throw new NotFoundException();
    }

    public async Task<IReadOnlyList<ActivityDto>> GetActivityAsync(Guid id, CancellationToken ct)
    {
        var me = await access.ForCurrentUserAsync(ct);
        if (!await access.CanViewAsync(id, me, ct))
        {
            throw new NotFoundException();
        }

        var newestFirst = await db.Requests.AsNoTracking()
            .Where(r => r.Id == id)
            .SelectMany(r => r.Activities)
            // The newest entries win the cap (#37), so the latest decisions always show; returned oldest first.
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Take(ActivityCap)
            .Select(a => new ActivityDto(
                a.Id,
                a.Type,
                db.Users.Where(u => u.Id == a.ActorId).Select(u => new UserRef(u.Id, u.DisplayName)).FirstOrDefault(),
                a.Stage,
                a.Body,
                a.OccurredAt))
            .ToListAsync(ct);
        newestFirst.Reverse();
        return newestFirst;
    }

    private static IQueryable<Request> Filter(IQueryable<Request> requests, RequestListQuery query)
    {
        if (query.Status is { } status)
        {
            requests = requests.Where(r => r.Status == status);
        }

        if (query.Type is { } type)
        {
            requests = requests.Where(r => r.Type == type);
        }

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return requests;
        }

        var term = query.Search.Trim();
        var numberPrefix = term.ToUpperInvariant();
        var itemName = term.ToLowerInvariant();
        // The number is stored as text through a value converter; the double cast makes EF compare the column itself.
        // SQL, not .NET: lower(...) LIKE with EF escaping, hence the suppressions (as in UserAdministrationService).
#pragma warning disable CA1304, CA1311, CA1862
        return requests.Where(r => ((string)(object)r.Number).StartsWith(numberPrefix)
            || (r.Purchase != null && r.Purchase.ItemName.ToLower().Contains(itemName)));
#pragma warning restore CA1304, CA1311, CA1862
    }
}
