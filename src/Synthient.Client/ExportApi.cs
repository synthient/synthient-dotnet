using System.Globalization;
using System.Runtime.CompilerServices;

namespace Synthient;

// The export routes that feeds and datasets share. Datasets publish only daily exports, so they
// pass hourly: false to reject hourly IDs, whose routes do not exist.
internal sealed class ExportApi(Transport transport, string path, bool hourly)
{
    internal static string Query(params (string Name, string? Value)[] parameters)
    {
        var pairs = parameters
            .Where(parameter => !string.IsNullOrEmpty(parameter.Value))
            .Select(parameter => parameter.Name + "=" + Uri.EscapeDataString(parameter.Value!))
            .ToArray();
        return pairs.Length > 0 ? "?" + string.Join('&', pairs) : "";
    }

    public Task<ExportPage> ListAsync(int? limit, string? cursor, string? day, string? variant, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit ?? 1, 1, nameof(limit));
        var query = Query(("cursor", cursor), ("limit", limit?.ToString(CultureInfo.InvariantCulture)), ("day", day), ("variant", variant));
        return transport.GetJsonAsync<ExportPage>(new ApiRequest(HttpMethod.Get, path + "/export" + query, RetryPolicy.Transient), cancellationToken);
    }

    public IAsyncEnumerable<Export> ListAll(int? limit, string? cursor, string? day, string? variant, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit ?? 1, 1, nameof(limit));
        return ListAllCoreAsync(limit, cursor, day, variant, cancellationToken);
    }

    public Task<ExportVolume> GetVolumeAsync(int? days, CancellationToken cancellationToken)
    {
        var query = Query(("days", days?.ToString(CultureInfo.InvariantCulture)));
        return transport.GetJsonAsync<ExportVolume>(new ApiRequest(HttpMethod.Get, path + "/export/daily" + query, RetryPolicy.Transient), cancellationToken);
    }

    public Task<ExportMeta> GetMetaAsync(string id, string query, CancellationToken cancellationToken)
    {
        var request = new ApiRequest(HttpMethod.Get, path + "/export/" + ExportPath(id) + "/meta" + query, RetryPolicy.Transient);
        return transport.GetJsonAsync<ExportMeta>(request, cancellationToken);
    }

    public async Task<Uri> GetDownloadUrlAsync(string id, string query, CancellationToken cancellationToken)
    {
        var request = new ApiRequest(HttpMethod.Get, path + "/export/" + ExportPath(id) + query, RetryPolicy.Transient);
        using var response = await transport.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode >= 400)
        {
            throw await transport.CreateApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        // The SDK sends no conditional headers, but a custom handler could turn this into a 304.
        if (response.StatusCode is not (301 or 302 or 303 or 307 or 308))
        {
            throw new SynthientException($"Expected a redirect to the export, got status {response.StatusCode}.");
        }
        return Transport.ResolveRedirect(response.RequestUri, response.Metadata.Header("Location"));
    }

    public async Task<Stream> DownloadAsync(string id, string query, CancellationToken cancellationToken)
    {
        var location = await GetDownloadUrlAsync(id, query, cancellationToken).ConfigureAwait(false);
        return await transport.DownloadAsync(location, cancellationToken).ConfigureAwait(false);
    }

    private async IAsyncEnumerable<Export> ListAllCoreAsync(
        int? limit,
        string? cursor,
        string? day,
        string? variant,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { cursor ?? "" };
        while (true)
        {
            var page = await ListAsync(limit, cursor, day, variant, cancellationToken).ConfigureAwait(false);
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

    // Writes the hour unpadded, as the API does in export IDs and download paths.
    private string ExportPath(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var parts = id.Split('/');
        var validDay = DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day);
        if (id == ExportId.Latest || (validDay && parts.Length == 1))
        {
            return id;
        }
        if (hourly
            && validDay
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
