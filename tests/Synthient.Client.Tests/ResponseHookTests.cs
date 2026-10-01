using System.Collections.Concurrent;
using System.Net;

namespace Synthient.Tests;

public sealed class ResponseHookTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeesEveryApiResponse()
    {
        var seen = new ConcurrentQueue<ResponseMetadata>();
        var handler = new StubHandler(request => request.Uri.Host == "storage.test"
            ? Fixtures.Text(200, "PAR1", "application/octet-stream")
            : request.Path switch
            {
                "/api/v4/account/me" when seen.IsEmpty => Fixtures.Json(429, "", ("Retry-After", "0"), ("X-Request-Id", "r1")),
                "/api/v4/account/me" => Fixtures.Json(200, Fixtures.Account, ("X-Request-Id", "r2")),
                "/api/v4/feeds/proxies/export" => Fixtures.Json(200, FeedTests.Page(["2026-05-03/1"], request.Uri.Query.Length > 0 ? null : "c1")),
                "/api/v4/feeds/proxies/stream" => Fixtures.Text(200, "{}\n", "application/x-ndjson"),
                "/api/v4/feeds/proxies/export/latest" => Fixtures.Redirect(307, "https://storage.test/file"),
                _ => Fixtures.Json(200, "{bad json"),
            });
        using var client = Fixtures.StubClient(handler, o => o.ResponseHook = seen.Enqueue);

        await client.GetAccountAsync(Token);
        await foreach (var _ in client.Proxies.ListAllExportsAsync(cancellationToken: Token))
        {
        }
        await foreach (var _ in client.Proxies.StreamAsync(Token))
        {
        }
        await using (await client.Proxies.DownloadAsync(ExportId.Latest, Token))
        {
        }
        await Assert.ThrowsAsync<SynthientException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));

        Assert.Equal([429, 200, 200, 200, 200, 307, 200], seen.Select(m => m.StatusCode));
        Assert.Equal(["r1", "r2"], seen.Take(2).Select(m => m.RequestId));
        Assert.Equal(8, handler.Count);
    }

    [Fact]
    public async Task AThrowingHookFailsTheCall()
    {
        var content = new TrackingContent();
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        using var client = Fixtures.StubClient(handler, o => o.ResponseHook = _ => throw new InvalidOperationException("hook"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAccountAsync(Token));

        Assert.Equal("hook", error.Message);
        Assert.True(content.Disposed);
    }

    [Fact]
    public async Task KeepsConcurrentCallsApart()
    {
        var seen = new ConcurrentQueue<string?>();
        var handler = new StubHandler(request =>
            Fixtures.Json(200, $$"""{"ip":"{{request.Path[(request.Path.LastIndexOf('/') + 1)..]}}"}""", ("X-Request-Id", request.Path)));
        using var client = Fixtures.StubClient(handler, o => o.ResponseHook = metadata => seen.Enqueue(metadata.RequestId));
        var addresses = Enumerable.Range(1, 20).Select(i => new IPAddress([10, 0, 0, (byte)i])).ToArray();

        var results = await Task.WhenAll(addresses.Select(address => client.LookupIPAsync(address, Token)));

        Assert.Equal(addresses, results.Select(r => r.IP));
        Assert.Equal(addresses.Select(a => "/api/v4/lookup/ip/" + a).Order(), seen.Order());
    }

    public static TheoryData<string?, string?, string?, RateLimit?> RateLimits => new()
    {
        { "200", "199", "1", new RateLimit(200, 199, TimeSpan.FromSeconds(1)) },
        { "10", "0", "-5", new RateLimit(10, 0, TimeSpan.Zero) },
        { "10", "0", "99999999999", new RateLimit(10, 0, TimeSpan.FromSeconds(1L << 31)) },
        { "10", null, "1", null },
        { "10", "x", "1", null },
        { null, null, null, null },
    };

    [Theory]
    [MemberData(nameof(RateLimits))]
    public void ParsesRateLimits(string? limit, string? remaining, string? reset, RateLimit? expected)
    {
        var headers = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var (name, value) in new[] { ("RateLimit-Limit", limit), ("RateLimit-Remaining", remaining), ("RateLimit-Reset", reset) })
        {
            if (value is not null)
            {
                headers[name] = [value];
            }
        }

        Assert.Equal(expected, new ResponseMetadata(200, headers).RateLimit);
    }

    [Fact]
    public void MergesHeaderNamesThatDifferOnlyInCase()
    {
        var headers = new Dictionary<string, IReadOnlyList<string>>
        {
            ["X-Request-Id"] = ["first"],
            ["x-request-id"] = ["second"],
        };

        var metadata = new ResponseMetadata(200, headers);

        Assert.Equal("second", metadata.RequestId);
        Assert.Single(metadata.Headers);
    }

    [Fact]
    public async Task SnapshotsTheHeaders()
    {
        ResponseMetadata? metadata = null;
        using var client = Fixtures.StubClient(
            new StubHandler(Fixtures.Json(200, Fixtures.Account, ("X-Request-Id", "abc"), ("RateLimit-Limit", "10"))),
            o => o.ResponseHook = m => metadata = m);

        await client.GetAccountAsync(Token);

        Assert.Equal("abc", metadata!.Headers["x-request-id"][0]);
        Assert.StartsWith("application/json", metadata.Headers["Content-Type"][0], StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, IReadOnlyList<string>>)metadata.Headers).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)metadata.Headers["X-Request-Id"])[0] = "changed");
        Assert.DoesNotContain("RateLimit", metadata.ToString(), StringComparison.Ordinal);
    }
}
