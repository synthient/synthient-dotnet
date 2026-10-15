using System.Globalization;

namespace Synthient;

/// <summary>
/// Describes an API response. <see cref="SynthientClientOptions.ResponseHook"/> receives one for
/// every response from the Synthient API, including each retry and page.
/// </summary>
public sealed class ResponseMetadata
{
    /// <summary>Initializes a new instance from a status code and response headers.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="headers">The response headers. Names are matched case-insensitively.</param>
    public ResponseMetadata(int statusCode, IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
    {
        StatusCode = statusCode;
        Headers = CopyHeaders(headers).AsReadOnly();
        var requestId = Header("X-Request-Id");
        RequestId = string.IsNullOrEmpty(requestId) ? null : requestId;
        RateLimit = ParseRateLimit();
    }

    /// <summary>The HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>The ID of the request, for Synthient support.</summary>
    public string? RequestId { get; }

    /// <summary>The rate limit state, or null if the response does not report it.</summary>
    public RateLimit? RateLimit { get; }

    /// <summary>
    /// The response and content headers, with case-insensitive names. The collection is a read-only
    /// copy, so a response hook cannot change the headers the client reads.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

    internal string? Header(string name) =>
        Headers.TryGetValue(name, out var values) && values.Count > 0 ? values[0] : null;

    private static Dictionary<string, IReadOnlyList<string>> CopyHeaders(IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, values) in headers)
        {
            copy[name] = values.ToArray().AsReadOnly();
        }
        return copy;
    }

    private RateLimit? ParseRateLimit()
    {
        if (!int.TryParse(Header("RateLimit-Limit"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var limit)
            || !int.TryParse(Header("RateLimit-Remaining"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var remaining)
            || !long.TryParse(Header("RateLimit-Reset"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var reset))
        {
            return null;
        }
        return new RateLimit(limit, remaining, TimeSpan.FromSeconds(Math.Clamp(reset, 0, 1L << 31)));
    }
}

/// <summary>
/// The rate limit state of the bucket a request was charged to. Buckets are shared by every key of
/// a team.
/// </summary>
/// <param name="Limit">The bucket's burst size.</param>
/// <param name="Remaining">The number of requests left in the bucket.</param>
/// <param name="Reset">How long until the bucket is full again.</param>
public sealed record RateLimit(int Limit, int Remaining, TimeSpan Reset);
