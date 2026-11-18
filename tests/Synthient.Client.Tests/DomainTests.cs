using System.Reflection;
using Microsoft.AspNetCore.Http;

#pragma warning disable CS0618 // These tests cover domain lookup, which stays until the next major version.

namespace Synthient.Tests;

public sealed class DomainTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void DomainLookupIsDeprecated()
    {
        var method = typeof(SynthientClient).GetMethod(nameof(SynthientClient.LookupDomainAsync))!;

        Assert.Equal(SynthientClient.DomainLookupRemoved, method.GetCustomAttribute<ObsoleteAttribute>()?.Message);
        Assert.NotNull(typeof(DomainInfo).GetCustomAttribute<ObsoleteAttribute>());
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
