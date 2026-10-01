using System.Text.Json.Serialization;

namespace Synthient;

/// <summary>The Helios honeypot sensor that captured an event. The API may omit any field.</summary>
public sealed class HeliosMeta
{
    /// <summary>The proxy exit address the traffic arrived through.</summary>
    [JsonPropertyName("proxy_ip")]
    public string? ProxyIP { get; init; }

    /// <summary>The sensor that captured the traffic.</summary>
    [JsonPropertyName("server")]
    public string? Server { get; init; }

    /// <summary>The sensor pool.</summary>
    [JsonPropertyName("pool_id")]
    public string? PoolId { get; init; }

    /// <summary>The proxy service the traffic arrived through.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }
}

/// <summary>An HTTP request captured by a Helios honeypot.</summary>
public sealed class HeliosHttpEvent
{
    /// <summary>When the request was captured.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The proxy tunnel the request arrived through, an unsigned 64-bit ID.</summary>
    [JsonPropertyName("tunnel_id")]
    public required ulong TunnelId { get; init; }

    /// <summary>The host the tunnel was opened to.</summary>
    [JsonPropertyName("domain")]
    public required string Domain { get; init; }

    /// <summary>The destination port.</summary>
    [JsonPropertyName("port")]
    public required int Port { get; init; }

    /// <summary>The protocol, "http".</summary>
    [JsonPropertyName("protocol")]
    public required string Protocol { get; init; }

    /// <summary>The capturing sensor.</summary>
    [JsonPropertyName("meta")]
    public required HeliosMeta Meta { get; init; }

    /// <summary>The parsed request.</summary>
    [JsonPropertyName("details")]
    public HttpRequestDetails? Details { get; init; }

    /// <summary>The raw request bytes.</summary>
    [JsonPropertyName("raw")]
    public byte[]? Raw { get; init; }
}

/// <summary>A parsed HTTP request. The API may omit any field.</summary>
public sealed class HttpRequestDetails
{
    /// <summary>The request method.</summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>The request target.</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; init; }

    /// <summary>The HTTP version, such as "HTTP/1.1".</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>The request headers.</summary>
    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>A TLS ClientHello captured by a Helios honeypot.</summary>
public sealed class HeliosHttpsEvent
{
    /// <summary>When the handshake was captured.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The proxy tunnel the handshake arrived through, an unsigned 64-bit ID.</summary>
    [JsonPropertyName("tunnel_id")]
    public required ulong TunnelId { get; init; }

    /// <summary>The host the tunnel was opened to.</summary>
    [JsonPropertyName("domain")]
    public required string Domain { get; init; }

    /// <summary>The destination port.</summary>
    [JsonPropertyName("port")]
    public required int Port { get; init; }

    /// <summary>The protocol, "https".</summary>
    [JsonPropertyName("protocol")]
    public required string Protocol { get; init; }

    /// <summary>The capturing sensor.</summary>
    [JsonPropertyName("meta")]
    public required HeliosMeta Meta { get; init; }

    /// <summary>The parsed ClientHello, or null if the sensor could not parse the handshake.</summary>
    [JsonPropertyName("details")]
    public TlsClientHello? Details { get; init; }

    /// <summary>Always null, because the sensors do not capture handshake bytes.</summary>
    [JsonPropertyName("raw")]
    public string? Raw { get; init; }
}

/// <summary>
/// A TLS ClientHello parsed by a Helios sensor. Fields the sensor could not parse are null, and new
/// fields may be added.
/// </summary>
public sealed class TlsClientHello
{
    /// <summary>The record layer version, such as "TLS 1.0".</summary>
    [JsonPropertyName("record_version")]
    public string? RecordVersion { get; init; }

    /// <summary>The ClientHello version, such as "TLS 1.2".</summary>
    [JsonPropertyName("handshake_version")]
    public string? HandshakeVersion { get; init; }

    /// <summary>The hex-encoded 32-byte client random.</summary>
    [JsonPropertyName("client_random")]
    public string? ClientRandom { get; init; }

    /// <summary>The hex-encoded session ID.</summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>The length of the session ID in bytes.</summary>
    [JsonPropertyName("session_id_length")]
    public int? SessionIdLength { get; init; }

    /// <summary>The offered cipher suites.</summary>
    [JsonPropertyName("cipher_suites")]
    public IReadOnlyList<TlsCodeName>? CipherSuites { get; init; }

    /// <summary>The offered compression methods.</summary>
    [JsonPropertyName("compression_methods")]
    public IReadOnlyList<int>? CompressionMethods { get; init; }

    /// <summary>The extensions, in wire order.</summary>
    [JsonPropertyName("extensions")]
    public IReadOnlyList<TlsExtension>? Extensions { get; init; }

    /// <summary>The server_name extension value.</summary>
    [JsonPropertyName("sni")]
    public string? Sni { get; init; }

    /// <summary>The offered application protocols.</summary>
    [JsonPropertyName("alpn")]
    public IReadOnlyList<string>? Alpn { get; init; }

    /// <summary>The offered TLS versions.</summary>
    [JsonPropertyName("supported_versions")]
    public IReadOnlyList<string>? SupportedVersions { get; init; }

    /// <summary>The offered key exchange groups.</summary>
    [JsonPropertyName("supported_groups")]
    public IReadOnlyList<TlsCodeName>? SupportedGroups { get; init; }

    /// <summary>The offered signature algorithms.</summary>
    [JsonPropertyName("signature_algorithms")]
    public IReadOnlyList<TlsCodeName>? SignatureAlgorithms { get; init; }

    /// <summary>The offered elliptic curve point formats.</summary>
    [JsonPropertyName("ec_point_formats")]
    public IReadOnlyList<string>? EcPointFormats { get; init; }

    /// <summary>The groups the client sent key shares for.</summary>
    [JsonPropertyName("key_share_groups")]
    public IReadOnlyList<string>? KeyShareGroups { get; init; }

    /// <summary>The offered pre-shared key exchange modes.</summary>
    [JsonPropertyName("psk_key_exchange_modes")]
    public IReadOnlyList<string>? PskKeyExchangeModes { get; init; }

    /// <summary>The offered certificate compression algorithms.</summary>
    [JsonPropertyName("compress_certificate")]
    public IReadOnlyList<string>? CompressCertificate { get; init; }

    /// <summary>Whether the ClientHello contains GREASE values.</summary>
    [JsonPropertyName("has_grease")]
    public bool? HasGrease { get; init; }

    /// <summary>Whether the client offered application settings (ALPS).</summary>
    [JsonPropertyName("application_settings")]
    public bool? ApplicationSettings { get; init; }

    /// <summary>Whether the client offered delegated credentials.</summary>
    [JsonPropertyName("delegated_credentials")]
    public bool? DelegatedCredentials { get; init; }

    /// <summary>Whether the client offered encrypt-then-MAC.</summary>
    [JsonPropertyName("encrypt_then_mac")]
    public bool? EncryptThenMac { get; init; }

    /// <summary>Whether the client offered the extended master secret.</summary>
    [JsonPropertyName("extended_master_secret")]
    public bool? ExtendedMasterSecret { get; init; }

    /// <summary>Whether the client offered post-handshake authentication.</summary>
    [JsonPropertyName("post_handshake_auth")]
    public bool? PostHandshakeAuth { get; init; }

    /// <summary>Whether the client sent renegotiation info.</summary>
    [JsonPropertyName("renegotiation_info")]
    public bool? RenegotiationInfo { get; init; }

    /// <summary>Whether the client requested signed certificate timestamps.</summary>
    [JsonPropertyName("signed_certificate_timestamps")]
    public bool? SignedCertificateTimestamps { get; init; }

    /// <summary>Whether the client requested OCSP stapling.</summary>
    [JsonPropertyName("status_request")]
    public bool? StatusRequest { get; init; }
}

/// <summary>A TLS registry code point and its name. GREASE values are named "GREASE(0x....)".</summary>
public sealed class TlsCodeName
{
    /// <summary>The code point.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>The code point's registered name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>An extension in a TLS ClientHello.</summary>
public sealed class TlsExtension
{
    /// <summary>The extension type.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>The extension's registered name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The extension body length in bytes.</summary>
    [JsonPropertyName("length")]
    public int? Length { get; init; }
}

/// <summary>
/// A destination that a Helios honeypot tunnel was asked to reach. Despite the feed's name, it is
/// not a DNS query: names resolved inside a tunneled DNS session are not visible.
/// </summary>
public sealed class HeliosDnsEvent
{
    /// <summary>When the event was captured.</summary>
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>The proxy tunnel, an unsigned 64-bit ID.</summary>
    [JsonPropertyName("tunnel_id")]
    public required ulong TunnelId { get; init; }

    /// <summary>The host name or address literal the tunnel was opened to.</summary>
    [JsonPropertyName("domain")]
    public required string Domain { get; init; }

    /// <summary>The destination port.</summary>
    [JsonPropertyName("port")]
    public required int Port { get; init; }

    /// <summary>The protocol, "dns".</summary>
    [JsonPropertyName("protocol")]
    public required string Protocol { get; init; }

    /// <summary>The capturing sensor.</summary>
    [JsonPropertyName("meta")]
    public required HeliosMeta Meta { get; init; }
}

/// <summary>
/// An Android Debug Bridge shell command captured by a Helios honeypot. It has no timestamp because
/// the sensor sends none.
/// </summary>
public sealed class HeliosAdbEvent
{
    /// <summary>The ADB session, as 32 hex characters.</summary>
    [JsonPropertyName("session")]
    public required string Session { get; init; }

    /// <summary>The 1-based position of the command within its session.</summary>
    [JsonPropertyName("sequential_id")]
    public required long SequentialId { get; init; }

    /// <summary>The shell command. Invalid UTF-8 bytes are replaced with U+FFFD.</summary>
    [JsonPropertyName("command")]
    public required string Command { get; init; }

    /// <summary>
    /// The hex-encoded SHA-256 the sensor assigns to the session's payload. It is not a hash of
    /// <see cref="Command"/>: every command in a session shares it, and sessions that replay the
    /// same script share it too.
    /// </summary>
    [JsonPropertyName("hash")]
    public required string Hash { get; init; }
}
