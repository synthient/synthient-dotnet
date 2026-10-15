using System.Text;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class StreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HttpResponseMessage Ndjson(string body, int chunk = int.MaxValue) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StreamContent(new ChunkedStream(Encoding.UTF8.GetBytes(body), chunk)),
    };

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> events)
    {
        var list = new List<T>();
        await foreach (var item in events)
        {
            list.Add(item);
        }
        return list;
    }

    [Fact]
    public async Task SkipsKeepalivesAndCarriageReturns()
    {
        var handler = new StubHandler(Ndjson("\n  \n{\"ip\":\"1.1.1.1\",\"timestamp\":1759000000}\r\n\t\n{\"ip\":\"2.2.2.2\"}\n\n"));
        using var client = Fixtures.StubClient(handler);

        var events = await CollectAsync(client.Proxies.StreamAsync(Token));

        Assert.Equal(["1.1.1.1", "2.2.2.2"], events.Select(e => e.IP));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1759000000), events[0].Timestamp);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/v4/feeds/proxies/stream", request.Path);
        Assert.Equal("application/x-ndjson", request.Header("Accept"));
    }

    [Fact]
    public async Task LeavingTheLoopClosesTheConnection()
    {
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await TestServer.StartAsync(async context =>
        {
            await context.Response.WriteAsync("{\"ip\":\"1.1.1.1\"}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            try
            {
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                disconnected.SetResult();
            }
        });
        using var client = server.Client();

        await foreach (var _ in client.Proxies.StreamAsync(Token))
        {
            break;
        }

        // SocketsHttpHandler's default drain would keep a quiet stream open for 2 seconds.
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(1), Token);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task HandlesLinesSplitAcrossReads(int chunk)
    {
        const string body = "{\"ip\":\"1.1.1.1\",\"provider\":\"日本の🦊\"}\n\n{\"ip\":\"2.2.2.2\",\"provider\":\"é\"}";
        using var client = Fixtures.StubClient(new StubHandler(Ndjson(body, chunk)));

        var events = await CollectAsync(client.Proxies.StreamAsync(Token));

        Assert.Equal(["日本の🦊", "é"], events.Select(e => e.Provider));
    }

    [Theory]
    [InlineData("{\"ip\":\"1.1.1.1\"}\n{\"ip\":")]
    [InlineData("{\"ip\":\"1.1.1.1\"}\nnot json\n")]
    [InlineData("{\"ip\":\"1.1.1.1\"}\nnull\n")]
    [InlineData("{\"ip\":\"1.1.1.1\"}\n{\"ip\":\"2.2.2.2\"} {}\n")]
    [InlineData("{\"ip\":\"1.1.1.1\"}\n{\"asn\":\"1\"}\n")]
    public async Task InvalidEventsEndTheStream(string body)
    {
        using var client = Fixtures.StubClient(new StubHandler(Ndjson(body)));
        var events = new List<ProxyEvent>();

        await Assert.ThrowsAsync<SynthientException>(async () =>
        {
            await foreach (var proxy in client.Proxies.StreamAsync(Token))
            {
                events.Add(proxy);
            }
        });

        Assert.Single(events);
    }

    [Fact]
    public async Task RejectsOversizedEvents()
    {
        var body = "{\"ip\":\"1.1.1.1\"}\n{\"provider\":\"" + new string('a', NdjsonReader.MaxLineSize) + "\"}\n";
        using var client = Fixtures.StubClient(new StubHandler(Ndjson(body, 1 << 20)));

        var error = await Assert.ThrowsAsync<SynthientException>(() => CollectAsync(client.Proxies.StreamAsync(Token)));

        Assert.Contains("exceeds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecodesHeliosEvents()
    {
        const string http = """{"timestamp":1759000000123,"tunnel_id":7,"domain":"example.com","port":80,"protocol":"http","meta":{"proxy_ip":"203.0.113.1","server":"s1","pool_id":"p","provider":"x"},"details":{"method":"GET","uri":"/","version":"HTTP/1.1","headers":{"Host":"example.com"}},"raw":"R0VUIC8="}""";
        const string https = """{"timestamp":1759000000123,"tunnel_id":17257744507466107949,"domain":"example.com","port":443,"protocol":"https","meta":{},"details":null,"raw":null}""";
        const string hello = """{"timestamp":1759000000123,"tunnel_id":8,"domain":"example.com","port":443,"protocol":"https","meta":{},"details":{"sni":"example.com","cipher_suites":[{"code":4865,"name":"TLS_AES_128_GCM_SHA256"}],"has_grease":true,"compression_methods":[0]}}""";
        const string dns = """{"timestamp":1759000000123,"tunnel_id":1,"domain":"1.1.1.1","port":53,"protocol":"dns","meta":{}}""";
        const string adb = """{"session":"0123456789abcdef0123456789abcdef","sequential_id":1,"command":"id","hash":"ab"}""";
        var handler = new StubHandler(Ndjson(http), Ndjson(https + "\n" + hello), Ndjson(dns), Ndjson(adb));
        using var client = Fixtures.StubClient(handler);

        var httpEvent = Assert.Single(await CollectAsync(client.HeliosHttp.StreamAsync(Token)));
        var httpsEvents = await CollectAsync(client.HeliosHttps.StreamAsync(Token));
        var dnsEvent = Assert.Single(await CollectAsync(client.HeliosDns.StreamAsync(Token)));
        var adbEvent = Assert.Single(await CollectAsync(client.HeliosAdb.StreamAsync(Token)));

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759000000123), httpEvent.Timestamp);
        Assert.Equal("GET /"u8.ToArray(), httpEvent.Raw);
        Assert.Equal("example.com", httpEvent.Details!.Headers!["Host"]);
        Assert.Equal("203.0.113.1", httpEvent.Meta.ProxyIP);
        Assert.Equal(17257744507466107949UL, httpsEvents[0].TunnelId);
        Assert.Null(httpsEvents[0].Details);
        Assert.Null(httpsEvents[0].Raw);
        Assert.Equal("example.com", httpsEvents[1].Details!.Sni);
        Assert.Equal(4865, httpsEvents[1].Details!.CipherSuites![0].Code);
        Assert.Equal(53, dnsEvent.Port);
        Assert.Equal("id", adbEvent.Command);
    }

    [Theory]
    [InlineData("""{"timestamp":1,"tunnel_id":7,"domain":"d","port":80,"protocol":"http","meta":{},"details":{"headers":{"Host":null}}}""")]
    [InlineData("""{"tunnel_id":7,"domain":"d","port":80,"protocol":"http","meta":{}}""")]
    public async Task RejectsInvalidHeliosEvents(string line)
    {
        using var client = Fixtures.StubClient(new StubHandler(Ndjson(line)));

        await Assert.ThrowsAsync<SynthientException>(() => CollectAsync(client.HeliosHttp.StreamAsync(Token)));
    }

    [Fact]
    public async Task FailedConnectsThrowApiException()
    {
        var handler = new StubHandler(_ => Fixtures.Json(429, """{"type":"t","title":"Too Many Requests","status":429,"detail":"too many streams"}"""));
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => CollectAsync(client.Torrents.StreamAsync(Token)));

        Assert.Equal(429, error.StatusCode);
        Assert.Equal("too many streams", error.Detail);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task RejectsNonOkSuccessStatuses()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(204, "")));

        var error = await Assert.ThrowsAsync<ApiException>(() => CollectAsync(client.Proxies.StreamAsync(Token)));

        Assert.Equal(204, error.StatusCode);
    }

    [Fact]
    public async Task RetriesTheConnect()
    {
        var handler = new StubHandler(Fixtures.Json(503, ""), Ndjson("{\"ip\":\"1.1.1.1\"}\n"));
        using var client = Fixtures.StubClient(handler);

        var events = await CollectAsync(client.Proxies.StreamAsync(Token));

        Assert.Single(events);
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task EachEnumerationConnects()
    {
        var handler = new StubHandler(_ => Ndjson("{\"ip\":\"1.1.1.1\"}\n"));
        using var client = Fixtures.StubClient(handler);
        var stream = client.Proxies.StreamAsync(Token);

        await CollectAsync(stream);
        await CollectAsync(stream);

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task DeliversEventsBeforeTheBodyEnds()
    {
        var aborted = new TaskCompletionSource();
        await using var server = await TestServer.StartAsync(async context =>
        {
            context.RequestAborted.Register(() => aborted.TrySetResult());
            await context.Response.WriteAsync("{\"ip\":\"1.1.1.1\"}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        using var client = server.Client();

        await foreach (var proxy in client.Proxies.StreamAsync(Token))
        {
            Assert.Equal("1.1.1.1", proxy.IP);
            break;
        }

        await aborted.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task CancellationThrowsOperationCanceled()
    {
        await using var server = await TestServer.StartAsync(async context =>
        {
            await context.Response.WriteAsync("{\"ip\":\"1.1.1.1\"}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        using var client = server.Client();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.Proxies.StreamAsync(cancel.Token))
            {
                cancel.CancelAfter(50);
            }
        });
    }

    [Fact]
    public async Task CancellationStopsBufferedEvents()
    {
        var handler = new StubHandler(Ndjson("{\"ip\":\"1.1.1.1\"}\n{\"ip\":\"2.2.2.2\"}\n"));
        using var client = Fixtures.StubClient(handler);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await using var events = client.Proxies.StreamAsync().GetAsyncEnumerator(cancel.Token);

        Assert.True(await events.MoveNextAsync());
        Assert.Equal("1.1.1.1", events.Current.IP);
        cancel.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => events.MoveNextAsync().AsTask());

        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task StalledStreamsTimeOut()
    {
        await using var server = await TestServer.StartAsync(async context =>
        {
            await context.Response.WriteAsync("{\"ip\":\"1.1.1.1\"}\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        var responses = 0;
        using var client = server.Client(o =>
        {
            o.Timeout = TimeSpan.FromSeconds(1);
            o.ResponseHook = _ => Interlocked.Increment(ref responses);
        });
        var events = 0;

        await Assert.ThrowsAsync<ApiTimeoutException>(async () =>
        {
            await foreach (var _ in client.Proxies.StreamAsync(Token))
            {
                events++;
            }
        });

        Assert.Equal(1, events);
        Assert.Equal(1, responses);
    }

    [Fact]
    public async Task SlowConsumersDoNotTimeOut()
    {
        await using var server = await TestServer.StartAsync(context =>
            context.Response.WriteAsync("{\"ip\":\"1.1.1.1\"}\n{\"ip\":\"2.2.2.2\"}\n{\"ip\":\"3.3.3.3\"}\n", context.RequestAborted));
        using var client = server.Client(o => o.Timeout = TimeSpan.FromMilliseconds(200));
        var events = 0;

        await foreach (var _ in client.Proxies.StreamAsync(Token))
        {
            events++;
            await Task.Delay(400, Token);
        }

        Assert.Equal(3, events);
    }
}

// Returns at most chunk bytes per read, so tests can split a body at every boundary.
internal sealed class ChunkedStream(byte[] data, int chunk) : MemoryStream(data)
{
    public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, chunk));

    public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, chunk)]);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        base.ReadAsync(buffer, offset, Math.Min(count, chunk), cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        base.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], cancellationToken);
}
