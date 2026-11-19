using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UnionFlight.TestKit;

/// <summary>
/// A self-contained X-Plane 12 stand-in: local REST + WebSocket Web API and a
/// UDP RREF server. It lets the library run end-to-end without a simulator.
/// </summary>
public sealed class FakeXPlaneServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly UdpClient _udp;
    private readonly FakeXPlaneState _state;
    private readonly CancellationTokenSource _cts = new();
    private Task? _udpTask;

    private FakeXPlaneServer(
        WebApplication app,
        UdpClient udp,
        FakeXPlaneState state,
        int webApiPort,
        int udpPort)
    {
        _app = app;
        _udp = udp;
        _state = state;
        WebApiPort = webApiPort;
        UdpPort = udpPort;
    }

    public int WebApiPort { get; }

    public int UdpPort { get; }

    public static async Task<FakeXPlaneServer> StartAsync(
        FakeXPlaneState state,
        int webApiPort = 0,
        int udpPort = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.AssignIds();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{webApiPort}");

        var app = builder.Build();
        app.UseWebSockets();

        app.MapGet("/api/capabilities", () => Results.Text(
            $$$"""{"api":{"versions":["v1","v2","v3"]},"x-plane":{"version":"{{{state.Version}}}"}}""",
            "application/json"));

        app.MapGet("/api/v3/datarefs", (HttpContext context) =>
        {
            var name = context.Request.Query["filter[name]"].ToString();
            if (string.IsNullOrEmpty(name) || !state.Ids.TryGetValue(name, out var id))
            {
                return Results.NotFound(new
                {
                    error_code = "invalid_dataref_name",
                    error_message = "Dataref not found"
                });
            }

            var valueType = state.Strings.ContainsKey(name) ? "data" : "float";
            return Results.Text(
                JsonSerializer.Serialize(new
                {
                    data = new[] { new { id, name, value_type = valueType } }
                }),
                "application/json");
        });

        app.MapGet("/api/v3/datarefs/{id:long}/value", (long id) =>
        {
            if (state.TryGetString(id, out var text))
            {
                var bytes = Encoding.UTF8.GetBytes(text + "\0");
                return Results.Text(
                    JsonSerializer.Serialize(new { data = Convert.ToBase64String(bytes) }),
                    "application/json");
            }

            if (state.TryGetNumeric(id, out var number))
            {
                return Results.Text(
                    JsonSerializer.Serialize(new { data = number }),
                    "application/json");
            }

            return Results.NotFound(new
            {
                error_code = "invalid_dataref_id",
                error_message = "Dataref not found"
            });
        });

        app.Map("/api/v3", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await ReceiveSubscribeAsync(socket, context.RequestAborted);
            await SendUpdateAsync(socket, state, context.RequestAborted);
            await WaitForCloseAsync(socket, context.RequestAborted);
        });

        await app.StartAsync(cancellationToken);

        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?
            .Addresses;

        var address = addresses?.FirstOrDefault()
            ?? throw new InvalidOperationException("Kestrel did not report an address.");
        var actualWebApiPort = new Uri(address).Port;

        var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, udpPort));
        var actualUdpPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;

        var server = new FakeXPlaneServer(app, udp, state, actualWebApiPort, actualUdpPort);
        server._udpTask = Task.Run(() => server.UdpLoopAsync(server._cts.Token));
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _app.StopAsync(timeout.Token);
        }
        catch (Exception)
        {
        }

        await _app.DisposeAsync();
        _udp.Dispose();

        if (_udpTask is not null)
        {
            try
            {
                await _udpTask;
            }
            catch (Exception)
            {
            }
        }

        _cts.Dispose();
    }

    private async Task UdpLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                await HandleUdpRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleUdpRequestAsync(
        UdpReceiveResult request,
        CancellationToken cancellationToken)
    {
        var packet = request.Buffer;
        if (packet.Length < 5 ||
            !packet.AsSpan(0, 4).SequenceEqual("RREF"u8) ||
            packet[4] != 0)
        {
            return;
        }

        var position = 5;
        while (position + 8 <= packet.Length)
        {
            _ = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(position, 4));
            var id = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(position + 4, 4));

            var nameStart = position + 8;
            var nameEnd = Array.IndexOf(packet, (byte)0, nameStart);
            if (nameEnd < 0)
            {
                break;
            }

            var name = Encoding.ASCII.GetString(packet, nameStart, nameEnd - nameStart);
            if (name.Length == 0)
            {
                break;
            }

            var value = ResolveValue(name);
            var response = BuildResponse(id, value);
            await _udp.SendAsync(response, request.RemoteEndPoint, cancellationToken)
                .ConfigureAwait(false);

            position = nameEnd + 1;
        }
    }

    private float ResolveValue(string name)
    {
        var bracket = name.LastIndexOf('[');
        if (bracket >= 0 && name.EndsWith(']'))
        {
            var baseName = name[..bracket];
            var indexText = name[(bracket + 1)..^1];

            if (int.TryParse(indexText, out var index) &&
                _state.Strings.TryGetValue(baseName, out var text))
            {
                return index < text.Length ? text[index] : 0f;
            }
        }

        return _state.Numeric.TryGetValue(name, out var numeric) ? (float)numeric : 0f;
    }

    private static byte[] BuildResponse(int id, float value)
    {
        var response = new byte[13];
        "RREF"u8.CopyTo(response);
        response[4] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(5, 4), id);
        BinaryPrimitives.WriteSingleLittleEndian(response.AsSpan(9, 4), value);
        return response;
    }

    private static async Task ReceiveSubscribeAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
        _ = result;
    }

    private static async Task SendUpdateAsync(
        WebSocket socket,
        FakeXPlaneState state,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, object>();
        foreach (var (name, value) in state.Numeric)
        {
            data[state.Ids[name].ToString()] = value;
        }

        var message = JsonSerializer.Serialize(new
        {
            type = "dataref_update_values",
            data
        });

        await socket.SendAsync(
            Encoding.UTF8.GetBytes(message),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task WaitForCloseAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (WebSocketException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }
}
