using System.Diagnostics.CodeAnalysis;

namespace Synthient;

/// <summary>The known values of <see cref="Network.Type"/>. New values may be added.</summary>
public static class NetworkType
{
    /// <summary>A mobile carrier network.</summary>
    public const string Mobile = "MOBILE";

    /// <summary>A satellite internet provider.</summary>
    public const string Satellite = "SATELLITE";

    /// <summary>In-flight Wi-Fi.</summary>
    public const string InFlightWifi = "IN_FLIGHT_WIFI";

    /// <summary>A residential internet provider.</summary>
    public const string Residential = "RESIDENTIAL";

    /// <summary>A corporate network.</summary>
    public const string Corporate = "CORPORATE";

    /// <summary>An academic network.</summary>
    public const string Academic = "ACADEMIC";

    /// <summary>A datacenter or hosting provider.</summary>
    public const string Datacenter = "DATACENTER";

    /// <summary>A government network.</summary>
    public const string Government = "GOVERNMENT";
}

/// <summary>
/// The known kinds of service seen on an address, used by <see cref="ProxyEvent.Type"/>,
/// <see cref="AnonymizerEvent.Type"/>, and <see cref="Ja4tEvent.Type"/>. New values may be added.
/// </summary>
public static class ProxyType
{
    /// <summary>A free VPN.</summary>
    public const string FreeVpn = "FREE_VPN";

    /// <summary>A commercial VPN.</summary>
    public const string CommercialVpn = "COMMERCIAL_VPN";

    /// <summary>An enterprise VPN.</summary>
    public const string EnterpriseVpn = "ENTERPRISE_VPN";

    /// <summary>A mobile proxy.</summary>
    public const string MobileProxy = "MOBILE_PROXY";

    /// <summary>A blockchain-based proxy network.</summary>
    public const string BlockchainProxy = "BLOCKCHAIN_PROXY";

    /// <summary>A residential proxy.</summary>
    public const string ResidentialProxy = "RESIDENTIAL_PROXY";

    /// <summary>A public proxy.</summary>
    public const string PublicProxy = "PUBLIC_PROXY";

    /// <summary>A datacenter proxy.</summary>
    public const string DatacenterProxy = "DATACENTER_PROXY";

    /// <summary>A Tor node.</summary>
    public const string TorNode = "TOR_NODE";

    /// <summary>A private relay, such as iCloud Private Relay.</summary>
    public const string PrivateRelay = "PRIVATE_RELAY";

    /// <summary>A botnet.</summary>
    public const string Botnet = "BOTNET";

    /// <summary>A search engine crawler.</summary>
    public const string SearchEngine = "SEARCH_ENGINE";

    /// <summary>An AI crawler.</summary>
    public const string AICrawler = "AI_CRAWLER";

    /// <summary>A social media crawler.</summary>
    public const string SocialMedia = "SOCIAL_MEDIA";

    /// <summary>An uptime monitor.</summary>
    public const string UptimeMonitor = "UPTIME_MONITOR";

    /// <summary>A link preview fetcher.</summary>
    public const string LinkPreview = "LINK_PREVIEW";

    /// <summary>An SEO crawler.</summary>
    public const string SeoCrawler = "SEO_CRAWLER";

    /// <summary>A web archiver.</summary>
    public const string WebArchiver = "WEB_ARCHIVER";

    /// <summary>A webhook provider.</summary>
    public const string WebhookProvider = "WEBHOOK_PROVIDER";

    /// <summary>A payment processor.</summary>
    public const string PaymentProcessor = "PAYMENT_PROCESSOR";

    /// <summary>An internet scanner.</summary>
    public const string InternetScanner = "INTERNET_SCANNER";
}

/// <summary>The known values of <see cref="Device.OS"/>. New values may be added.</summary>
public static class DeviceOS
{
    /// <summary>Android.</summary>
    public const string Android = "Android";

    /// <summary>Chrome OS.</summary>
    public const string ChromeOS = "Chrome OS";

    /// <summary>FreeBSD.</summary>
    public const string FreeBsd = "FreeBSD";

    /// <summary>iOS.</summary>
    public const string IOS = "iOS";

    /// <summary>Linux.</summary>
    public const string Linux = "Linux";

    /// <summary>macOS.</summary>
    public const string MacOSX = "Mac OS X";

    /// <summary>Windows.</summary>
    public const string Windows = "Windows";
}

/// <summary>
/// The known API key scopes, used by <see cref="Account.Scopes"/>. <see cref="Basic"/> authorizes
/// lookups and the account. Each feed has one scope for its exports and one for its stream, and
/// each dataset has one for its exports. New values may be added.
/// </summary>
public static class Scope
{
    /// <summary>Lookups and the account.</summary>
    public const string Basic = "BASIC";

    /// <summary>Proxy feed exports.</summary>
    public const string ProxyFeeds = "PROXY_FEEDS";

    /// <summary>The proxy feed stream.</summary>
    public const string ProxyFirehose = "PROXY_FIREHOSE";

    /// <summary>Anonymizer feed exports.</summary>
    public const string AnonymizersFeed = "ANONYMIZERS_FEED";

    /// <summary>The anonymizer feed stream.</summary>
    public const string AnonymizersStream = "ANONYMIZERS_STREAM";

    /// <summary>JA3 feed exports.</summary>
    public const string Ja3Feed = "JA3_FEED";

    /// <summary>The JA3 feed stream.</summary>
    public const string Ja3Stream = "JA3_STREAM";

    /// <summary>Torrent feed exports.</summary>
    public const string TorrentsFeed = "TORRENTS_FEED";

    /// <summary>The torrent feed stream.</summary>
    public const string TorrentsStream = "TORRENTS_STREAM";

    /// <summary>Helios HTTP feed exports.</summary>
    public const string HoneypotHttpFeed = "HONEYPOT_HTTP_FEED";

    /// <summary>The Helios HTTP feed stream.</summary>
    public const string HoneypotHttpStream = "HONEYPOT_HTTP_STREAM";

    /// <summary>Helios HTTPS feed exports.</summary>
    public const string HoneypotHttpsFeed = "HONEYPOT_HTTPS_FEED";

    /// <summary>The Helios HTTPS feed stream.</summary>
    public const string HoneypotHttpsStream = "HONEYPOT_HTTPS_STREAM";

    /// <summary>Helios ADB feed exports.</summary>
    public const string HoneypotAdbFeed = "HONEYPOT_ADB_FEED";

    /// <summary>The Helios ADB feed stream.</summary>
    public const string HoneypotAdbStream = "HONEYPOT_ADB_STREAM";

    /// <summary>Helios DNS feed exports.</summary>
    public const string HoneypotDnsFeed = "HONEYPOT_DNS_FEED";

    /// <summary>The Helios DNS feed stream.</summary>
    public const string HoneypotDnsStream = "HONEYPOT_DNS_STREAM";

    /// <summary>JA4 feed exports.</summary>
    public const string Ja4Feed = "JA4_FEED";

    /// <summary>The JA4 feed stream.</summary>
    public const string Ja4Stream = "JA4_STREAM";

    /// <summary>JA4T feed exports.</summary>
    public const string Ja4tFeed = "JA4T_FEED";

    /// <summary>The JA4T feed stream.</summary>
    public const string Ja4tStream = "JA4T_STREAM";

    /// <summary>Geolocation dataset exports.</summary>
    public const string GeolocationFeed = "GEOLOCATION_FEED";

    /// <summary>WHOIS dataset exports.</summary>
    public const string WhoisFeed = "WHOIS_FEED";

    /// <summary>Reserved for a WHOIS stream. No operation accepts it yet.</summary>
    public const string WhoisStream = "WHOIS_STREAM";

    /// <summary>Reserved for an upcoming network IOC feed's exports.</summary>
    public const string NetworkIocsFeed = "NETWORK_IOCS_FEED";

    /// <summary>Reserved for an upcoming network IOC feed's stream.</summary>
    public const string NetworkIocsStream = "NETWORK_IOCS_STREAM";

    /// <summary>Reserved for an upcoming file IOC feed's exports.</summary>
    public const string FileIocsFeed = "FILE_IOCS_FEED";

    /// <summary>Reserved for an upcoming file IOC feed's stream.</summary>
    public const string FileIocsStream = "FILE_IOCS_STREAM";

    /// <summary>A dashboard permission that authorizes no API operation.</summary>
    public const string Helios = "HELIOS";

    /// <summary>Proxy administration.</summary>
    [Obsolete("ADMIN_PROXIES was removed from the Synthient API and never authorized a public operation. It will be removed in the next major version.")]
    public const string AdminProxies = "ADMIN_PROXIES";
}

/// <summary>The values of <see cref="Organization.Relation"/>.</summary>
public static class Relation
{
    /// <summary>The organization's owner.</summary>
    public const string Owner = "OWNER";

    /// <summary>An administrator.</summary>
    public const string Admin = "ADMIN";

    /// <summary>A member.</summary>
    public const string Member = "MEMBER";
}

/// <summary>The values of <see cref="DomainInfo.Status"/>.</summary>
[Obsolete(SynthientClient.DomainLookupRemoved)]
public static class DomainStatus
{
    /// <summary>The domain had events in the last 24 hours.</summary>
    public const string Active = "active";

    /// <summary>The domain had no events in the last 24 hours, or has never been observed.</summary>
    public const string Inactive = "inactive";
}

/// <summary>The values of <see cref="ActivityStats.Cadence"/>, each a sentence.</summary>
[Obsolete(SynthientClient.DomainLookupRemoved)]
public static class Cadence
{
    /// <summary>No events in the last 30 days.</summary>
    public const string NoActivity = "No activity in the last 30 days.";

    /// <summary>Events are spread evenly over the day.</summary>
    public const string EvenlyDistributed = "Activity is evenly distributed across the day.";

    /// <summary>Events follow weekday business hours.</summary>
    public const string BusinessHours = "Activity follows a weekday business-hours pattern.";

    /// <summary>Events happen mostly on weekends.</summary>
    public const string Weekend = "Activity follows a weekend pattern.";

    /// <summary>Events come in bursts.</summary>
    public const string BurstDriven = "Activity is burst-driven with significant peaks.";
}

/// <summary>The values of <see cref="Export.Kind"/>, <see cref="ExportMeta.Kind"/>, and <see cref="ExportVolumeDay.Kind"/>.</summary>
public static class ExportKind
{
    /// <summary>An export of one UTC hour.</summary>
    public const string Hourly = "hourly";

    /// <summary>An export of one UTC day.</summary>
    public const string Daily = "daily";
}

/// <summary>
/// The known values of <see cref="ParquetColumn.Type"/>. New values may be added, and the datasets
/// already use others, such as "double", "date", and "list&lt;string&gt;".
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The names are the parquet type names.")]
public static class ParquetType
{
    /// <summary>A UTF-8 string.</summary>
    public const string String = "string";

    /// <summary>A signed 64-bit integer.</summary>
    public const string Int64 = "int64";

    /// <summary>An unsigned 32-bit integer.</summary>
    public const string UInt32 = "uint32";

    /// <summary>An unsigned 64-bit integer.</summary>
    public const string UInt64 = "uint64";

    /// <summary>A boolean.</summary>
    public const string Bool = "bool";

    /// <summary>Raw bytes.</summary>
    public const string Bytes = "bytes";
}

/// <summary>
/// The known export file formats, used by <see cref="ExportMeta.Format"/>, <see cref="ExportFile.Format"/>,
/// and the format argument of <see cref="Dataset"/> methods. New values may be added.
/// </summary>
public static class ExportFormat
{
    /// <summary>Parquet, the format of every feed export and the default for geolocation.</summary>
    public const string Parquet = "parquet";

    /// <summary>Gzipped CSV with a header row, served as a .csv.gz file.</summary>
    public const string Csv = "csv";

    /// <summary>A MaxMind database, of database type Synthient-Enterprise.</summary>
    public const string Mmdb = "mmdb";

    /// <summary>A gzipped tar archive, the only format of the WHOIS dataset.</summary>
    public const string TarGz = "tar.gz";
}

/// <summary>
/// The variants of a feed export, used by the variant argument of <see cref="Feed{TEvent}"/> methods
/// and by <see cref="Export.Variant"/>. Only the proxies, anonymizers, and JA4T feeds publish lite
/// files. Elsewhere, a lite listing is empty and a lite export fails with a 404.
/// </summary>
public static class ExportVariant
{
    /// <summary>Every sighting, the default.</summary>
    public const string Full = "full";

    /// <summary>
    /// The export deduplicated to the latest row per address and provider for proxies, per address,
    /// fingerprint, and provider for JA4T, and per range and provider for anonymizers. It is
    /// published shortly after the full file, and fails with a 404 until then.
    /// </summary>
    public const string Lite = "lite";
}

/// <summary>The known values of <see cref="ExportEntity.Entity"/>. New values may be added.</summary>
public static class WhoisEntity
{
    /// <summary>Autonomous systems.</summary>
    public const string Asns = "asns";

    /// <summary>Networks.</summary>
    public const string Networks = "networks";

    /// <summary>Organisations.</summary>
    public const string Organisations = "organisations";

    /// <summary>Contacts.</summary>
    public const string Contacts = "contacts";

    /// <summary>Maintainers.</summary>
    public const string Maintainers = "maintainers";

    /// <summary>Transfers.</summary>
    public const string Transfers = "transfers";

    /// <summary>Delegations.</summary>
    public const string Delegations = "delegations";
}

/// <summary>
/// The known tunnel transports, used by <see cref="HeliosDnsEvent.Transport"/>,
/// <see cref="HeliosHttpsEvent.Transport"/>, and <see cref="TlsClientHello.Transport"/>. New values
/// may be added.
/// </summary>
public static class HeliosTransport
{
    /// <summary>TCP.</summary>
    public const string Tcp = "tcp";

    /// <summary>UDP.</summary>
    public const string Udp = "udp";

    /// <summary>QUIC.</summary>
    public const string Quic = "quic";
}
