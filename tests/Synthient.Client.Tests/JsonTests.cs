using System.Net;
using System.Text;

namespace Synthient.Tests;

public sealed class JsonTests
{
    private static T Decode<T>(string json) => Json.Deserialize(Encoding.UTF8.GetBytes(json), Json.TypeInfo<T>());

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1759000000", 1759000000000L)]
    [InlineData("253402300799", 253402300799000L)]
    [InlineData("1000000000000", 1000000000000L)]
    [InlineData("1759000000123", 1759000000123L)]
    public void ReadsUnixSecondsAndMilliseconds(string timestamp, long milliseconds)
    {
        var proxy = Decode<ProxyEvent>($$"""{"timestamp":{{timestamp}}}""");

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds), proxy.Timestamp);
        Assert.Equal(TimeSpan.Zero, proxy.Timestamp!.Value.Offset);
    }

    [Theory]
    [InlineData("""{"timestamp":"yesterday"}""")]
    [InlineData("""{"timestamp":99999999999999999}""")]
    [InlineData("""{"timestamp":-99999999999999}""")]
    [InlineData("""{"timestamp":1.5}""")]
    [InlineData("""{"timestamp":true}""")]
    public void RejectsInvalidTimestamps(string json)
    {
        Assert.Throws<SynthientException>(() => Decode<ProxyEvent>(json));
    }

    [Fact]
    public void ReadsDurations()
    {
        var quota = Decode<LookupQuota>("""{"credits":1,"resets_in":90}""");

        Assert.Equal(TimeSpan.FromSeconds(90), quota.ResetsIn);
        Assert.Throws<SynthientException>(() => Decode<LookupQuota>("""{"credits":1,"resets_in":9223372036854775807}"""));
        Assert.Throws<SynthientException>(() => Decode<LookupQuota>("""{"credits":1,"resets_in":"90"}"""));
    }

    [Fact]
    public void KeepsUnknownValues()
    {
        var proxy = Decode<ProxyEvent>("""{"type":"QUANTUM_PROXY","asn":4294967295}""");

        Assert.Equal("QUANTUM_PROXY", proxy.Type);
        Assert.Equal(4294967295, proxy.Asn);
    }

    [Fact]
    public void ReadsExportIds()
    {
        var export = Decode<ExportMeta>(FeedTests.MetaJson);

        Assert.Equal(ExportId.Hourly(new DateOnly(2026, 5, 3), 7), export.Id);
        Assert.Throws<SynthientException>(() => Decode<ExportMeta>(FeedTests.MetaJson.Replace("\"2026-05-03/7\"", "7", StringComparison.Ordinal)));
    }

    [Fact]
    public void ReadsIPAddresses()
    {
        Assert.Equal(IPAddress.Parse("2001:db8::1"), Decode<IPInfo>("""{"ip":"2001:db8::1"}""").IP);
        Assert.Throws<SynthientException>(() => Decode<IPInfo>("""{"ip":17}"""));
    }

    [Fact]
    public void AcceptsOptionalNulls()
    {
        var page = Decode<ExportPage>("""{"stream":"proxies","feeds":[],"next_cursor":null}""");

        Assert.Null(page.NextCursor);
        Assert.Empty(page.Exports);
    }

    [Theory]
    [InlineData("""{"fields":[]}""")]
    [InlineData("""{"fields":null}""")]
    [InlineData("""{}""")]
    public void ReadsEmptySchemas(string schema)
    {
        var meta = Decode<ExportMeta>(FeedTests.MetaJson.Replace(
            """{"fields":[{"name":"ip","type":"string"},{"name":"asn","type":"uint32"}]}""", schema, StringComparison.Ordinal));

        Assert.Empty(meta.Schema.Fields);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("mmdb")]
    [InlineData("tar.gz")]
    [InlineData("zip")]
    public void ReadsExportFormats(string format)
    {
        var meta = Decode<ExportMeta>(FeedTests.MetaJson
            .Replace("\"format\":\"parquet\",\"size\"", $"\"format\":\"{format}\",\"size\"", StringComparison.Ordinal)
            .Replace("[{\"format\":\"parquet\"", $"[{{\"format\":\"{format}\"", StringComparison.Ordinal));

        Assert.Equal(format, meta.Format);
        Assert.Equal(format, Assert.Single(meta.Formats).Format);
        Assert.Equal(99, meta.Formats[0].SizeBytes);
    }

    [Theory]
    [InlineData("double")]
    [InlineData("int32")]
    [InlineData("date")]
    [InlineData("float")]
    [InlineData("timestamp")]
    [InlineData("list<string>")]
    public void ReadsDatasetColumnTypes(string type)
    {
        var schema = Decode<ParquetSchema>($$"""{"fields":[{"name":"c","type":"{{type}}"}]}""");

        Assert.Equal(type, Assert.Single(schema.Fields).Type);
    }

    [Fact]
    public void ReadsLiteExports()
    {
        var page = Decode<ExportPage>(FeedTests.Page(["latest"], null).Replace("\"kind\"", "\"variant\":\"lite\",\"kind\"", StringComparison.Ordinal));

        Assert.Equal(ExportVariant.Lite, page.Exports[0].Variant);
        Assert.Equal(ExportFormat.Parquet, Assert.Single(page.Exports[0].Formats).Format);
    }

    [Fact]
    public void ReadsUnknownWhoisEntities()
    {
        var entity = Decode<ExportEntity>("""{"entity":"routes","row_count":5000000000,"size_bytes":1,"checksum":"ab"}""");

        Assert.Equal("routes", entity.Entity);
        Assert.Equal(5_000_000_000, entity.RowCount);
        Assert.Null(entity.Schema);
    }

    [Fact]
    public void RequiresExportFormats()
    {
        const string export = """{"id":"latest","kind":"daily","date":"2026-05-03","size_bytes":1,"row_count":1,"checksum":"ab","created_at":1,"download_path":"/x"}""";
        Assert.Throws<SynthientException>(() => Decode<ExportPage>($$"""{"stream":"proxies","feeds":[{{export}}]}"""));
        Assert.Throws<SynthientException>(() => Decode<ExportMeta>(FeedTests.MetaJson.Replace(
            "\"formats\":[{\"format\":\"parquet\",\"size_bytes\":99,\"checksum\":\"ab\"}]", "\"formats\":null", StringComparison.Ordinal)));
    }
}
