namespace FlowOps.Api;

/// <summary><c>FlowOps:Realtime</c>: how the notifications hub is mapped (BE-14).</summary>
public sealed class RealtimeHubOptions
{
    public const string SectionName = "FlowOps:Realtime";

    /// <summary>
    /// Close a hub connection when its session cookie expires. On by default. The integration tests turn it off: their
    /// cookies are issued by a fake clock, which SignalR compares with real time, so every connection would close at once.
    /// </summary>
    public bool CloseOnAuthenticationExpiration { get; init; } = true;
}
