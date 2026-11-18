using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>One page of a feed's or dataset's exports, newest first.</summary>
public sealed class ExportPage
{
    /// <summary>The feed's stream identifier, such as "proxies".</summary>
    [JsonPropertyName("stream")]
    public required string Stream { get; init; }

    /// <summary>The exports on this page.</summary>
    [JsonPropertyName("feeds")]
    public required IReadOnlyList<Export> Exports { get; init; }

    /// <summary>Fetches the next page. It is null or empty on the last page.</summary>
    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; init; }
}

/// <summary>An export of a feed or dataset.</summary>
public sealed class Export
{
    /// <summary>
    /// Identifies the export for downloads and metadata. On the first page, the newest hourly export
    /// may have the ID <see cref="ExportId.Latest"/>.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Whether the export covers an hour or a day. See <see cref="ExportKind"/>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>The UTC day of the export.</summary>
    [JsonPropertyName("date")]
    public required DateOnly Date { get; init; }

    /// <summary>The UTC hour of an hourly export, or null for a daily export.</summary>
    [JsonPropertyName("hour")]
    public int? Hour { get; init; }

    /// <summary>The size in bytes of the first file in <see cref="Formats"/>.</summary>
    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }

    /// <summary>The number of rows. For WHOIS, it is the sum over <see cref="Entities"/>.</summary>
    [JsonPropertyName("row_count")]
    public required long RowCount { get; init; }

    /// <summary>The hex-encoded SHA-256 of the first file in <see cref="Formats"/>.</summary>
    [JsonPropertyName("checksum")]
    public required string Checksum { get; init; }

    /// <summary>When the export was produced.</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The API path of the export's download, including the version prefix.</summary>
    [JsonPropertyName("download_path")]
    public required string DownloadPath { get; init; }

    /// <summary>Every file of the export, its default format first. Feeds have one parquet file.</summary>
    [JsonPropertyName("formats")]
    [JsonRequired]
    public IReadOnlyList<ExportFile> Formats { get; init; } = [];

    /// <summary>The parquet files inside a WHOIS archive, or null for any other export.</summary>
    [JsonPropertyName("entities")]
    public IReadOnlyList<ExportEntity>? Entities { get; init; }

    /// <summary>
    /// <see cref="ExportVariant.Lite"/> on a lite listing, whose sizes, checksums, and download path
    /// describe the lite file, or null otherwise. <see cref="Id"/> does not name the variant.
    /// </summary>
    [JsonPropertyName("variant")]
    public string? Variant { get; init; }
}

/// <summary>An export's checksum, size, and schema, without its contents.</summary>
public sealed class ExportMeta
{
    /// <summary>Identifies the export.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The feed's stream identifier.</summary>
    [JsonPropertyName("stream")]
    public required string Stream { get; init; }

    /// <summary>Whether the export covers an hour or a day. See <see cref="ExportKind"/>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>
    /// The format of the file that <see cref="Checksum"/> and <see cref="Size"/> describe: the one
    /// requested, or else the default. See <see cref="ExportFormat"/> for known values.
    /// </summary>
    [JsonPropertyName("format")]
    public required string Format { get; init; }

    /// <summary>The start of the export's UTC day or hour.</summary>
    [JsonPropertyName("date")]
    public required DateTimeOffset Date { get; init; }

    /// <summary>The UTC hour of an hourly export, or null for a daily export.</summary>
    [JsonPropertyName("hour")]
    public int? Hour { get; init; }

    /// <summary>The number of rows.</summary>
    [JsonPropertyName("rows")]
    public required long Rows { get; init; }

    /// <summary>The size of the file in bytes.</summary>
    [JsonPropertyName("size")]
    public required long Size { get; init; }

    /// <summary>The hex-encoded SHA-256 of the file.</summary>
    [JsonPropertyName("checksum")]
    public required string Checksum { get; init; }

    /// <summary>When the export was produced.</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The parquet columns. It has none for a WHOIS archive, whose columns are in <see cref="Entities"/>.</summary>
    [JsonPropertyName("schema")]
    public required ParquetSchema Schema { get; init; }

    /// <summary>Every file of the export, its default format first. Feeds have one parquet file.</summary>
    [JsonPropertyName("formats")]
    [JsonRequired]
    public IReadOnlyList<ExportFile> Formats { get; init; } = [];

    /// <summary>The parquet files inside a WHOIS archive, each with its schema, or null for any other export.</summary>
    [JsonPropertyName("entities")]
    public IReadOnlyList<ExportEntity>? Entities { get; init; }

    /// <summary><see cref="ExportVariant.Lite"/> when the lite file was requested, or null otherwise.</summary>
    [JsonPropertyName("variant")]
    public string? Variant { get; init; }
}

/// <summary>The columns of a parquet file.</summary>
public sealed class ParquetSchema
{
    /// <summary>The columns in file order.</summary>
    /// <remarks>The API once sent null for a WHOIS archive, so null or missing reads as empty.</remarks>
    [AllowNull]
    [JsonPropertyName("fields")]
    public IReadOnlyList<ParquetColumn> Fields { get; init => field = value ?? []; } = [];
}

/// <summary>A column of a parquet file.</summary>
public sealed class ParquetColumn
{
    /// <summary>The column name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The column type. See <see cref="ParquetType"/> for known values.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }
}

/// <summary>One file of an export.</summary>
public sealed class ExportFile
{
    /// <summary>The file format. See <see cref="ExportFormat"/> for known values.</summary>
    [JsonPropertyName("format")]
    public required string Format { get; init; }

    /// <summary>The size of the file in bytes.</summary>
    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }

    /// <summary>The hex-encoded SHA-256 of the file as downloaded, so of the gzipped file for csv.</summary>
    [JsonPropertyName("checksum")]
    public required string Checksum { get; init; }
}

/// <summary>A parquet file inside a WHOIS archive.</summary>
public sealed class ExportEntity
{
    /// <summary>The kind of record in the file. See <see cref="WhoisEntity"/> for known values.</summary>
    [JsonPropertyName("entity")]
    public required string Entity { get; init; }

    /// <summary>The number of rows.</summary>
    [JsonPropertyName("row_count")]
    public required long RowCount { get; init; }

    /// <summary>The size of the file in bytes.</summary>
    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }

    /// <summary>The hex-encoded SHA-256 of the file.</summary>
    [JsonPropertyName("checksum")]
    public required string Checksum { get; init; }

    /// <summary>The parquet columns. Only <see cref="ExportMeta.Entities"/> has them, so it is null in a listing.</summary>
    [JsonPropertyName("schema")]
    public ParquetSchema? Schema { get; init; }
}

/// <summary>How much a feed or dataset exported on each UTC day.</summary>
public sealed class ExportVolume
{
    /// <summary>The feed's or dataset's stream identifier, such as "proxies".</summary>
    [JsonPropertyName("stream")]
    public required string Stream { get; init; }

    /// <summary>The days, oldest first. Today is included and still growing, and days without exports are left out.</summary>
    [JsonPropertyName("days")]
    public required IReadOnlyList<ExportVolumeDay> Days { get; init; }
}

/// <summary>How much a feed or dataset exported on one UTC day.</summary>
public sealed class ExportVolumeDay
{
    /// <summary>The UTC day.</summary>
    [JsonPropertyName("date")]
    public required DateOnly Date { get; init; }

    /// <summary>
    /// <see cref="ExportKind.Daily"/> when the day's daily export gives the totals, or
    /// <see cref="ExportKind.Hourly"/> when its hourly exports were summed, as for today.
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>The number of files counted: 1 for a daily export, or the hourly exports so far, up to 24.</summary>
    [JsonPropertyName("snapshots")]
    public required int Snapshots { get; init; }

    /// <summary>The number of rows exported.</summary>
    [JsonPropertyName("row_count")]
    public required long RowCount { get; init; }

    /// <summary>The number of bytes exported.</summary>
    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }
}
