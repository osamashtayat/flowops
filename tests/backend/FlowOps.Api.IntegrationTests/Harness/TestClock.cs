using Microsoft.Extensions.Time.Testing;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>
/// The app's <see cref="TimeProvider"/> in integration tests. Delegates to a <see cref="FakeTimeProvider"/>
/// that <see cref="Reset"/> replaces (a fake cannot move backwards), so every test starts at <see cref="Start"/>
/// while the singleton registered in the app's container stays the same instance.
/// </summary>
public sealed class TestClock : TimeProvider
{
    /// <summary>Monday 2026-03-02 09:00 in Asia/Amman (UTC+3): company-time-zone dates are unambiguous.</summary>
    public static readonly DateTimeOffset Start = new(2026, 3, 2, 6, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _current = new(Start);

    public void Reset() => _current = new FakeTimeProvider(Start);

    public void Advance(TimeSpan delta) => _current.Advance(delta);

    public void SetUtcNow(DateTimeOffset value) => _current.SetUtcNow(value);

    public override DateTimeOffset GetUtcNow() => _current.GetUtcNow();

    public override long GetTimestamp() => _current.GetTimestamp();

    public override long TimestampFrequency => _current.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => _current.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _current.CreateTimer(callback, state, dueTime, period);
}
