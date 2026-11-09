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
    /// Checked and added under the user's lock, so parallel connects can't overshoot the cap. If <see cref="AbortAll"/>
    /// detached the user's set in the meantime, retries on the fresh set, so a connection is never added to a set nobody
    /// will abort (#78).
    /// </summary>
    public bool TryAdd(HubCallerContext connection, int maxPerUser)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (connection.UserIdentifier is not { } user)
        {
            return false;
        }

        while (true)
        {
            var connections = _byUser.GetOrAdd(user, _ => new(StringComparer.Ordinal));
            lock (connections)
            {
                if (!_byUser.TryGetValue(user, out var current) || !ReferenceEquals(current, connections))
                {
                    continue;
                }

                if (connections.Count >= maxPerUser)
                {
                    return false;
                }

                connections[connection.ConnectionId] = connection;
                return true;
            }
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

    /// <summary>
    /// Aborts every open connection of <paramref name="userId"/>; the client sees a closed connection. The set is detached
    /// first and then read under its lock, so a <see cref="TryAdd"/> racing with this either lands in the set (and is
    /// aborted here) or sees it detached and registers in a fresh one (#78).
    /// </summary>
    public void AbortAll(Guid userId)
    {
        if (!_byUser.TryRemove(userId.ToString(), out var connections))
        {
            return;
        }

        HubCallerContext[] toAbort;
        lock (connections)
        {
            toAbort = [.. connections.Values];
            connections.Clear();
        }

        foreach (var connection in toAbort)
        {
            connection.Abort();
        }
    }
}
