namespace UnionFlight.XPlane;

public sealed class XPlaneOptions
{
    public string Host { get; init; } = "127.0.0.1";

    public int WebApiPort { get; init; } = 8086;

    public int UdpPort { get; init; } = 49000;

    public bool PreferWebApi { get; init; } = true;

    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan UdpTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>How often the UDP RREF subscriptions are refreshed.</summary>
    public TimeSpan SubscriptionRefresh { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Snapshot publication rate. 20 Hz is enough for a UI/logger.</summary>
    public TimeSpan SnapshotInterval { get; init; } = TimeSpan.FromMilliseconds(50);
}
