# synthient-dotnet

![Synthient](https://raw.githubusercontent.com/synthient/synthient-dotnet/main/assets/readme-header.svg)

[![NuGet](https://img.shields.io/nuget/v/Synthient.Client)](https://www.nuget.org/packages/Synthient.Client)
[![test](https://github.com/synthient/synthient-dotnet/actions/workflows/test.yml/badge.svg)](https://github.com/synthient/synthient-dotnet/actions/workflows/test.yml)
[![lint](https://github.com/synthient/synthient-dotnet/actions/workflows/lint.yml/badge.svg)](https://github.com/synthient/synthient-dotnet/actions/workflows/lint.yml)

The official .NET client for the [Synthient API](https://docs.synthient.com): IP and domain intelligence, account details, and real-time and bulk data feeds.

## Install

```sh
dotnet add package Synthient.Client
```

Requires .NET 8 or later.

## Usage

```csharp
using System.Net;
using Synthient;

using var client = new SynthientClient(); // reads SYNTHIENT_API_KEY

var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"));
if (info.Intelligence is not null)
{
    Console.WriteLine($"{info.Intelligence.RiskScore} {string.Join(", ", info.Intelligence.Categories)}");
}
```

Look up many addresses in one request:

```csharp
var results = await client.LookupIPsAsync([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1")]);
foreach (var result in results)
{
    if (result.Intelligence is not null)
    {
        Console.WriteLine($"{result.IP} {result.Intelligence.RiskScore}");
    }
}
```

Consume a real-time feed:

```csharp
await foreach (var proxy in client.Proxies.StreamAsync())
{
    Console.WriteLine($"{proxy.IP} {proxy.Provider}");
}
```

## Documentation

Every public type and member has XML documentation, shown in your editor, including errors, retries, exports, and downloads. Runnable programs are in [examples](https://github.com/synthient/synthient-dotnet/tree/main/examples), for example `dotnet run examples/lookup-ip.cs 8.8.8.8`. Guides and field references are at [docs.synthient.com](https://docs.synthient.com). The `Synthient.Client.GrpcSchema` package fetches the protobuf schema of the Synthient gRPC API.

## License

[Apache 2.0](https://github.com/synthient/synthient-dotnet/blob/main/LICENSE)
