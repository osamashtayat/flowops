using System.Collections.Concurrent;
using FlowOps.Application.Common;
using FlowOps.Infrastructure.Realtime;

namespace FlowOps.Api.IntegrationTests.Harness;

/// <summary>One published hint, as the app asked for it.</summary>
public sealed record RealtimeEvent(string Name, IReadOnlyCollection<Guid> Recipients, Guid? NotificationId, Guid? RequestId);

/// <summary>
/// What tests see as <c>Factory.Realtime</c> (TESTING.md §2): every hint the app published, plus a switch that makes
/// publishing throw, to prove a failure never fails the request. Shared by the factory and any host derived from it.
/// </summary>
public sealed class RealtimeRecorder
{
    private readonly ConcurrentQueue<RealtimeEvent> _events = new();

    public bool FailPublishing { get; set; }

    public IReadOnlyList<RealtimeEvent> Events => [.. _events];

    public void Reset()
    {
        _events.Clear();
        FailPublishing = false;
    }

    internal void Record(RealtimeEvent realtimeEvent)
    {
        if (FailPublishing)
        {
            throw new InvalidOperationException("Simulated realtime failure.");
        }

        _events.Enqueue(realtimeEvent);
    }
}

/// <summary>
/// The test <see cref="IRealtimePublisher"/>: records into the shared <see cref="RealtimeRecorder"/>, then forwards to the
/// real SignalR publisher <b>of the same host</b> (a derived host must not redirect the main app's events), so hub tests
/// still receive them.
/// </summary>
public sealed class RecordingRealtimePublisher(RealtimeRecorder recorder, SignalRRealtimePublisher inner) : IRealtimePublisher
{
    public Task NotificationCreatedAsync(Guid recipientId, Guid notificationId, Guid? requestId, CancellationToken ct)
    {
        recorder.Record(new RealtimeEvent("notificationCreated", [recipientId], notificationId, requestId));
        return inner.NotificationCreatedAsync(recipientId, notificationId, requestId, ct);
    }

    public Task RequestChangedAsync(IReadOnlyCollection<Guid> recipients, Guid requestId, CancellationToken ct)
    {
        recorder.Record(new RealtimeEvent("requestChanged", [.. recipients], null, requestId));
        return inner.RequestChangedAsync(recipients, requestId, ct);
    }

    public Task TasksChangedAsync(IReadOnlyCollection<Guid> recipients, CancellationToken ct)
    {
        recorder.Record(new RealtimeEvent("tasksChanged", [.. recipients], null, null));
        return inner.TasksChangedAsync(recipients, ct);
    }
}
