using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class LookupTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DecodesAnIPLookup()
    {
        var handler = new StubHandler(Fixtures.Json(200, Fixtures.IPInfo));
        using var client = Fixtures.StubClient(handler);

        var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"), Token);

        Assert.Equal(IPAddress.Parse("8.8.8.8"), info.IP);
        Assert.Equal(15169, info.Network!.Asn);
        Assert.Equal(NetworkType.Datacenter, info.Network.Type);
        Assert.Null(info.Network.AbuseEmail);
        Assert.Equal("Mountain View", info.Location!.City);
        Assert.Equal(-122.0775, info.Location.Longitude);
        Assert.Equal(12, info.Intelligence!.RiskScore);
        Assert.Equal([ProxyType.DatacenterProxy], info.Intelligence.Categories);
        Assert.Empty(info.Intelligence.Behavior);
        var provider = Assert.Single(info.Intelligence.Providers);
        Assert.Equal("BRIGHTDATA", provider.Name);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1759000000), provider.LastSeen);
        Assert.Equal(DeviceOS.Linux, Assert.Single(info.Intelligence.Devices).OS);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/v4/lookup/ip/8.8.8.8", request.Path);
    }

    [Fact]
    public async Task AbsentSectionsAreNull()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, """{"ip":"1.1.1.1","network":null}""")));

        var info = await client.LookupIPAsync(IPAddress.Parse("1.1.1.1"), Token);

        Assert.Null(info.Network);
        Assert.Null(info.Location);
        Assert.Null(info.Intelligence);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"ip":null}""")]
    [InlineData("""{"ip":"not-an-ip"}""")]
    [InlineData("""{"ip":"1.1.1.1","network":{"isp":"x","type":"y"}}""")]
    [InlineData("""{"ip":"1.1.1.1","network":{"asn":1,"isp":null,"type":"y"}}""")]
    [InlineData("""{"ip":"1.1.1.1","network":{"asn":"1","isp":"x","type":"y"}}""")]
    [InlineData("""{"ip":"1.1.1.1","network":{"asn":1.5,"isp":"x","type":"y"}}""")]
    [InlineData("""{"ip":"1.1.1.1","intelligence":{"risk_score":1,"categories":[null],"behavior":[],"providers":[],"devices":[]}}""")]
    [InlineData("""{"ip":"1.1.1.1","intelligence":{"risk_score":1,"categories":[],"behavior":[],"providers":[null],"devices":[]}}""")]
    [InlineData("""{"ip":"1.1.1.1","intelligence":{"risk_score":1,"categories":[],"behavior":[],"providers":[{"provider":"p","type":"t","last_seen":99999999999999999}],"devices":[]}}""")]
    [InlineData("""{"ip":"1.1.1.1","intelligence":{"risk_score":1,"categories":[],"behavior":[],"providers":[{"provider":"p","type":"t","last_seen":1e30}],"devices":[]}}""")]
    [InlineData("""{"ip":"1.1.1.1"} {"ip":"2.2.2.2"}""")]
    [InlineData("""{"ip":"1.1.1.1" """)]
    [InlineData("null")]
    public async Task RejectsInvalidResponses(string json)
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, json)));

        await Assert.ThrowsAsync<SynthientException>(() => client.LookupIPAsync(IPAddress.Parse("1.1.1.1"), Token));
    }

    [Fact]
    public async Task SendsIPv6AddressesUnescaped()
    {
        await using var server = await TestServer.StartAsync(context => context.Response.WriteAsync("""{"ip":"2001:db8::1"}""", context.RequestAborted));
        using var client = server.Client();

        var info = await client.LookupIPAsync(IPAddress.Parse("2001:0db8:0000::1"), Token);

        Assert.Equal(IPAddress.Parse("2001:db8::1"), info.IP);
        Assert.Equal("/api/v4/lookup/ip/2001:db8::1", Assert.Single(server.Requests).RawTarget);
    }

    [Fact]
    public async Task RejectsScopedAddressesWithoutSending()
    {
        var handler = new StubHandler();
        using var client = Fixtures.StubClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.LookupIPAsync(IPAddress.Parse("fe80::1%3"), Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.LookupIPsAsync([IPAddress.Parse("fe80::1%3")], Token));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task LooksUpABatch()
    {
        var handler = new StubHandler(Fixtures.Json(200, $$"""{"results":[{{Fixtures.IPInfo}},{"ip":"2001:db8::1"}]}"""));
        using var client = Fixtures.StubClient(handler);

        var results = await client.LookupIPsAsync([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:db8::1")], Token);

        Assert.Equal([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("2001:db8::1")], results.Select(r => r.IP));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v4/lookup/ips", request.Path);
        Assert.Equal("application/json", request.Header("Content-Type"));
        Assert.Equal("""{"ips":["8.8.8.8","2001:db8::1"]}""", Encoding.UTF8.GetString(request.Body));
    }

    [Fact]
    public async Task InvalidBatchesSendNothing()
    {
        var handler = new StubHandler();
        using var client = Fixtures.StubClient(handler);

        Assert.Empty(await client.LookupIPsAsync([], Token));
        var tooMany = Enumerable.Range(0, 1001).Select(i => new IPAddress(i));
        await Assert.ThrowsAsync<ArgumentException>(() => client.LookupIPsAsync(tooMany, Token));
        await Assert.ThrowsAsync<ArgumentException>(() => client.LookupIPsAsync([IPAddress.Loopback, null!], Token));
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task BatchRequiresResults()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, "{}")));

        await Assert.ThrowsAsync<SynthientException>(() => client.LookupIPsAsync([IPAddress.Loopback], Token));
    }

    [Theory]
    [InlineData("example.com", null, "/api/v4/lookup/domain/example.com")]
    [InlineData("example.com", 5, "/api/v4/lookup/domain/example.com?limit=5")]
    [InlineData("a/b?c#d", null, "/api/v4/lookup/domain/a%2Fb%3Fc%23d")]
    [InlineData("../account/me", null, "/api/v4/lookup/domain/..%2Faccount%2Fme")]
    [InlineData("100%", null, "/api/v4/lookup/domain/100%25")]
    [InlineData("a b+c", null, "/api/v4/lookup/domain/a%20b%2Bc")]
    [InlineData("bücher.de", null, "/api/v4/lookup/domain/b%C3%BCcher.de")]
    [InlineData("...", null, "/api/v4/lookup/domain/...")]
    public async Task EscapesDomains(string domain, int? limit, string target)
    {
        await using var server = await TestServer.StartAsync(context => context.Response.WriteAsync(DomainJson, context.RequestAborted));
        using var client = server.Client();

        var info = await client.LookupDomainAsync(domain, limit, Token);

        Assert.Equal("example.com", info.Domain);
        Assert.Equal(target, Assert.Single(server.Requests).RawTarget);
    }

    [Fact]
    public async Task DecodesADomainLookup()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, DomainJson)));

        var info = await client.LookupDomainAsync("example.com", cancellationToken: Token);

        Assert.Equal(DomainStatus.Active, info.Status);
        Assert.Equal(7, info.HourDowHeatmap.Count);
        Assert.Equal(3, info.HourDowHeatmap[0][2]);
        Assert.Equal(64496, info.TopAsn!.Asn);
        Assert.Equal(Cadence.BurstDriven, info.ActivityStats.Cadence);
        Assert.Equal("203.0.113.9", Assert.Single(info.RecentEvents).SourceIP);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1759017600), Assert.Single(info.TimeSeries).Date);
    }

    [Fact]
    public async Task InvalidDomainsSendNothing()
    {
        var handler = new StubHandler();
        using var client = Fixtures.StubClient(handler);

        foreach (var domain in new[] { "", ".", ".." })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => client.LookupDomainAsync(domain, cancellationToken: Token));
        }
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.LookupDomainAsync("example.com", 0, Token));
        Assert.Equal(0, handler.Count);
    }

    internal static readonly string DomainJson = $$"""
        {
          "domain": "example.com", "status": "active",
          "stats": {"total_events_30d": 40, "events_24h": 4},
          "time_series": [{"date": 1759017600, "events": 4, "unique_ips": 2}],
          "unique_ips": {"value_24h": 2, "value_30d": 9, "sparkline_24h": [0, 1, 1]},
          "top_asn": {"asn": 64496, "events": 30},
          "top_subdomains": [{"subdomain": "www", "count": 20}],
          "top_ports": [{"port": 443, "count": 38}],
          "geo_distribution": [{"country_code": "US", "unique_ips": 3, "events": 21}],
          "hour_dow_heatmap": [{{string.Join(",", Enumerable.Repeat("[0,0,3]", 7))}}],
          "activity_stats": {"peak_hour": 14, "quiet_hour": 3, "median_per_hour": 1, "p95_per_hour": 5,
                             "cadence": "Activity is burst-driven with significant peaks."},
          "recent_events": [{"timestamp": 1759020000, "source_ip_masked": "203.0.113.9", "target_subdomain": "www",
                             "port": 443, "country_code": "US"}]
        }
        """;
}
