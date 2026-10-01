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
/// lookups and the account. Each feed has one scope for its exports and one for its stream. New
/// values may be added.
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

    /// <summary>Proxy administration.</summary>
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
public static class DomainStatus
{
    /// <summary>The domain had events in the last 24 hours.</summary>
    public const string Active = "active";

    /// <summary>The domain had no events in the last 24 hours, or has never been observed.</summary>
    public const string Inactive = "inactive";
}

/// <summary>The values of <see cref="ActivityStats.Cadence"/>, each a sentence.</summary>
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

/// <summary>The values of <see cref="Export.Kind"/> and <see cref="ExportMeta.Kind"/>.</summary>
public static class ExportKind
{
    /// <summary>An export of one UTC hour.</summary>
    public const string Hourly = "hourly";

    /// <summary>An export of one UTC day.</summary>
    public const string Daily = "daily";
}

/// <summary>The known values of <see cref="ParquetColumn.Type"/>. New values may be added.</summary>
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
