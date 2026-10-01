using System.Collections.Concurrent;
using System.Net;
using System.Security.Authentication;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class RetryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A status sequence, such as "429:1,200": each entry is a status with an optional Retry-After.
    private static StubHandler Sequence(string statuses)
    {
        var responses = statuses.Split(',').Select(entry =>
        {
            var parts = entry.Split(':', 2);
            var status = int.Parse(parts[0]);
            var body = status == 200 ? Fixtures.Account : "";
            return parts.Length == 2 ? Fixtures.Json(status, body, ("Retry-After", parts[1])) : Fixtures.Json(status, body);
        });
        return new StubHandler([.. responses]);
    }

    private static Task CallAsync(SynthientClient client, bool billed) => billed
        ? client.CheckHealthAsync(Token)
        : client.GetAccountAsync(Token);

    [Theory]
    [InlineData(false, "503,200", 2, true)]
    [InlineData(false, "500,502,200", 3, true)]
    [InlineData(false, "504,504,504", 3, false)]
    [InlineData(false, "429:1,200", 2, true)]
    [InlineData(false, "429,200", 1, false)]
    [InlineData(false, "429:soon,200", 1, false)]
    [InlineData(false, "400,200", 1, false)]
    [InlineData(false, "501,200", 1, false)]
    [InlineData(true, "503,200", 1, false)]
    [InlineData(true, "500,200", 1, false)]
    [InlineData(true, "429:1,200", 2, true)]
    [InlineData(true, "429:0,429:0,200", 3, true)]
    [InlineData(true, "429,200", 1, false)]
    public async Task FollowsThePolicy(bool billed, string statuses, int requests, bool succeeds)
    {
        var handler = Sequence(statuses);
        using var client = Fixtures.StubClient(handler);

        if (succeeds)
        {
            await CallAsync(client, billed);
        }
        else
        {
            await Assert.ThrowsAsync<ApiException>(() => CallAsync(client, billed));
        }
        Assert.Equal(requests, handler.Count);
    }

    [Fact]
    public async Task WaitsForRetryAfter()
    {
        var delays = new ConcurrentQueue<TimeSpan>();
        using var client = Fixtures.StubClient(Sequence("429:3,200"), delays: delays);

        await client.GetAccountAsync(Token);

        Assert.Equal(TimeSpan.FromSeconds(3), Assert.Single(delays));
    }

    [Fact]
    public async Task StopsWhenRetryAfterIsTooLong()
    {
        var handler = Sequence("429:120,200");
        using var client = Fixtures.StubClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => client.GetAccountAsync(Token));

        Assert.Equal(TimeSpan.FromSeconds(120), error.RetryAfter);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task RetriesCanBeDisabled()
    {
        var handler = Sequence("503,200");
        using var client = Fixtures.StubClient(handler, o => o.MaxRetries = 0);

        await Assert.ThrowsAsync<ApiException>(() => client.GetAccountAsync(Token));

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task BacksOffWithJitter()
    {
        var delays = new ConcurrentQueue<TimeSpan>();
        using var client = Fixtures.StubClient(Sequence("503,503,503,503,503,503,503,200"), o => o.MaxRetries = 7, delays);

        await client.GetAccountAsync(Token);

        var bases = new[] { 1, 2, 4, 8, 16, 30, 30 };
        Assert.Equal(bases.Length, delays.Count);
        foreach (var (delay, seconds) in delays.Zip(bases))
        {
            Assert.InRange(delay.TotalSeconds, seconds * 0.75, seconds * 1.25);
        }
    }

    public static TheoryData<Exception> ConnectionFailures => new()
    {
        new HttpRequestException("connection refused"),
        new TaskCanceledException("connect timed out", new TimeoutException()),
    };

    [Theory]
    [MemberData(nameof(ConnectionFailures))]
    public async Task RetriesConnectionFailuresOnlyForFreeCalls(Exception failure)
    {
        var handler = new StubHandler(_ => throw failure);
        using var client = Fixtures.StubClient(handler);

        await Assert.ThrowsAnyAsync<ApiConnectionException>(() => client.GetAccountAsync(Token));
        Assert.Equal(3, handler.Count);

        await Assert.ThrowsAnyAsync<ApiConnectionException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));
        Assert.Equal(4, handler.Count);
    }

    [Fact]
    public async Task ConnectTimeoutsAreTimeouts()
    {
        using var client = Fixtures.StubClient(new StubHandler(_ => throw new TaskCanceledException("timeout", new TimeoutException())));

        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.GetAccountAsync(Token));
    }

    [Fact]
    public async Task DoesNotRetryTlsFailures()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("ssl", new AuthenticationException("bad certificate")));
        using var client = Fixtures.StubClient(handler);

        await Assert.ThrowsAsync<ApiConnectionException>(() => client.GetAccountAsync(Token));

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task DoesNotRetryBodyFailures()
    {
        await using var server = await TestServer.StartAsync(async context =>
        {
            context.Response.ContentLength = 1000;
            await context.Response.WriteAsync("""{"email":""", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await Task.Delay(200, context.RequestAborted);
            context.Abort();
        });
        using var client = server.Client();

        await Assert.ThrowsAsync<ApiConnectionException>(() => client.GetAccountAsync(Token));

        Assert.Equal(1, server.Count);
    }

    [Fact]
    public async Task CancellationDuringBackoffStops()
    {
        var handler = new StubHandler(_ => Fixtures.Json(503, ""));
        using var client = Fixtures.StubClient(handler);
        client.Transport.Delay = Task.Delay;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(200);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAccountAsync(cancel.Token));

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task DisposeEndsABackoffWait()
    {
        var handler = new StubHandler(_ => Fixtures.Json(503, "", ("Retry-After", "30")));
        var client = Fixtures.StubClient(handler);
        client.Transport.Delay = Task.Delay;
        var call = client.GetAccountAsync(Token);
        while (handler.Count == 0)
        {
            await Task.Delay(10, Token);
        }

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => call.WaitAsync(TimeSpan.FromSeconds(10), Token));
    }

    [Fact]
    public async Task DisposesEveryAttempt()
    {
        var contents = new ConcurrentQueue<TrackingContent>();
        var handler = new StubHandler(_ =>
        {
            var content = new TrackingContent();
            contents.Enqueue(content);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = content };
        });
        using var client = Fixtures.StubClient(handler);

        await Assert.ThrowsAsync<ApiException>(() => client.GetAccountAsync(Token));

        Assert.Equal(3, contents.Count);
        Assert.All(contents, content => Assert.True(content.Disposed));
    }

    public static TheoryData<string?, double?> RetryAfterValues => new()
    {
        { null, null },
        { "", null },
        { "5", 5 },
        { " 12 ", 12 },
        { "-3", 0 },
        { "soon", null },
        { "1.5", null },
        { "99999999999999999999999", 1L << 31 },
        { "Sun, 06 Nov 1994 08:49:37 GMT", 0 },
        { "Sunday, 06-Nov-94 08:49:37 GMT", 0 },
        { "Sun Nov  6 08:49:37 1994", 0 },
    };

    [Theory]
    [MemberData(nameof(RetryAfterValues))]
    public void ParsesRetryAfter(string? value, double? seconds)
    {
        Assert.Equal(seconds is null ? null : TimeSpan.FromSeconds(seconds.Value), Transport.ParseRetryAfter(value));
    }

    [Fact]
    public void ParsesFutureRetryAfterDates()
    {
        var value = DateTimeOffset.UtcNow.AddHours(1).ToString("r", System.Globalization.CultureInfo.InvariantCulture);

        Assert.InRange(Transport.ParseRetryAfter(value)!.Value.TotalMinutes, 58, 60);
    }
}

internal sealed class TrackingContent : HttpContent
{
    public bool Disposed { get; private set; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
