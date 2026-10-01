using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Synthient.Tests;

internal static class Fixtures
{
    public const string ApiKey = "test-key";

    public static readonly Uri StubBase = new("https://api.test/api/v4");

    public const string IPInfo = """
        {
          "ip": "8.8.8.8",
          "network": {"asn": 15169, "isp": "Google LLC", "type": "DATACENTER", "org": "Google LLC", "domain": "google.com"},
          "location": {"country": "US", "state": "California", "city": "Mountain View", "timezone": "America/Los_Angeles",
                       "latitude": 37.4056, "longitude": -122.0775, "geo_hash": "9q9hvu"},
          "intelligence": {
            "risk_score": 12,
            "categories": ["DATACENTER_PROXY"],
            "behavior": [],
            "providers": [{"provider": "BRIGHTDATA", "type": "DATACENTER_PROXY", "last_seen": 1759000000}],
            "devices": [{"os": "Linux", "version": "6.1", "last_seen": 1759000001}]
          },
          "future_field": {"anything": true}
        }
        """;

    public const string Account = """
        {
          "email": "ada@example.com", "first_name": "Ada", "last_name": "Lovelace",
          "organization": {"id": "8d6f2c1e-1b7a-4c1f-9a4b-2f0e5d3c7a91", "name": "Analytical", "relation": "OWNER"},
          "scopes": ["BASIC", "PROXY_FEEDS"],
          "lookup_quota": {"credits": 9000000000, "resets_in": 3600},
          "api_key": {"id": "3c1f9a4b-2f0e-4d3c-8a91-8d6f2c1e1b7a", "name": "sdk", "created_at": "2026-09-28T19:04:52.463869Z"}
        }
        """;

    public static SynthientClient Client(Uri baseAddress, HttpMessageHandler? handler = null, Action<SynthientClientOptions>? configure = null, ConcurrentQueue<TimeSpan>? delays = null)
    {
        var options = new SynthientClientOptions { ApiKey = ApiKey, BaseAddress = baseAddress, Handler = handler };
        configure?.Invoke(options);
        var client = new SynthientClient(options);
        client.Transport.Delay = (delay, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            delays?.Enqueue(delay);
            return Task.CompletedTask;
        };
        return client;
    }

    public static SynthientClient StubClient(StubHandler handler, Action<SynthientClientOptions>? configure = null, ConcurrentQueue<TimeSpan>? delays = null) =>
        Client(StubBase, handler, configure, delays);

    public static HttpResponseMessage Json(int status, string body, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        foreach (var (name, value) in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(name, value))
            {
                response.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }
        return response;
    }

    public static HttpResponseMessage Redirect(int status, string location)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status);
        response.Headers.TryAddWithoutValidation("Location", location);
        return response;
    }

    public static HttpResponseMessage Text(int status, string body, string contentType, params (string Name, string Value)[] headers)
    {
        var response = Json(status, "", headers);
        response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        if (contentType.Length > 0)
        {
            response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }
        return response;
    }
}

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;

    public string Path => Uri.AbsolutePath;

    public string PathAndQuery => Uri.PathAndQuery;
}

// A fake transport that records each request and answers it with a delegate.
internal sealed class StubHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public StubHandler(params HttpResponseMessage[] responses)
        : this(QueueResponder(responses))
    {
    }

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    public int Count => Requests.Count;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = request.Headers.NonValidated
            .Concat(request.Content?.Headers.NonValidated ?? [])
            .ToDictionary(header => header.Key, header => string.Join(", ", header.Value), StringComparer.OrdinalIgnoreCase);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, headers, body);
        Requests.Enqueue(recorded);
        var response = respond(recorded);
        response.RequestMessage = request;
        return response;
    }

    private static Func<RecordedRequest, HttpResponseMessage> QueueResponder(HttpResponseMessage[] responses)
    {
        var queue = new ConcurrentQueue<HttpResponseMessage>(responses);
        return _ => queue.TryDequeue(out var response) ? response : throw new InvalidOperationException("No response left.");
    }
}

internal sealed record ServerRequest(string Method, string RawTarget, IReadOnlyDictionary<string, string> Headers);

// A real HTTP server on the loopback interface, for behavior that needs sockets: streaming,
// stalls, redirects, and cancellation.
internal sealed class TestServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestServer(WebApplication app, string origin)
    {
        _app = app;
        Origin = new Uri(origin);
    }

    public ConcurrentQueue<ServerRequest> Requests { get; } = new();

    public Uri Origin { get; }

    public Uri BaseAddress => new(Origin, "/api/v4");

    public int Count => Requests.Count;

    public static async Task<TestServer> StartAsync(RequestDelegate handler, string host = "127.0.0.1")
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        TestServer? server = null;
        app.Run(async context =>
        {
            var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var target = context.Features.Get<IHttpRequestFeature>()!.RawTarget;
            server!.Requests.Enqueue(new ServerRequest(context.Request.Method, target, headers));
            await handler(context);
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var port = new Uri(address).Port;
        server = new TestServer(app, $"http://{host}:{port}");
        return server;
    }

    public SynthientClient Client(Action<SynthientClientOptions>? configure = null, ConcurrentQueue<TimeSpan>? delays = null) =>
        Fixtures.Client(BaseAddress, null, configure, delays);

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();
}

// A minimal HTTP/1.1 server over raw sockets, for checking exact request bytes and for closing a
// connection without responding.
internal sealed class RawHttpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<int, string, string?> _respond;
    private int _count;

    // respond receives the request index and text, and returns the raw response, or null to close
    // the connection without responding.
    public RawHttpServer(Func<int, string, string?> respond)
    {
        _respond = respond;
        _listener.Start();
        _ = AcceptAsync();
    }

    public ConcurrentQueue<string> Requests { get; } = new();

    public Uri BaseAddress => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/api/v4");

    public static string Ok(string json) =>
        $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (true)
            {
                var socket = await _listener.AcceptSocketAsync(_stop.Token);
                _ = HandleAsync(socket);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
    }

    private async Task HandleAsync(Socket socket)
    {
        using var _ = socket;
        var buffer = new byte[64 << 10];
        var received = 0;
        try
        {
            while (true)
            {
                int end;
                while ((end = buffer.AsSpan(0, received).IndexOf("\r\n\r\n"u8)) < 0)
                {
                    var read = await socket.ReceiveAsync(buffer.AsMemory(received), _stop.Token);
                    if (read == 0)
                    {
                        return;
                    }
                    received += read;
                }
                var head = Encoding.ASCII.GetString(buffer, 0, end);
                var length = head.Split("\r\n")
                    .Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    .Select(line => int.Parse(line["Content-Length:".Length..].Trim()))
                    .FirstOrDefault();
                while (received < end + 4 + length)
                {
                    received += await socket.ReceiveAsync(buffer.AsMemory(received), _stop.Token);
                }
                var text = Encoding.UTF8.GetString(buffer, 0, end + 4 + length);
                buffer.AsSpan(end + 4 + length, received - end - 4 - length).CopyTo(buffer);
                received -= end + 4 + length;

                Requests.Enqueue(text);
                var response = _respond(Interlocked.Increment(ref _count) - 1, text);
                if (response is null)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    return;
                }
                await socket.SendAsync(Encoding.UTF8.GetBytes(response), _stop.Token);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException)
        {
        }
    }
}
