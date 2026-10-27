namespace Orbit.Shared;

/// <summary>Names of the client-bound methods invoked on the metrics SignalR hub. Kept here so server and client never drift.</summary>
public static class MetricsHubMethods
{
    /// <summary>Server -&gt; client: a fresh metrics reading for one droplet.</summary>
    public const string ReceiveMetrics = "ReceiveMetrics";

    /// <summary>Server -&gt; client: a droplet's status/action state changed (e.g. reboot started/finished).</summary>
    public const string ReceiveDropletState = "ReceiveDropletState";

    /// <summary>Server -&gt; client: the full droplet list was refreshed.</summary>
    public const string ReceiveDropletsRefreshed = "ReceiveDropletsRefreshed";
}
