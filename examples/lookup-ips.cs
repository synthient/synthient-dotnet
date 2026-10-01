#:project ../src/Synthient.Client/Synthient.Client.csproj

// Looks up several IP addresses in one request and prints each risk score.
// The batch costs ceil(n * 0.9) credits for the n distinct addresses the API
// looks up.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/lookup-ips.cs 8.8.8.8 1.1.1.1

using System.Net;
using Synthient;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: lookup-ips <ip>...");
    return 2;
}

using var client = new SynthientClient();
var results = await client.LookupIPsAsync(args.Select(IPAddress.Parse));

// Results can be fewer than the inputs and are matched by address, not position.
foreach (var result in results)
{
    if (result.Intelligence is { } intelligence)
    {
        Console.WriteLine($"{result.IP}: risk {intelligence.RiskScore}, {string.Join(", ", intelligence.Categories)}");
    }
    else
    {
        Console.WriteLine($"{result.IP}: no intelligence available");
    }
}
return 0;
