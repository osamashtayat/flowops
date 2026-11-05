using FlowOps.Application.Requests;
using Microsoft.EntityFrameworkCore;

namespace FlowOps.Infrastructure.Persistence;

/// <summary><c>nextval</c> on <see cref="AppDbContext.RequestNumberSequence"/>; not transactional, so a failed submission leaves a gap.</summary>
internal sealed class RequestNumberSequence(AppDbContext db) : IRequestNumberSequence
{
    public async Task<long> NextAsync(CancellationToken ct) =>
        await db.Database.SqlQueryRaw<long>($"SELECT nextval('{AppDbContext.RequestNumberSequence}') AS \"Value\"").SingleAsync(ct);
}
