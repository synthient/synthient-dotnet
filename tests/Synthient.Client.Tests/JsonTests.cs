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
}
