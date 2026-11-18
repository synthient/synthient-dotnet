using System.ComponentModel;
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
    private readonly ExportApi _exports;
    private readonly JsonTypeInfo<TEvent> _eventInfo;

    internal Feed(Transport transport, string path)
    {
        _transport = transport;
        _path = path;
        _exports = new ExportApi(transport, path, hourly: true);
        _eventInfo = Json.TypeInfo<TEvent>();
    }

    /// <summary>
    /// Returns one page of the feed's exports, newest first. Use <see cref="ListAllExportsAsync(int?, string?, string?, string?, CancellationToken)"/>
    /// to read every page. It costs no credits.
    /// </summary>
    /// <param name="limit">The page size, from 1 to 500. Null uses the API default of 100.</param>
    /// <param name="cursor">Continues a listing from a previous page's <see cref="ExportPage.NextCursor"/>.</param>
    /// <param name="day">Lists only the exports of a UTC day, given as "YYYY-MM-DD", such as from <see cref="ExportId.Daily"/>.</param>
    /// <param name="variant">
    /// Lists the <see cref="ExportVariant.Lite"/> files instead of the full ones. Only the proxies, anonymizers,
    /// and JA4T feeds publish them. The listed IDs do not name the variant, so pass it again to download.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<ExportPage> ListExportsAsync(
        int? limit = null,
        string? cursor = null,
        string? day = null,
        string? variant = null,
        CancellationToken cancellationToken = default) =>
        _exports.ListAsync(limit, cursor, day, variant, cancellationToken);

    /// <summary>Returns one page of the feed's exports. It is kept for binary compatibility with 1.0.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<ExportPage> ListExportsAsync(int? limit, string? cursor, CancellationToken cancellationToken) =>
        _exports.ListAsync(limit, cursor, null, null, cancellationToken);

    /// <summary>
    /// Returns every export of the feed, newest first, fetching pages lazily as enumeration
    /// proceeds. Leaving the loop early fetches no more pages. A failure on any page ends the
    /// enumeration with its exception.
    /// </summary>
    /// <param name="limit">The page size, from 1 to 500. Null uses the API default of 100.</param>
    /// <param name="cursor">Starts the listing from a previous page's <see cref="ExportPage.NextCursor"/>.</param>
    /// <param name="day">Lists only the exports of a UTC day, given as "YYYY-MM-DD", such as from <see cref="ExportId.Daily"/>.</param>
    /// <param name="variant">Lists the <see cref="ExportVariant.Lite"/> files instead. See <see cref="ListExportsAsync(int?, string?, string?, string?, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    public IAsyncEnumerable<Export> ListAllExportsAsync(
        int? limit = null,
        string? cursor = null,
        string? day = null,
        string? variant = null,
        CancellationToken cancellationToken = default) =>
        _exports.ListAll(limit, cursor, day, variant, cancellationToken);

    /// <summary>Returns every export of the feed. It is kept for binary compatibility with 1.0.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IAsyncEnumerable<Export> ListAllExportsAsync(int? limit, string? cursor, CancellationToken cancellationToken) =>
        _exports.ListAll(limit, cursor, null, null, cancellationToken);

    /// <summary>
    /// Returns the rows and bytes the feed exported on each UTC day, oldest first. Today is included
    /// and still growing, and days without exports are left out. It costs no credits.
    /// </summary>
    /// <param name="days">How many UTC days to cover, including today, from 1 to 366. Null uses the API default of 90.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<ExportVolume> GetExportVolumeAsync(int? days = null, CancellationToken cancellationToken = default) =>
        _exports.GetVolumeAsync(days, cancellationToken);

    /// <summary>Returns the checksum, size, row count, and parquet schema of an export. It costs no credits.</summary>
    /// <param name="id">The export ID. See <see cref="ExportId"/>.</param>
    /// <param name="variant">Describes the <see cref="ExportVariant.Lite"/> file instead of the full one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<ExportMeta> GetExportMetaAsync(string id, string? variant = null, CancellationToken cancellationToken = default) =>
        _exports.GetMetaAsync(id, ExportApi.Query(("variant", variant)), cancellationToken);

    /// <summary>Returns the checksum, size, row count, and parquet schema of an export. It is kept for binary compatibility with 1.0.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<ExportMeta> GetExportMetaAsync(string id, CancellationToken cancellationToken) =>
        GetExportMetaAsync(id, null, cancellationToken);

    /// <summary>
    /// Returns a presigned URL for an export's parquet file. The URL is valid for 24 hours and grants
    /// access without an API key, so treat it as a secret.
    /// </summary>
    /// <param name="id">The export ID. See <see cref="ExportId"/>.</param>
    /// <param name="variant">Returns the URL of the <see cref="ExportVariant.Lite"/> file instead of the full one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<Uri> GetDownloadUrlAsync(string id, string? variant = null, CancellationToken cancellationToken = default) =>
        _exports.GetDownloadUrlAsync(id, ExportApi.Query(("variant", variant)), cancellationToken);

    /// <summary>Returns a presigned URL for an export's parquet file. It is kept for binary compatibility with 1.0.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<Uri> GetDownloadUrlAsync(string id, CancellationToken cancellationToken) =>
        GetDownloadUrlAsync(id, null, cancellationToken);

    /// <summary>
    /// Opens an export's parquet file for reading. Dispose the returned stream when done. The API
    /// key is never sent to the storage host, and exceptions never include the presigned URL. Pass
    /// a token to each read to bound the rest of the transfer. The SDK does not verify the file:
    /// compare its SHA-256 with <see cref="Export.Checksum"/> if needed.
    /// </summary>
    /// <param name="id">The export ID. See <see cref="ExportId"/>.</param>
    /// <param name="variant">Downloads the <see cref="ExportVariant.Lite"/> file instead of the full one.</param>
    /// <param name="cancellationToken">Cancels opening the file.</param>
    public Task<Stream> DownloadAsync(string id, string? variant = null, CancellationToken cancellationToken = default) =>
        _exports.DownloadAsync(id, ExportApi.Query(("variant", variant)), cancellationToken);

    /// <summary>Opens an export's parquet file for reading. It is kept for binary compatibility with 1.0.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<Stream> DownloadAsync(string id, CancellationToken cancellationToken) =>
        DownloadAsync(id, null, cancellationToken);

    /// <summary>
    /// Streams the feed's events in real time. Each enumeration opens a new connection, which the
    /// server can close at any time, such as during a deploy, ending the enumeration without an
    /// error. Events sent between connections are not replayed, so reconnect promptly to keep up.
    /// Leaving the loop closes the connection.
    /// </summary>
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
}
