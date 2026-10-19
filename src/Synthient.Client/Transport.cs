using System.Collections.Frozen;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;

namespace Synthient;

internal enum RetryPolicy
{
    // Retries 5xx statuses, connection failures, timeouts, and 429s with Retry-After.
    Transient,

    // Retries only 429s with Retry-After, which the rate limiter returns before doing any work, so
    // a billed call is never sent twice.
    RateLimitOnly,
}

internal sealed record ApiRequest(
    HttpMethod Method,
    string Path,
    RetryPolicy Policy,
    byte[]? Body = null,
    string Accept = "application/json");

internal sealed class ApiResponse : IDisposable
{
    public ApiResponse(HttpResponseMessage message, Uri requestUri)
    {
        Message = message;
        RequestUri = requestUri;
        var headers = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var (name, values) in message.Headers.NonValidated.Concat(message.Content.Headers.NonValidated))
        {
            headers[name] = [.. values];
        }
        Metadata = new ResponseMetadata((int)message.StatusCode, headers);
        RetryAfter = Transport.ParseRetryAfter(Metadata.Header("Retry-After"));
    }

    public HttpResponseMessage Message { get; }

    public Uri RequestUri { get; }

    public ResponseMetadata Metadata { get; }

    public TimeSpan? RetryAfter { get; }

    public int StatusCode => Metadata.StatusCode;

    public void Dispose() => Message.Dispose();
}

internal sealed class Transport : IDisposable
{
    internal const int MaxResponseSize = 16 << 20;
    private const int MaxRedirects = 10;
    private const int MaxErrorSize = 64 << 10;
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _userAgent;
    private readonly string _baseAddress;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;
    private readonly Action<ResponseMetadata>? _hook;
    private readonly CancellationTokenSource _disposing = new();
    private int _disposed;

    internal Transport(SynthientClientOptions options)
    {
        var apiKey = (options.ApiKey ?? Environment.GetEnvironmentVariable("SYNTHIENT_API_KEY"))?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("An API key is required. Pass one or set SYNTHIENT_API_KEY.", nameof(options));
        }
        if (!IsPrintableAscii(apiKey))
        {
            throw new ArgumentException("The API key must contain only printable ASCII characters.", nameof(options));
        }

        var baseAddress = options.BaseAddress ?? throw new ArgumentException("BaseAddress must not be null.", nameof(options));
        var validBase = baseAddress.IsAbsoluteUri
            && (baseAddress.Scheme == Uri.UriSchemeHttps || baseAddress.Scheme == Uri.UriSchemeHttp)
            && baseAddress.Host.Length > 0
            && baseAddress.UserInfo.Length == 0
            && baseAddress.Query.Length == 0
            && baseAddress.Fragment.Length == 0;
        if (!validBase)
        {
            throw new ArgumentException(
                "BaseAddress must be an absolute http or https URI with no user info, query, or fragment.",
                nameof(options));
        }

        if (options.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxRetries must not be negative.");
        }
        var timeout = options.Timeout;
        if (timeout != Timeout.InfiniteTimeSpan && (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Timeout must be positive and at most int.MaxValue milliseconds, or Timeout.InfiniteTimeSpan.");
        }

        var userAgent = "synthient-dotnet/" + SynthientClient.Version;
        if (!string.IsNullOrEmpty(options.UserAgent))
        {
            if (!IsPrintableAscii(options.UserAgent))
            {
                throw new ArgumentException("UserAgent must contain only printable ASCII characters.", nameof(options));
            }
            userAgent += " " + options.UserAgent;
        }

        for (var handler = options.Handler; handler is not null; handler = (handler as DelegatingHandler)?.InnerHandler)
        {
            if (handler is SocketsHttpHandler { AllowAutoRedirect: true } or HttpClientHandler { AllowAutoRedirect: true })
            {
                throw new ArgumentException(
                    "The handler must not follow redirects. Set AllowAutoRedirect to false.",
                    nameof(options));
            }
        }

        var ownHandler = options.Handler is null;
        var transport = options.Handler ?? new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),

            // Leaving a stream or download early closes its connection, instead of reading up to
            // 1 MiB more of it in the background to reuse the connection.
            MaxResponseDrainSize = 0,
        };

        _http = new HttpClient(transport, ownHandler) { Timeout = Timeout.InfiniteTimeSpan };
        _apiKey = apiKey;
        _userAgent = userAgent;
        _baseAddress = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress.AbsoluteUri[..^1] : baseAddress.AbsoluteUri;
        _maxRetries = options.MaxRetries;
        _timeout = timeout;
        _hook = options.ResponseHook;
    }

    // Tests replace the backoff wait so they can record delays instead of sleeping.
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _disposing.Cancel();
            _http.Dispose();
        }
    }

    internal static TimeSpan? ParseRetryAfter(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        if (long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 1L << 31));
        }
        if (value.All(char.IsAsciiDigit))
        {
            return TimeSpan.FromSeconds(1L << 31);
        }
        if (RetryConditionHeaderValue.TryParse(value, out var header) && header.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }
        return null;
    }

    // Resolves a redirect target and rejects anything but https, or http when the redirect came from http.
    internal static Uri ResolveRedirect(Uri from, string? location)
    {
        if (string.IsNullOrWhiteSpace(location) || !Uri.TryCreate(from, location, out var target))
        {
            throw new SynthientException("The export redirect has no valid location.");
        }
        if (target.Scheme != Uri.UriSchemeHttps && !(from.Scheme == Uri.UriSchemeHttp && target.Scheme == Uri.UriSchemeHttp))
        {
            throw new SynthientException("The export redirect does not use https.");
        }
        if (target.UserInfo.Length > 0)
        {
            throw new SynthientException("The export redirect contains user info.");
        }
        return target;
    }

    internal async Task<T> GetJsonAsync<T>(ApiRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is < 200 or >= 300)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }
        var body = await ReadUpToAsync(response.Message, MaxResponseSize, cancellationToken).ConfigureAwait(false)
            ?? throw new SynthientException($"The response body exceeds {MaxResponseSize} bytes.");
        return Json.Deserialize(body, Json.TypeInfo<T>());
    }

    // Fetches an export from storage, following redirects itself so that each hop is checked. Only
    // the User-Agent is sent, never the API key.
    internal async Task<Stream> DownloadAsync(Uri location, CancellationToken cancellationToken)
    {
        var url = location;
        for (var redirects = 0; ; redirects++)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, typeof(SynthientClient));
            HttpResponseMessage response;
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
                response = await SendWithTimeoutAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var status = (int)response.StatusCode;
            if (status == 200)
            {
                try
                {
                    return await OpenBodyAsync(response, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }
            }
            var redirect = status is 301 or 302 or 303 or 307 or 308
                && response.Headers.NonValidated.TryGetValues("Location", out var values)
                ? values.FirstOrDefault()
                : null;
            response.Dispose();
            if (redirect is null)
            {
                throw new ApiException(status);
            }
            if (redirects == MaxRedirects)
            {
                throw new SynthientException("The export download redirected too many times.");
            }
            url = ResolveRedirect(url, redirect);
        }
    }

    // Opens a response body whose reads fail with ApiTimeoutException if they stall for longer than
    // the timeout. Disposing the body disposes the response.
    internal async Task<Stream> OpenBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new ResponseStream(this, response, content);
    }

    internal async ValueTask<int> ReadAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        using var timeout = StartTimeout(cancellationToken);
        try
        {
            return await stream.ReadAsync(buffer, timeout?.Token ?? cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException e)
        {
            throw CanceledOrTimedOut(e, "Timed out reading the response.", cancellationToken);
        }
        catch (Exception e) when (e is IOException or HttpRequestException or InvalidDataException)
        {
            throw new ApiConnectionException("Reading the response failed.", e);
        }
    }

    // Sends an API request, retrying as its policy allows, and returns the final response whatever
    // its status. The caller owns the response and checks its status.
    internal async Task<ApiResponse> SendAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        var uri = new Uri(_baseAddress + request.Path);
        for (var attempt = 0; ; attempt++)
        {
            var canRetry = attempt < _maxRetries;
            HttpResponseMessage message;
            try
            {
                message = await SendOnceAsync(request, uri, cancellationToken).ConfigureAwait(false);
            }
            catch (ApiConnectionException e) when (canRetry && request.Policy == RetryPolicy.Transient && !IsTlsFailure(e))
            {
                await WaitAsync(Backoff(attempt), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var response = new ApiResponse(message, uri);
            try
            {
                _hook?.Invoke(response.Metadata);
            }
            catch
            {
                response.Dispose();
                throw;
            }

            var retryAfter = response.RetryAfter;
            var retryable = response.StatusCode switch
            {
                429 => retryAfter is not null,
                500 or 502 or 503 or 504 => request.Policy == RetryPolicy.Transient,
                _ => false,
            };
            if (!canRetry || !retryable || retryAfter > MaxRetryAfter)
            {
                return response;
            }
            response.Dispose();
            var backoff = Backoff(attempt);
            await WaitAsync(retryAfter > backoff ? retryAfter.Value : backoff, cancellationToken).ConfigureAwait(false);
        }
    }

    internal async Task<ApiException> CreateApiExceptionAsync(ApiResponse response, CancellationToken cancellationToken)
    {
        ArraySegment<byte>? body = null;
        Exception? failure = null;
        try
        {
            body = await ReadUpToAsync(response.Message, MaxErrorSize, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiConnectionException e)
        {
            failure = e;
        }

        var metadata = response.Metadata;
        string? type = null, title = null, detail = null, instance = null;
        IReadOnlyDictionary<string, IReadOnlyList<string>> errors = FrozenDictionary<string, IReadOnlyList<string>>.Empty;
        if (body is { Count: > 0 } bytes)
        {
            using var document = TryParseJson(bytes);
            if (document?.RootElement is { ValueKind: JsonValueKind.Object } problem)
            {
                type = StringProperty(problem, "type");
                title = StringProperty(problem, "title");
                detail = StringProperty(problem, "detail");
                instance = StringProperty(problem, "instance");
                errors = ParseErrors(problem);
            }
            else if (MediaTypeHeaderValue.TryParse(metadata.Header("Content-Type"), out var mediaType)
                && string.Equals(mediaType.MediaType, "text/plain", StringComparison.OrdinalIgnoreCase))
            {
                detail = Encoding.UTF8.GetString(bytes).Trim();
            }
        }

        return new ApiException(metadata.StatusCode, failure)
        {
            Type = type,
            Title = title,
            Detail = detail,
            Errors = errors,
            RequestId = metadata.RequestId ?? instance,
            RetryAfter = response.RetryAfter,
            Headers = metadata.Headers,
        };
    }

    // Waits before a retry. Disposing the client ends the wait.
    private async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposing.Token);
        try
        {
            await Delay(delay, wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(SynthientClient));
        }
    }

    private static bool IsPrintableAscii(string value) => !value.AsSpan().ContainsAnyExceptInRange(' ', '~');

    // Reports a cancelled network wait as the caller's cancellation when their token fired, and as a
    // timeout otherwise.
    private static Exception CanceledOrTimedOut(OperationCanceledException e, string message, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? new OperationCanceledException(e.Message, e, cancellationToken)
            : new ApiTimeoutException(message, e);

    private static bool IsTlsFailure(Exception exception)
    {
        for (Exception? inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is AuthenticationException)
            {
                return true;
            }
        }
        return false;
    }

    private static TimeSpan Backoff(int attempt)
    {
        var delay = TimeSpan.FromSeconds(Math.Min(1 << Math.Min(attempt, 5), 30));
        return delay * (0.75 + (Random.Shared.NextDouble() / 2));
    }

    private static JsonDocument? TryParseJson(ArraySegment<byte> body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Returns the validation errors of a problem, or none if they are missing or have the wrong shape.
    private static FrozenDictionary<string, IReadOnlyList<string>> ParseErrors(JsonElement problem)
    {
        var none = FrozenDictionary<string, IReadOnlyList<string>>.Empty;
        if (!problem.TryGetProperty("errors", out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return none;
        }
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var field in value.EnumerateObject())
        {
            if (field.Value.ValueKind != JsonValueKind.Array)
            {
                return none;
            }
            var messages = new List<string>();
            foreach (var message in field.Value.EnumerateArray())
            {
                if (message.ValueKind != JsonValueKind.String)
                {
                    return none;
                }
                messages.Add(message.GetString()!);
            }
            errors[field.Name] = messages.AsReadOnly();
        }
        return errors.ToFrozenDictionary();
    }

    private async Task<HttpResponseMessage> SendOnceAsync(ApiRequest request, Uri uri, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, typeof(SynthientClient));
        using var message = new HttpRequestMessage(request.Method, uri)
        {
            // Content on every request, even a GET, stops SocketsHttpHandler from silently resending
            // a request whose pooled connection closed before responding, which could bill a lookup
            // twice. An empty body is sent as Content-Length: 0.
            Content = new ByteArrayContent(request.Body ?? []),
        };
        if (request.Body is not null)
        {
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        message.Headers.TryAddWithoutValidation("X-Api-Key", _apiKey);
        message.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        message.Headers.TryAddWithoutValidation("Accept", request.Accept);
        return await SendWithTimeoutAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendWithTimeoutAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using var timeout = StartTimeout(cancellationToken);
        try
        {
            return await _http
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout?.Token ?? cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException e)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0 && !cancellationToken.IsCancellationRequested, typeof(SynthientClient));
            throw CanceledOrTimedOut(e, "Timed out waiting for the response.", cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new ApiConnectionException("The request failed before a response arrived.", e);
        }
    }

    // Reads a whole body, or returns null if it is longer than limit bytes.
    private async Task<ArraySegment<byte>?> ReadUpToAsync(HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        var stream = await OpenBodyAsync(response, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new byte[Math.Clamp(response.Content.Headers.ContentLength ?? 0, 4096, limit + 1L)];
            var length = 0;
            while (true)
            {
                if (length == buffer.Length)
                {
                    if (length > limit)
                    {
                        return null;
                    }
                    Array.Resize(ref buffer, (int)Math.Min(buffer.Length * 2L, limit + 1L));
                }
                var read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return new ArraySegment<byte>(buffer, 0, length);
                }
                length += read;
            }
        }
    }

    private CancellationTokenSource? StartTimeout(CancellationToken cancellationToken)
    {
        if (_timeout == Timeout.InfiniteTimeSpan)
        {
            return null;
        }
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(_timeout);
        return source;
    }
}
