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

    /// <summary>
    /// Registers <paramref name="connection"/> unless its user already has <paramref name="maxPerUser"/> open (#55).
    /// Checked and added under the user's lock, so parallel connects can't overshoot the cap.
    /// </summary>
    public bool TryAdd(HubCallerContext connection, int maxPerUser)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.UserIdentifier is not { } user)
        {
            return false;
        }

        var connections = _byUser.GetOrAdd(user, _ => new(StringComparer.Ordinal));
        lock (connections)
        {
            if (connections.Count >= maxPerUser)
            {
                return false;
            }

            connections[connection.ConnectionId] = connection;
            return true;
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
