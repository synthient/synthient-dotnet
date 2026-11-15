![Synthient](https://raw.githubusercontent.com/synthient/synthient-dotnet/main/assets/readme-header.svg)

[Documentation](https://docs.synthient.com) · [NuGet](https://www.nuget.org/packages/Synthient.Client) · [Support](https://docs.synthient.com/misc/support)

# synthient-dotnet

`synthient-dotnet` is the official .NET SDK for the [Synthient API](https://docs.synthient.com): IP and domain intelligence, account details, and real-time and bulk data feeds.

[![NuGet](https://img.shields.io/nuget/v/Synthient.Client)](https://www.nuget.org/packages/Synthient.Client)
[![test](https://github.com/synthient/synthient-dotnet/actions/workflows/test.yml/badge.svg)](https://github.com/synthient/synthient-dotnet/actions/workflows/test.yml)
[![lint](https://github.com/synthient/synthient-dotnet/actions/workflows/lint.yml/badge.svg)](https://github.com/synthient/synthient-dotnet/actions/workflows/lint.yml)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/synthient/synthient-dotnet/blob/main/LICENSE)

## Table of Contents

- [Installation](#installation)
- [Quick start](#quick-start)
- [Authentication](#authentication)
- [IP and domain lookups](#ip-and-domain-lookups)
- [Account](#account)
- [Feeds](#feeds)
- [Errors](#errors)
- [Retries and timeouts](#retries-and-timeouts)
- [Configuration](#configuration)
- [Response metadata](#response-metadata)
- [gRPC schema](#grpc-schema)
- [Requirements](#requirements)
- [Versioning](#versioning)
- [Documentation](#documentation)
- [Contributions](#contributions)
- [License](#license)

## Installation

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

## Quick start

This program creates a client and looks up one address. It reads the API key from the `SYNTHIENT_API_KEY` environment variable:

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

See [Authentication](https://docs.synthient.com/authentication).

### API keys

Every request needs a Synthient API key. Keys are issued from the Synthient dashboard; if you don't have one, [contact us](mailto:contact@synthient.com). A key's scopes decide what it can call: `BASIC` covers the lookups and `GetAccountAsync`, and each feed has its own scopes, listed under [Feeds](#feeds).

If a request fails even though you are sending a key:

- **401 Unauthorized**: the key is malformed, unknown, or revoked.
- **402 Payment Required**: the team is out of lookup credits.
- **403 Forbidden**: the key lacks the scope for that endpoint or feed.

`GetAccountAsync` shows the key's scopes and the team's remaining credits.

## IP and domain lookups

See the [lookups reference](https://docs.synthient.com/sdks/csharp/lookups) and the [IP API](https://docs.synthient.com/ipapi).

### Look up an IP address

Get the network, location, and risk intelligence for an IPv4 or IPv6 address. Each lookup costs one credit; failed lookups are free.

```csharp
var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"));
if (info.Network is { } network)
{
    Console.WriteLine($"{network.Asn} {network.Isp} {network.Type}");
}
if (info.Location is { } location)
{
    Console.WriteLine($"{location.City} {location.Country}");
}
```

An IPv6 address with a scope ID, such as `fe80::1%3`, is rejected with `ArgumentException` before any request is sent. A private or reserved address fails with a 422 `ApiException`.

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

An empty batch returns no results without a request. More than 1000 addresses are rejected with `ArgumentException`.

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

`limit` caps the recent events, from 1 to 1000, and defaults to 50. Internationalized names must be sent in Punycode. A domain that has never been observed returns the status `inactive` (`DomainStatus.Inactive`) and zero counts, not an error.

## Account

Get the key's account, scopes, and remaining lookup credits, at no cost.

```csharp
var account = await client.GetAccountAsync();
Console.WriteLine($"{account.Organization.Name} {string.Join(", ", account.Scopes)} {account.LookupQuota.Credits}");
```

`account.LookupQuota.ResetsIn` is how long until the credits reset. The known scopes are constants of `Scope`, such as `Scope.Basic`.

`CheckHealthAsync` completes if the API and its dependencies are healthy, and throws an `ApiException` if not, such as a 503 when a dependency is down. It costs no credits:

```csharp
await client.CheckHealthAsync();
```

## Feeds

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

See the [feeds reference](https://docs.synthient.com/sdks/csharp/feeds), and [Firehose](https://docs.synthient.com/enterprise/firehose) and [Feeds](https://docs.synthient.com/enterprise/feeds) for the data behind them.

### Streaming

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

`cancellationToken` is your application's shutdown token, such as one from a `CancellationTokenSource`.

### Exports

Each feed has hourly Parquet exports that roll up into daily exports. `ListExportsAsync` returns one page of a feed's exports, newest first. `limit` is the page size, from 1 to 500, and defaults to 100. Pass `page.NextCursor` as `cursor` to get the next page; it is null or empty on the last one.

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

On the first page, the newest hourly export has the ID `latest`, a moving alias for the freshest export. Pass its `Date` and `Hour` to `ExportId.Hourly` for its stable ID, such as `2026-10-14/21`:

```csharp
var newest = page.Exports[0];
var id = newest.Hour is { } hour ? ExportId.Hourly(newest.Date, hour) : newest.Id;
```

### Downloads

Exports are Parquet files. `DownloadAsync` returns a stream of a file's contents, which you must dispose:

```csharp
await using var body = await client.Proxies.DownloadAsync(ExportId.Latest);
await using var file = File.Create("proxies.parquet");
await body.CopyToAsync(file);
```

- Name an export by its `Id` from a listing, by `ExportId.Latest`, or by `ExportId.Daily(day)` or `ExportId.Hourly(day, hour)` for a UTC day or hour.
- The token passed to `DownloadAsync` covers only opening the file. To bound the rest, pass a token to each read, such as `body.CopyToAsync(file, cancellationToken)`.
- The SDK does not verify the file. `GetExportMetaAsync` returns the file's SHA-256 checksum, which is also `Checksum` on a listed export. Use a fixed ID for both calls, because `latest` can move between them.
- `GetDownloadUrlAsync` returns a presigned URL that works without a key for 24 hours, so treat it as a secret. Your API key is never sent to the storage host.
- Hourly exports roll into the daily export shortly after 00:30 UTC the next day. After that, the hour returns a 400.

## Errors

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

### Error types

- `SynthientException`: the base type of the SDK's exceptions, also thrown for a response the SDK can't decode.
- `ApiException`: the API, or the storage host during `DownloadAsync`, returned an error status.
- `ApiConnectionException`: no response arrived, or reading the response failed.
- `ApiTimeoutException`: an `ApiConnectionException` thrown when `Timeout` or the 10-second connect timeout elapses.
- `OperationCanceledException`: the call's `CancellationToken` was cancelled.
- `GrpcSchemaException`: a `SynthientException` thrown by the [gRPC schema fetcher](#grpc-schema), with the gRPC `StatusCode`.
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

See the [errors reference](https://docs.synthient.com/sdks/csharp/errors) and [Errors](https://docs.synthient.com/errors) for what each status means.

## Retries and timeouts

### Retries

The client retries a failed request up to twice, backing off exponentially with jitter and honoring `Retry-After`:

| Requests                              | Retried on                                                                                   |
| ------------------------------------- | -------------------------------------------------------------------------------------------- |
| Account and feed requests             | 500, 502, 503, and 504, most connection failures and timeouts, and 429 with `Retry-After`    |
| Billed lookups and `CheckHealthAsync` | Only 429 with `Retry-After`, which comes before any work, so a lookup is never charged twice |

A 429 without `Retry-After` means the monthly request budget is spent or too many streams are open, so it is thrown at once. Downloads from the storage host are never retried, and neither is a failure after the response has started, such as a stream that breaks off.

The backoff starts at one second and doubles up to 30 seconds. The wait is never shorter than `Retry-After`, and a `Retry-After` longer than a minute is not retried.

Set the number of retries with `MaxRetries`. Zero disables them:

```csharp
using var client = new SynthientClient(new SynthientClientOptions { MaxRetries = 4 });
```

### Timeouts

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

See [Retries and timeouts](https://docs.synthient.com/sdks/csharp/errors#retries-and-timeouts) and [Rate Limits](https://docs.synthient.com/rate-limits).

## Configuration

`SynthientClientOptions` configures the client. The client copies the options when it is created, so later changes have no effect on it. An invalid option is rejected with `ArgumentException`.

| Option         | Type                        | Default                            | Description                                                                         |
| -------------- | --------------------------- | ---------------------------------- | ----------------------------------------------------------------------------------- |
| `ApiKey`       | `string?`                   | `SYNTHIENT_API_KEY`                | The API key. Surrounding whitespace is removed                                      |
| `BaseAddress`  | `Uri`                       | `https://api.synthient.com/api/v4` | The API base address, including the version path                                    |
| `MaxRetries`   | `int`                       | `2`                                | How many times a failed request is retried. See [Retries](#retries)                 |
| `Timeout`      | `TimeSpan`                  | 60 seconds                         | How long each wait on the network can take. See [Timeouts](#timeouts)               |
| `UserAgent`    | `string?`                   | `null`                             | A product token appended to the `User-Agent` header                                 |
| `ResponseHook` | `Action<ResponseMetadata>?` | `null`                             | Called with every API response. See [Response metadata](#response-metadata)         |
| `Handler`      | `HttpMessageHandler?`       | `null`                             | The HTTP handler that sends requests. See [Custom HTTP client](#custom-http-client) |

See the [client reference](https://docs.synthient.com/sdks/csharp/client).

### Server URL

The default server is `https://api.synthient.com/api/v4`. Set `BaseAddress` to send requests elsewhere, such as a mock server in tests:

```csharp
using var client = new SynthientClient(new SynthientClientOptions
{
    BaseAddress = new Uri("http://localhost:8080/api/v4"),
});
```

### Custom HTTP client

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

### User agent

Set `UserAgent = "my-app/1.2"` to add your product token to the `User-Agent` header, after `synthient-dotnet/` and the SDK version, which is also the constant `SynthientClient.Version`.

## Response metadata

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

`RateLimit` is null when the response doesn't report it; its `Reset` is how long until the bucket is full again. The hook is not called for export downloads from the storage host.

The hook must be safe to call concurrently. If it throws, the call fails with its exception.

> [!WARNING]
> Response headers can contain secrets, such as presigned download URLs. Avoid logging `metadata.Headers` in production.

See [Response metadata](https://docs.synthient.com/sdks/csharp/client#response-metadata).

## gRPC schema

The `Synthient.Client.GrpcSchema` package fetches the protobuf schema of the [Synthient gRPC API](https://docs.synthient.com/grpc) through server reflection:

```csharp
using Synthient;

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

`schema.DescriptorSet` is a `Google.Protobuf.Reflection.FileDescriptorSet`. `GrpcSchema.Explain` describes common gRPC errors in plain language. `FetchAsync` does not read `SYNTHIENT_API_KEY` on its own, so pass the key.

By default it fetches every service of `grpc.synthient.com:443` with a 30-second deadline. Pass `GrpcSchemaOptions` to change the endpoint, name the services or messages to fetch, or set the deadline:

```csharp
var options = new GrpcSchemaOptions
{
    Endpoint = "localhost:50051",
    Plaintext = true,
    Timeout = TimeSpan.FromSeconds(10),
};
var schema = await GrpcSchema.FetchAsync(apiKey, options);
Console.WriteLine($"{schema.Endpoint} {string.Join(", ", schema.Symbols)}");
```

`Plaintext` disables TLS, for local development servers. To fetch only some services or messages, add their fully qualified names to `options.Symbols`.

See the [gRPC schema reference](https://docs.synthient.com/sdks/csharp/grpc-schema).

## Requirements

.NET 8 or later. `Synthient.Client` targets net8.0 and net10.0, depends only on System.Text.Json on net8.0, and supports trimming and Native AOT. `Synthient.Client.GrpcSchema` targets the same frameworks and depends on Grpc.Net.Client, Grpc.Reflection, and Google.Protobuf. Every type is in the `Synthient` namespace.

## Versioning

This SDK follows [Semantic Versioning](https://semver.org). Breaking changes ship only in a new major version. `Synthient.Client` and `Synthient.Client.GrpcSchema` are released together with the same version.

## Documentation

Every public type and member has XML documentation, which your editor shows as you type.

- [C# SDK guide](https://docs.synthient.com/sdks/csharp)
- [Client](https://docs.synthient.com/sdks/csharp/client): options and response metadata
- [Lookups](https://docs.synthient.com/sdks/csharp/lookups): IP and domain lookups, the account, and the health check
- [Feeds](https://docs.synthient.com/sdks/csharp/feeds): streams and Parquet exports
- [Errors and retries](https://docs.synthient.com/sdks/csharp/errors)
- [Types](https://docs.synthient.com/sdks/csharp/types): the fields of every response and event type
- [gRPC schema](https://docs.synthient.com/sdks/csharp/grpc-schema)

## Contributions

Bug reports and feature requests are welcome as [GitHub issues](https://github.com/synthient/synthient-dotnet/issues). For questions about the API or your account, contact [Synthient support](https://docs.synthient.com/misc/support).

## License

[Apache 2.0](https://github.com/synthient/synthient-dotnet/blob/main/LICENSE)
