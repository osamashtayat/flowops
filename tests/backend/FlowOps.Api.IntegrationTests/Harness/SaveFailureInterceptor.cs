using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// TESTING.md §3.5: makes the next <c>SaveChanges</c> fail after the domain call, so tests can show that nothing was
/// written (notification rows included) and nothing was published.
/// </summary>
public sealed class SaveFailureInterceptor : SaveChangesInterceptor
{
    private int _failNext;

    public void FailNextSave() => Interlocked.Exchange(ref _failNext, 1);

    public void Reset() => Interlocked.Exchange(ref _failNext, 0);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _failNext, 0) == 1
            ? throw new DbUpdateException("Simulated database failure.")
            : base.SavingChangesAsync(eventData, result, cancellationToken);
}
