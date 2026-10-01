using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

[CollectionDefinition(nameof(SharedEnvironment), DisableParallelization = true)]
public sealed class SharedEnvironment;

[Collection(nameof(SharedEnvironment))]
public sealed class ClientTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadsTheKeyFromTheEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("SYNTHIENT_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("SYNTHIENT_API_KEY", " env-key\n");
            var handler = new StubHandler(Fixtures.Json(200, Fixtures.Account));
            using var client = new SynthientClient(new SynthientClientOptions { BaseAddress = Fixtures.StubBase, Handler = handler });
            await client.GetAccountAsync(Token);
            Assert.Equal("env-key", Assert.Single(handler.Requests).Header("X-Api-Key"));

            Environment.SetEnvironmentVariable("SYNTHIENT_API_KEY", null);
            Assert.Throws<ArgumentException>(() => new SynthientClient());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SYNTHIENT_API_KEY", previous);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("key\nwith-newline")]
    [InlineData("kéy")]
    public void RejectsInvalidKeys(string key)
    {
        Assert.Throws<ArgumentException>(() => new SynthientClient(key));
    }

    [Theory]
    [InlineData("ftp://api.synthient.com/api/v4")]
    [InlineData("https://user:pass@api.synthient.com/api/v4")]
    [InlineData("https://api.synthient.com/api/v4?debug=1")]
    [InlineData("https://api.synthient.com/api/v4#top")]
    [InlineData("/api/v4")]
    public void RejectsInvalidBaseAddresses(string address)
    {
        var options = new SynthientClientOptions { ApiKey = "k", BaseAddress = new Uri(address, UriKind.RelativeOrAbsolute) };
        Assert.Throws<ArgumentException>(() => new SynthientClient(options));
    }

    [Fact]
    public void RejectsInvalidOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", MaxRetries = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", Timeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", Timeout = TimeSpan.FromDays(30) }));
        Assert.Throws<ArgumentException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", UserAgent = "app\r\nX-Evil: 1" }));
        new SynthientClient(new SynthientClientOptions { ApiKey = "k", Timeout = Timeout.InfiniteTimeSpan, MaxRetries = 0 }).Dispose();
    }

    [Fact]
    public async Task SendsTheExpectedHeadersAndPath()
    {
        var handler = new StubHandler(Fixtures.Json(200, Fixtures.Account));
        using var client = Fixtures.Client(new Uri("https://api.test/base/v4/"), handler, o =>
        {
            o.ApiKey = "  test-key ";
            o.UserAgent = "my-app/1.2";
        });

        await client.GetAccountAsync(Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.test/base/v4/account/me", request.Uri.AbsoluteUri);
        Assert.Equal("test-key", request.Header("X-Api-Key"));
        Assert.Equal("synthient-dotnet/" + SynthientClient.Version + " my-app/1.2", request.Header("User-Agent"));
        Assert.Equal("application/json", request.Header("Accept"));
    }

    [Fact]
    public async Task CopiesItsOptions()
    {
        var handler = new StubHandler(Fixtures.Json(200, Fixtures.Account));
        var options = new SynthientClientOptions { ApiKey = "first", BaseAddress = Fixtures.StubBase, Handler = handler };
        using var client = new SynthientClient(options);
        options.ApiKey = "second";
        options.BaseAddress = new Uri("https://elsewhere.test/");

        await client.GetAccountAsync(Token);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("first", request.Header("X-Api-Key"));
        Assert.Equal("api.test", request.Uri.Host);
    }

    [Fact]
    public void RejectsHandlersThatFollowRedirects()
    {
        Assert.Throws<ArgumentException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", Handler = new SocketsHttpHandler() }));
        Assert.Throws<ArgumentException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", Handler = new HttpClientHandler() }));
        var wrapped = new TrackingHandler(new SocketsHttpHandler());
        Assert.Throws<ArgumentException>(() => new SynthientClient(new SynthientClientOptions { ApiKey = "k", Handler = wrapped }));

        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        new SynthientClient(new SynthientClientOptions { ApiKey = "k", Handler = handler }).Dispose();
    }

    [Fact]
    public async Task DoesNotDisposeABorrowedHandler()
    {
        using var handler = new TrackingHandler(new StubHandler(Fixtures.Json(200, Fixtures.Account), Fixtures.Json(200, Fixtures.Account)));
        var client = Fixtures.Client(Fixtures.StubBase, handler);
        await client.GetAccountAsync(Token);

        client.Dispose();

        Assert.False(handler.Disposed);
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, Fixtures.StubBase), Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ThrowsAfterDispose()
    {
        var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, Fixtures.Account)));
        client.Dispose();
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAccountAsync(Token));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await foreach (var _ in client.Proxies.StreamAsync(Token))
            {
            }
        });
    }

    [Fact]
    public async Task DisposeCancelsAWaitingCall()
    {
        var received = new TaskCompletionSource();
        await using var server = await TestServer.StartAsync(async context =>
        {
            received.SetResult();
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        var client = server.Client();
        var call = client.GetAccountAsync(Token);
        await received.Task.WaitAsync(Token);

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => call);
    }

    [Fact]
    public async Task OpenStreamsOutliveDispose()
    {
        var next = new TaskCompletionSource();
        await using var server = await TestServer.StartAsync(async context =>
        {
            await context.Response.WriteAsync("{\"ip\":\"1.1.1.1\"}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await next.Task.WaitAsync(context.RequestAborted);
            await context.Response.WriteAsync("{\"ip\":\"2.2.2.2\"}\n", context.RequestAborted);
        });
        var client = server.Client();
        var events = new List<string?>();

        await foreach (var proxy in client.Proxies.StreamAsync(Token))
        {
            events.Add(proxy.IP);
            if (events.Count == 1)
            {
                client.Dispose();
                next.SetResult();
            }
        }

        Assert.Equal(["1.1.1.1", "2.2.2.2"], events);
    }

    [Fact]
    public void VersionMatchesTheAssembly()
    {
        var version = typeof(SynthientClient).Assembly.GetName().Version!;
        Assert.Equal(SynthientClient.Version, version.ToString(3));
        var informational = typeof(SynthientClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!;
        Assert.Equal(SynthientClient.Version, informational.InformationalVersion);
    }
}

internal sealed class TrackingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    public bool Disposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
