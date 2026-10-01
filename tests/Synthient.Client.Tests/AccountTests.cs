namespace Synthient.Tests;

public sealed class AccountTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DecodesTheAccount()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, Fixtures.Account)));

        var account = await client.GetAccountAsync(Token);

        Assert.Equal("ada@example.com", account.Email);
        Assert.Equal("Ada", account.FirstName);
        Assert.Equal("Lovelace", account.LastName);
        Assert.Equal(Relation.Owner, account.Organization.Relation);
        Assert.Equal([Scope.Basic, Scope.ProxyFeeds], account.Scopes);
        Assert.Equal(9_000_000_000, account.LookupQuota.Credits);
        Assert.Equal(TimeSpan.FromHours(1), account.LookupQuota.ResetsIn);
        Assert.Equal("sdk", account.ApiKey!.Name);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 19, 4, 52, TimeSpan.Zero).AddTicks(4638690), account.ApiKey.CreatedAt);
    }

    [Fact]
    public async Task ApiKeyIsOptional()
    {
        var json = Fixtures.Account[..Fixtures.Account.IndexOf(",\n  \"api_key\"", StringComparison.Ordinal)] + "}";
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, json)));

        var account = await client.GetAccountAsync(Token);

        Assert.Null(account.ApiKey);
    }

    [Fact]
    public async Task HealthSucceeds()
    {
        var handler = new StubHandler(Fixtures.Json(200, """{"status":"ok"}"""));
        using var client = Fixtures.StubClient(handler);

        await client.CheckHealthAsync(Token);

        Assert.Equal("/api/v4/health", Assert.Single(handler.Requests).Path);
    }

    [Theory]
    [InlineData(503, """{"status":"unhealthy","problems":["database"]}""", "application/json")]
    [InlineData(503, "<html>Service Unavailable</html>", "text/html")]
    [InlineData(401, "401 invalid api key", "text/plain")]
    public async Task HealthFailuresAreNotRetried(int status, string body, string contentType)
    {
        var handler = new StubHandler(_ => Fixtures.Text(status, body, contentType));
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => client.CheckHealthAsync(Token));

        Assert.Equal(status, error.StatusCode);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task HealthRequiresAJsonBody()
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Text(200, "", "")));

        await Assert.ThrowsAsync<SynthientException>(() => client.CheckHealthAsync(Token));
    }
}
