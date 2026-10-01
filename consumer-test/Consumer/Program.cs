using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Synthient;

using var client = new SynthientClient(new SynthientClientOptions
{
    ApiKey = "test-key",
    BaseAddress = new Uri("https://api.test/api/v4"),
    Handler = new StubHandler(),
});

var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"));
Check(info.IP.Equals(IPAddress.Parse("8.8.8.8")) && info.Intelligence?.RiskScore == 12, "lookup");

try
{
    await client.GetAccountAsync();
    Check(false, "403");
}
catch (ApiException e)
{
    Check(e.StatusCode == 403 && e.Detail == "Requires one of: BASIC", "403");
}

var events = new List<string?>();
await foreach (var proxy in client.Proxies.StreamAsync())
{
    events.Add(proxy.IP);
}
Check(events.SequenceEqual(["1.1.1.1", "2.2.2.2"]), "stream");

#if !CORE_ONLY
using (var listener = new TcpListener(IPAddress.Loopback, 0))
{
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    try
    {
        await GrpcSchema.FetchAsync("test-key", new GrpcSchemaOptions { Endpoint = $"127.0.0.1:{port}", Plaintext = true });
        Check(false, "grpc");
    }
    catch (GrpcSchemaException e)
    {
        Check(e.StatusCode == Grpc.Core.StatusCode.Unavailable, "grpc");
    }
}
#endif

Console.WriteLine($"consumer ok: Synthient.Client {SynthientClient.Version} on {RuntimeInformation.FrameworkDescription}");

static void Check(bool ok, string name)
{
    if (!ok)
    {
        throw new InvalidOperationException($"{name} check failed");
    }
}

internal sealed class StubHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var (status, body) = request.RequestUri!.AbsolutePath switch
        {
            "/api/v4/lookup/ip/8.8.8.8" => (200, """{"ip":"8.8.8.8","intelligence":{"risk_score":12,"categories":[],"behavior":[],"providers":[],"devices":[]}}"""),
            "/api/v4/feeds/proxies/stream" => (200, "{\"ip\":\"1.1.1.1\"}\n\n{\"ip\":\"2.2.2.2\"}\n"),
            _ => (403, """{"type":"t","title":"Forbidden","status":403,"detail":"Requires one of: BASIC"}"""),
        };
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        });
    }
}
