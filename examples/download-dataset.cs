#:project ../src/Synthient.Client/Synthient.Client.csproj

// Downloads the newest geolocation dataset as a MaxMind database to
// geolocation.mmdb and verifies its checksum. It needs an API key with the
// GEOLOCATION_FEED scope.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/download-dataset.cs

using System.Security.Cryptography;
using Synthient;

using var client = new SynthientClient();
var page = await client.Geolocation.ListExportsAsync(limit: 1);
if (page.Exports.Count == 0)
{
    Console.Error.WriteLine("no geolocation export found");
    return 1;
}
var export = page.Exports[0];
var mmdb = export.Formats.FirstOrDefault(file => file.Format == ExportFormat.Mmdb);
if (mmdb is null)
{
    Console.Error.WriteLine($"{export.Id} has no mmdb file");
    return 1;
}
Console.WriteLine($"{export.Id}: {mmdb.SizeBytes} bytes");

// Writing through a CryptoStream hashes the file as it is saved.
using var sha256 = SHA256.Create();
await using (var body = await client.Geolocation.DownloadAsync(export.Id, ExportFormat.Mmdb))
await using (var file = new CryptoStream(File.Create("geolocation.mmdb"), sha256, CryptoStreamMode.Write))
{
    await body.CopyToAsync(file);
}

if (!string.Equals(Convert.ToHexString(sha256.Hash!), mmdb.Checksum, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("checksum mismatch");
    return 1;
}
Console.WriteLine($"saved {export.Id} to geolocation.mmdb");
return 0;
