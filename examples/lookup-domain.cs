#:project ../src/Synthient.Client/Synthient.Client.csproj

// Prints the honeypot traffic Synthient has observed for a domain. Each lookup
// costs one credit.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/lookup-domain.cs example.com

using Synthient;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: lookup-domain <domain>");
    return 2;
}

using var client = new SynthientClient();
var info = await client.LookupDomainAsync(args[0], limit: 5);

Console.WriteLine($"domain: {info.Domain} ({info.Status})");
Console.WriteLine($"events: {info.Stats.Events24h} in 24h, {info.Stats.TotalEvents30d} in 30d");
Console.WriteLine($"unique ips: {info.UniqueIPs.Value24h} in 24h, {info.UniqueIPs.Value30d} in 30d");
foreach (var subdomain in info.TopSubdomains)
{
    Console.WriteLine($"subdomain: {subdomain.Subdomain} ({subdomain.Count})");
}
foreach (var recent in info.RecentEvents)
{
    Console.WriteLine($"event: {recent.Timestamp:u} {recent.SourceIP} -> {recent.TargetSubdomain}:{recent.Port}");
}
return 0;
