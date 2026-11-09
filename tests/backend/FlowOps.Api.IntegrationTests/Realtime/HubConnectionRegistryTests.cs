using System.Security.Claims;
using FlowOps.Infrastructure.Realtime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace FlowOps.Api.IntegrationTests.Realtime;

/// <summary>#78: a connection registered while its user's connections are being aborted must never be lost.</summary>
public sealed class HubConnectionRegistryTests
{
    private const int Runs = 500;

    [Fact]
    public async Task TryAdd_RacingWithAbortAll_NeverLeavesAnUnabortableConnection()
    {
        var userId = Guid.CreateVersion7();
        for (var run = 0; run < Runs; run++)
        {
            var registry = new HubConnectionRegistry();
            var existing = new FakeConnection(userId);
            registry.TryAdd(existing, maxPerUser: 10).ShouldBeTrue();
            var racing = new FakeConnection(userId);
            using var start = new Barrier(2);

            var add = Task.Run(() =>
            {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                return registry.TryAdd(racing, maxPerUser: 10);
            }, TestContext.Current.CancellationToken);
            var abort = Task.Run(() =>
            {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                registry.AbortAll(userId);
            }, TestContext.Current.CancellationToken);
            await Task.WhenAll(add, abort);
            registry.AbortAll(userId);

            existing.Aborts.ShouldBe(1, $"run {run}");
            (await add).ShouldBeTrue();
            // Either the racing abort caught it, or it landed in a fresh set that the second abort reached: exactly once.
            racing.Aborts.ShouldBe(1, $"run {run}");
        }
    }

    [Fact]
    public void TryAdd_AfterAbortAll_CountsTowardsTheCapAgain()
    {
        var userId = Guid.CreateVersion7();
        var registry = new HubConnectionRegistry();
        registry.TryAdd(new FakeConnection(userId), maxPerUser: 1).ShouldBeTrue();
        registry.AbortAll(userId);

        registry.TryAdd(new FakeConnection(userId), maxPerUser: 1).ShouldBeTrue();
        registry.TryAdd(new FakeConnection(userId), maxPerUser: 1).ShouldBeFalse();
    }

    private sealed class FakeConnection(Guid userId) : HubCallerContext
    {
        private int _aborts;

        public int Aborts => Volatile.Read(ref _aborts);

        public override string ConnectionId { get; } = Guid.NewGuid().ToString();

        public override string? UserIdentifier { get; } = userId.ToString();

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => Interlocked.Increment(ref _aborts);
    }
}
