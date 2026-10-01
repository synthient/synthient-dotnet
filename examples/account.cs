#:project ../src/Synthient.Client/Synthient.Client.csproj

// Prints the account that owns the API key, its scopes, and its remaining
// lookup credits. It costs no credits.
//
//   SYNTHIENT_API_KEY=... dotnet run examples/account.cs

using Synthient;

using var client = new SynthientClient();
var account = await client.GetAccountAsync();

Console.WriteLine($"name: {account.FirstName} {account.LastName}");
Console.WriteLine($"email: {account.Email}");
Console.WriteLine($"organization: {account.Organization.Name} ({account.Organization.Relation})");
Console.WriteLine($"scopes: {string.Join(", ", account.Scopes)}");
Console.WriteLine($"credits: {account.LookupQuota.Credits}, reset in {account.LookupQuota.ResetsIn}");
