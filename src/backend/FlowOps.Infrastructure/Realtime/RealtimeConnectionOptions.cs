namespace FlowOps.Infrastructure.Realtime;

/// <summary><c>FlowOps:Realtime</c>: limits on hub connections (#55). The Api's mapping options share the section.</summary>
public sealed class RealtimeConnectionOptions
{
    public const string SectionName = "FlowOps:Realtime";

    /// <summary>
    /// Open connections one user may hold at once: one per tab, plus room for reconnects. Beyond it the new connection is
    /// refused (the existing ones keep working), so a signed-in user can't exhaust server memory and sockets.
    /// </summary>
    public int MaxConnectionsPerUser { get; set; } = 10;
}
