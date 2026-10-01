using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Synthient;

/// <summary>
/// A client for the Synthient API: IP and domain intelligence, account details, and the Synthient
/// data feeds. A client is safe for concurrent use and should be reused for the life of the
/// application, because it pools connections.
/// </summary>
/// <remarks>
/// <para>
/// Calls that fail with an HTTP error status throw <see cref="ApiException"/>. Calls that get no
/// response throw <see cref="ApiConnectionException"/>, or <see cref="ApiTimeoutException"/> when
/// <see cref="SynthientClientOptions.Timeout"/> elapses. Responses the SDK cannot decode throw
/// <see cref="SynthientException"/>. Cancelling a call's token throws
/// <see cref="OperationCanceledException"/>.
/// </para>
/// <para>
/// Calls that cost no credits, other than <see cref="CheckHealthAsync"/>, are retried on 500, 502,
/// 503, and 504 responses, on connection failures and timeouts, and on 429 responses that carry
/// Retry-After. Billed lookups and health checks are retried only on 429 responses that carry
/// Retry-After, which the rate limiter returns before doing any work, so a lookup is never sent
/// twice after the API may have charged for it. A 429 without
/// Retry-After means the key's monthly request budget is spent or it has too many open streams,
/// so it is not retried. Retries back off exponentially with jitter, wait at least as long as
/// Retry-After asks, and stop if it asks for more than a minute.
/// </para>
/// </remarks>
public sealed class SynthientClient : IDisposable
{
    /// <summary>The version of this SDK. It is sent in the User-Agent header.</summary>
    public const string Version = "1.0.0";

    /// <summary>Creates a client that reads its API key from the <c>SYNTHIENT_API_KEY</c> environment variable.</summary>
    /// <exception cref="ArgumentException">The environment variable is not set.</exception>
    public SynthientClient()
        : this(new SynthientClientOptions())
    {
    }

    /// <summary>Creates a client that authenticates with an API key.</summary>
    /// <param name="apiKey">The API key.</param>
    /// <exception cref="ArgumentException">The key is empty.</exception>
    public SynthientClient(string apiKey)
        : this(new SynthientClientOptions { ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey)) })
    {
    }

    /// <summary>Creates a client from options. It performs no network I/O.</summary>
    /// <param name="options">The client options, which are copied.</param>
    /// <exception cref="ArgumentException">The API key is missing or an option is invalid.</exception>
    public SynthientClient(SynthientClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Transport = new Transport(options);
        Proxies = new Feed<ProxyEvent>(Transport, "/feeds/proxies");
        Anonymizers = new Feed<AnonymizerEvent>(Transport, "/feeds/anonymizers");
        Torrents = new Feed<TorrentEvent>(Transport, "/feeds/torrents");
        Ja4t = new Feed<Ja4tEvent>(Transport, "/feeds/ja4t");
        HeliosHttp = new Feed<HeliosHttpEvent>(Transport, "/feeds/helio/http");
        HeliosHttps = new Feed<HeliosHttpsEvent>(Transport, "/feeds/helio/https");
        HeliosDns = new Feed<HeliosDnsEvent>(Transport, "/feeds/helio/dns");
        HeliosAdb = new Feed<HeliosAdbEvent>(Transport, "/feeds/helio/adb");
    }

    /// <summary>The base address of the Synthient HTTP API.</summary>
    public static Uri DefaultBaseAddress { get; } = new("https://api.synthient.com/api/v4");

    /// <summary>The residential, mobile, and datacenter proxy feed.</summary>
    public Feed<ProxyEvent> Proxies { get; }

    /// <summary>The VPN and anonymizer IP range feed.</summary>
    public Feed<AnonymizerEvent> Anonymizers { get; }

    /// <summary>The BitTorrent DHT and tracker feed.</summary>
    public Feed<TorrentEvent> Torrents { get; }

    /// <summary>The JA4T TCP fingerprint feed.</summary>
    public Feed<Ja4tEvent> Ja4t { get; }

    /// <summary>The Helios honeypot HTTP capture feed.</summary>
    public Feed<HeliosHttpEvent> HeliosHttp { get; }

    /// <summary>The Helios honeypot TLS ClientHello capture feed.</summary>
    public Feed<HeliosHttpsEvent> HeliosHttps { get; }

    /// <summary>The Helios honeypot tunnel destination feed. Despite its name, it does not carry DNS queries.</summary>
    public Feed<HeliosDnsEvent> HeliosDns { get; }

    /// <summary>The Helios honeypot Android Debug Bridge command feed.</summary>
    public Feed<HeliosAdbEvent> HeliosAdb { get; }

    internal Transport Transport { get; }

    /// <summary>
    /// Returns the account that owns the API key, including its scopes and remaining lookup
    /// credits. It costs no credits.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account.</returns>
    public Task<Account> GetAccountAsync(CancellationToken cancellationToken = default) =>
        Transport.GetJsonAsync<Account>(new ApiRequest(HttpMethod.Get, "/account/me", RetryPolicy.Transient), cancellationToken);

    /// <summary>
    /// Completes if the API and its dependencies are healthy, and throws a 503
    /// <see cref="ApiException"/> if not. It costs no credits.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the API reports it is healthy.</returns>
    public async Task CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        // An unhealthy 503 is reported rather than retried.
        using var response = await Transport
            .SendAsync(new ApiRequest(HttpMethod.Get, "/health", RetryPolicy.RateLimitOnly), cancellationToken)
            .ConfigureAwait(false);
        var body = await Transport.ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(body);
        }
        catch (JsonException e)
        {
            throw new SynthientException("The API returned an invalid health response.", e);
        }
    }

    /// <summary>
    /// Returns intelligence for an IPv4 or IPv6 address. Private and reserved addresses fail with a
    /// 422 <see cref="ApiException"/>. Each lookup costs one credit, and failed lookups are not
    /// billed.
    /// </summary>
    /// <param name="ip">The address.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The intelligence for the address.</returns>
    /// <exception cref="ArgumentException">The address is an IPv6 address with a scope ID.</exception>
    public Task<IPInfo> LookupIPAsync(IPAddress ip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ip);
        if (HasScopeId(ip))
        {
            throw new ArgumentException("IPv6 addresses with a scope ID cannot be looked up.", nameof(ip));
        }
        return Transport.GetJsonAsync<IPInfo>(new ApiRequest(HttpMethod.Get, "/lookup/ip/" + ip, RetryPolicy.RateLimitOnly), cancellationToken);
    }

    /// <summary>
    /// Looks up as many as 1000 addresses in one request. Private and reserved addresses are skipped
    /// and duplicates collapse into one result, so there can be fewer results than addresses. Match
    /// results by <see cref="IPInfo.IP"/> rather than by position. A batch with no address the API
    /// can look up fails with a 422 <see cref="ApiException"/>. The API bills ceil(n * 0.9) credits
    /// for the n distinct addresses it looks up, and bills nothing if the account cannot cover the
    /// whole batch. An empty batch returns no results without a request.
    /// </summary>
    /// <param name="ips">The addresses.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The intelligence for each address the API looked up.</returns>
    /// <exception cref="ArgumentException">
    /// There are more than 1000 addresses, or one is null or an IPv6 address with a scope ID.
    /// </exception>
    public async Task<IReadOnlyList<IPInfo>> LookupIPsAsync(IEnumerable<IPAddress> ips, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ips);
        var addresses = ips.ToArray();
        if (addresses.Length > 1000)
        {
            throw new ArgumentException($"{addresses.Length} addresses exceed the batch limit of 1000.", nameof(ips));
        }
        for (var i = 0; i < addresses.Length; i++)
        {
            if (addresses[i] is null || HasScopeId(addresses[i]))
            {
                throw new ArgumentException($"The address at index {i} is null or has an IPv6 scope ID.", nameof(ips));
            }
        }
        if (addresses.Length == 0)
        {
            return [];
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(
            new IPBatchRequest { IPs = addresses.Select(address => address.ToString()).ToArray() },
            Json.TypeInfo<IPBatchRequest>());
        var response = await Transport
            .GetJsonAsync<IPBatchResponse>(new ApiRequest(HttpMethod.Post, "/lookup/ips", RetryPolicy.RateLimitOnly, body), cancellationToken)
            .ConfigureAwait(false);
        return response.Results;
    }

    /// <summary>
    /// Returns the honeypot traffic observed for a domain. Internationalized names must be sent in
    /// Punycode. A domain that has never been observed returns <see cref="DomainStatus.Inactive"/>
    /// and zero counts rather than an error. Each lookup costs one credit.
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <param name="limit">Caps the number of recent events returned, from 1 to 1000. Null uses the API default of 50.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The traffic observed for the domain.</returns>
    /// <exception cref="ArgumentException">The domain is empty, ".", or "..".</exception>
    public Task<DomainInfo> LookupDomainAsync(string domain, int? limit = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domain);
        if (domain is "" or "." or "..")
        {
            throw new ArgumentException($"\"{domain}\" is not a valid domain.", nameof(domain));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(limit ?? 1, 1, nameof(limit));
        var path = "/lookup/domain/" + Uri.EscapeDataString(domain) + (limit is null ? "" : string.Create(CultureInfo.InvariantCulture, $"?limit={limit}"));
        return Transport.GetJsonAsync<DomainInfo>(new ApiRequest(HttpMethod.Get, path, RetryPolicy.RateLimitOnly), cancellationToken);
    }

    /// <summary>
    /// Releases the client's connections. Calls made afterwards throw
    /// <see cref="ObjectDisposedException"/>, and calls waiting for a response are cancelled.
    /// </summary>
    public void Dispose() => Transport.Dispose();

    private static bool HasScopeId(IPAddress ip) => ip.AddressFamily == AddressFamily.InterNetworkV6 && ip.ScopeId != 0;
}
