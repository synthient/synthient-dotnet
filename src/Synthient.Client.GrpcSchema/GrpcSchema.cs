using System.Globalization;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1;

namespace Synthient;

/// <summary>Fetches the protobuf schema of the Synthient gRPC API using gRPC server reflection.</summary>
public static class GrpcSchema
{
    /// <summary>The address of the Synthient gRPC API.</summary>
    public const string DefaultEndpoint = "grpc.synthient.com:443";

    private const int MaxMessageSize = 32 << 20;

    /// <summary>
    /// Connects to a Synthient gRPC endpoint and uses server reflection to fetch the protobuf files
    /// that define <see cref="GrpcSchemaOptions.Symbols"/>, or every listed service when there are
    /// none, along with all of their imports.
    /// </summary>
    /// <param name="apiKey">The API key, sent as x-api-key metadata when it is not empty.</param>
    /// <param name="options">The endpoint and symbols to fetch, or null for every service of <see cref="DefaultEndpoint"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The schema.</returns>
    /// <exception cref="GrpcSchemaException">Reflection failed. <see cref="Explain"/> describes common causes.</exception>
    public static async Task<GrpcSchemaResult> FetchAsync(string? apiKey, GrpcSchemaOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new GrpcSchemaOptions();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Timeout, TimeSpan.Zero, nameof(options));
        var (endpoint, host) = ParseEndpoint(options.Endpoint);
        using var channel = GrpcChannel.ForAddress((options.Plaintext ? "http://" : "https://") + endpoint, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = MaxMessageSize,
            ThrowOperationCanceledOnCancellation = true,
        });
        var headers = new Metadata();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            headers.Add("x-api-key", apiKey.Trim());
        }

        try
        {
            using var call = new ServerReflection.ServerReflectionClient(channel)
                .ServerReflectionInfo(headers, DateTime.UtcNow + options.Timeout, cancellationToken);
            var reflector = new Reflector(call, host, cancellationToken);

            var symbols = options.Symbols.Select(symbol => symbol.Trim()).Where(symbol => symbol.Length > 0).Distinct().ToList();
            if (symbols.Count == 0)
            {
                symbols = await reflector.ListServicesAsync().ConfigureAwait(false);
            }
            if (symbols.Count == 0)
            {
                throw new GrpcSchemaException(StatusCode.Unknown, "The server lists no services.");
            }

            var files = new Dictionary<string, FileDescriptorProto>(StringComparer.Ordinal);
            foreach (var symbol in symbols)
            {
                AddFiles(files, await reflector.FilesAsync(new ServerReflectionRequest { FileContainingSymbol = symbol }).ConfigureAwait(false));
            }
            while (MissingDependencies(files) is { Count: > 0 } missing)
            {
                foreach (var name in missing)
                {
                    AddFiles(files, await reflector.FilesAsync(new ServerReflectionRequest { FileByFilename = name }).ConfigureAwait(false));
                    if (!files.ContainsKey(name))
                    {
                        throw new GrpcSchemaException(StatusCode.Unknown, $"Reflection did not return the dependency {name}.");
                    }
                }
            }

            var set = new FileDescriptorSet();
            set.File.AddRange(OrderedFiles(files));
            return new GrpcSchemaResult(endpoint, symbols, set);
        }
        catch (RpcException e)
        {
            throw new GrpcSchemaException(e.StatusCode, $"gRPC reflection failed with status {e.StatusCode}.", e);
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // Grpc.Net.Client reports a passed deadline as a cancellation.
            throw new GrpcSchemaException(StatusCode.DeadlineExceeded, "gRPC reflection timed out.", e);
        }
    }

    /// <summary>
    /// Returns a human-readable explanation of a common gRPC failure from <see cref="FetchAsync"/>,
    /// or null if there is none.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The explanation, or null.</returns>
    public static string? Explain(Exception exception)
    {
        StatusCode? code = exception switch
        {
            GrpcSchemaException e => e.StatusCode,
            RpcException e => e.StatusCode,
            _ => null,
        };
        return code switch
        {
            StatusCode.Unauthenticated => "Missing or invalid API key.",
            StatusCode.PermissionDenied => "This API key does not have permission to inspect gRPC schemas.",
            StatusCode.NotFound => "No protobuf schema found for the requested symbol. Fetch without symbols to list the available services.",
            StatusCode.Unimplemented => "The gRPC endpoint does not expose server reflection.",
            StatusCode.Unavailable => "Could not reach the gRPC endpoint. Check the address, network access, and TLS settings.",
            StatusCode.DeadlineExceeded => "Timed out reading protobuf schemas from gRPC reflection. Use a longer timeout.",
            _ => null,
        };
    }

    internal static (string Endpoint, string Host) ParseEndpoint(string? endpoint)
    {
        var raw = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
        var scheme = raw.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            raw = raw[(scheme + 3)..];
            var end = raw.IndexOfAny(['/', '?', '#']);
            raw = end < 0 ? raw : raw[..end];
        }
        if (raw.EndsWith('/'))
        {
            raw = raw[..^1];
        }
        if (raw.Length == 0)
        {
            throw new ArgumentException("The endpoint is empty.", nameof(endpoint));
        }

        string host, port;
        var close = raw.IndexOf(']', StringComparison.Ordinal);
        var colon = raw.IndexOf(':', StringComparison.Ordinal);
        if (raw.StartsWith('[') && close > 0)
        {
            host = raw[1..close];
            port = raw[(close + 1)..] switch
            {
                "" => "443",
                [':', .. var rest] => rest,
                _ => "",
            };
        }
        else if (colon >= 0 && colon == raw.LastIndexOf(':'))
        {
            host = raw[..colon];
            port = raw[(colon + 1)..];
        }
        else
        {
            host = raw;
            port = "443";
        }
        var validPort = int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 65535;
        if (host.Length == 0 || !validPort)
        {
            throw new ArgumentException($"\"{endpoint}\" is not a valid endpoint.", nameof(endpoint));
        }
        return (host.Contains(':', StringComparison.Ordinal) ? $"[{host}]:{port}" : $"{host}:{port}", host);
    }

    private static void AddFiles(Dictionary<string, FileDescriptorProto> files, IEnumerable<Google.Protobuf.ByteString> encoded)
    {
        foreach (var data in encoded)
        {
            FileDescriptorProto file;
            try
            {
                file = FileDescriptorProto.Parser.ParseFrom(data);
            }
            catch (Google.Protobuf.InvalidProtocolBufferException e)
            {
                throw new GrpcSchemaException(StatusCode.Unknown, "Reflection returned an invalid file descriptor.", e);
            }
            if (string.IsNullOrEmpty(file.Name))
            {
                throw new GrpcSchemaException(StatusCode.Unknown, "Reflection returned a file descriptor without a name.");
            }
            files.TryAdd(file.Name, file);
        }
    }

    internal static List<string> MissingDependencies(Dictionary<string, FileDescriptorProto> files) =>
        [.. files.Values.SelectMany(file => file.Dependency).Where(name => !files.ContainsKey(name)).Distinct().Order(StringComparer.Ordinal)];

    // Orders files so that each comes after the files it imports, breaking ties by name.
    internal static List<FileDescriptorProto> OrderedFiles(Dictionary<string, FileDescriptorProto> files)
    {
        var ordered = new List<FileDescriptorProto>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in files.Keys.Order(StringComparer.Ordinal))
        {
            Visit(name);
        }
        return ordered;

        void Visit(string name)
        {
            if (!files.TryGetValue(name, out var file) || !visited.Add(name))
            {
                return;
            }
            foreach (var dependency in file.Dependency.Order(StringComparer.Ordinal))
            {
                Visit(dependency);
            }
            ordered.Add(file);
        }
    }

    // Sends one reflection request at a time over a single stream.
    private sealed class Reflector(
        AsyncDuplexStreamingCall<ServerReflectionRequest, ServerReflectionResponse> call,
        string host,
        CancellationToken cancellationToken)
    {
        public async Task<List<string>> ListServicesAsync()
        {
            var response = await SendAsync(new ServerReflectionRequest { ListServices = "" }).ConfigureAwait(false);
            if (response.MessageResponseCase != ServerReflectionResponse.MessageResponseOneofCase.ListServicesResponse)
            {
                throw new GrpcSchemaException(StatusCode.Unknown, "The reflection response has no service list.");
            }
            return
            [
                .. response.ListServicesResponse.Service
                    .Select(service => service.Name.Trim())
                    .Where(name => name.Length > 0 && !name.StartsWith("grpc.reflection.", StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal),
            ];
        }

        public async Task<IEnumerable<Google.Protobuf.ByteString>> FilesAsync(ServerReflectionRequest request)
        {
            var response = await SendAsync(request).ConfigureAwait(false);
            if (response.MessageResponseCase != ServerReflectionResponse.MessageResponseOneofCase.FileDescriptorResponse)
            {
                throw new GrpcSchemaException(StatusCode.Unknown, "The reflection response has no file descriptors.");
            }
            return response.FileDescriptorResponse.FileDescriptorProto;
        }

        private async Task<ServerReflectionResponse> SendAsync(ServerReflectionRequest request)
        {
            request.Host = host;
            try
            {
                await call.RequestStream.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A write fails once the call has ended, and the response headers report why.
                await call.ResponseHeadersAsync.ConfigureAwait(false);
                throw;
            }
            if (!await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
            {
                throw new GrpcSchemaException(StatusCode.Unknown, "The server closed the reflection stream.");
            }
            var response = call.ResponseStream.Current;
            if (response.MessageResponseCase == ServerReflectionResponse.MessageResponseOneofCase.ErrorResponse)
            {
                var error = response.ErrorResponse;
                throw new GrpcSchemaException((StatusCode)error.ErrorCode, $"Reflection failed with status {(StatusCode)error.ErrorCode}: {error.ErrorMessage}");
            }
            return response;
        }
    }
}

/// <summary>Configures <see cref="GrpcSchema.FetchAsync"/>.</summary>
public sealed class GrpcSchemaOptions
{
    /// <summary>
    /// The server address as host:port, a bare host (port 443 is assumed), or a grpc:// or https://
    /// URL. Null uses <see cref="GrpcSchema.DefaultEndpoint"/>.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>Disables TLS, for local development servers.</summary>
    public bool Plaintext { get; set; }

    /// <summary>
    /// Fully qualified service or message names to resolve. Empty resolves every service the server
    /// lists, except the grpc.reflection services.
    /// </summary>
    public IList<string> Symbols { get; } = [];

    /// <summary>The deadline for the whole fetch. The default is 30 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>The protobuf schema returned by <see cref="GrpcSchema.FetchAsync"/>.</summary>
/// <param name="Endpoint">The normalized address that was queried.</param>
/// <param name="Symbols">The service or message names that were resolved.</param>
/// <param name="DescriptorSet">The resolved files, each after the files it imports.</param>
public sealed record GrpcSchemaResult(string Endpoint, IReadOnlyList<string> Symbols, FileDescriptorSet DescriptorSet);

/// <summary>Fetching a gRPC schema failed.</summary>
public sealed class GrpcSchemaException : SynthientException
{
    /// <summary>Initializes a new instance with a gRPC status, a message, and the exception that caused it.</summary>
    /// <param name="statusCode">The gRPC status code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public GrpcSchemaException(StatusCode statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>The gRPC status code, or <see cref="StatusCode.Unknown"/> if the server broke the reflection protocol.</summary>
    public StatusCode StatusCode { get; }
}
