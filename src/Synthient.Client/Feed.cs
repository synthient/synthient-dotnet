using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;

namespace Synthient;

/// <summary>
/// A Synthient data feed. Each feed has a real-time stream of events and hourly parquet exports
/// that roll up into daily exports. Get feeds from the properties of <see cref="SynthientClient"/>,
/// such as <see cref="SynthientClient.Proxies"/>.
/// </summary>
/// <remarks>
/// An hourly export is available until its day's daily export is rolled up, shortly after 00:30 UTC
/// the next day. After that, requests for the hour fail with a 400 <see cref="ApiException"/>.
/// While a daily export is being rolled up, requests for it fail with a 404
/// <see cref="ApiException"/> that has <see cref="ApiException.RetryAfter"/> set.
/// </remarks>
/// <typeparam name="TEvent">The type of the feed's stream events.</typeparam>
public sealed class Feed<TEvent>
{
    private readonly Transport _transport;
    private readonly string _path;
    private readonly JsonTypeInfo<TEvent> _eventInfo;

    internal Feed(Transport transport, string path)
    {
        _transport = transport;
        _path = path;
        _eventInfo = Json.TypeInfo<TEvent>();
    }

    /// <summary>
    /// Returns one page of the feed's exports, newest first. Use <see cref="ListAllExportsAsync"/>
    /// to read every page. It costs no credits.
    /// </summary>
    /// <param name="limit">The page size, from 1 to 500. Null uses the API default of 100.</param>
    /// <param name="cursor">Continues a listing from a previous page's <see cref="ExportPage.NextCursor"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    public Task<ExportPage> ListExportsAsync(int? limit = null, string? cursor = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit ?? 1, 1, nameof(limit));
        var query = new List<string>();
        if (!string.IsNullOrEmpty(cursor))
        {
            query.Add("cursor=" + Uri.EscapeDataString(cursor));
        }
        if (limit is not null)
        {
            query.Add(string.Create(CultureInfo.InvariantCulture, $"limit={limit}"));
        }
        var path = _path + "/export" + (query.Count > 0 ? "?" + string.Join('&', query) : "");
        return _transport.GetJsonAsync<ExportPage>(new ApiRequest(HttpMethod.Get, path, RetryPolicy.Transient), cancellationToken);
    }

    /// <summary>
    /// Returns every export of the feed, newest first, fetching pages lazily as enumeration
    /// proceeds. Leaving the loop early fetches no more pages. A failure on any page ends the
    /// enumeration with its exception.
    /// </summary>
    /// <param name="limit">The page size, from 1 to 500. Null uses the API default of 100.</param>
    /// <param name="cursor">Starts the listing from a previous page's <see cref="ExportPage.NextCursor"/>.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The exports.</returns>
    public IAsyncEnumerable<Export> ListAllExportsAsync(int? limit = null, string? cursor = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit ?? 1, 1, nameof(limit));
        return ListAllExportsCoreAsync(limit, cursor, cancellationToken);
    }

    /// <summary>Returns the checksum, size, row count, and parquet schema of an export. It costs no credits.</summary>
    /// <param name="id">The export ID. See <see cref="ExportId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The export's metadata.</returns>
    public Task<ExportMeta> GetExportMetaAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = _path + "/export/" + ExportPath(id) + "/meta";
        return _transport.GetJsonAsync<ExportMeta>(new ApiRequest(HttpMethod.Get, path, RetryPolicy.Transient), cancellationToken);
    }

    /// <summary>
    /// Returns a presigned URL for an export's parquet file. The URL is valid for 24 hours and grants
    /// access without an API key, so treat it as a secret. It is useful for tools that read parquet
    /// over HTTPS directly.
    /// </summary>
    /// <param name="id">The export ID. See <see cref="ExportId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The presigned URL.</returns>
    public async Task<Uri> GetDownloadUrlAsync(string id, CancellationToken cancellationToken = default)
    {
        var request = new ApiRequest(HttpMethod.Get, _path + "/export/" + ExportPath(id), RetryPolicy.Transient);
        using var response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode >= 400)
        {
            throw await _transport.CreateApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }
        if (response.StatusCode < 300)
        {
            throw new SynthientException($"Expected a redirect to the export, got status {response.StatusCode}.");
        }
        return Transport.ResolveRedirect(response.RequestUri, response.Metadata.Header("Location"));
    }

    /// <summary>
    /// Opens an export's parquet file for reading. Dispose the returned stream when done. The API
    /// key is sent only to the Synthient API, never to the storage host that serves the file, and
    /// exceptions never include the presigned URL. The cancellation token covers opening the file;
    /// pass a token to each read to bound the rest. The SDK does not verify the file: compare its
    /// SHA-256 with <see cref="Export.Checksum"/> if needed.
    /// </summary>
    /// <param name="id">The export ID. See <see cref="ExportId"/>.</param>
    /// <param name="cancellationToken">Cancels opening the file.</param>
    /// <returns>The file contents.</returns>
    public async Task<Stream> DownloadAsync(string id, CancellationToken cancellationToken = default)
    {
        var location = await GetDownloadUrlAsync(id, cancellationToken).ConfigureAwait(false);
        return await _transport.DownloadAsync(location, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Streams the feed's events in real time. Each enumeration opens a new connection, which the
    /// API closes after up to 30 minutes, ending the enumeration without an error. Events sent
    /// between connections are not replayed, so reconnect promptly to keep up. Leaving the loop
    /// closes the connection.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The events.</returns>
    public async IAsyncEnumerable<TEvent> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new ApiRequest(HttpMethod.Get, _path + "/stream", RetryPolicy.Transient, Accept: "application/x-ndjson");
        using var response = await _transport.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A stream that answers with a 2xx other than 200 would end without events.
        if (response.StatusCode != 200)
        {
            throw await _transport.CreateApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }
        var body = await _transport.OpenBodyAsync(response.Message, cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            var reader = new NdjsonReader(body);
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                yield return Json.Deserialize(line.Span, _eventInfo);
            }
        }
    }

    private async IAsyncEnumerable<Export> ListAllExportsCoreAsync(
        int? limit,
        string? cursor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { cursor ?? "" };
        while (true)
        {
            var page = await ListExportsAsync(limit, cursor, cancellationToken).ConfigureAwait(false);
            foreach (var export in page.Exports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return export;
            }
            if (string.IsNullOrEmpty(page.NextCursor))
            {
                yield break;
            }
            if (!seen.Add(page.NextCursor))
            {
                throw new SynthientException("The export listing returned a repeated cursor.");
            }
            cursor = page.NextCursor;
        }
    }

    // Returns an export ID as a URL path, with the hour unpadded.
    private static string ExportPath(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var parts = id.Split('/');
        var validDay = DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day);
        if (id == ExportId.Latest || (validDay && parts.Length == 1))
        {
            return id;
        }
        if (validDay
            && parts.Length == 2
            && parts[1].Length is 1 or 2
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            && hour <= 23)
        {
            return ExportId.Hourly(day, hour);
        }
        throw new ArgumentException($"\"{id}\" is not a valid export ID.", nameof(id));
    }
}
