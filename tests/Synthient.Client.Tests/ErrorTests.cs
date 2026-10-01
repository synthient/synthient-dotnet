using System.Net;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class ErrorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ApiException> FailAsync(HttpResponseMessage response)
    {
        using var client = Fixtures.StubClient(new StubHandler(response));
        return await Assert.ThrowsAsync<ApiException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));
    }

    [Fact]
    public async Task ParsesProblemDetails()
    {
        var error = await FailAsync(Fixtures.Text(
            403,
            """{"type":"https://docs.synthient.com/errors/forbidden","title":"Forbidden","status":403,"detail":"Requires one of: BASIC","instance":"inst"}""",
            "application/problem+json",
            ("X-Request-Id", "0123456789abcdef0123456789abcdef")));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal("https://docs.synthient.com/errors/forbidden", error.Type);
        Assert.Equal("Forbidden", error.Title);
        Assert.Equal("Requires one of: BASIC", error.Detail);
        Assert.Equal("0123456789abcdef0123456789abcdef", error.RequestId);
        Assert.Empty(error.Errors);
        Assert.Null(error.RetryAfter);
        Assert.Equal("application/problem+json", error.Headers["content-type"][0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task FallsBackToTheProblemInstance(string? header)
    {
        var headers = header is null ? [] : new[] { ("X-Request-Id", header) };
        var error = await FailAsync(Fixtures.Json(500, """{"type":"t","title":"x","status":500,"detail":"d","instance":"inst"}""", headers));

        Assert.Equal("inst", error.RequestId);
    }

    [Fact]
    public async Task ParsesValidationErrors()
    {
        var error = await FailAsync(Fixtures.Json(422, """
            {"type":"https://docs.synthient.com/errors/validation-failed","title":"One or more validation errors occurred.",
             "status":422,"errors":{"ip_address":["Invalid IP address format.","Another."]}}
            """));

        Assert.Null(error.Detail);
        Assert.Equal(["Invalid IP address format.", "Another."], error.Errors["ip_address"]);
    }

    [Theory]
    [InlineData("""{"title":"x","errors":["cursor"]}""")]
    [InlineData("""{"title":"x","errors":{"cursor":"bad"}}""")]
    [InlineData("""{"title":"x","errors":{"cursor":[1]}}""")]
    public async Task IgnoresMalformedValidationErrors(string body)
    {
        var error = await FailAsync(Fixtures.Json(400, body));

        Assert.Equal("x", error.Title);
        Assert.Empty(error.Errors);
    }

    [Fact]
    public async Task IgnoresMistypedFields()
    {
        var error = await FailAsync(Fixtures.Json(400, """{"title":5,"detail":["x"],"type":"t"}"""));

        Assert.Null(error.Title);
        Assert.Null(error.Detail);
        Assert.Equal("t", error.Type);
    }

    [Theory]
    [InlineData("401 invalid api key", "text/plain", "401 invalid api key")]
    [InlineData("404 page not found\n", "text/plain; charset=utf-8", "404 page not found")]
    [InlineData("404", "text/plain", "404")]
    [InlineData("\"x\"", "text/plain", "\"x\"")]
    [InlineData("<html>bad gateway</html>", "text/html", null)]
    [InlineData("not json", "application/json", null)]
    [InlineData("", "text/plain", null)]
    public async Task UsesPlainTextBodiesAsTheDetail(string body, string contentType, string? detail)
    {
        var error = await FailAsync(Fixtures.Text(401, body, contentType));

        Assert.Equal(detail, error.Detail);
        Assert.Null(error.Title);
    }

    [Fact]
    public async Task KeepsRetryAfterWithoutABody()
    {
        var error = await FailAsync(Fixtures.Text(429, "", "", ("Retry-After", "120")));

        Assert.Equal(TimeSpan.FromSeconds(120), error.RetryAfter);
    }

    [Fact]
    public async Task IgnoresOversizedErrorBodies()
    {
        var body = """{"title":"x","detail":" """ + new string('a', 70_000) + "\"}";

        var error = await FailAsync(Fixtures.Json(400, body));

        Assert.Null(error.Title);
        Assert.Null(error.Detail);
    }

    [Fact]
    public async Task KeepsTheStatusWhenTheBodyFails()
    {
        await using var server = await TestServer.StartAsync(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentLength = 1000;
            await context.Response.WriteAsync("""{"title":""", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(200, context.RequestAborted);
            context.Abort();
        });
        using var client = server.Client();

        var error = await Assert.ThrowsAsync<ApiException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));

        Assert.Equal(500, error.StatusCode);
        Assert.IsType<ApiConnectionException>(error.InnerException);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(403)]
    public async Task CorruptCompressedBodiesAreReadFailures(int status)
    {
        await using var server = await TestServer.StartAsync(async context =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.ContentEncoding = "gzip";
            context.Response.ContentType = "application/json";
            await context.Response.Body.WriteAsync("not gzip at all"u8.ToArray(), context.RequestAborted);
        });
        using var client = server.Client();

        var error = await Assert.ThrowsAnyAsync<SynthientException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));

        if (status == 200)
        {
            Assert.IsType<ApiConnectionException>(error);
        }
        else
        {
            Assert.Equal(403, Assert.IsType<ApiException>(error).StatusCode);
            Assert.IsType<ApiConnectionException>(error.InnerException);
        }
    }

    [Fact]
    public async Task MessagesContainOnlyTheStatus()
    {
        var handler = new StubHandler(Fixtures.Json(
            403,
            """{"type":"t","title":"secret-title","status":403,"detail":"secret-detail\nline","instance":"secret-instance"}""",
            ("X-Request-Id", "secret-request")));
        using var client = Fixtures.StubClient(handler, o => o.ApiKey = "secret-key");

        var error = await Assert.ThrowsAsync<ApiException>(() => client.LookupDomainAsync("secret-domain.example", cancellationToken: Token));

        Assert.Equal("403 Forbidden", error.Message);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.Ordinal);
        Assert.Equal("599", new ApiException(599).Message);
        Assert.Equal("422 Unprocessable Content", new ApiException(422) { Detail = "x" }.Message);
    }

    [Theory]
    [InlineData("""{"ip":"1.1.1.1"} trailing""")]
    [InlineData("""{"ip":""")]
    [InlineData("null")]
    [InlineData("")]
    public async Task RejectsInvalidSuccessBodies(string body)
    {
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, body)));

        var error = await Assert.ThrowsAsync<SynthientException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));

        Assert.IsNotType<ApiException>(error);
    }

    [Fact]
    public async Task RejectsOversizedSuccessBodies()
    {
        var body = """{"ip":"1.1.1.1","pad":" """ + new string('a', Transport.MaxResponseSize) + "\"}";
        using var client = Fixtures.StubClient(new StubHandler(Fixtures.Json(200, body)));

        var error = await Assert.ThrowsAsync<SynthientException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));

        Assert.Contains("exceeds", error.Message, StringComparison.Ordinal);
    }
}
