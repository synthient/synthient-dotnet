using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>The honeypot traffic Synthient has observed for a domain.</summary>
public sealed class DomainInfo
{
    /// <summary>The domain that was looked up.</summary>
    [JsonPropertyName("domain")]
    public required string Domain { get; init; }

    /// <summary>Whether the domain had events in the last 24 hours. See <see cref="DomainStatus"/>.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Aggregate event counts.</summary>
    [JsonPropertyName("stats")]
    public required DomainStats Stats { get; init; }

    /// <summary>Daily event counts.</summary>
    [JsonPropertyName("time_series")]
    public required IReadOnlyList<TimeSeriesPoint> TimeSeries { get; init; }

    /// <summary>The number of distinct source addresses.</summary>
    [JsonPropertyName("unique_ips")]
    public required UniqueIPCounts UniqueIPs { get; init; }

    /// <summary>The network that sent the most events, or null if there were none.</summary>
    [JsonPropertyName("top_asn")]
    public AsnCount? TopAsn { get; init; }

    /// <summary>The most targeted subdomains.</summary>
    [JsonPropertyName("top_subdomains")]
    public required IReadOnlyList<SubdomainCount> TopSubdomains { get; init; }

    /// <summary>The most targeted ports.</summary>
    [JsonPropertyName("top_ports")]
    public required IReadOnlyList<PortCount> TopPorts { get; init; }

    /// <summary>Event counts by source country.</summary>
    [JsonPropertyName("geo_distribution")]
    public required IReadOnlyList<CountryCount> GeoDistribution { get; init; }

    /// <summary>
    /// The last 30 days of events by UTC day of week and hour, indexed as
    /// <c>HourDowHeatmap[day][hour]</c>. Day 0 is Monday, unlike <see cref="DayOfWeek"/>, where 0
    /// is Sunday.
    /// </summary>
    [JsonPropertyName("hour_dow_heatmap")]
    public required IReadOnlyList<IReadOnlyList<long>> HourDowHeatmap { get; init; }

    /// <summary>The hourly activity pattern.</summary>
    [JsonPropertyName("activity_stats")]
    public required ActivityStats ActivityStats { get; init; }

    /// <summary>The most recent events, capped by the lookup's limit.</summary>
    [JsonPropertyName("recent_events")]
    public required IReadOnlyList<DomainEvent> RecentEvents { get; init; }
}

/// <summary>Aggregate event counts for a domain.</summary>
public sealed class DomainStats
{
    /// <summary>The number of events in the last 24 hours.</summary>
    [JsonPropertyName("events_24h")]
    public required long Events24h { get; init; }

    /// <summary>The number of events in the last 30 days.</summary>
    [JsonPropertyName("total_events_30d")]
    public required long TotalEvents30d { get; init; }
}

/// <summary>The number of distinct source addresses seen for a domain.</summary>
public sealed class UniqueIPCounts
{
    /// <summary>The count for the last 24 hours.</summary>
    [JsonPropertyName("value_24h")]
    public required long Value24h { get; init; }

    /// <summary>The count for the last 30 days.</summary>
    [JsonPropertyName("value_30d")]
    public required long Value30d { get; init; }

    /// <summary>Hourly counts for the last 24 hours.</summary>
    [JsonPropertyName("sparkline_24h")]
    public required IReadOnlyList<long> Sparkline24h { get; init; }
}

/// <summary>The event counts for one day.</summary>
public sealed class TimeSeriesPoint
{
    /// <summary>The start of the UTC day.</summary>
    [JsonPropertyName("date")]
    public required DateTimeOffset Date { get; init; }

    /// <summary>The number of events that day.</summary>
    [JsonPropertyName("events")]
    public required long Events { get; init; }

    /// <summary>The number of distinct source addresses that day.</summary>
    [JsonPropertyName("unique_ips")]
    public required long UniqueIPs { get; init; }
}

/// <summary>An autonomous system and its event count.</summary>
public sealed class AsnCount
{
    /// <summary>The autonomous system number.</summary>
    [JsonPropertyName("asn")]
    public required long Asn { get; init; }

    /// <summary>The number of events from the network.</summary>
    [JsonPropertyName("events")]
    public required long Events { get; init; }
}

/// <summary>A subdomain and its event count.</summary>
public sealed class SubdomainCount
{
    /// <summary>The targeted subdomain.</summary>
    [JsonPropertyName("subdomain")]
    public required string Subdomain { get; init; }

    /// <summary>The number of events that targeted it.</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }
}

/// <summary>A port and its event count.</summary>
public sealed class PortCount
{
    /// <summary>The targeted port.</summary>
    [JsonPropertyName("port")]
    public required int Port { get; init; }

    /// <summary>The number of events that targeted it.</summary>
    [JsonPropertyName("count")]
    public required long Count { get; init; }
}

/// <summary>A source country and its counts.</summary>
public sealed class CountryCount
{
    /// <summary>The ISO 3166-1 alpha-2 country code.</summary>
    [JsonPropertyName("country_code")]
    public required string CountryCode { get; init; }

    /// <summary>The number of events from the country.</summary>
    [JsonPropertyName("events")]
    public required long Events { get; init; }

    /// <summary>The number of distinct addresses from the country.</summary>
    [JsonPropertyName("unique_ips")]
    public required long UniqueIPs { get; init; }
}

/// <summary>The hourly activity pattern of a domain.</summary>
public sealed class ActivityStats
{
    /// <summary>A sentence describing how events are spread over the last 30 days. See <see cref="Synthient.Cadence"/>.</summary>
    [JsonPropertyName("cadence")]
    public required string Cadence { get; init; }

    /// <summary>The median number of events per hour.</summary>
    [JsonPropertyName("median_per_hour")]
    public required long MedianPerHour { get; init; }

    /// <summary>The 95th percentile of events per hour.</summary>
    [JsonPropertyName("p95_per_hour")]
    public required long P95PerHour { get; init; }

    /// <summary>The busiest UTC hour of the day.</summary>
    [JsonPropertyName("peak_hour")]
    public required int PeakHour { get; init; }

    /// <summary>The quietest UTC hour of the day.</summary>
    [JsonPropertyName("quiet_hour")]
    public required int QuietHour { get; init; }
}

/// <summary>A single honeypot event for a domain.</summary>
public sealed class DomainEvent
{
    /// <summary>When the event happened.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The full proxy exit address that carried the event. The API names the field
    /// source_ip_masked, but the address is not masked.
    /// </summary>
    [JsonPropertyName("source_ip_masked")]
    public required string SourceIP { get; init; }

    /// <summary>The subdomain that was targeted.</summary>
    [JsonPropertyName("target_subdomain")]
    public required string TargetSubdomain { get; init; }

    /// <summary>The source country.</summary>
    [JsonPropertyName("country_code")]
    public required string CountryCode { get; init; }

    /// <summary>The targeted port.</summary>
    [JsonPropertyName("port")]
    public required int Port { get; init; }
}
