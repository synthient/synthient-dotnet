using System.Net;
using Microsoft.AspNetCore.Http;

namespace Synthient.Tests;

public sealed class TimeoutTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<TestServer> StallingServer(bool sendHeaders) => TestServer.StartAsync(async context =>
    {
        if (sendHeaders)
        {
            context.Response.ContentLength = 1000;
            await context.Response.WriteAsync("{", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        await Task.Delay(Timeout.Infinite, context.RequestAborted);
    });

    [Fact]
    public async Task StalledHeadersTimeOutAndAreRetriedForFreeCalls()
    {
        await using var server = await StallingServer(sendHeaders: false);
        using var client = server.Client(o => o.Timeout = TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.GetAccountAsync(Token));
        Assert.Equal(3, server.Count);

        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.LookupIPAsync(IPAddress.Loopback, Token));
        Assert.Equal(4, server.Count);
    }

    [Fact]
    public async Task StalledBodiesTimeOutWithoutRetrying()
    {
        await using var server = await StallingServer(sendHeaders: true);
        var responses = 0;
        using var client = server.Client(o =>
        {
            o.Timeout = TimeSpan.FromSeconds(1);
            o.ResponseHook = _ => Interlocked.Increment(ref responses);
        });

        var error = await Assert.ThrowsAsync<ApiTimeoutException>(() => client.GetAccountAsync(Token));

        Assert.Equal("Timed out reading the response.", error.Message);
        Assert.Equal(1, responses);
    }

    [Fact]
    public async Task CallerCancellationIsNotATimeout()
    {
        await using var server = await StallingServer(sendHeaders: false);
        using var client = server.Client(o => o.Timeout = TimeSpan.FromSeconds(30));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(200);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAccountAsync(cancel.Token));

        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(1, server.Count);
    }

    [Fact]
    public async Task RecoversAfterALateResponse()
    {
        var calls = 0;
        await using var server = await TestServer.StartAsync(async context =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                await Task.Delay(2000, CancellationToken.None);
            }
            await context.Response.WriteAsync(Fixtures.Account, CancellationToken.None);
        });
        using var client = server.Client(o =>
        {
            o.Timeout = TimeSpan.FromSeconds(1);
            o.MaxRetries = 0;
        });

        await Assert.ThrowsAsync<ApiTimeoutException>(() => client.GetAccountAsync(Token));
        await Task.Delay(1500, Token);
        var account = await client.GetAccountAsync(Token);

        Assert.Equal("Ada", account.FirstName);
    }
}
