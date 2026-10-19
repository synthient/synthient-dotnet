![Synthient](https://raw.githubusercontent.com/synthient/synthient-dotnet/main/assets/readme-header.svg)

[Documentation](https://docs.synthient.com) · [NuGet](https://www.nuget.org/packages/Synthient.Client) · [Support](https://docs.synthient.com/misc/support)

# synthient-dotnet

`synthient-dotnet` is the official .NET SDK for the [Synthient API](https://docs.synthient.com): IP and domain intelligence, account details, and real-time and bulk data feeds.

[![NuGet](https://img.shields.io/nuget/v/Synthient.Client)](https://www.nuget.org/packages/Synthient.Client)
[![test](https://github.com/synthient/synthient-dotnet/actions/workflows/test.yml/badge.svg)](https://github.com/synthient/synthient-dotnet/actions/workflows/test.yml)
[![lint](https://github.com/synthient/synthient-dotnet/actions/workflows/lint.yml/badge.svg)](https://github.com/synthient/synthient-dotnet/actions/workflows/lint.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/synthient/synthient-dotnet/blob/main/LICENSE)

## Table of Contents

- [synthient-dotnet](#synthient-dotnet)
  - [SDK Installation](#sdk-installation)
  - [Requirements](#requirements)
  - [API Keys](#api-keys)
  - [Authentication](#authentication)
  - [SDK Example Usage](#sdk-example-usage)
  - [Available Resources and Operations](#available-resources-and-operations)
  - [Streaming](#streaming)
  - [Pagination](#pagination)
  - [Downloads](#downloads)
  - [Retries](#retries)
  - [Timeouts](#timeouts)
  - [Error Handling](#error-handling)
  - [Server Selection](#server-selection)
  - [Custom HTTP Client](#custom-http-client)
  - [Debugging](#debugging)
  - [gRPC Schema](#grpc-schema)
- [Development](#development)
  - [Maturity](#maturity)
  - [Contributions](#contributions)
  - [License](#license)

## SDK Installation

### .NET CLI

```sh
dotnet add package Synthient.Client
```

The [gRPC schema fetcher](#grpc-schema) is a separate package:

```sh
dotnet add package Synthient.Client.GrpcSchema
```

### PackageReference

The second reference is only needed for the gRPC schema fetcher:

```xml
<ItemGroup>
  <PackageReference Include="Synthient.Client" Version="1.0.0" />
  <PackageReference Include="Synthient.Client.GrpcSchema" Version="1.0.0" />
</ItemGroup>
```

## Requirements

.NET 8 or later. `Synthient.Client` targets net8.0 and net10.0, depends only on System.Text.Json on net8.0, and supports trimming and Native AOT.

## API Keys

Every request needs a Synthient API key. Keys are issued from the Synthient dashboard; if you don't have one, [contact us](mailto:contact@synthient.com). A key's scopes decide what it can call: `BASIC` covers the lookups and `GetAccountAsync`, and each feed has its own scopes, listed under [Feeds](#feeds).

If a request fails even though you are sending a key:

- **401 Unauthorized**: the key is malformed, unknown, or revoked.
- **402 Payment Required**: the team is out of lookup credits.
- **403 Forbidden**: the key lacks the scope for that endpoint or feed.

`GetAccountAsync` shows the key's scopes and the team's remaining credits.

## Authentication

This SDK supports the following security scheme:

| Name     | Type    | Scheme             | Environment Variable |
| -------- | ------- | ------------------ | -------------------- |
| `ApiKey` | API key | `x-api-key` header | `SYNTHIENT_API_KEY`  |

The parameterless constructor reads the key from `SYNTHIENT_API_KEY`:

```csharp
using var client = new SynthientClient();
```

To pass the key yourself, such as from a secret manager, use the constructor that takes it, or set `ApiKey` on `SynthientClientOptions`:

```csharp
using var client = new SynthientClient(apiKey);
```

The constructor makes no network calls. To check a key, call `GetAccountAsync`, which costs no credits.

## SDK Example Usage

### Look up an IP address

Get the network, location, and risk intelligence for an IPv4 or IPv6 address. Each lookup costs one credit; failed lookups are free.

```csharp
using System.Net;
using Synthient;

using var client = new SynthientClient();

var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"));
if (info.Intelligence is { } intelligence)
{
    Console.WriteLine($"{intelligence.RiskScore} {string.Join(", ", intelligence.Categories)}");
}
```

`Network`, `Location`, and `Intelligence` are null when the API has no data for them. A client is safe for concurrent use and pools its connections, so reuse one for the life of your application and dispose it when you are done. The examples below reuse `client`, and runnable programs are in [examples](https://github.com/synthient/synthient-dotnet/tree/main/examples).

### Look up many IP addresses

Look up as many as 1000 addresses in one request for `ceil(n * 0.9)` credits. Private and reserved addresses are skipped and duplicates are merged, so match results by `IP`.

```csharp
var results = await client.LookupIPsAsync([IPAddress.Parse("8.8.8.8"), IPAddress.Parse("1.1.1.1")]);
foreach (var result in results)
{
    if (result.Intelligence is { } intelligence)
    {
        Console.WriteLine($"{result.IP} {intelligence.RiskScore}");
    }
}
```

### Look up a domain

Get the honeypot traffic observed for a domain and its most recent events. Each lookup costs one credit.

```csharp
var domain = await client.LookupDomainAsync("example.com", limit: 10);
Console.WriteLine($"{domain.Status} {domain.Stats.Events24h}");
foreach (var recent in domain.RecentEvents)
{
    Console.WriteLine($"{recent.Timestamp:u} {recent.TargetSubdomain} {recent.Port}");
}
```

### Check your account

Get the key's account, scopes, and remaining lookup credits, at no cost.

```csharp
var account = await client.GetAccountAsync();
Console.WriteLine($"{account.Organization.Name} {string.Join(", ", account.Scopes)} {account.LookupQuota.Credits}");
```

## Available Resources and Operations

Every public type and member has XML documentation, which your editor shows as you type.

### SynthientClient

- `LookupIPAsync` - Look up an IPv4 or IPv6 address
- `LookupIPsAsync` - Look up as many as 1000 addresses in one request
- `LookupDomainAsync` - Look up the honeypot traffic observed for a domain
- `GetAccountAsync` - Get the key's account, scopes, and remaining lookup credits
- `CheckHealthAsync` - Check that the API and its dependencies are healthy

### Feeds

Each feed is a property of the client, such as `client.Proxies`, and has the same methods:

- `StreamAsync` - Stream the feed's real-time events
- `ListExportsAsync` - List one page of the feed's Parquet exports, newest first
- `ListAllExportsAsync` - Enumerate every export, fetching pages as needed
- `GetExportMetaAsync` - Get an export's checksum, size, row count, and Parquet schema
- `DownloadAsync` - Download an export's Parquet file
- `GetDownloadUrlAsync` - Get a presigned URL for an export's Parquet file, valid for 24 hours

| Property      | Data                                          | Event type         | Stream scope            | Export scope          |
| ------------- | --------------------------------------------- | ------------------ | ----------------------- | --------------------- |
| `Proxies`     | Residential, mobile, and datacenter proxies   | `ProxyEvent`       | `PROXY_FIREHOSE`        | `PROXY_FEEDS`         |
| `Anonymizers` | VPN and anonymizer IP ranges                  | `AnonymizerEvent`  | `ANONYMIZERS_STREAM`    | `ANONYMIZERS_FEED`    |
| `Torrents`    | BitTorrent DHT and tracker peers              | `TorrentEvent`     | `TORRENTS_STREAM`       | `TORRENTS_FEED`       |
| `Ja4t`        | JA4T TCP fingerprints                         | `Ja4tEvent`        | `JA4T_STREAM`           | `JA4T_FEED`           |
| `HeliosHttp`  | Helios honeypot HTTP requests                 | `HeliosHttpEvent`  | `HONEYPOT_HTTP_STREAM`  | `HONEYPOT_HTTP_FEED`  |
| `HeliosHttps` | Helios honeypot TLS ClientHellos              | `HeliosHttpsEvent` | `HONEYPOT_HTTPS_STREAM` | `HONEYPOT_HTTPS_FEED` |
| `HeliosDns`   | Helios honeypot tunnel destinations           | `HeliosDnsEvent`   | `HONEYPOT_DNS_STREAM`   | `HONEYPOT_DNS_FEED`   |
| `HeliosAdb`   | Helios honeypot Android Debug Bridge commands | `HeliosAdbEvent`   | `HONEYPOT_ADB_STREAM`   | `HONEYPOT_ADB_FEED`   |

## Streaming

`StreamAsync` returns an `IAsyncEnumerable` of a feed's real-time events. Each `await foreach` opens a connection, and leaving the loop closes it:

```csharp
while (!cancellationToken.IsCancellationRequested)
{
    try
    {
        await foreach (var proxy in client.Proxies.StreamAsync(cancellationToken))
        {
            Console.WriteLine($"{proxy.IP} {proxy.Provider} {proxy.Type}");
        }
    }
    catch (SynthientException e) when (e is not ApiException { StatusCode: 401 or 403 })
    {
        Console.Error.WriteLine($"proxy stream: {e.Message}");
        // Avoid reconnecting in a tight loop.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}
```

- The server closes each stream after up to 30 minutes. That ends the `await foreach` without an exception, and the outer loop reconnects at once.
- A 401 or 403 isn't caught, so it ends the loop, because reconnecting can't fix a rejected key or a missing scope. Cancelling the token ends it with an `OperationCanceledException`.
- Any other failure throws a `SynthientException`, and the loop waits 2 seconds before reconnecting.
- Events sent while disconnected are missed. The API has no resume cursor.

## Pagination

`ListExportsAsync` returns one page of a feed's exports, newest first. Pass `page.NextCursor` as `cursor` to get the next page; it is null or empty on the last one.

```csharp
var page = await client.Proxies.ListExportsAsync(limit: 50);
foreach (var export in page.Exports)
{
    Console.WriteLine($"{export.Id} {export.Kind} {export.RowCount}");
}
```

`ListAllExportsAsync` fetches the pages for you as the loop needs them:

```csharp
await foreach (var export in client.Proxies.ListAllExportsAsync())
{
    Console.WriteLine($"{export.Id} {export.Kind} {export.RowCount}");
}
```

On the first page, the newest hourly export has the ID `latest`, a moving alias for the freshest export. Pass its `Date` and `Hour` to `ExportId.Hourly` for its stable ID, such as `2026-10-14/21`.

## Downloads

Exports are Parquet files. `DownloadAsync` returns a stream of a file's contents, which you must dispose:

```csharp
await using var body = await client.Proxies.DownloadAsync(ExportId.Latest);
await using var file = File.Create("proxies.parquet");
await body.CopyToAsync(file);
```

- Name an export by its `Id` from a listing, by `ExportId.Latest`, or by `ExportId.Daily(day)` or `ExportId.Hourly(day, hour)` for a UTC day or hour.
- The token passed to `DownloadAsync` covers only opening the file. To bound the rest, pass a token to each read, such as `body.CopyToAsync(file, cancellationToken)`.
- `GetExportMetaAsync` returns the file's SHA-256 checksum. Use a fixed ID for both calls, because `latest` can move between them.
- `GetDownloadUrlAsync` returns a presigned URL that works without a key for 24 hours, so treat it as a secret. Your API key is never sent to the storage host.
- Hourly exports roll into the daily export shortly after 00:30 UTC the next day. After that, the hour returns a 400.

## Retries

The client retries a failed request up to twice, backing off exponentially with jitter and honoring `Retry-After`:

| Requests                              | Retried on                                                                                   |
| ------------------------------------- | -------------------------------------------------------------------------------------------- |
| Account and feed requests             | 500, 502, 503, and 504, most connection failures and timeouts, and 429 with `Retry-After`    |
| Billed lookups and `CheckHealthAsync` | Only 429 with `Retry-After`, which comes before any work, so a lookup is never charged twice |

A 429 without `Retry-After` means the monthly request budget is spent or too many streams are open, so it is thrown at once. Downloads from the storage host are never retried.

Set the number of retries with `MaxRetries`. Zero disables them:

```csharp
using var client = new SynthientClient(new SynthientClientOptions { MaxRetries = 4 });
```

## Timeouts

`Timeout` bounds each wait on the network, such as for response headers or the next read of a stream, but not a whole call, so streams and downloads are not cut off. It defaults to 60 seconds, and `Timeout.InfiniteTimeSpan` disables it:

```csharp
using var client = new SynthientClient(new SynthientClientOptions { Timeout = TimeSpan.FromSeconds(30) });
```

When it elapses, the call throws `ApiTimeoutException`, so a stream that sends nothing for that long fails. The default handler also gives up connecting after 10 seconds.

To bound a whole call, pass a `CancellationToken`. Cancelling it throws `OperationCanceledException`:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"), cancellation.Token);
```

## Error Handling

A request that fails with an error status throws an `ApiException`:

| Property     | Type                                                 | Description                                    |
| ------------ | ---------------------------------------------------- | ---------------------------------------------- |
| `StatusCode` | `int`                                                | HTTP status code, such as `404`                |
| `Type`       | `string?`                                            | URI identifying the problem type               |
| `Title`      | `string?`                                            | Short summary of the problem type              |
| `Detail`     | `string?`                                            | Explanation of this occurrence                 |
| `Errors`     | `IReadOnlyDictionary<string, IReadOnlyList<string>>` | Validation messages by request field           |
| `RequestId`  | `string?`                                            | Request ID to give Synthient support           |
| `RetryAfter` | `TimeSpan?`                                          | How long the server asked you to wait, or null |
| `Headers`    | `IReadOnlyDictionary<string, IReadOnlyList<string>>` | Response headers, with case-insensitive names  |

`Message` holds only the status, such as `403 Forbidden`, so it is safe to log. The properties hold untrusted server data.

### Example

```csharp
try
{
    await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"));
}
catch (ApiException e) when (e.StatusCode == 402)
{
    Console.WriteLine("out of lookup credits");
}
catch (ApiException e) when (e.StatusCode == 429 && e.RetryAfter is { } retryAfter)
{
    Console.WriteLine($"rate limited, retry after {retryAfter}");
}
catch (ApiException e) when (e.StatusCode == 429)
{
    Console.WriteLine("monthly request budget spent");
}
catch (ApiException e)
{
    Console.WriteLine($"request {e.RequestId} failed: {e.Detail ?? e.Title}");
}
```

### Error Types

- `SynthientException`: the base type of the SDK's exceptions, also thrown for a response the SDK can't decode.
- `ApiException`: the API, or the storage host during `DownloadAsync`, returned an error status.
- `ApiConnectionException`: no response arrived, or reading the response failed.
- `ApiTimeoutException`: an `ApiConnectionException` thrown when `Timeout` or the 10-second connect timeout elapses.
- `OperationCanceledException`: the call's `CancellationToken` was cancelled.
- `ArgumentException`: a missing API key or another invalid argument or option, rejected before any request is sent.
- `ObjectDisposedException`: the client was disposed.

#### Status codes

| Status | Meaning                                                                                         |
| ------ | ----------------------------------------------------------------------------------------------- |
| 400    | The request is invalid, such as an hourly export that was rolled up                             |
| 401    | The API key is malformed, unknown, or revoked                                                   |
| 402    | The team is out of lookup credits                                                               |
| 403    | The key lacks the scope for the endpoint or feed                                                |
| 404    | The export doesn't exist, or a daily export is still rolling up (`RetryAfter` is set)           |
| 422    | The input is invalid, such as a private address or a malformed domain                           |
| 429    | Rate limited if `RetryAfter` is set; otherwise the budget is spent or too many streams are open |
| 5xx    | The server failed                                                                               |

## Server Selection

### Override Server URL Per-Client

The default server is `https://api.synthient.com/api/v4`. Set `BaseAddress` to send requests elsewhere, such as a mock server in tests:

```csharp
using var client = new SynthientClient(new SynthientClientOptions
{
    BaseAddress = new Uri("http://localhost:8080/api/v4"),
});
```

## Custom HTTP Client

The client takes an `HttpMessageHandler`, not an `HttpClient`. Pass your own as `Handler`, for example to use a proxy:

```csharp
var handler = new SocketsHttpHandler
{
    Proxy = new WebProxy("http://proxy.example.com:8080"),
    AllowAutoRedirect = false,
    MaxResponseDrainSize = 0,
};
using var client = new SynthientClient(new SynthientClientOptions { Handler = handler });
```

- Turn off `AllowAutoRedirect`, because a followed export redirect would send the API key to the storage host. A handler with it set is rejected with `ArgumentException`.
- The handler must not retry requests, so a billed lookup is never sent twice.
- Set `MaxResponseDrainSize` to 0 so that leaving a stream or download early closes its connection.
- The client never disposes your handler.

Set `UserAgent = "my-app/1.2"` to add your product token to the `User-Agent` header.

## Debugging

`ResponseHook` is called after every API response, including errors, retries, pages, and stream handshakes, with the status, request ID, rate limit, and headers:

```csharp
using var client = new SynthientClient(new SynthientClientOptions
{
    ResponseHook = metadata =>
    {
        Console.WriteLine($"status={metadata.StatusCode} request_id={metadata.RequestId}");
        if (metadata.RateLimit is { } rateLimit)
        {
            Console.WriteLine($"rate limit: {rateLimit.Remaining} of {rateLimit.Limit} left");
        }
    },
});
```

The hook must be safe to call concurrently. If it throws, the call fails with its exception.

> [!WARNING]
> Response headers can contain secrets, such as presigned download URLs. Avoid logging `metadata.Headers` in production.

## gRPC Schema

The `Synthient.Client.GrpcSchema` package fetches the protobuf schema of the [Synthient gRPC API](https://docs.synthient.com/grpc) through server reflection:

```csharp
try
{
    var schema = await GrpcSchema.FetchAsync(Environment.GetEnvironmentVariable("SYNTHIENT_API_KEY"));
    foreach (var file in schema.DescriptorSet.File)
    {
        Console.WriteLine($"{file.Name} {file.Package}");
    }
}
catch (GrpcSchemaException e)
{
    var explanation = GrpcSchema.Explain(e);
    Console.Error.WriteLine(explanation is null ? e.Message : $"{explanation} {e.Message}");
}
```

`schema.DescriptorSet` is a `Google.Protobuf.Reflection.FileDescriptorSet`. `GrpcSchema.Explain` describes common gRPC errors in plain language.

# Development

## Maturity

This SDK follows [Semantic Versioning](https://semver.org). Breaking changes ship only in a new major version. `Synthient.Client` and `Synthient.Client.GrpcSchema` are released together with the same version.

## Contributions

Bug reports and feature requests are welcome as [GitHub issues](https://github.com/synthient/synthient-dotnet/issues). For questions about the API or your account, contact [Synthient support](https://docs.synthient.com/misc/support).

## License

[Apache 2.0](https://github.com/synthient/synthient-dotnet/blob/main/LICENSE)
