namespace Synthient.Tests;

public sealed class FeedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> ValidIds => new()
    {
        { "latest", "latest" },
        { "2026-05-03", "2026-05-03" },
        { "2026-05-03/07", "2026-05-03/7" },
        { "2026-05-03/7", "2026-05-03/7" },
        { "2026-05-03/00", "2026-05-03/0" },
        { "2026-05-03/23", "2026-05-03/23" },
    };

    [Theory]
    [MemberData(nameof(ValidIds))]
    public async Task NormalizesExportIds(string id, string path)
    {
        var handler = new StubHandler(_ => Fixtures.Json(200, MetaJson));
        using var client = Fixtures.StubClient(handler);

        await client.Proxies.GetExportMetaAsync(new ExportId(id), Token);

        Assert.Equal($"/api/v4/feeds/proxies/export/{path}/meta", Assert.Single(handler.Requests).Path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Latest")]
    [InlineData("latest/3")]
    [InlineData("2026-5-3")]
    [InlineData("2026-02-30")]
    [InlineData("2026-05-03/24")]
    [InlineData("2026-05-03/")]
    [InlineData("2026-05-03/007")]
    [InlineData("2026-05-03/+1")]
    [InlineData("2026-05-03/07/meta")]
    [InlineData("../account/me")]
    public async Task InvalidExportIdsSendNothing(string id)
    {
        var handler = new StubHandler();
        using var client = Fixtures.StubClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Proxies.GetExportMetaAsync(new ExportId(id), Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Proxies.GetDownloadUrlAsync(new ExportId(id), Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Proxies.GetExportMetaAsync(default, Token));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void BuildsExportIds()
    {
        Assert.Equal("2026-05-04", ExportId.Daily(new DateOnly(2026, 5, 4)).ToString());
        Assert.Equal("2026-05-04/3", ExportId.Hourly(new DateOnly(2026, 5, 4), 3).ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => ExportId.Hourly(new DateOnly(2026, 5, 4), 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExportId.Hourly(new DateOnly(2026, 5, 4), -1));
        Assert.Equal(new ExportId("latest"), ExportId.Latest);
        Assert.True(ExportId.Latest == new ExportId("latest"));
        Assert.NotEqual(new ExportId("2026-05-04/03"), ExportId.Hourly(new DateOnly(2026, 5, 4), 3));
        Assert.Equal("", default(ExportId).ToString());
    }

    public static TheoryData<string> Feeds => ["proxies", "anonymizers", "torrents", "ja4t", "helio/http", "helio/https", "helio/dns", "helio/adb"];

    [Theory]
    [MemberData(nameof(Feeds))]
    public async Task RoutesEveryFeed(string feed)
    {
        var handler = new StubHandler(_ => Fixtures.Json(200, MetaJson));
        using var client = Fixtures.StubClient(handler);

        await FeedOf(client, feed)(ExportId.Hourly(new DateOnly(2026, 5, 3), 7));

        Assert.Equal($"/api/v4/feeds/{feed}/export/2026-05-03/7/meta", Assert.Single(handler.Requests).Path);
    }

    [Fact]
    public async Task ListsExports()
    {
        var handler = new StubHandler(Fixtures.Json(200, Page(["latest", "2026-05-03/6"], "next one")));
        using var client = Fixtures.StubClient(handler);

        var page = await client.Proxies.ListExportsAsync(2, "c/1+2", Token);

        Assert.Equal("/api/v4/feeds/proxies/export?cursor=c%2F1%2B2&limit=2", Assert.Single(handler.Requests).PathAndQuery);
        Assert.Equal("proxies", page.Stream);
        Assert.Equal("next one", page.NextCursor);
        Assert.Equal(ExportId.Latest, page.Exports[0].Id);
        Assert.Equal(new DateOnly(2026, 5, 3), page.Exports[1].Date);
        Assert.Equal(6, page.Exports[1].Hour);
        Assert.Equal(ExportKind.Hourly, page.Exports[1].Kind);
    }

    [Fact]
    public async Task ListsAllExportsAcrossPages()
    {
        var handler = new StubHandler(
            Fixtures.Json(200, Page(["2026-05-03/2", "2026-05-03/1"], "c1")),
            Fixtures.Json(200, Page([], "c2")),
            Fixtures.Json(200, Page(["2026-05-03/0"], "")));
        using var client = Fixtures.StubClient(handler);

        var ids = new List<string>();
        await foreach (var export in client.Proxies.ListAllExportsAsync(2, cancellationToken: Token))
        {
            ids.Add(export.Id.ToString());
        }

        Assert.Equal(["2026-05-03/2", "2026-05-03/1", "2026-05-03/0"], ids);
        Assert.Equal(
            ["/api/v4/feeds/proxies/export?limit=2", "/api/v4/feeds/proxies/export?cursor=c1&limit=2", "/api/v4/feeds/proxies/export?cursor=c2&limit=2"],
            handler.Requests.Select(r => r.PathAndQuery));
    }

    [Theory]
    [InlineData(null, "c1", 2)]
    [InlineData("c0", "c0", 1)]
    public async Task RepeatedCursorsFail(string? start, string repeated, int items)
    {
        var handler = new StubHandler(_ => Fixtures.Json(200, Page(["2026-05-03/2"], repeated)));
        using var client = Fixtures.StubClient(handler);
        var seen = 0;

        await Assert.ThrowsAsync<SynthientException>(async () =>
        {
            await foreach (var _ in client.Proxies.ListAllExportsAsync(cursor: start, cancellationToken: Token))
            {
                seen++;
            }
        });

        Assert.Equal(items, seen);
    }

    [Fact]
    public async Task LaterPageFailuresSurface()
    {
        var handler = new StubHandler(
            Fixtures.Json(200, Page(["2026-05-03/2"], "c1")),
            Fixtures.Json(403, """{"type":"t","title":"Forbidden","status":403,"detail":"Requires one of: PROXY_FEEDS"}"""));
        using var client = Fixtures.StubClient(handler);
        var seen = new List<Export>();

        var error = await Assert.ThrowsAsync<ApiException>(async () =>
        {
            await foreach (var export in client.Proxies.ListAllExportsAsync(cancellationToken: Token))
            {
                seen.Add(export);
            }
        });

        Assert.Single(seen);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task StoppingEarlyFetchesNoMorePages()
    {
        var handler = new StubHandler(_ => Fixtures.Json(200, Page(["2026-05-03/2", "2026-05-03/1"], "c1")));
        using var client = Fixtures.StubClient(handler);

        await foreach (var _ in client.Proxies.ListAllExportsAsync(cancellationToken: Token))
        {
            break;
        }

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task InvalidLimitsFailBeforeEnumeration()
    {
        var handler = new StubHandler();
        using var client = Fixtures.StubClient(handler);

        Assert.Throws<ArgumentOutOfRangeException>(() => client.Proxies.ListAllExportsAsync(0, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Proxies.ListExportsAsync(0, cancellationToken: Token));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task DecodesExportMeta()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, MetaJson)));

        var meta = await client.Torrents.GetExportMetaAsync(ExportId.Latest, Token);

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1777766400), meta.Date);
        Assert.Equal(7, meta.Hour);
        Assert.Equal(ParquetType.String, meta.Schema.Fields[0].Type);
        Assert.Equal(1234, meta.Rows);
    }

    internal static Func<ExportId, Task> FeedOf(SynthientClient client, string feed) => feed switch
    {
        "proxies" => id => client.Proxies.GetExportMetaAsync(id, Token),
        "anonymizers" => id => client.Anonymizers.GetExportMetaAsync(id, Token),
        "torrents" => id => client.Torrents.GetExportMetaAsync(id, Token),
        "ja4t" => id => client.Ja4t.GetExportMetaAsync(id, Token),
        "helio/http" => id => client.HeliosHttp.GetExportMetaAsync(id, Token),
        "helio/https" => id => client.HeliosHttps.GetExportMetaAsync(id, Token),
        "helio/dns" => id => client.HeliosDns.GetExportMetaAsync(id, Token),
        "helio/adb" => id => client.HeliosAdb.GetExportMetaAsync(id, Token),
        _ => throw new ArgumentException(feed),
    };

    internal static string Page(string[] ids, string? next)
    {
        var exports = ids.Select(id => $$"""
            {"kind":"hourly","date":"2026-05-03","hour":6,"size_bytes":10,"row_count":2,"checksum":"ab",
             "id":"{{id}}","created_at":1777788000,"download_path":"/api/v4/feeds/proxies/export/{{id}}"}
            """);
        var cursor = next is null ? "" : $",\"next_cursor\":\"{next}\"";
        return $"{{\"stream\":\"proxies\",\"feeds\":[{string.Join(",", exports)}]{cursor}}}";
    }

    internal const string MetaJson = """
        {"checksum":"ab","date":1777766400,"format":"parquet","size":99,"rows":1234,"hour":7,
         "schema":{"fields":[{"name":"ip","type":"string"},{"name":"asn","type":"uint32"}]},
         "stream":"torrents","kind":"hourly","id":"2026-05-03/7","created_at":1777770000}
        """;
}
