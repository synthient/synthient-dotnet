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

To use the [gRPC schema fetcher](#grpc-schema), also add its package:

```sh
dotnet add package Synthient.Client.GrpcSchema
```

### PackageReference

Add the package to your project file. The second reference is only needed for the gRPC schema fetcher:

```xml
<ItemGroup>
  <PackageReference Include="Synthient.Client" Version="1.0.0" />
  <PackageReference Include="Synthient.Client.GrpcSchema" Version="1.0.0" />
</ItemGroup>
```

## Requirements

.NET 8 or later. `Synthient.Client` targets net8.0 and net10.0, depends only on System.Text.Json on net8.0, and supports trimming and Native AOT. `Synthient.Client.GrpcSchema` also depends on Grpc.Net.Client, Grpc.Reflection, and Google.Protobuf.

## API Keys

Every request to the HTTP API needs a Synthient API key. Keys are issued from the Synthient dashboard; if you don't have one yet, [contact us](mailto:contact@synthient.com). Each key carries scopes that decide what it can call. `BASIC` covers the lookups and `GetAccountAsync`, and each feed has one scope for its real-time stream and one for its exports, listed under [Feeds](#feeds). See [Authentication](https://docs.synthient.com/authentication) for how scopes work.

If a request fails even though you are sending a key, it is usually one of these:

- **401 Unauthorized**: the key is malformed, unknown, or revoked. Check that it was loaded from the environment and isn't truncated.
- **402 Payment Required**: the team is out of lookup credits. `GetAccountAsync` reports the remaining balance and when it resets.
- **403 Forbidden**: the key lacks the scope for that endpoint or feed. `GetAccountAsync` lists the key's scopes.

## Authentication

This SDK supports the following security scheme:

| Name     | Type    | Scheme             | Environment Variable |
| -------- | ------- | ------------------ | -------------------- |
| `ApiKey` | API key | `x-api-key` header | `SYNTHIENT_API_KEY`  |

The parameterless constructor reads the key from the `SYNTHIENT_API_KEY` environment variable:

```csharp
using var client = new SynthientClient();
```

To pass the key yourself, for example from your configuration or a secret manager, use the constructor that takes it, or set `ApiKey` on `SynthientClientOptions`:

```csharp
using var client = new SynthientClient(apiKey);
```

A missing or empty key throws `ArgumentException`. The constructor makes no network calls, so an invalid key shows up as a 401 on the first request. The key is sent only to the Synthient API, never to the storage host that serves export downloads.

## SDK Example Usage

### Look up an IP address

Get the network, location, and risk intelligence for an IPv4 or IPv6 address. Each lookup costs one credit, and failed lookups are free.

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

`Network`, `Location`, and `Intelligence` are null when the API has no data for that section, so check them before reading their properties. A client is safe for concurrent use and pools its connections, so create one, reuse it for the life of your application, and dispose it when you are done. The examples below reuse `client`. Runnable programs are in [examples](https://github.com/synthient/synthient-dotnet/tree/main/examples), for example `dotnet run examples/lookup-ip.cs 8.8.8.8` from the repository root, which needs a .NET 10 SDK, 10.0.401 or later (pinned in global.json).

### Look up many IP addresses

Look up as many as 1000 addresses in one request, billed at `ceil(n * 0.9)` credits for the `n` distinct addresses looked up. Private and reserved addresses are skipped and duplicates collapse into one result, so match results by `IP` rather than by position.

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

Get the honeypot traffic observed for a domain, with its most recent events. A domain that has never been observed returns an inactive status and zero counts rather than an error. Each lookup costs one credit.

```csharp
var domain = await client.LookupDomainAsync("example.com", limit: 10);
Console.WriteLine($"{domain.Status} {domain.Stats.Events24h}");
foreach (var recent in domain.RecentEvents)
{
    Console.WriteLine($"{recent.Timestamp:u} {recent.TargetSubdomain} {recent.Port}");
}
```

### Check your account

Get the account that owns the API key, with the key's scopes and the team's remaining lookup credits. It costs no credits.

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
- `GetAccountAsync` - Get the account that owns the API key, the key's scopes, and the team's remaining lookup credits
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

`StreamAsync` returns an `IAsyncEnumerable` of a feed's real-time events. Each `await foreach` over it opens a new connection, and leaving the loop closes it. The server keeps a stream open for up to 30 minutes and then closes it, which ends the loop without an exception. The API has no resume cursor, so loop again to reconnect:

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
        // Pause after a failure so a failing stream doesn't reconnect in a
        // tight loop.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }
}
```

A 401 or 403 is not caught, so its `ApiException` ends the loop and reaches your code, because reconnecting can't fix a rejected key or a missing scope. Other failures throw from the `await foreach` and are caught: an `ApiException` for any other non-success response, an `ApiConnectionException` if the connection fails, or a `SynthientException` for an event the SDK can't decode. After a failure the loop waits 2 seconds before reconnecting, and after the server's routine close it reconnects at once. Cancelling the token throws `OperationCanceledException`, which ends the loop. A 429 `ApiException` without `RetryAfter` means the key already has too many open streams.

## Pagination

`ListExportsAsync` returns one page of a feed's exports, newest first:

```csharp
var page = await client.Proxies.ListExportsAsync(limit: 50);
foreach (var export in page.Exports)
{
    Console.WriteLine($"{export.Id} {export.Kind} {export.RowCount}");
}
```

To get the next page, pass `page.NextCursor` as the `cursor` argument. It is null or empty on the last page. `ListAllExportsAsync` does the paging for you. It fetches pages as the loop needs them and stops fetching when you leave the loop:

```csharp
await foreach (var export in client.Proxies.ListAllExportsAsync())
{
    Console.WriteLine($"{export.Id} {export.Kind} {export.RowCount}");
}
```

On the first page, the newest hourly export has the ID `latest`, a moving alias for the freshest export. To keep a stable ID, build it from the export's `Date` and `Hour` with `ExportId.Hourly`, such as `2026-10-14/21`.

## Downloads

Exports are Parquet files. Identify one by the `Id` of an `Export` from a listing, by `ExportId.Latest` for the freshest one, or by `ExportId.Daily(day)` or `ExportId.Hourly(day, hour)` for a UTC day or hour. An invalid ID throws `ArgumentException` before any request is sent. `DownloadAsync` returns a stream of the file's contents, which you must dispose:

```csharp
await using var body = await client.Proxies.DownloadAsync(ExportId.Latest);
await using var file = File.Create("proxies.parquet");
await body.CopyToAsync(file);
```

The cancellation token passed to `DownloadAsync` covers opening the file. To bound the rest, pass a token to each read, for example `body.CopyToAsync(file, cancellationToken)`.

The file comes from a presigned storage URL that is requested without your API key. The SDK does not verify the file, so compare its SHA-256 with `Export.Checksum` or with the checksum that `GetExportMetaAsync` returns. Read the checksum and download the file by the same stable ID, because `ExportId.Latest` can move to a newer export between the two calls. `GetDownloadUrlAsync` returns the presigned URL itself for tools that read Parquet over HTTPS. The URL grants access without a key for 24 hours, so treat it as a secret.

An hourly export is available until its day's daily export is rolled up, shortly after 00:30 UTC the next day. After that, requests for the hour fail with a 400. While a daily export is being rolled up, requests for it fail with a 404 that has `RetryAfter` set.

## Retries

The client retries a failed request up to twice by default, backing off exponentially with jitter and honoring `Retry-After`:

- Account and feed requests are retried on 500, 502, 503, and 504 responses, on 429 responses that carry `Retry-After`, and when no response arrives because the connection failed (other than a TLS error) or `Timeout` elapsed. A failure after the response has started, such as a body or stream that breaks off or stalls, is not retried, and neither is the file transfer from the storage host in `DownloadAsync`.
- Billed lookups (`LookupIPAsync`, `LookupIPsAsync`, and `LookupDomainAsync`) are retried only on 429 responses that carry `Retry-After`, which the rate limiter returns before doing any work, so a lookup is never charged twice. `CheckHealthAsync` is retried the same way, so an unhealthy API is reported at once.

A 429 without `Retry-After` means the key's monthly request budget is spent or it has too many open streams, so it is not retried. A `Retry-After` longer than a minute is not waited out; the call throws an `ApiException` with `RetryAfter` set instead.

To change the number of retries, set `MaxRetries` when creating the client. Zero disables retries:

```csharp
using var client = new SynthientClient(new SynthientClientOptions { MaxRetries = 4 });
```

## Timeouts

`Timeout` bounds each wait on the network: each wait for response headers, and each read of a response body, stream, or download. It does not bound a whole call, so long-lived streams and large downloads are not cut off, but a stream that sends nothing for that long fails. When it elapses, the call throws `ApiTimeoutException`. An account or feed request whose response headers time out is retried first, so with the defaults it can take about three minutes to fail. The default is 60 seconds, and `Timeout.InfiniteTimeSpan` disables it:

```csharp
using var client = new SynthientClient(new SynthientClientOptions { Timeout = TimeSpan.FromSeconds(30) });
```

To bound a whole call, pass a `CancellationToken`. Cancelling it throws `OperationCanceledException`, not `ApiTimeoutException`:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var info = await client.LookupIPAsync(IPAddress.Parse("8.8.8.8"), cancellation.Token);
```

The default handler also gives up connecting after 10 seconds, which also throws `ApiTimeoutException` and is retried like any other timeout.

## Error Handling

A request that fails with an HTTP error status throws an `ApiException`. It has the following properties:

| Property     | Type                                                 | Description                                                                                 |
| ------------ | ---------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `StatusCode` | `int`                                                | HTTP response status code, such as `404`                                                    |
| `Type`       | `string?`                                            | URI identifying the problem type, such as `https://docs.synthient.com/errors/validation-failed` |
| `Title`      | `string?`                                            | Short summary of the problem type                                                           |
| `Detail`     | `string?`                                            | Explanation of this occurrence. For a plain-text error, the response body                   |
| `Errors`     | `IReadOnlyDictionary<string, IReadOnlyList<string>>` | Validation messages by request field                                                        |
| `RequestId`  | `string?`                                            | Request ID to give Synthient support                                                        |
| `RetryAfter` | `TimeSpan?`                                          | How long the server asked you to wait before retrying, or null                              |
| `Headers`    | `IReadOnlyDictionary<string, IReadOnlyList<string>>` | The response headers, with case-insensitive names                                          |

`Message` holds only the status, such as `403 Forbidden`, so it is safe to log. The other properties hold untrusted server data that can be sensitive, so log them deliberately.

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

- `SynthientException`: the base type of the SDK's exceptions. It is thrown directly when the API returns a response the SDK can't use, such as a malformed body.
- `ApiException`: the API returned an error status. An error status from the storage host during `DownloadAsync` also throws one, carrying only `StatusCode` so that the presigned URL never appears in it.
- `ApiConnectionException`: no response arrived, because the connection failed or was closed, or reading the response failed. The server may still have processed the request.
- `ApiTimeoutException`: an `ApiConnectionException` thrown when `Timeout` elapses while waiting on the network, or when the default handler's 10-second connect timeout elapses.
- `OperationCanceledException`: the call's `CancellationToken` was cancelled.
- `ArgumentException`: an argument or option is invalid, such as a missing API key, more than 1000 addresses, or an invalid export ID. It is thrown before any request is sent.
- `ObjectDisposedException`: the client was disposed.

#### Status codes

| Status | Meaning                                                                                                            |
| ------ | ------------------------------------------------------------------------------------------------------------------ |
| 400    | The request is invalid, such as asking for an hourly export that has been rolled up                                |
| 401    | The API key is malformed, unknown, or revoked                                                                      |
| 402    | The team is out of lookup credits                                                                                  |
| 403    | The key lacks the scope for the endpoint or feed                                                                   |
| 404    | The export does not exist, or a daily export is still being rolled up and `RetryAfter` is set                      |
| 422    | The input is invalid, such as a private or reserved address, a malformed domain, or a batch with no usable address |
| 429    | With `RetryAfter`, the rate limit was hit. Without it, the request budget is spent or too many streams are open    |
| 5xx    | The server failed. Account and feed requests are retried on 500, 502, 503, and 504                                 |

## Server Selection

### Override Server URL Per-Client

The default server is `https://api.synthient.com/api/v4`. To send requests elsewhere, such as through a gateway or to a mock server in tests, set `BaseAddress` when creating the client. The address includes the version path:

```csharp
using var client = new SynthientClient(new SynthientClientOptions
{
    BaseAddress = new Uri("http://localhost:8080/api/v4"),
});
```

## Custom HTTP Client

The client sends requests through an `HttpMessageHandler`. Pass your own as `Handler`, for example to route requests through a proxy:

```csharp
var handler = new SocketsHttpHandler
{
    Proxy = new WebProxy("http://proxy.example.com:8080"),
    AllowAutoRedirect = false,
    MaxResponseDrainSize = 0,
};
using var client = new SynthientClient(new SynthientClientOptions { Handler = handler });
```

The client takes a handler rather than an `HttpClient` so that it can check how the handler treats redirects. A followed export redirect would send the API key to the storage host, so a `SocketsHttpHandler` or `HttpClientHandler` with `AllowAutoRedirect` set is rejected with `ArgumentException`. The handler should not retry requests either: the client retries on its own, and a handler that resends a billed lookup can be charged twice. Setting `MaxResponseDrainSize` to 0 makes leaving a stream or download early close its connection, as the client's own handler does. The client never disposes your handler.

To identify your application, set `UserAgent = "my-app/1.2"` to append a product token to the `User-Agent` header.

## Debugging

`ResponseHook` is called after every response from the Synthient API, including error responses and each retry, page, and stream handshake. It receives a `ResponseMetadata` with the status code, the request ID to give Synthient support, the operation's rate limit, and a copy of the response headers:

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

The hook runs synchronously and must be safe to call concurrently. If it throws, the call fails with its exception. Export downloads from storage are not passed to it.

> [!WARNING]
> Response headers can contain secrets, such as presigned download URLs. Avoid logging `metadata.Headers` in production.

## gRPC Schema

The `Synthient.Client.GrpcSchema` package fetches the protobuf schema of the [Synthient gRPC API](https://docs.synthient.com/grpc) through gRPC server reflection:

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

`schema.DescriptorSet` is a `Google.Protobuf.Reflection.FileDescriptorSet` with each file after the files it imports. `GrpcSchema.Explain` describes common gRPC errors in plain language, and returns null for the rest. `GrpcSchemaOptions` sets the endpoint, the symbols to resolve, and the deadline for the whole fetch, which is 30 seconds by default.

# Development

## Maturity

This SDK follows [Semantic Versioning](https://semver.org). Breaking changes to its public API ship only in a new major version. `Synthient.Client` and `Synthient.Client.GrpcSchema` are released together with the same version.

## Contributions

Bug reports and feature requests are welcome as [GitHub issues](https://github.com/synthient/synthient-dotnet/issues). For questions about the API or your account, contact [Synthient support](https://docs.synthient.com/misc/support).

## License

[Apache 2.0](https://github.com/synthient/synthient-dotnet/blob/main/LICENSE)
