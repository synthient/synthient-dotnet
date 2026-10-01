using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class DownloadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> ReadAllAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(Token);
    }

    [Fact]
    public async Task DownloadsWithoutSendingTheKeyToStorage()
    {
        await using var storage = await TestServer.StartAsync(context => context.Response.WriteAsync("PAR1 data", context.RequestAborted), host: "localhost");
        await using var api = await TestServer.StartAsync(context =>
        {
            context.Response.StatusCode = 307;
            context.Response.Headers.Location = storage.Origin + "proxies/2026-05-03.parquet?X-Amz-Signature=secret";
            return Task.CompletedTask;
        });
        using var client = api.Client(o => o.UserAgent = "my-app/1.2");

        await using var file = await client.Proxies.DownloadAsync(ExportId.Daily(new DateOnly(2026, 5, 3)), Token);

        Assert.Equal("PAR1 data", await ReadAllAsync(file));
        var apiRequest = Assert.Single(api.Requests);
        Assert.Equal("/api/v4/feeds/proxies/export/2026-05-03", apiRequest.RawTarget);
        Assert.Equal(Fixtures.ApiKey, apiRequest.Headers["X-Api-Key"]);
        var storageRequest = Assert.Single(storage.Requests);
        Assert.Equal("/proxies/2026-05-03.parquet?X-Amz-Signature=secret", storageRequest.RawTarget);
        Assert.False(storageRequest.Headers.ContainsKey("X-Api-Key"));
        Assert.Equal("synthient-dotnet/" + SynthientClient.Version + " my-app/1.2", storageRequest.Headers["User-Agent"]);
    }

    [Theory]
    [InlineData(302, "https://storage.test/file?sig=1", "https://storage.test/file?sig=1")]
    [InlineData(307, "https://storage.test/file", "https://storage.test/file")]
    [InlineData(307, "/files/latest.parquet", "https://api.test/files/latest.parquet")]
    [InlineData(307, "files/latest.parquet", "https://api.test/api/v4/feeds/proxies/export/files/latest.parquet")]
    public async Task ResolvesTheDownloadUrl(int status, string location, string expected)
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Redirect(status, location)));

        var url = await client.Proxies.GetDownloadUrlAsync(ExportId.Latest, Token);

        Assert.Equal(expected, url.AbsoluteUri);
    }

    [Fact]
    public async Task ResolvesRedirectsFromHandlersThatOmitTheRequest()
    {
        using var client = Fixtures.Client(Fixtures.StubBase, new BareRedirectHandler());

        var url = await client.Proxies.GetDownloadUrlAsync(ExportId.Latest, Token);

        Assert.Equal("https://api.test/files/latest.parquet", url.AbsoluteUri);
    }

    // Builds its own response without setting RequestMessage, as mocking libraries do.
    private sealed class BareRedirectHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.TryAddWithoutValidation("Location", "/files/latest.parquet");
            return Task.FromResult(response);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://storage.test/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://storage.test/file")]
    [InlineData("https://user:pass@storage.test/file")]
    public async Task RejectsUnsafeRedirects(string location)
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Redirect(307, location)));

        await Assert.ThrowsAsync<SynthientException>(() => client.Proxies.GetDownloadUrlAsync(ExportId.Latest, Token));
    }

    [Fact]
    public async Task RejectsDowngradesDuringStorageRedirects()
    {
        var handler = new StubHandler(Fixtures.Redirect(307, "https://storage.test/a"), Fixtures.Redirect(302, "http://storage.test/b?sig=secret"));
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<SynthientException>(() => client.Proxies.DownloadAsync(ExportId.Latest, Token));

        Assert.Equal(2, handler.Count);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public async Task LimitsStorageRedirects(int redirects, bool succeeds)
    {
        var handler = new StubHandler(request =>
        {
            if (request.Uri.Host == "api.test")
            {
                return Fixtures.Redirect(307, "https://storage.test/0");
            }
            var hop = int.Parse(request.Uri.AbsolutePath[1..]);
            return hop < redirects ? Fixtures.Redirect(302, $"/{hop + 1}") : Fixtures.Text(200, "PAR1", "application/octet-stream");
        });
        using var client = Fixtures.StubClient(handler);

        if (succeeds)
        {
            await using var file = await client.Proxies.DownloadAsync(ExportId.Latest, Token);
            Assert.Equal("PAR1", await ReadAllAsync(file));
        }
        else
        {
            await Assert.ThrowsAsync<SynthientException>(() => client.Proxies.DownloadAsync(ExportId.Latest, Token));
        }
    }

    [Fact]
    public async Task StorageErrorsCarryOnlyTheStatus()
    {
        var handler = new StubHandler(
            Fixtures.Redirect(307, "https://storage.test/file?sig=secret"),
            Fixtures.Text(403, "<Error>signature https://storage.test/file?sig=secret expired</Error>", "application/xml"));
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => client.Proxies.DownloadAsync(ExportId.Latest, Token));

        Assert.Equal(403, error.StatusCode);
        Assert.Null(error.Detail);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnreachableStorageDoesNotLeakTheUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        await using var api = await TestServer.StartAsync(context =>
        {
            context.Response.StatusCode = 307;
            context.Response.Headers.Location = $"http://127.0.0.1:{port}/file?sig=secret";
            return Task.CompletedTask;
        });
        using var client = api.Client();

        var error = await Assert.ThrowsAsync<ApiConnectionException>(() => client.Proxies.DownloadAsync(ExportId.Latest, Token));

        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpectsARedirect()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, "{}")));

        var error = await Assert.ThrowsAsync<SynthientException>(() => client.Proxies.GetDownloadUrlAsync(ExportId.Latest, Token));

        Assert.IsNotType<ApiException>(error);
    }

    [Fact]
    public async Task RollupsInProgressCarryRetryAfter()
    {
        var handler = new StubHandler(Fixtures.Json(404, """{"type":"t","title":"Not Found","status":404,"detail":"rolling up"}""", ("Retry-After", "60")));
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => client.Proxies.DownloadAsync(ExportId.Daily(new DateOnly(2026, 5, 3)), Token));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), error.RetryAfter);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task StreamsTheFileAndClosesItOnDispose()
    {
        var aborted = new TaskCompletionSource();
        await using var server = await TestServer.StartAsync(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 307;
                context.Response.Headers.Location = "/storage/file";
                return;
            }
            context.RequestAborted.Register(() => aborted.TrySetResult());
            await context.Response.WriteAsync("PAR1", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        using var client = server.Client();

        var file = await client.Proxies.DownloadAsync(ExportId.Latest, Token);
        var buffer = new byte[4];
        await file.ReadExactlyAsync(buffer, Token);
        Assert.Equal("PAR1", Encoding.ASCII.GetString(buffer));
        await file.DisposeAsync();

        await aborted.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task StalledDownloadsTimeOut()
    {
        await using var server = await TestServer.StartAsync(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 307;
                context.Response.Headers.Location = "/storage/file";
                return;
            }
            await context.Response.WriteAsync("PAR1", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        using var client = server.Client(o => o.Timeout = TimeSpan.FromMilliseconds(300));

        await using var file = await client.Proxies.DownloadAsync(ExportId.Latest, Token);

        await Assert.ThrowsAsync<ApiTimeoutException>(() => file.CopyToAsync(Stream.Null, Token));
    }
}
