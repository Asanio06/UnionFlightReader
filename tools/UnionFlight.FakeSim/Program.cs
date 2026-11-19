using UnionFlight.TestKit;

var state = FakeXPlaneState.CreateDefault();
await using var server = await FakeXPlaneServer.StartAsync(
    state,
    webApiPort: 8086,
    udpPort: 49000);

Console.WriteLine(
    $"Fake X-Plane ready. Web API: http://127.0.0.1:{server.WebApiPort}  UDP: {server.UdpPort}");
Console.WriteLine(
    "Run the sample in another terminal: dotnet run --project samples/UnionFlight.Sample.Console");
Console.WriteLine("Press Ctrl+C to stop.");

await Task.Delay(Timeout.Infinite);
