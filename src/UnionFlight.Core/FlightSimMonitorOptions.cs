namespace UnionFlight;

public sealed class FlightSimMonitorOptions
{
    /// <summary>Maximum time allowed for all providers to answer a probe.</summary>
    public TimeSpan ProbeTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Delay before re-probing after the active provider faults.</summary>
    public TimeSpan RestartDelay { get; init; } = TimeSpan.FromSeconds(1);
}
