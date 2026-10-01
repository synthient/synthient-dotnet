using System.Net;
using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>
/// The intelligence Synthient holds for an IP address. <see cref="Network"/>,
/// <see cref="Location"/>, and <see cref="Intelligence"/> are null when the API has no data for
/// them.
/// </summary>
public sealed class IPInfo
{
    /// <summary>The address that was looked up, as the API canonicalized it.</summary>
    [JsonPropertyName("ip")]
    public required IPAddress IP { get; init; }

    /// <summary>The network that announces the address.</summary>
    [JsonPropertyName("network")]
    public Network? Network { get; init; }

    /// <summary>The address's geolocation.</summary>
    [JsonPropertyName("location")]
    public Location? Location { get; init; }

    /// <summary>The risk score and the proxy, VPN, and device observations for the address.</summary>
    [JsonPropertyName("intelligence")]
    public Intelligence? Intelligence { get; init; }
}

/// <summary>The network that announces an address.</summary>
public sealed class Network
{
    /// <summary>The autonomous system number, or 0 if the address is not routed.</summary>
    [JsonPropertyName("asn")]
    public required long Asn { get; init; }

    /// <summary>The name of the internet service provider.</summary>
    [JsonPropertyName("isp")]
    public required string Isp { get; init; }

    /// <summary>The network type. See <see cref="NetworkType"/> for known values.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>The organization the address is registered to, or null if unknown.</summary>
    [JsonPropertyName("org")]
    public string? Org { get; init; }

    /// <summary>The network operator's domain, or null if unknown.</summary>
    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    /// <summary>The operator's abuse contact email, or null if unknown.</summary>
    [JsonPropertyName("abuse_email")]
    public string? AbuseEmail { get; init; }

    /// <summary>The operator's abuse contact phone number, or null if unknown.</summary>
    [JsonPropertyName("abuse_phone")]
    public string? AbusePhone { get; init; }
}

/// <summary>The geolocation of an address.</summary>
public sealed class Location
{
    /// <summary>The ISO 3166-1 alpha-2 country code.</summary>
    [JsonPropertyName("country")]
    public required string Country { get; init; }

    /// <summary>The state or region.</summary>
    [JsonPropertyName("state")]
    public required string State { get; init; }

    /// <summary>The city.</summary>
    [JsonPropertyName("city")]
    public required string City { get; init; }

    /// <summary>The IANA time zone name.</summary>
    [JsonPropertyName("timezone")]
    public required string Timezone { get; init; }

    /// <summary>The latitude in degrees.</summary>
    [JsonPropertyName("latitude")]
    public required double Latitude { get; init; }

    /// <summary>The longitude in degrees.</summary>
    [JsonPropertyName("longitude")]
    public required double Longitude { get; init; }

    /// <summary>The geohash of the coordinates.</summary>
    [JsonPropertyName("geo_hash")]
    public required string GeoHash { get; init; }
}

/// <summary>Synthient's assessment of an address.</summary>
public sealed class Intelligence
{
    /// <summary>The risk of the address, from 0 to 100.</summary>
    [JsonPropertyName("risk_score")]
    public required int RiskScore { get; init; }

    /// <summary>
    /// The kinds of services seen on the address, such as "RESIDENTIAL_PROXY". New values may be
    /// added.
    /// </summary>
    [JsonPropertyName("categories")]
    public required IReadOnlyList<string> Categories { get; init; }

    /// <summary>Behaviors observed from the address. New values may be added.</summary>
    [JsonPropertyName("behavior")]
    public required IReadOnlyList<string> Behavior { get; init; }

    /// <summary>
    /// The proxy, VPN, and anonymizer services seen using the address. An empty list does not mean
    /// the address is clean.
    /// </summary>
    [JsonPropertyName("providers")]
    public required IReadOnlyList<Provider> Providers { get; init; }

    /// <summary>The device types seen behind the address.</summary>
    [JsonPropertyName("devices")]
    public required IReadOnlyList<Device> Devices { get; init; }
}

/// <summary>A proxy, VPN, or anonymizer service seen using an address.</summary>
public sealed class Provider
{
    /// <summary>The service tag, such as "BRIGHTDATA".</summary>
    [JsonPropertyName("provider")]
    public required string Name { get; init; }

    /// <summary>The service category.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>When the service was last seen using the address.</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }
}

/// <summary>A device type seen behind an address.</summary>
public sealed class Device
{
    /// <summary>The operating system. See <see cref="DeviceOS"/> for known values.</summary>
    [JsonPropertyName("os")]
    public required string OS { get; init; }

    /// <summary>The operating system version.</summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>When the device was last seen.</summary>
    [JsonPropertyName("last_seen")]
    public required DateTimeOffset LastSeen { get; init; }
}

internal sealed class IPBatchRequest
{
    [JsonPropertyName("ips")]
    public required IReadOnlyList<string> IPs { get; init; }
}

internal sealed class IPBatchResponse
{
    [JsonPropertyName("results")]
    public required IReadOnlyList<IPInfo> Results { get; init; }
}
