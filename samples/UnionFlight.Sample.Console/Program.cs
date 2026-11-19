using UnionFlight;
using UnionFlight.XPlane;

await using var client = new UnionFlightClient(new IFlightSimProvider[]
{
    new XPlaneProvider()
});

client.ProviderChanged += (_, provider) =>
    Console.WriteLine($"Connected to {provider.DisplayName}.");

client.SnapshotReceived += (_, snapshot) =>
{
    Console.WriteLine(
        $"{snapshot.Simulator} | " +
        $"{snapshot.AircraftTitle ?? "?"} ({snapshot.AircraftTailNumber ?? "?"}) | " +
        $"{snapshot.AltitudeFt:F0} ft | {snapshot.GroundspeedKt:F0} kt | " +
        $"paused={snapshot.IsPaused}");
};

await client.StartAsync();

if (client.ActiveProvider is null)
{
    Console.WriteLine("No simulator detected. Start X-Plane 11/12 and run again.");
    return;
}

Console.WriteLine("Press Ctrl+C to stop.");
await Task.Delay(Timeout.Infinite);
