namespace Synthient;

/// <summary>
/// A Synthient dataset: a file published once a day, with no stream and no hourly exports. Get
/// datasets from the properties of <see cref="SynthientClient"/>, such as
/// <see cref="SynthientClient.Geolocation"/>. Their export IDs are <see cref="ExportId.Latest"/>,
/// the newest day, or a UTC day from <see cref="ExportId.Daily"/>. An hourly ID throws
/// <see cref="ArgumentException"/> before any request is sent.
/// </summary>
/// <remarks>
/// A day that is not published yet, or has no file in the requested format, fails with a 404
/// <see cref="ApiException"/>.
/// </remarks>
public sealed class Dataset
{
    private readonly ExportApi _exports;

    internal Dataset(Transport transport, string path)
    {
        _exports = new ExportApi(transport, path, hourly: false);
    }

    /// <summary>
    /// Returns one page of the dataset's exports, newest first, one per day. Use
    /// <see cref="ListAllExportsAsync"/> to read every page. It costs no credits.
    /// </summary>
    /// <param name="limit">The page size, from 1 to 500. Null uses the API default of 100.</param>
    /// <param name="cursor">Continues a listing from a previous page's <see cref="ExportPage.NextCursor"/>.</param>
    /// <param name="day">Lists only the export of a UTC day, given as "YYYY-MM-DD", such as from <see cref="ExportId.Daily"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<ExportPage> ListExportsAsync(int? limit = null, string? cursor = null, string? day = null, CancellationToken cancellationToken = default) =>
        _exports.ListAsync(limit, cursor, day, null, cancellationToken);

    /// <summary>
    /// Returns every export of the dataset, newest first, fetching pages lazily as enumeration
    /// proceeds. Leaving the loop early fetches no more pages. A failure on any page ends the
    /// enumeration with its exception.
    /// </summary>
    /// <param name="limit">The page size, from 1 to 500. Null uses the API default of 100.</param>
    /// <param name="cursor">Starts the listing from a previous page's <see cref="ExportPage.NextCursor"/>.</param>
    /// <param name="day">Lists only the export of a UTC day, given as "YYYY-MM-DD", such as from <see cref="ExportId.Daily"/>.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    public IAsyncEnumerable<Export> ListAllExportsAsync(int? limit = null, string? cursor = null, string? day = null, CancellationToken cancellationToken = default) =>
        _exports.ListAll(limit, cursor, day, null, cancellationToken);

    /// <summary>
    /// Returns the rows and bytes the dataset exported on each UTC day, oldest first. Days without
    /// an export are left out. It costs no credits.
    /// </summary>
    /// <param name="days">How many UTC days to cover, including today, from 1 to 366. Null uses the API default of 90.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<ExportVolume> GetExportVolumeAsync(int? days = null, CancellationToken cancellationToken = default) =>
        _exports.GetVolumeAsync(days, cancellationToken);

    /// <summary>
    /// Returns the checksum, size, and row count of an export's file, and the formats it is
    /// available in. It costs no credits.
    /// </summary>
    /// <param name="id">The export ID, <see cref="ExportId.Latest"/> or a UTC day.</param>
    /// <param name="format">
    /// The file to describe, such as <see cref="ExportFormat.Mmdb"/>. Null describes the default file:
    /// parquet for geolocation and tar.gz, the only format, for WHOIS.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<ExportMeta> GetExportMetaAsync(string id, string? format = null, CancellationToken cancellationToken = default) =>
        _exports.GetMetaAsync(id, ExportApi.Query(("format", format)), cancellationToken);

    /// <summary>
    /// Returns a presigned URL for an export's file. The URL is valid for 24 hours and grants access
    /// without an API key, so treat it as a secret.
    /// </summary>
    /// <param name="id">The export ID, <see cref="ExportId.Latest"/> or a UTC day.</param>
    /// <param name="format">The file to return, or null for the default. See <see cref="GetExportMetaAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public Task<Uri> GetDownloadUrlAsync(string id, string? format = null, CancellationToken cancellationToken = default) =>
        _exports.GetDownloadUrlAsync(id, ExportApi.Query(("format", format)), cancellationToken);

    /// <summary>
    /// Opens an export's file for reading, exactly as served, so a csv stays gzipped and the WHOIS
    /// archive stays packed. Dispose the returned stream when done. The API key is never sent to
    /// the storage host, and exceptions never include the presigned URL. Pass a token to each read
    /// to bound the rest of the transfer. The SDK does not verify the file: compare its SHA-256
    /// with the checksum in <see cref="Export.Formats"/> if needed.
    /// </summary>
    /// <param name="id">The export ID, <see cref="ExportId.Latest"/> or a UTC day.</param>
    /// <param name="format">The file to download, or null for the default. See <see cref="GetExportMetaAsync"/>.</param>
    /// <param name="cancellationToken">Cancels opening the file.</param>
    public Task<Stream> DownloadAsync(string id, string? format = null, CancellationToken cancellationToken = default) =>
        _exports.DownloadAsync(id, ExportApi.Query(("format", format)), cancellationToken);
}
