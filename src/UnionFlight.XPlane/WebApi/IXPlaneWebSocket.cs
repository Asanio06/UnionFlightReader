namespace UnionFlight.XPlane.WebApi;

public interface IXPlaneWebSocket : IAsyncDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    Task SendTextAsync(string text, CancellationToken cancellationToken);

    /// <summary>Returns null when the peer closes the socket.</summary>
    Task<string?> ReceiveTextAsync(CancellationToken cancellationToken);
}
