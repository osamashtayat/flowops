using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace FlowOps.Infrastructure.Realtime;

/// <summary>
/// The open hub connections per user, so they can be closed when the user's sessions end. SignalR keeps no such index
/// itself (it only sends to users). In memory, like SignalR's own state: Phase 1 runs one API instance.
/// </summary>
public sealed class HubConnectionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HubCallerContext>> _byUser = new(StringComparer.Ordinal);

    public void Add(HubCallerContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.UserIdentifier is { } user)
        {
            _byUser.GetOrAdd(user, _ => new(StringComparer.Ordinal))[connection.ConnectionId] = connection;
        }
    }

    public void Remove(HubCallerContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.UserIdentifier is { } user && _byUser.TryGetValue(user, out var connections))
        {
            connections.TryRemove(connection.ConnectionId, out _);
        }
    }

    /// <summary>Aborts every open connection of <paramref name="userId"/>; the client sees a closed connection.</summary>
    public void AbortAll(Guid userId)
    {
        if (_byUser.TryRemove(userId.ToString(), out var connections))
        {
            foreach (var connection in connections.Values)
            {
                connection.Abort();
            }
        }
    }
}
