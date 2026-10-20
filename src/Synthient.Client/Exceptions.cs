using System.Collections.Frozen;

namespace Synthient;

/// <summary>
/// The base type of the exceptions the SDK throws for failed API calls. It is thrown directly when
/// the API returns a response the SDK cannot use, such as a malformed body.
/// </summary>
public class SynthientException : Exception
{
    /// <summary>Initializes a new instance with a message.</summary>
    public SynthientException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and the exception that caused it.</summary>
    public SynthientException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The API returned an error status. <see cref="Exception.Message"/> holds only the status, such
/// as "403 Forbidden", so it is safe to log. The server-provided properties can contain sensitive
/// data.
/// </summary>
public sealed class ApiException : SynthientException
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Empty =
        FrozenDictionary<string, IReadOnlyList<string>>.Empty;

    /// <summary>
    /// Initializes a new instance for an HTTP status. The constructor is public so applications can
    /// stub the SDK in their own tests.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="innerException">The exception that interrupted reading the error body, if any.</param>
    public ApiException(int statusCode, Exception? innerException = null)
        : base(StatusLine(statusCode), innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The HTTP status code.</summary>
    public int StatusCode { get; }

    /// <summary>The problem type URI, such as "https://docs.synthient.com/errors/validation-failed".</summary>
    public string? Type { get; init; }

    /// <summary>A short summary of the problem.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// A description of this occurrence of the problem. For the plain-text errors that the API's
    /// edge returns, such as a 401 for a malformed key, it is the response body.
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>Validation errors by request field, from a 422 response.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; init; } = Empty;

    /// <summary>The ID of the request, for Synthient support.</summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// How long the server asked the client to wait before retrying, or null if the response had no
    /// valid Retry-After header.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>The response headers, with case-insensitive names.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; init; } = Empty;

    private static string StatusLine(int statusCode)
    {
        var text = statusCode switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            402 => "Payment Required",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            408 => "Request Timeout",
            409 => "Conflict",
            410 => "Gone",
            413 => "Content Too Large",
            415 => "Unsupported Media Type",
            422 => "Unprocessable Content",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            501 => "Not Implemented",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => null,
        };
        return text is null ? statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{statusCode} {text}";
    }
}

/// <summary>
/// No response arrived: the connection failed or was closed, or reading the response failed. The
/// server may still have processed the request.
/// </summary>
public class ApiConnectionException : SynthientException
{
    /// <summary>Initializes a new instance with a message and the exception that caused it.</summary>
    public ApiConnectionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The client's <see cref="SynthientClientOptions.Timeout"/> elapsed while waiting on the network,
/// or the default handler's 10-second connect timeout elapsed. Cancelling a call's
/// <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/> instead.
/// </summary>
public sealed class ApiTimeoutException : ApiConnectionException
{
    /// <summary>Initializes a new instance with a message and the exception that caused it.</summary>
    public ApiTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
