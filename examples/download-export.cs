#:project ../src/Synthient.Client/Synthient.Client.csproj

// Lists the newest exports of the proxies feed, then downloads the most recent
// daily export to proxies.parquet and verifies its checksum. It needs an API
// key with the PROXY_FEEDS scope.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/download-export.cs

using System.Security.Cryptography;
using Synthient;

using var client = new SynthientClient();
Export? daily = null;
await foreach (var export in client.Proxies.ListAllExportsAsync())
{
    Console.WriteLine($"{export.Id,-16} {export.Kind,-6} {export.RowCount,10} rows {export.SizeBytes,12} bytes");
    if (export.Kind == ExportKind.Daily)
    {
        daily = export;
        break;
    }
}
if (daily is null)
{
    Console.Error.WriteLine("no daily export found");
    return 1;
}

// Writing through a CryptoStream hashes the file as it is saved.
using var sha256 = SHA256.Create();
await using (var body = await client.Proxies.DownloadAsync(daily.Id))
await using (var file = new CryptoStream(File.Create("proxies.parquet"), sha256, CryptoStreamMode.Write))
{
    await body.CopyToAsync(file);
}

if (!string.Equals(Convert.ToHexString(sha256.Hash!), daily.Checksum, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("checksum mismatch");
    return 1;
}
Console.WriteLine($"saved {daily.Id} to proxies.parquet");
return 0;
