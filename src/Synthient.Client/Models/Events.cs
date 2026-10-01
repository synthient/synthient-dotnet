using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>A proxy sighting from the proxies feed. The API may omit any field.</summary>
public sealed class ProxyEvent
{
    /// <summary>The proxy exit address.</summary>
    [JsonPropertyName("ip")]
    public string? IP { get; init; }

    /// <summary>The proxy service tag.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>The proxy category. See <see cref="ProxyType"/> for known values.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The ISO 3166-1 alpha-2 country of the address.</summary>
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    /// <summary>The autonomous system number of the address.</summary>
    [JsonPropertyName("asn")]
    public long? Asn { get; init; }

    /// <summary>When the proxy was seen.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>A VPN or anonymizer IP range from the anonymizers feed. The API may omit any field.</summary>
public sealed class AnonymizerEvent
{
    /// <summary>The first address of the range.</summary>
    [JsonPropertyName("range_start")]
    public string? RangeStart { get; init; }

    /// <summary>The last address of the range.</summary>
    [JsonPropertyName("range_end")]
    public string? RangeEnd { get; init; }

    /// <summary>The service tag.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>The service category. See <see cref="ProxyType"/> for known values.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The ISO 3166-1 alpha-2 country of <see cref="RangeStart"/>.</summary>
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    /// <summary>The autonomous system number of <see cref="RangeStart"/>.</summary>
    [JsonPropertyName("asn")]
    public long? Asn { get; init; }

    /// <summary>When the range was seen.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>A torrent observed on the BitTorrent DHT or a tracker. The API may omit any field.</summary>
public sealed class TorrentEvent
{
    /// <summary>The hex-encoded SHA-1 info hash.</summary>
    [JsonPropertyName("info_hash")]
    public string? InfoHash { get; init; }

    /// <summary>The torrent name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The torrent's magnet link.</summary>
    [JsonPropertyName("magnet_uri")]
    public string? MagnetUri { get; init; }

    /// <summary>The total size of the torrent's files in bytes.</summary>
    [JsonPropertyName("total_size")]
    public long? TotalSize { get; init; }

    /// <summary>The piece size in bytes.</summary>
    [JsonPropertyName("piece_length")]
    public long? PieceLength { get; init; }

    /// <summary>The number of files in the torrent.</summary>
    [JsonPropertyName("file_count")]
    public int? FileCount { get; init; }

    /// <summary>The torrent's files.</summary>
    [JsonPropertyName("files")]
    public IReadOnlyList<TorrentFile>? Files { get; init; }

    /// <summary>The peers seen sharing the torrent.</summary>
    [JsonPropertyName("peers")]
    public IReadOnlyList<TorrentPeer>? Peers { get; init; }

    /// <summary>When the torrent was seen.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>A file in a torrent.</summary>
public sealed class TorrentFile
{
    /// <summary>The file path within the torrent.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>The file size in bytes.</summary>
    [JsonPropertyName("length")]
    public long? Length { get; init; }
}

/// <summary>A peer seen sharing a torrent.</summary>
public sealed class TorrentPeer
{
    /// <summary>The peer's address.</summary>
    [JsonPropertyName("ip")]
    public string? IP { get; init; }

    /// <summary>The peer's port.</summary>
    [JsonPropertyName("port")]
    public int? Port { get; init; }

    /// <summary>Where the peer was found, such as "DHT", "PEX", or "tracker".</summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>Whether the peer supports encrypted connections.</summary>
    [JsonPropertyName("encrypted")]
    public bool? Encrypted { get; init; }
}

/// <summary>A JA4T TCP fingerprint sighting. The API may omit any field.</summary>
public sealed class Ja4tEvent
{
    /// <summary>The address the fingerprint was seen from.</summary>
    [JsonPropertyName("ip")]
    public string? IP { get; init; }

    /// <summary>The JA4T fingerprint.</summary>
    [JsonPropertyName("ja4t")]
    public string? Ja4t { get; init; }

    /// <summary>The service tag of the address.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>The service category. See <see cref="ProxyType"/> for known values.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The ISO 3166-1 alpha-2 country of the address.</summary>
    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    /// <summary>The autonomous system number of the address.</summary>
    [JsonPropertyName("asn")]
    public long? Asn { get; init; }

    /// <summary>When the fingerprint was seen.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }
}
