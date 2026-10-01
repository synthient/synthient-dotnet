#:project ../src/Synthient.Client/Synthient.Client.csproj

// Prints what Synthient knows about an IP address. Each lookup costs one
// credit.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/lookup-ip.cs 8.8.8.8

using System.Net;
using Synthient;

if (args.Length != 1 || !IPAddress.TryParse(args[0], out var address))
{
    Console.Error.WriteLine("usage: lookup-ip <ip>");
    return 2;
}

using var client = new SynthientClient();
IPInfo info;
try
{
    info = await client.LookupIPAsync(address);
}
catch (ApiException e) when (e.StatusCode == 402)
{
    Console.Error.WriteLine("out of lookup credits");
    return 1;
}

Console.WriteLine($"ip: {info.IP}");
if (info.Network is { } network)
{
    Console.WriteLine($"network: {network.Isp} (AS{network.Asn}, {network.Type})");
}
if (info.Location is { } location)
{
    Console.WriteLine($"location: {location.City}, {location.Country}");
}
if (info.Intelligence is not { } intelligence)
{
    Console.WriteLine("no intelligence available");
    return 0;
}
Console.WriteLine($"risk score: {intelligence.RiskScore}");
Console.WriteLine($"categories: {string.Join(", ", intelligence.Categories)}");
foreach (var provider in intelligence.Providers)
{
    Console.WriteLine($"provider: {provider.Name} ({provider.Type}), last seen {provider.LastSeen:u}");
}
return 0;
