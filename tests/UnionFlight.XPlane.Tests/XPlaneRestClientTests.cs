using System.Net;
using System.Text;
using UnionFlight.XPlane.WebApi;

namespace UnionFlight.XPlane.Tests;

public sealed class XPlaneRestClientTests
{
    [Fact]
    public async Task GetCapabilities_Reads_Versions_And_Prefers_V3()
    {
        using var client = Client(_ => Json(
            """{"api":{"versions":["v1","v2","v3"]},"x-plane":{"version":"12.4.0"}}"""));

        var capabilities = await client.GetCapabilitiesAsync(CancellationToken.None);

        Assert.NotNull(capabilities);
        Assert.Equal("12.4.0", capabilities!.Version);
        Assert.Equal("v3", capabilities.PreferredApiVersion);
    }

    [Fact]
    public async Task ResolveDataref_Returns_64Bit_Id()
    {
        using var client = Client(request =>
        {
            Assert.Contains("/api/v3/datarefs", request.RequestUri!.PathAndQuery);
            return Json(
                """{"data":[{"id":1253033683792,"name":"sim/flightmodel/position/latitude","value_type":"float"}]}""");
        });

        var id = await client.ResolveDatarefAsync(
            "sim/flightmodel/position/latitude",
            CancellationToken.None);

        Assert.Equal(1253033683792L, id);
    }

    [Fact]
    public async Task ResolveDataref_Returns_Null_On_404()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var id = await client.ResolveDatarefAsync("sim/does/not/exist", CancellationToken.None);

        Assert.Null(id);
    }

    [Fact]
    public async Task GetDatarefDataString_Decodes_Base64_And_Trims_At_Nul()
    {
        using var client = Client(_ => Json(
            $$"""{"data":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes("N12345\0"))}}"}"""));

        var value = await client.GetDatarefDataStringAsync(20, CancellationToken.None);

        Assert.Equal("N12345", value);
    }

    [Fact]
    public async Task GetCapabilities_Throws_On_Forbidden()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var exception = await Assert.ThrowsAsync<XPlaneApiException>(
            () => client.GetCapabilitiesAsync(CancellationToken.None));

        Assert.Equal("forbidden", exception.Code);
    }

    private static XPlaneRestClient Client(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new StubHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:8086")
        };

        return new XPlaneRestClient(http, ownsHttp: true);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
