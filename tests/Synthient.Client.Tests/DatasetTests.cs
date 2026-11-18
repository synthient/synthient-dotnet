using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class DatasetTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HttpResponseMessage Respond(RecordedRequest request) => request.Path switch
    {
        var path when path.EndsWith("/meta", StringComparison.Ordinal) => Fixtures.Json(200, WhoisMetaJson),
        var path when path.EndsWith("/export/daily", StringComparison.Ordinal) => Fixtures.Json(200, FeedTests.VolumeJson),
        var path when path.EndsWith("/export", StringComparison.Ordinal) => Fixtures.Json(200, GeolocationPageJson),
        _ => Fixtures.Redirect(307, "https://storage.test/file.mmdb"),
    };

    [Fact]
    public async Task RoutesGeolocation()
    {
        var handler = new StubHandler(Respond);
        using var client = Fixtures.StubClient(handler);

        await client.Geolocation.ListExportsAsync(limit: 1, day: "2026-11-17", cancellationToken: Token);
        await client.Geolocation.GetExportVolumeAsync(30, Token);
        await client.Geolocation.GetExportMetaAsync(ExportId.Latest, cancellationToken: Token);
        await client.Geolocation.GetExportMetaAsync("2026-11-17", ExportFormat.Mmdb, Token);
        await client.Geolocation.GetDownloadUrlAsync("2026-11-17", ExportFormat.Csv, Token);
        await client.Geolocation.GetDownloadUrlAsync(ExportId.Latest, cancellationToken: Token);

        Assert.Equal(
            [
                "/api/v4/feeds/geolocation/export?limit=1&day=2026-11-17",
                "/api/v4/feeds/geolocation/export/daily?days=30",
                "/api/v4/feeds/geolocation/export/latest/meta",
                "/api/v4/feeds/geolocation/export/2026-11-17/meta?format=mmdb",
                "/api/v4/feeds/geolocation/export/2026-11-17?format=csv",
                "/api/v4/feeds/geolocation/export/latest",
            ],
            handler.Requests.Select(r => r.PathAndQuery));
    }

    [Fact]
    public async Task RoutesWhois()
    {
        var handler = new StubHandler(Respond);
        using var client = Fixtures.StubClient(handler);

        await client.Whois.ListExportsAsync(cancellationToken: Token);
        await client.Whois.GetExportVolumeAsync(cancellationToken: Token);
        await client.Whois.GetExportMetaAsync(ExportId.Daily(new DateOnly(2026, 11, 17)), cancellationToken: Token);
        await client.Whois.GetDownloadUrlAsync(ExportId.Latest, ExportFormat.TarGz, Token);

        Assert.Equal(
            [
                "/api/v4/feeds/whois/export",
                "/api/v4/feeds/whois/export/daily",
                "/api/v4/feeds/whois/export/2026-11-17/meta",
                "/api/v4/feeds/whois/export/latest?format=tar.gz",
            ],
            handler.Requests.Select(r => r.PathAndQuery));
    }

    [Fact]
    public async Task ListsAllExportsWithTheDayOnEveryPage()
    {
        var handler = new StubHandler(
            Fixtures.Json(200, FeedTests.Page(["2026-11-17"], "c1")),
            Fixtures.Json(200, FeedTests.Page([], null)));
        using var client = Fixtures.StubClient(handler);

        await foreach (var _ in client.Whois.ListAllExportsAsync(day: "2026-11-17", cancellationToken: Token))
        {
        }

        Assert.Equal(
            ["/api/v4/feeds/whois/export?day=2026-11-17", "/api/v4/feeds/whois/export?cursor=c1&day=2026-11-17"],
            handler.Requests.Select(r => r.PathAndQuery));
    }

    [Theory]
    [InlineData("2026-11-17/3")]
    [InlineData("2026-11-17/03")]
    [InlineData("2026-11-17/0")]
    [InlineData("daily")]
    [InlineData("")]
    public async Task RejectsHourlyAndInvalidIdsBeforeSending(string id)
    {
        var handler = new StubHandler();
        using var client = Fixtures.StubClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Geolocation.GetExportMetaAsync(id, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Geolocation.GetDownloadUrlAsync(id, ExportFormat.Mmdb, Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Whois.DownloadAsync(id, cancellationToken: Token));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task DownloadsWithoutSendingTheKeyToStorage()
    {
        await using var storage = await TestServer.StartAsync(context => context.Response.WriteAsync("MMDB data", context.RequestAborted), host: "localhost");
        await using var api = await TestServer.StartAsync(context =>
        {
            context.Response.StatusCode = 307;
            context.Response.Headers.Location = storage.Origin + "geolocation/2026-11-17.mmdb?X-Amz-Signature=secret";
            return Task.CompletedTask;
        });
        using var client = api.Client();

        await using var file = await client.Geolocation.DownloadAsync(ExportId.Latest, ExportFormat.Mmdb, Token);

        using var reader = new StreamReader(file);
        Assert.Equal("MMDB data", await reader.ReadToEndAsync(Token));
        var apiRequest = Assert.Single(api.Requests);
        Assert.Equal("/api/v4/feeds/geolocation/export/latest?format=mmdb", apiRequest.RawTarget);
        Assert.Equal(Fixtures.ApiKey, apiRequest.Headers["X-Api-Key"]);
        var storageRequest = Assert.Single(storage.Requests);
        Assert.Equal("/geolocation/2026-11-17.mmdb?X-Amz-Signature=secret", storageRequest.RawTarget);
        Assert.False(storageRequest.Headers.ContainsKey("X-Api-Key"));
    }

    [Fact]
    public async Task StorageErrorsCarryOnlyTheStatus()
    {
        var handler = new StubHandler(
            Fixtures.Redirect(307, "https://storage.test/file?sig=secret"),
            Fixtures.Text(403, "<Error>signature https://storage.test/file?sig=secret expired</Error>", "application/xml"));
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => client.Whois.DownloadAsync(ExportId.Latest, cancellationToken: Token));

        Assert.Equal(403, error.StatusCode);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecodesADatasetListing()
    {
        using var client = Fixtures.StubClient(new StubHandler(Respond));

        var export = Assert.Single((await client.Geolocation.ListExportsAsync(cancellationToken: Token)).Exports);

        Assert.Equal(ExportKind.Daily, export.Kind);
        Assert.Null(export.Hour);
        Assert.Null(export.Entities);
        Assert.Null(export.Variant);
        Assert.Equal([ExportFormat.Parquet, ExportFormat.Csv, ExportFormat.Mmdb], export.Formats.Select(f => f.Format));
        Assert.Equal(1_874_587_084, export.Formats[2].SizeBytes);
        Assert.Equal("ae71", export.Formats[2].Checksum);
    }

    [Fact]
    public async Task DecodesAWhoisMeta()
    {
        using var client = Fixtures.StubClient(new StubHandler(Respond));

        var meta = await client.Whois.GetExportMetaAsync(ExportId.Latest, cancellationToken: Token);

        Assert.Equal(ExportFormat.TarGz, meta.Format);
        Assert.Empty(meta.Schema.Fields);
        Assert.Equal(ExportFormat.TarGz, Assert.Single(meta.Formats).Format);
        Assert.Equal([WhoisEntity.Asns, WhoisEntity.Contacts], meta.Entities!.Select(e => e.Entity));
        Assert.Equal(6_459_660, meta.Entities![1].RowCount);
        Assert.Equal(["int64", "timestamp"], meta.Entities[0].Schema!.Fields.Select(f => f.Type));
        Assert.Equal("list<string>", meta.Entities[1].Schema!.Fields[0].Type);
    }

    internal const string GeolocationPageJson = """
        {"stream":"geolocation","feeds":[
          {"id":"2026-11-17","kind":"daily","date":"2026-11-17","size_bytes":466431938,"row_count":27456467,"checksum":"9712",
           "created_at":1794960045,"download_path":"/api/v4/feeds/geolocation/export/2026-11-17",
           "formats":[{"format":"parquet","size_bytes":466431938,"checksum":"9712"},
                      {"format":"csv","size_bytes":540196440,"checksum":"de74"},
                      {"format":"mmdb","size_bytes":1874587084,"checksum":"ae71"}]}]}
        """;

    internal const string WhoisMetaJson = """
        {"id":"latest","stream":"whois","kind":"daily","format":"tar.gz","date":1794873600,"rows":23481094,"size":997234948,
         "checksum":"add0","created_at":1794960929,"schema":{"fields":[]},
         "formats":[{"format":"tar.gz","size_bytes":997234948,"checksum":"add0"}],
         "entities":[
           {"entity":"asns","row_count":123778,"size_bytes":5442736,"checksum":"062f",
            "schema":{"fields":[{"name":"asn","type":"int64"},{"name":"updated","type":"timestamp"}]}},
           {"entity":"contacts","row_count":6459660,"size_bytes":205447373,"checksum":"cf61",
            "schema":{"fields":[{"name":"emails","type":"list<string>"}]}}]}
        """;
}
