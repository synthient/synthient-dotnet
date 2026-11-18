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

    [Theory]
    [InlineData("""{"ip":"1.1.1.1"}""")]
    [InlineData("""{"ip":"1.1.1.1","network":null,"location":null,"intelligence":null}""")]
    public async Task AbsentSectionsAreNull(string json)
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, json)));

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
}
