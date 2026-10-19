namespace Synthient;

/// <summary>
/// Configures a <see cref="SynthientClient"/>. The client copies the options when it is created, so
/// later changes have no effect on it.
/// </summary>
public sealed class SynthientClientOptions
{
    /// <summary>
    /// The API key. When null, the client reads the <c>SYNTHIENT_API_KEY</c> environment variable.
    /// Surrounding whitespace is removed.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The API base address, including the version path. The default is
    /// <see cref="SynthientClient.DefaultBaseAddress"/>.
    /// </summary>
    public Uri BaseAddress { get; set; } = SynthientClient.DefaultBaseAddress;

    /// <summary>
    /// How many times a failed request is retried. The default is 2, and 0 disables retries. See
    /// <see cref="SynthientClient"/> for which failures are retried.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// How long the client waits on the network before failing with
    /// <see cref="ApiTimeoutException"/>. It bounds each wait for response headers and each read of
    /// a response body, stream, or download, not a whole call, so long-lived streams and large
    /// downloads are not cut off. The default is 60 seconds, and
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> disables it. The default handler also
    /// gives up connecting after 10 seconds. Use a <see cref="CancellationToken"/> to bound a whole
    /// call.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>A product token, such as "my-app/1.2", appended to the User-Agent header.</summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Called synchronously with the metadata of every response from the Synthient API, including
    /// error responses, each retry, and each page. It is not called for export downloads from
    /// storage. If it throws, the call fails with its exception. It must be safe to call
    /// concurrently.
    /// </summary>
    public Action<ResponseMetadata>? ResponseHook { get; set; }

    /// <summary>
    /// The HTTP handler that sends requests, for custom proxies, certificates, or tests. The client
    /// never disposes it. It must not follow redirects, because a followed export redirect would
    /// send the API key to the storage host, so a <see cref="SocketsHttpHandler"/> or
    /// <see cref="HttpClientHandler"/> with <c>AllowAutoRedirect</c> set is rejected. It should not
    /// retry either, because a resent billed lookup can be charged twice. Set
    /// <see cref="SocketsHttpHandler.MaxResponseDrainSize"/> to 0 so that leaving a stream or
    /// download early closes its connection instead of reading up to 1 MiB more of it.
    /// </summary>
    public HttpMessageHandler? Handler { get; set; }
}
