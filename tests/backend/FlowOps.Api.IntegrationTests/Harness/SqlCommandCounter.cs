using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>Counts the SQL commands EF sends while <see cref="Start"/> is on, e.g. to check a page's round-trip budget.</summary>
public sealed class SqlCommandCounter : DbCommandInterceptor
{
    private int _count;
    private volatile bool _counting;

    public int Count => _count;

    public void Start()
    {
        Interlocked.Exchange(ref _count, 0);
        _counting = true;
    }

    public int Stop()
    {
        _counting = false;
        return _count;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Tally();
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Tally();
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Tally()
    {
        if (_counting)
        {
            Interlocked.Increment(ref _count);
        }
    }
}
