using System.Net;

namespace Synthient.Tests;

// SocketsHttpHandler silently resends a bodiless request when a reused HTTP/1.1 connection closes
// before any response bytes arrive. The server may already have handled that request, so the SDK
// must never let it happen to a billed lookup.
public sealed class ReplayTests
{
    // The first request warms a pooled connection, the second is read and then the connection is
    // closed without a response, and any later request is a resend.
    private static RawHttpServer DropSecondRequest() =>
        new((index, _) => index == 1 ? null : RawHttpServer.Ok(index == 0 ? Fixtures.Account : Fixtures.IPInfo));

    [Fact]
    public async Task BilledLookupIsNotResentOnAReusedConnection()
    {
        await using var server = DropSecondRequest();
        using var client = Fixtures.Client(server.BaseAddress, configure: o => o.MaxRetries = 0);
        await client.GetAccountAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ApiConnectionException>(
            () => client.LookupIPAsync(IPAddress.Parse("8.8.8.8"), TestContext.Current.CancellationToken));

        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task FreeCallIsNotResentByTheHandler()
    {
        await using var server = DropSecondRequest();
        using var client = Fixtures.Client(server.BaseAddress, configure: o => o.MaxRetries = 0);
        await client.GetAccountAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ApiConnectionException>(() => client.GetAccountAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, server.Requests.Count);
    }

    [Fact]
    public async Task BodilessRequestIsResentByTheHandler()
    {
        // The control case: without the SDK's guard, the handler resends the dropped request.
        await using var server = DropSecondRequest();
        using var http = new HttpClient(new SocketsHttpHandler());
        var url = new Uri(server.BaseAddress + "/account/me");
        (await http.GetAsync(url, TestContext.Current.CancellationToken)).Dispose();

        using var response = await http.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, server.Requests.Count);
    }

    [Fact]
    public async Task GetSendsAnEmptyBody()
    {
        await using var server = new RawHttpServer((_, _) => RawHttpServer.Ok(Fixtures.IPInfo));
        using var client = Fixtures.Client(server.BaseAddress);

        await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"), TestContext.Current.CancellationToken);

        var request = Assert.Single(server.Requests);
        Assert.StartsWith("GET /api/v4/lookup/ip/8.8.8.8 HTTP/1.1\r\n", request);
        Assert.Contains("\r\nContent-Length: 0\r\n", request);
        Assert.DoesNotContain("Transfer-Encoding", request, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Content-Type", request, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostSendsAnExactLength()
    {
        await using var server = new RawHttpServer((_, _) => RawHttpServer.Ok($$"""{"results":[{{Fixtures.IPInfo}}]}"""));
        using var client = Fixtures.Client(server.BaseAddress);

        await client.LookupIPsAsync([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:db8::1")], TestContext.Current.CancellationToken);

        var request = Assert.Single(server.Requests);
        const string body = """{"ips":["8.8.8.8","2001:db8::1"]}""";
        Assert.StartsWith("POST /api/v4/lookup/ips HTTP/1.1\r\n", request);
        Assert.Contains($"\r\nContent-Length: {body.Length}\r\n", request);
        Assert.Contains("\r\nContent-Type: application/json\r\n", request);
        Assert.DoesNotContain("Transfer-Encoding", request, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("\r\n\r\n" + body, request);
    }
}
