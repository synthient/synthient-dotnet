using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>One page of a feed's exports, newest first.</summary>
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

/// <summary>A parquet export of a feed.</summary>
public sealed class Export
{
    /// <summary>
    /// Identifies the export for downloads and metadata. On the first page, the newest hourly export
    /// may have the ID <see cref="ExportId.Latest"/>.
    /// </summary>
    [JsonPropertyName("id")]
    public required ExportId Id { get; init; }

    /// <summary>Whether the export covers an hour or a day. See <see cref="ExportKind"/>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>The UTC day of the export.</summary>
    [JsonPropertyName("date")]
    public required DateOnly Date { get; init; }

    /// <summary>The UTC hour of an hourly export, or null for a daily export.</summary>
    [JsonPropertyName("hour")]
    public int? Hour { get; init; }

    /// <summary>The size of the parquet file in bytes.</summary>
    [JsonPropertyName("size_bytes")]
    public required long SizeBytes { get; init; }

    /// <summary>The number of rows in the parquet file.</summary>
    [JsonPropertyName("row_count")]
    public required long RowCount { get; init; }

    /// <summary>The hex-encoded SHA-256 of the parquet file.</summary>
    [JsonPropertyName("checksum")]
    public required string Checksum { get; init; }

    /// <summary>When the export was produced.</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The API path of the export's download, including the version prefix.</summary>
    [JsonPropertyName("download_path")]
    public required string DownloadPath { get; init; }
}

/// <summary>A parquet export's checksum, size, and schema, without its contents.</summary>
public sealed class ExportMeta
{
    /// <summary>Identifies the export.</summary>
    [JsonPropertyName("id")]
    public required ExportId Id { get; init; }

    /// <summary>The feed's stream identifier.</summary>
    [JsonPropertyName("stream")]
    public required string Stream { get; init; }

    /// <summary>Whether the export covers an hour or a day. See <see cref="ExportKind"/>.</summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>The file format, "parquet".</summary>
    [JsonPropertyName("format")]
    public required string Format { get; init; }

    /// <summary>The start of the export's UTC day or hour.</summary>
    [JsonPropertyName("date")]
    public required DateTimeOffset Date { get; init; }

    /// <summary>The UTC hour of an hourly export, or null for a daily export.</summary>
    [JsonPropertyName("hour")]
    public int? Hour { get; init; }

    /// <summary>The number of rows in the parquet file.</summary>
    [JsonPropertyName("rows")]
    public required long Rows { get; init; }

    /// <summary>The size of the parquet file in bytes.</summary>
    [JsonPropertyName("size")]
    public required long Size { get; init; }

    /// <summary>The hex-encoded SHA-256 of the parquet file.</summary>
    [JsonPropertyName("checksum")]
    public required string Checksum { get; init; }

    /// <summary>When the export was produced.</summary>
    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The parquet columns.</summary>
    [JsonPropertyName("schema")]
    public required ParquetSchema Schema { get; init; }
}

/// <summary>The columns of a parquet export.</summary>
public sealed class ParquetSchema
{
    /// <summary>The columns in file order.</summary>
    [JsonPropertyName("fields")]
    public required IReadOnlyList<ParquetColumn> Fields { get; init; }
}

/// <summary>A column of a parquet export.</summary>
public sealed class ParquetColumn
{
    /// <summary>The column name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The column type. See <see cref="ParquetType"/> for known values.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }
}
